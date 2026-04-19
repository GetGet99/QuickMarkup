# Analyzer Generated Property Symbol Overlay Plan

This future plan covers updating `QuickMarkupAnalyzer` so analyzer diagnostics use the same generated QuickMarkup property symbol overlay as source generation.

The source generator path now understands generated QuickMarkup refs from other QuickMarkup targets in the same compilation. The diagnostic analyzer still binds one attribute at a time with a resolver that only sees Roslyn symbols. That means generated code can be correct while analyzer diagnostics are incomplete or misleading.

## Background

QuickMarkup lets a target class declare refs in the markup header:

```csharp
[QuickMarkup("""
string Text = "";
TextKinds Kind = Default;
bool IsImportant = false;
""")]
public partial class StyledText : TestElement;
```

The generator emits C# properties like:

```csharp
public string Text { get; set; }
public TestKinds Kind { get; set; }
public bool IsImportant { get; set; }
```

Another QuickMarkup target should be able to use those generated properties:

```csharp
[QuickMarkup("""
<root>
    <StyledText Text="Hello" Kind=Heading1 IsImportant />
</root>
""")]
public partial class Page : TestRoot;
```

Without a generated-property overlay, the binder cannot know that:

- `Text` exists.
- `Kind` has enum type `TextKinds`, so `Heading1` can be resolved as shorthand.
- `IsImportant` is a bool property, so bare `IsImportant` means `IsImportant=true`.

## Current Implemented Sourcegen State

Relevant files from the implemented source generator path:

- `QuickMarkup.CodeAnalysis/QuickMarkupGeneratedMemberTable.cs`
- `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
- `QuickMarkup.SourceGen.Test/GeneratedCases.cs`
- `QuickMarkup.SourceGen.Test/SourceGenBehaviorTests.cs`
- `QuickMarkup.SourceGen.Test/TestControls.cs`

Important current behavior:

- `QuickMarkupGeneratedMemberTable` stores generated property metadata for a compilation.
- The table stores generated property type identity as strings, not `ITypeSymbol`, to keep incremental generator values cache-friendly.
- `ResolvedProperty` is created only during binding and may contain Roslyn symbols because it is not stored as an incremental value.
- `CodeTypeResolver.FindProperty(...)` now checks:
  1. real Roslyn property on current type
  2. generated property on current type
  3. real Roslyn property on base type
  4. generated property on base type
  5. repeat through base types
- Event lookup remains Roslyn-only.
- Dependency-property lookup remains Roslyn-only.
- Generic QuickMarkup targets are only partially supported: the table can record that a property exists, but its type may be unknown.

Sourcegen builds the table in `QuickMarkupGenerator.Initialize` before INIT/REFS binding:

```csharp
var generatedMemberTable = nonErrorMarkups
    .Combine(context.CompilationProvider)
    .Select((x, ct) => BuildGeneratedTypeMembers(...))
    .Collect()
    .Select((items, _) => new QuickMarkupGeneratedMemberTable(...));
```

Then sourcegen passes the table into the resolver:

```csharp
new CodeTypeResolver(
    compilation,
    usings,
    target.Namespace,
    generatedMembers,
    target.FullTypeName)
```

## Current Analyzer Gap

Relevant analyzer file:

- `QuickMarkup.SourceGen/QuickMarkupAnalyzer.cs`

Current analyzer registration:

```csharp
context.RegisterQuickMarkupAttributeInStringSyntaxAction((context, markupStr, locationProvider) =>
{
    ...
    var binder = new QuickMarkupBinder(
        new CodeTypeResolver(
            context.Compilation,
            qm.Usings,
            markupStr.Target.Namespace
        ),
        failFast: false
    );
    ...
});
```

The analyzer receives and binds one QuickMarkup attribute at a time. It does not collect all QuickMarkup attributes first, so it cannot build a cross-target generated-property table.

Impact:

- Sourcegen may compile successfully while analyzer diagnostics still behave as if generated refs do not exist.
- Analyzer diagnostics may fail to resolve enum shorthand on generated properties.
- Analyzer diagnostics may fail to resolve boolean shorthand on generated bool refs.
- Analyzer diagnostics may choose extension fallback where a generated property should win.
- Future component-wrapper diagnostics will be more visibly wrong because component inputs are likely to be generated QuickMarkup refs.

## Goal

Make `QuickMarkupAnalyzer` bind with a `CodeTypeResolver` that has the same generated-property overlay as `QuickMarkupGenerator`.

Expected analyzer resolver construction:

```csharp
new CodeTypeResolver(
    context.Compilation,
    qm.Usings,
    markupStr.Target.Namespace,
    generatedMemberTable,
    markupStr.Target.FullTypeName)
```

## Non-Goals

- Do not make event lookup synthetic. QuickMarkup cannot declare events.
- Do not make dependency-property lookup synthetic. QuickMarkup cannot declare dependency properties.
- Do not run a second compilation with generated source.
- Do not require full generic substitution for generated generic QuickMarkup targets.
- Do not add component-wrapper support here unless that has already been implemented.

## Recommended Refactor First

Before changing the analyzer, move sourcegen-only table-building code into `QuickMarkup.CodeAnalysis`.

Current sourcegen-only helper:

- `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
  - `BuildGeneratedTypeMembers(...)`
  - `AddGeneratedProperty(...)`
  - `ConstructBackingTypeName(...)`
  - `TypeName(...)`

Recommended new shared helper:

```text
QuickMarkup.CodeAnalysis/
  QuickMarkupGeneratedMemberTable.cs
  QuickMarkupGeneratedMemberTableBuilder.cs
```

Possible API:

```csharp
static class QuickMarkupGeneratedMemberTableBuilder
{
    public static QuickMarkupGeneratedTypeMembers? BuildTypeMembers(
        QuickMarkupParsedAttribute markup,
        Compilation compilation,
        CancellationToken ct);

    public static QuickMarkupGeneratedMemberTable BuildTable(
        IEnumerable<QuickMarkupParsedAttribute> markups,
        Compilation compilation,
        CancellationToken ct);
}
```

Reasoning:

- `QuickMarkup.CodeAnalysis` is shared infrastructure for sourcegen/analyzer/plugin-style consumers.
- Other generators such as QuickMarkup Snapshot should be able to reuse the same resolving behavior.
- Avoid duplicating generated-property table logic between `QuickMarkupGenerator` and `QuickMarkupAnalyzer`.

## Analyzer Implementation Options

### Option A: Compilation Start Action

Use `RegisterCompilationStartAction` in `QuickMarkupAnalyzer.Initialize`.

Inside compilation start:

1. Create a thread-safe collection for parsed QuickMarkup attributes.
2. Register a syntax-node action to parse and store each QuickMarkup attribute plus location provider.
3. Register a compilation-end action to build the generated-member table and bind/report diagnostics for all stored attributes.

This gives the analyzer all attributes before binding templates.

Pros:

- Works with the classic `DiagnosticAnalyzer` API.
- Analyzer can build the table once per compilation.
- Keeps diagnostics in the analyzer instead of depending on generated code errors.

Cons:

- Requires storing parsed results and location providers until compilation end.
- Diagnostics may be reported later than the current per-node action.
- Must ensure thread-safety because analyzer execution is concurrent.

### Option B: Compilation Action

Use `RegisterCompilationAction` and manually walk syntax trees for QuickMarkup attributes.

Pros:

- Very direct control over collection/build/bind order.
- Easier to reason about than coordinating syntax actions plus compilation-end action.

Cons:

- Duplicates some discovery logic currently hidden behind `RegisterQuickMarkupAttributeInStringSyntaxAction`.
- May be less incremental inside the analyzer.

### Recommendation

Prefer Option A if `QuickMarkupProviderExtension.Analyzer.cs` can be extended to provide a compilation-level collector cleanly.

Prefer Option B if extending the current helper becomes awkward.

## Files To Read

Start with these files:

- `QuickMarkup.SourceGen/QuickMarkupAnalyzer.cs`
- `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
- `QuickMarkup.CodeAnalysis/QuickMarkupGeneratedMemberTable.cs`
- `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.Refs.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupProviderExtension.Analyzer.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupProviderExtension.SourceGen.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupProviderExtension.Parsing.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupAttributeInString.cs`
- `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupTargetContext.cs`

Useful test files:

- `QuickMarkup.SourceGen.Test/GeneratedCases.cs`
- `QuickMarkup.SourceGen.Test/SourceGenBehaviorTests.cs`
- `QuickMarkup.SourceGen.Test/TestControls.cs`

## Implementation Steps

1. Move generated-member table construction helpers from `QuickMarkupGenerator` into `QuickMarkup.CodeAnalysis`.
2. Update `QuickMarkupGenerator` to call the shared builder.
3. Refactor or add analyzer collection logic so all parsed QuickMarkup attributes in the compilation are available before binding templates.
4. Build `QuickMarkupGeneratedMemberTable` from all successfully parsed QuickMarkup attributes.
5. Bind each template using `CodeTypeResolver` with the generated table and current target full type name.
6. Keep parse diagnostics attached to the same locations as today.
7. Keep binder diagnostics attached through `QuickMarkupSourceCodeLocationProvider`.
8. Add analyzer tests or sourcegen compile tests that fail without analyzer overlay support.

## Diagnostics To Preserve

Existing analyzer descriptors in `QuickMarkupAnalyzer.cs`:

- `QM1001`: unexpected token
- `QM1002`: unexpected ending
- `QM1003`: general typing error
- `QM1004`: too many children
- `QM1005`: close tag mismatched

The analyzer refactor should not change diagnostic IDs unless new collision diagnostics are intentionally added.

## Suggested Tests

If there is no dedicated analyzer test project yet, add compile-time sourcegen tests that assert no analyzer diagnostics for valid generated-property cases.

Minimum cases:

- Generated string ref property is assignable from another QuickMarkup target:

```csharp
<GeneratedPropertyElement Text="hello" />
```

- Generated enum ref property supports enum shorthand:

```csharp
<GeneratedPropertyElement Kind=Secondary />
```

- Generated bool ref property supports boolean shorthand:

```csharp
<GeneratedPropertyElement Flag />
```

- Generated property wins before extension fallback.
- Private generated refs are visible inside their own target but hidden cross-target.
- Generated `Child` / `Content` participates in child-content discovery.

There is already a behavioral sourcegen test for the first three cases:

- `GeneratedQuickMarkupPropertiesAreVisibleToOtherMarkup` in `QuickMarkup.SourceGen.Test/SourceGenBehaviorTests.cs`

That test proves sourcegen behavior, but a future analyzer-specific test should verify diagnostics.

## Risks

- Analyzer concurrency: do not mutate ordinary `List<T>` from concurrent syntax actions unless protected by a lock or replaced by a concurrent collection.
- Location provider lifetime: if stored for compilation-end diagnostics, ensure it only stores stable syntax references/spans, not per-action state that becomes invalid.
- Duplicate parse work: avoid parsing the same markup twice if possible, but correctness is more important than micro-optimization.
- Cache friendliness: keep generated table entries free of `ITypeSymbol` / `IPropertySymbol`; store strings and recover symbols during binding through `CodeTypeResolver`.

## Completion Criteria

- `QuickMarkupAnalyzer` uses `QuickMarkupGeneratedMemberTable` when binding templates.
- `QuickMarkupGenerator` and `QuickMarkupAnalyzer` share table-building logic.
- Sourcegen tests still pass.
- Analyzer diagnostics no longer reject or misclassify markup that uses generated QuickMarkup properties from another target in the same compilation.
