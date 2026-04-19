# Generated QuickMarkup Property Symbols Plan

This plan covers making properties generated from QuickMarkup declarations visible during binding of other QuickMarkup markup in the same compilation.

The immediate motivator is component wrappers:

```csharp
[QuickMarkup("""
string Text = "";
TextKinds Kind = Default;
<root>
    <TextBlock Text=`Text` />
</root>
""")]
public partial class StyledText : IQuickMarkupComponent<UIElement>;
```

Another QuickMarkup file should resolve this normally:

```csharp
<StyledText Text="Hello" Kind=Heading1 />
```

Today, source-generated members are not present in the compilation used by the same generator pass, so `Text` and `Kind` are invisible to `CodeTypeResolver.FindProperty`.

## Current State

Relevant files:

- `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.Refs.cs`
- `QuickMarkup.Language/Symbols/QuickMarkupSymbols.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupProviderExtension.SourceGen.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupTargetContext.cs`
- `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
- `QuickMarkup.SourceGen/CodeGen/RefsGenContext.cs`
- `QuickMarkup.SourceGen.Test/`

Existing behavior:

- `QuickMarkupGenerator` parses all QuickMarkup attributes into `nonErrorMarkups`.
- INIT source generation binds each template with a `CodeTypeResolver` created from the original compilation.
- REFS source generation separately binds ref declarations and emits generated properties.
- `CodeTypeResolver.FindProperty` inspects only Roslyn symbols from the current compilation.
- Event and dependency-property lookup is Roslyn-only and should remain that way because QuickMarkup cannot declare events or dependency properties.
- `QuickMarkupBinder.BindRefDeclarations` already understands ref/computed declarations and their types.

Problem:

- Generated ref/computed properties are not visible to the binder when another QuickMarkup tag uses the target class.
- This causes false misses for properties, enum shorthand, boolean shorthand, binding target type hints, and future component property-vs-output fallback.

## Goals

Add a synthetic generated-member overlay that lets binding see QuickMarkup-generated members from all successfully parsed QuickMarkup attributes in the current generator run.

The overlay should support:

- generated ref properties, e.g. `Text`
- generated ref backing properties, e.g. `TextProp`
- generated computed properties, e.g. `Output`
- generated computed backing properties, e.g. `OutputComp`
- generated component output property after the component plan lands
- inherited generated members from base QuickMarkup classes when resolvable
- duplicate detection against real and synthetic members
- generated content-like properties used by child-content discovery, including `Children`, `Items`, `Child`, and `Content`

It should improve resolution without requiring generated source to be added back into the same compilation.

## Non-Goals

- Do not run a second compilation pass with generated source.
- Do not attempt full C# overload resolution for synthetic members.
- Do not support arbitrary user-authored generated code.
- Do not replace Roslyn symbols for real members.

## Target Design

Introduce a lightweight symbol overlay.

Possible shape:

```csharp
sealed record QuickMarkupGeneratedMemberTable(
    IReadOnlyDictionary<string, QuickMarkupGeneratedTypeMembers> Types
);

sealed record QuickMarkupGeneratedTypeMembers(
    string FullTypeName,
    IReadOnlyDictionary<string, QuickMarkupGeneratedPropertySymbol> Properties
);

sealed record QuickMarkupGeneratedPropertySymbol(
    string Name,
    ITypeSymbol? Type,
    bool IsStatic = false,
    QuickMarkupGeneratedPropertyKind Kind = QuickMarkupGeneratedPropertyKind.RefValue
);

enum QuickMarkupGeneratedPropertyKind
{
    RefValue,
    RefBacking,
    ComputedValue,
    ComputedBacking,
    ComponentOutput
}
```

This does not have to implement `IPropertySymbol`. A small project-owned abstraction is likely simpler and avoids fake Roslyn symbol complexity.

Example abstraction:

```csharp
readonly record struct ResolvedProperty(
    string Name,
    ITypeSymbol? Type,
    IPropertySymbol? RoslynSymbol,
    QuickMarkupGeneratedPropertySymbol? GeneratedSymbol
);
```

Then binder code can use `ResolvedProperty` instead of raw `IPropertySymbol`.

## Generator Pipeline Plan

The source generator already has all successful parses:

```csharp
var (nonErrorMarkups, errorMarkups) = context.SyntaxProvider.ForAllParsedQuickMarkup();
```

Add a new incremental pipeline:

1. For each successful parsed QuickMarkup attribute, bind only its ref declarations.
2. Convert bound refs to synthetic member entries for that target type.
3. Collect all entries into `QuickMarkupGeneratedMemberTable`.
4. Combine INIT and REFS binding with the generated member table.
5. Construct `CodeTypeResolver(compilation, usings, namespace, generatedMemberTable)`.

Important: the table should be built before INIT binding so parent templates can see generated properties from other targets.

Potential sourcegen shape:

```csharp
var generatedMemberEntries = nonErrorMarkups
    .Combine(context.CompilationProvider)
    .Select((x, ct) => BuildGeneratedMemberEntry(x, ct));

var generatedMemberTable = generatedMemberEntries
    .Collect()
    .Select((entries, ct) => QuickMarkupGeneratedMemberTable.Create(entries));

var sources = sfcs
    .Combine(context.CompilationProvider)
    .Combine(generatedMemberTable)
    .Select(...);
```

Keep REFS codegen using the same ref binding results if practical, but do not over-refactor in the first pass.

## Ref Declaration Mapping

For each `QMRefDeclarationSymbol<ITypeSymbol?>`:

Public ref declaration:

```csharp
string Text = "";
```

Synthetic properties:

- `Text`: type `string`
- `TextProp`: type `Reference<string>`

Computed declaration:

```csharp
string Display => `Text.ToUpperInvariant()`;
```

Synthetic properties:

- `Display`: type `string`
- `DisplayComp`: type `Computed<string>`

Private declarations:

- If QuickMarkup has private refs, do not expose them to other target types.
- They should still be visible inside the same target's own template.
- If protected generated refs are added later, they should follow normal protected visibility expectations.

Accessibility recommendation:

- Add an accessibility field to synthetic entries.
- `CodeTypeResolver.FindProperty` should honor it for cross-type lookup.
- Same-type lookup can see private generated properties.

## Resolver Plan

Change `CodeTypeResolver` from static-only property lookup toward instance property lookup that can consult overlays.

Current static property call:

- `CodeTypeResolver.FindProperty(...)`

Recommended addition:

```csharp
public ResolvedProperty? FindProperty(ITypeSymbol? type, string property);
```

Keep event and dependency-property lookup Roslyn-only:

```csharp
public static IEventSymbol? FindEvent(ITypeSymbol? type, string eventName);
public static bool TryGetDependencyProperty(ITypeSymbol? type, string property, out string? dependencyPropertyName);
```

Resolution order:

1. Real Roslyn property on the current type.
2. Synthetic generated property on the current type.
3. Real Roslyn property on the base type.
4. Synthetic generated property on the base type.
5. Continue walking base types in the same real-then-synthetic order.

Reasoning:

- Real user-authored members win at each inheritance level.
- Generated members on a derived type should beat real members inherited from a base type.
- Duplicate synthetic/real names should still produce diagnostics, but lookup can continue using the real member for type checking.

Keep static helpers as low-level Roslyn-only helpers if useful:

```csharp
public static IPropertySymbol? FindRoslynProperty(...)
```

Then update binder call sites incrementally to use instance methods.

## Inheritance Plan

Generated-member lookup should consider base types.

Approach:

- Key the table by metadata-style full type name.
- During lookup, walk `type`, then `type.BaseType`, matching each full type name against the table.
- Generic QuickMarkup target types are not fully supported in v1.
- For generic targets, the table may record that a generated property exists while leaving its type unknown.
- Unknown synthetic property types should still prevent false extension fallback, but enum shorthand and other type-sensitive binding behavior may require explicit user syntax.

Open issue:

- If a base type is in another assembly and its generated members are compiled into metadata, Roslyn real symbol lookup should already see them. The overlay only needs current-compilation generated members.

## Same-Target Visibility

The target's own generated refs are already available inside its template as C# expressions because the final generated partial class compiles with those properties.

But binder type hints still benefit from overlay visibility. Example:

```csharp
TextKinds Kind = Default;
<root>
    <StyledText Kind=Heading1 />
</root>
```

If `StyledText.Kind` is generated from QuickMarkup in the same compilation, the binder should know that `Kind` is `TextKinds` and resolve `Heading1` as an enum shorthand.

## Component Integration

The component plan depends on this table.

Add synthetic component output property:

- name: final output property name, e.g. `MarkupNode`
- single component type: `T`
- fragment component type: `FragmentBlock<T>`
- kind: `ComponentOutput`

This allows:

- collision diagnostics
- property lookup on generated component output if needed
- component output fallback to know the property exists

## Diagnostics Plan

Add diagnostics for:

- generated ref property conflicts with real member
- generated ref backing property conflicts with real member
- generated computed property conflicts with real member
- generated member conflicts with another generated member in the same target
- generated member type could not be resolved
- generated member inherited collision that would make lookup ambiguous
- generated member conflicts with a real member, even though the real member is used for type checking

Keep diagnostic quality practical in the first pass:

- At minimum, detect same-target duplicate generated names before codegen.
- Prefer reporting on the QuickMarkup ref declaration location.

## Binder Call Site Plan

Update these areas in `QuickMarkupBinder`:

- inline property assignment:
  - use `resolver.FindProperty(tagInfo.TagType, property.Key)`
- value-list property binding:
  - use resolved property type
- bind-back and two-way:
  - use resolved property type
  - dependency property lookup remains Roslyn-only
- boolean shorthand:
  - use resolved property
- content property discovery:
  - `TryGetContentProperty` may need overlay lookup for generated `Children`, `Items`, `Child`, or `Content`

For content-property attributes:

- Attribute lookup remains Roslyn-only.
- The named property from the attribute should be resolved through overlay-aware lookup after reading the attribute name.

For content-property conventions:

- Overlay-aware lookup should participate in `Children`, `Items`, `Child`, and `Content` discovery.
- `Child` and `Content` are the most important generated-content-property cases.
- `Children` and `Items` are unusual as generated refs, but support them for consistency.

## Sourcegen Reuse Plan

Avoid binding refs twice if it becomes easy:

- `generatedMemberEntries` pipeline can produce both `QMRefDeclarationSymbol` and synthetic members.
- REFS codegen can consume the collected bound refs for its target.

However, minimizing diffs is more important:

- First implementation may bind refs separately in the table-building path and existing REFS path.
- Follow up with a small refactor if duplication becomes problematic.

## Tests

Minimum behavioral/sourcegen tests:

- tag property assignment resolves generated ref property on another QuickMarkup target
- enum shorthand resolves through generated property type
- boolean shorthand resolves generated bool property
- bind-back resolves generated property type
- two-way binding resolves generated property type
- generated property wins over extension fallback for component targets
- real property wins over generated synthetic property if both are visible, with diagnostic for duplicate
- derived generated property wins over real inherited base property
- derived real property wins over inherited generated property
- generated properties inherited from a QuickMarkup base class are visible
- private generated refs are not visible cross-type
- private generated refs are visible inside the same target
- same-target generated refs remain visible where expected
- generated `Child`/`Content` participates in child-content discovery
- generated `Children`/`Items` participates in additive child-content discovery

Regression cases:

- old workaround still compiles:
  - `Kind=TextKinds.Heading1`
  - `IsEnabled=true`
- unresolved synthetic type does not crash binder
- missing property still falls back to extension method behavior where appropriate

## Documentation Plan

Update docs only where user behavior changes:

- `docs/qm-language.md`: generated refs on QuickMarkup components can be used as normal markup properties from other markup.
- clarify that explicit enum and boolean forms remain valid but should no longer be required for generated QuickMarkup properties.

## Implementation Order

1. Add synthetic generated-member model types.
2. Add generator pipeline to build the member table from all successful QuickMarkup parses.
3. Extend `CodeTypeResolver` constructor to accept the table.
4. Add overlay-aware property lookup while preserving Roslyn-only helpers.
5. Update binder property call sites to use overlay-aware lookup.
6. Add collision diagnostics for same-target generated members.
7. Add inherited synthetic lookup.
8. Integrate component output property once component interfaces exist.
9. Add tests.
10. Update docs.

## Decisions

- Synthetic table lives in `QuickMarkup.CodeAnalysis`.
- Event lookup stays Roslyn-only.
- Dependency-property lookup stays Roslyn-only.
- Duplicate generated/real names produce diagnostics, but real members are used for type checking at the same inheritance level.
- Property lookup order walks each inheritance level as real member, then generated member, then moves to the base type.
- Private generated refs are visible inside the same target's own markup and hidden cross-type.
- Generated `Children`, `Items`, `Child`, and `Content` participate in content-property discovery.
- Generic QuickMarkup target types are represented in the synthetic table only as property-exists entries with unknown property types.
