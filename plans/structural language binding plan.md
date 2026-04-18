# Structural Language Binding Plan

This plan covers language model work for structural children after the infrastructure changes.

Scope:

- AST changes
- symbol / binding data structure changes
- binder changes

<!-- IMPLEMENTED: AST, symbol, and binder data-flow changes are in place. -->

Out of scope:

- parser / lexer implementation, except minimal compatibility edits needed after AST shape changes
- code generation / source generation

<!-- NOT IMPLEMENTED: parser / lexer syntax work and codegen lowering remain out of scope for this pass. -->

## Current State

Relevant files:

- `QuickMarkup.Language/AST/QuickMarkupSFC.cs`
- `QuickMarkup.Language/Symbols/QuickMarkupSymbols.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.Language/Parser/QuickMarkupParser.cs`

Existing pieces:

- `QuickMarkupParsedForNode` already exists.
- `QuickMarkupParsedIfNode` exists as a placeholder but is not bound.
- `QuickMarkupQMs` already represents `<>...</>`-style nested child lists as a value.
- `QMForNodeSymbol<T>` exists, but it represents old/static loop lowering and does not distinguish range/static loops from reactive collection loops.
- There is no bound symbol for `if`.
- There is no bound symbol for fragments.
- Binder currently only handles `QuickMarkupParsedTag`, `QuickMarkupValue`, and `QuickMarkupParsedForNode` in child lists.

<!-- IMPLEMENTED: Current-state gaps above have been addressed for AST/symbol/binder. `QuickMarkupParsedIfNode` binds, `QuickMarkupParsedFragmentNode` exists and binds, `QMForNodeSymbol<T>` now carries `QMForKind`, and child-list binding handles tag/value/if/foreach/fragment nodes. -->

## Goals

Support these structural forms at AST/symbol/binder level:

Single-child assignment context:

- `if` / `else`
- missing `else` exists in AST but binder rejects it
- each branch must bind to exactly one child value
- nested `if` allowed
- `foreach` rejected
- multiple children rejected

<!-- IMPLEMENTED: Single-child assignment binding supports conditional values, requires else, validates branch cardinality, allows nested conditionals, rejects foreach, and rejects fragments. -->

Multiple-child collection context:

- `if` / optional `else`
- `foreach`
- fragment
- static range loops
- reactive collection loops
- v1 detection for direct `Children.Add(...)` vs UI block lowering

<!-- IMPLEMENTED: Collection binding supports if, foreach, fragment, static range vs reactive collection classification, and collection-wide `ChildCollectionLowering` detection. -->

Implementation guidance:

- AST and symbol shapes may be changed more aggressively when compatibility constructors or default parameters keep existing parser, binder, and codegen call sites compiling during migration.
- Binder diagnostics should at least be centralized behind helper methods. New `QMBinderError` types / diagnostic descriptors are optional, but recommended when the diagnostic will be useful for analyzers or alternate reporting later.
- Structural lowering detection must not short-circuit binding. The binder should continue checking all nodes so users get as many syntax/binding diagnostics as possible in one pass.

<!-- IMPLEMENTED: Compatibility constructors/default parameters were used, diagnostics are centralized in binder helper methods, and structural lowering is detected after binding sibling children. -->

## AST Plan

### If Node

<!-- IMPLEMENTED: `QuickMarkupParsedIfNode` retains nullable `BodyWhenFalse`, so the AST can represent a missing else branch while binder decides whether that is valid. -->

Keep the AST able to represent a missing `else`:

```csharp
public record class QuickMarkupParsedIfNode(
    QuickMarkupValue Condition,
    ListAST<IQMNodeChild> BodyWhenTrue,
    ListAST<IQMNodeChild>? BodyWhenFalse
) : AST, IQMNodeChild;
```

Binder, not AST, enforces whether `else` is required.

Parser work is out of scope, but later parser rules should allow:

```csharp
if (`condition`) {
    ...
} else {
    ...
}
```

and possibly single-statement forms if desired.

### For Node

<!-- IMPLEMENTED: `QuickMarkupParsedForNode` now includes optional `IndexVarName` and `Key`, with the old constructor preserved for compatibility. Parser/codegen use of these fields remains future work. -->

Extend `QuickMarkupParsedForNode` when needed for index binding and keys. This can be done aggressively if compatibility constructors or defaults preserve existing construction paths.

Possible future shape:

```csharp
public record class QuickMarkupParsedForNode(
    TypeDeclaration? VarType,
    string VarName,
    QuickMarkupValue Iterable,
    ListAST<IQMNodeChild> Body,
    string? IndexVarName = null,
    QuickMarkupValue? Key = null
) : AST, IQMNodeChild
{
    public QuickMarkupParsedForNode(
        TypeDeclaration? VarType,
        string VarName,
        QuickMarkupValue Iterable,
        ListAST<IQMNodeChild> Body)
        : this(VarType, VarName, Iterable, Body, null, null)
    {
    }
}
```

For this binder pass, key and index syntax can remain parser/codegen out of scope. The AST can still be shaped for it now.

### Fragment Node

<!-- IMPLEMENTED: `QuickMarkupParsedFragmentNode` was added as an explicit structural child node. -->

Add an explicit fragment AST node instead of overloading `QuickMarkupQMs`.

```csharp
public record class QuickMarkupParsedFragmentNode(
    ListAST<IQMNodeChild> Children
) : AST, IQMNodeChild;
```

Reasoning:

- `QuickMarkupQMs` is currently a value form for property assignment-like nested values.
- A fragment in children is a structural child node.
- Keeping these separate lets the binder distinguish value-list assignment from child-list grouping.

Parser work is out of scope, but future syntax may be:

```csharp
{
    ...
}
```

or:

```xml
<>
    ...
</>
```

The `{ ... }` body used by `if` / `foreach` is also semantically a fragment.

## Symbol Plan

Add structural child symbols.

### Child Lowering Mode

<!-- IMPLEMENTED: `ChildCollectionLowering` was added with `DirectAdd` and `Blocks`. -->

Add a marker for whether a bound child list needs structural block lowering.

```csharp
public enum ChildCollectionLowering
{
    DirectAdd,
    Blocks
}
```

This is not codegen yet, but symbols should expose enough information for codegen to decide.

The lowering mode is semantically a property of a sibling child collection, not just one child. If one child in a collection requires blocks, all siblings in that same collection should be marked with `Blocks`.

<!-- IMPLEMENTED: Binder computes lowering over the bound sibling list and writes the same lowering value to each `QMAddChildMember` in that collection. -->

### If Symbol

<!-- IMPLEMENTED: `QMIfNodeSymbol<T>` and `QMConditionalValueSymbol<T>` were added. -->

For collection children:

```csharp
public record class QMIfNodeSymbol<T>(
    IQMValueSymbol Condition,
    IReadOnlyList<IQMMemberSymbol> BodyWhenTrue,
    IReadOnlyList<IQMMemberSymbol>? BodyWhenFalse
) : IQMNodeChildSymbol;
```

For single-child assignment:

```csharp
public record class QMConditionalValueSymbol<T>(
    IQMValueSymbol Condition,
    IQMNodeChildSymbol ValueWhenTrue,
    IQMNodeChildSymbol ValueWhenFalse
) : IQMNodeChildSymbol;
```

The two symbols are intentionally separate:

- collection `if` maps to `ConditionalBlock<TElement>`
- single-child `if` maps to `ConditionalSlot<T>`

### For Symbol

<!-- IMPLEMENTED: `QMForKind` and the extended `QMForNodeSymbol<T>` were added with a compatibility constructor. -->

Split loop kind explicitly:

```csharp
public enum QMForKind
{
    StaticRange,
    ReactiveCollection
}
```

Replace or extend current symbol:

```csharp
public record class QMForNodeSymbol<T>(
    QMForKind Kind,
    T? VarType,
    string VarName,
    IQMValueSymbol Iterable,
    IReadOnlyList<IQMMemberSymbol> Body,
    string? IndexVarName = null
) : IQMNodeChildSymbol
{
    public QMForNodeSymbol(
        T? VarType,
        string VarName,
        IQMValueSymbol Iterable,
        IReadOnlyList<IQMMemberSymbol> Body)
        : this(QMForKind.ReactiveCollection, VarType, VarName, Iterable, Body, null)
    {
    }
}
```

Semantics:

- range iterable (`QMRangeSymbol`) is `StaticRange`
- non-range iterable is `ReactiveCollection`
- `StaticRange` does not require UI block lowering by itself
- `ReactiveCollection` requires UI block lowering
- if the body of a static range contains structural children, the containing child list still needs block lowering

<!-- IMPLEMENTED: Binder sets `StaticRange` for `QMRangeSymbol` iterables and `ReactiveCollection` otherwise. Structural lowering treats reactive foreach as block-lowered and static range foreach as block-lowered only when its body contains structural children. -->

### Fragment Symbol

<!-- IMPLEMENTED: `QMFragmentNodeSymbol` was added. -->

```csharp
public record class QMFragmentNodeSymbol(
    IReadOnlyList<IQMMemberSymbol> Body
) : IQMNodeChildSymbol;
```

Fragments require block lowering when they need to preserve grouping or contain structural children.

<!-- IMPLEMENTED: V1 classification treats fragments as requiring block lowering. -->

For simple direct-add contexts, binder/codegen may flatten a static fragment later, but the symbol should keep it explicit for v1.

### Child Member Lowering Metadata

<!-- IMPLEMENTED: `QMAddChildMember` now carries `ChildCollectionLowering CollectionLowering = ChildCollectionLowering.DirectAdd`. -->

Current add child member:

```csharp
public record class QMAddChildMember(string ChildPropertyPath, IQMNodeChildSymbol Child) : IQMMemberSymbol;
```

<!-- UPDATED: `ChildPropertyPath` should be the child collection property, such as `Children`, not a baked method path like `Children.Add`. Codegen decides whether to call `.Add` or use block lowering. -->

Add collection-level metadata:

```csharp
public record class QMAddChildMember(
    string ChildPropertyPath,
    IQMNodeChildSymbol Child,
    ChildCollectionLowering CollectionLowering = ChildCollectionLowering.DirectAdd
) : IQMMemberSymbol;
```

Or add a wrapper child-list symbol:

```csharp
public record class QMChildCollectionMember(
    string ChildPropertyPath,
    IReadOnlyList<IQMNodeChildSymbol> Children,
    ChildCollectionLowering Lowering
) : IQMMemberSymbol;
```

Recommendation for v1:

Keep `QMAddChildMember` to minimize codegen impact, and add:

```csharp
ChildCollectionLowering CollectionLowering
```

Set this consistently for every child member in the same bound child collection.

<!-- IMPLEMENTED: Collection binding applies the computed lowering consistently to each pending `QMAddChildMember`. -->

## Binder Plan

### Compatibility Strategy

<!-- IMPLEMENTED: New AST/symbol shapes preserve old construction paths with default parameters or compatibility constructors. -->

For AST and symbol records that need new properties, prefer:

```text
new primary constructor shape
plus compatibility constructor for old call sites
```

or default parameters where they are safe.

This allows the language model to move toward the desired structure without forcing parser/codegen/sourcegen changes in the same commit.

### Core Classification

<!-- IMPLEMENTED: Binder has `RequiresStructuralLowering(...)` and `ContainsStructuralChildren(...)` helpers. An explicit `IsStructuralChild(...)` helper was not added because the current binder only needs lowering classification. -->

Add helpers:

```csharp
static bool IsStructuralChild(IQMNodeChildSymbol child);
static bool RequiresStructuralLowering(IQMNodeChildSymbol child);
static bool ContainsStructuralChildren(IReadOnlyList<IQMMemberSymbol> members);
```

Structural children:

- `QMIfNodeSymbol`
- `QMConditionalValueSymbol`
- `QMFragmentNodeSymbol`
- reactive `QMForNodeSymbol`
- static range `QMForNodeSymbol` only if its body contains structural children

Classification should be based on fully bound child symbols. Do not skip binding siblings just because one structural child has already been found.

<!-- IMPLEMENTED: Binder first binds each collection child into a pending member list, then computes lowering. -->

### Multiple-Child Collection Binding

<!-- IMPLEMENTED: `ChildrenModes.Add` binding now supports tag, value, if, foreach, and fragment children. -->

For `ChildrenModes.Add`:

- tags bind as before
- values bind as before
- `QuickMarkupParsedIfNode` binds to `QMIfNodeSymbol`
- `QuickMarkupParsedForNode` binds to `QMForNodeSymbol`
- `QuickMarkupParsedFragmentNode` binds to `QMFragmentNodeSymbol`

Rules:

```text
if:
  else optional
  true body may contain zero or more children
  false body may contain zero or more children

foreach:
  allowed
  range iterable -> static range symbol
  non-range iterable -> reactive collection symbol

fragment:
  allowed
  zero or more children
```

When any sibling in a collection child list requires structural lowering, all siblings in that same collection list must be lowered as blocks by codegen.

Binder should expose this through `CollectionLowering`.

Recommended binding flow:

```text
create temporary list of bound child symbols

for each parsed child:
  bind child
  collect diagnostics
  add bound child to temporary list

requiresBlocks = any bound child requires structural lowering
lowering = requiresBlocks ? Blocks : DirectAdd

for each bound child:
  emit QMAddChildMember(..., CollectionLowering = lowering)
```

Do not return early after detecting `requiresBlocks`.

<!-- IMPLEMENTED: Lowering is computed after all children are bound. -->

### Single-Child Assignment Binding

<!-- IMPLEMENTED: `ChildrenModes.Assignment` now routes through single-child binding and supports conditional values. -->

For `ChildrenModes.Assignment`:

Current behavior allows exactly one child and then sets `childrenMode = None`.

Add special handling for `QuickMarkupParsedIfNode`:

Rules:

```text
if:
  allowed only if BodyWhenFalse is not null
  true branch must bind to exactly one child value
  false branch must bind to exactly one child value
  nested if allowed if it also satisfies single-child rules
  foreach rejected
  fragment rejected unless it contains exactly one valid single-child value and binder chooses to unwrap it
```

Recommendation:

For v1, reject fragments in single-child assignment unless there is a clear need to unwrap. This keeps diagnostics simple.

<!-- IMPLEMENTED: Fragment is rejected in single-child assignment for v1. -->

Bind to:

```csharp
QMConditionalValueSymbol<T>
```

The resulting `QMAssignChildMember` child is the conditional value symbol.

### Single-Child Helper

<!-- IMPLEMENTED: Binder has `BindSingleChildNode(...)`, `BindSingleChildBranch(...)`, and diagnostics helpers rather than exactly the proposed helper name. -->

Add binder helper:

```csharp
IQMNodeChildSymbol BindSingleChildValue(
    ListAST<IQMNodeChild> children,
    QMBinderTagInfo tagInfo,
    AST errorOwner);
```

Behavior:

```text
children.Count != 1:
  error

child is tag:
  bind tag

child is QuickMarkupValue:
  bind value with target child type

child is QuickMarkupParsedIfNode:
  bind conditional value recursively

child is QuickMarkupParsedForNode:
  error

child is fragment:
  error in v1, or recursively unwrap only if exactly one child
```

### If Binder

<!-- IMPLEMENTED: Binder has `BindCollectionIf(...)` and `BindSingleChildIf(...)`; single-child binding rejects a missing else branch. -->

Collection context:

```csharp
QMIfNodeSymbol<ITypeSymbol?> BindCollectionIf(
    QuickMarkupParsedIfNode ifNode,
    QMBinderTagInfo parentTagInfo)
```

Single-child context:

```csharp
QMConditionalValueSymbol<ITypeSymbol?> BindSingleChildIf(
    QuickMarkupParsedIfNode ifNode,
    QMBinderTagInfo parentTagInfo)
```

Single-child binder must reject missing `else`.

### For Binder

<!-- IMPLEMENTED: `Bind(QuickMarkupParsedForNode, QMBinderTagInfo)` sets `QMForKind.StaticRange` for range iterables and `QMForKind.ReactiveCollection` otherwise. -->

Current binder:

```csharp
QMForNodeSymbol<ITypeSymbol> Bind(QuickMarkupParsedForNode forNode, QMBinderTagInfo tagInfo)
```

Update to set `QMForKind`:

```text
if Iterable binds to QMRangeSymbol:
  Kind = StaticRange
else:
  Kind = ReactiveCollection
```

Keep static range loops direct/codegen-compatible later.

Reject `QuickMarkupParsedForNode` in assignment mode.

<!-- IMPLEMENTED: Single-child assignment reports an error for foreach and returns a recovery child. -->

### Fragment Binder

<!-- IMPLEMENTED: Binder has `BindFragment(...)` for collection context and rejects fragment in single-child assignment context. -->

Collection context:

```csharp
QMFragmentNodeSymbol BindFragment(
    QuickMarkupParsedFragmentNode fragment,
    QMBinderTagInfo parentTagInfo)
```

Bind its body with the same parent tag info.

Assignment context:

v1 reject:

```text
Fragment is not allowed in single-child assignment context.
```

Possible future extension:

```text
unwrap if exactly one valid single child
```

## Diagnostics Plan

<!-- PARTIALLY IMPLEMENTED: The planned diagnostics are centralized behind helper methods and reported as general binder errors where appropriate. No new dedicated `QMBinderError` record types or analyzer diagnostic descriptors were added for structural errors. -->

Current binder uses `ErrorChildrenTooMany(...)` and a few structural errors.

Add diagnostics for:

- missing `else` in single-child conditional
- branch has zero children in single-child conditional
- branch has multiple children in single-child conditional
- `foreach` not allowed in single-child assignment
- fragment not allowed in single-child assignment, if v1 rejects it
- invalid child mode for structural node

<!-- IMPLEMENTED: Missing else, branch cardinality, foreach-in-single-child, and fragment-in-single-child diagnostics exist. Invalid child mode currently falls back to existing `QMBinderChildrenTooMany` / general binder behavior rather than a dedicated structural diagnostic. -->

At minimum, add binder helper functions so repeated diagnostics do not duplicate strings:

```csharp
void ErrorSingleChildConditionalRequiresElse(QuickMarkupParsedIfNode node);
void ErrorSingleChildBranchMustHaveExactlyOneChild(AST node, string branchName, int actualCount);
void ErrorForNotAllowedInSingleChild(AST node);
void ErrorFragmentNotAllowedInSingleChild(AST node);
void ErrorStructuralChildNotAllowed(AST node, QMBinderTagInfo parentTagInfo);
```

These helpers can initially call a generic binder error. New `QMBinderError` records / diagnostic descriptors can be added when useful for analyzers or richer diagnostics.

<!-- IMPLEMENTED: Helpers currently call generic binder errors, matching the minimal diagnostic plan. -->

## Direct Add vs UI Block Detection

<!-- IMPLEMENTED: Binder stores direct-add vs blocks metadata on child add members. Codegen consumption of that metadata remains future work. -->

V1 rule:

```text
If a child collection contains no structural nodes:
  codegen may keep direct Children.Add(...)

If a child collection contains any structural node requiring block lowering:
  the whole sibling child collection must use UIBlockHost / IUIBlock lowering
```

Do not inspect `INotifyPropertyChanged`.

Reasoning:

- property/value reactivity does not require UI blocks
- UI blocks are needed when the child collection shape can change or grouping matters
- static markup should keep the existing simple generated code path

Structural lowering requirements:

```text
collection if:
  requires blocks

reactive foreach:
  requires blocks

fragment:
  requires blocks in v1

static range foreach:
  does not require blocks by itself
  requires blocks if its body contains structural children
```

Detection should be a post-bind classification step over the bound child collection. It should not prevent binding or diagnostic collection for later siblings.

<!-- IMPLEMENTED: Detection is post-bind over the pending child collection. -->

## Range Loop Semantics

<!-- IMPLEMENTED AT BINDER/SYMBOL LEVEL: Range loops bind as `QMForKind.StaticRange`. Codegen behavior is still the existing non-UI-block loop lowering until structural codegen is designed. -->

Range loops remain static.

```csharp
foreach (var row in ..3) {
    <TextBlock Text=`$"Row {row + 1}"` />
}
```

Semantics:

```text
evaluated once during UI construction
no ForBlock
no key manager
no Refresh
loop variable is a normal generated C# local
```

If a range loop body contains structural children, the body can still require block lowering.

<!-- IMPLEMENTED: Static range foreach requires block lowering when `ContainsStructuralChildren(forNode.Body)` is true. -->

## Parser / Lexer Notes

<!-- NOT IMPLEMENTED: Parser / lexer structural syntax work remains future work and was intentionally out of scope. Existing lexer tests were updated from stale `for` text to current `foreach` text. -->

Out of scope for this plan.

Minimal compile-safe parser edits may be needed after AST constructor changes.

Current parser already references:

- `ParsedForNode`
- `ParsedIfNode`
- `QuickMarkupQMs`

Future parser work should:

- bind `ParsedIfNode` grammar to `QuickMarkupParsedIfNode`
- add `QuickMarkupParsedFragmentNode` grammar
- decide whether `{ ... }` standalone fragment and `<>...</>` both parse to the same AST
- keep range syntax under existing `QMRange`

## Tests To Add Later

<!-- PARTIALLY IMPLEMENTED: Existing syntax and integration tests were updated so the repository builds and tests pass. Dedicated binder tests for structural AST-created nodes have not been added yet because parser syntax is still out of scope and the binder is currently internal to the sourcegen/codeanalysis assembly. -->

AST / parser tests, once parser work enters scope:

- parse if with else
- parse if without else
- parse fragment
- parse nested if
- parse foreach range
- parse foreach collection

<!-- NOT IMPLEMENTED: Parser tests wait for parser / lexer syntax implementation. -->

Binder tests:

- collection `if` with no else binds
- collection `if` with else binds
- single-child `if` with else binds to conditional value
- single-child `if` without else errors
- single-child `if` true branch with multiple children errors
- single-child `if` false branch with multiple children errors
- single-child `foreach` errors
- collection static range foreach is `StaticRange`
- collection non-range foreach is `ReactiveCollection`
- fragment in collection binds
- fragment in single-child context errors
- child collection with structural node marks structural lowering
- child collection with only plain tags remains direct-add eligible

<!-- NOT IMPLEMENTED: Dedicated binder tests are still pending. They should either be added after parser support exists, or after introducing a small test-facing binder harness / InternalsVisibleTo path. -->

## Implementation Order

1. Add / finalize AST nodes:
   - keep `QuickMarkupParsedIfNode`
   - add `QuickMarkupParsedFragmentNode`
   - extend `QuickMarkupParsedForNode` for index/key if desired, with compatibility constructors/defaults
   <!-- IMPLEMENTED -->
2. Add symbols:
   - `QMIfNodeSymbol<T>`
   - `QMConditionalValueSymbol<T>`
   - `QMFragmentNodeSymbol`
   - `QMForKind`
   - extend `QMForNodeSymbol<T>` with compatibility constructors/defaults
   - add `ChildCollectionLowering` metadata
   <!-- IMPLEMENTED -->
3. Add binder helpers:
   - single-child binder
   - structural detection helpers
   - branch cardinality validation
   - centralized structural diagnostic helpers
   <!-- IMPLEMENTED -->
4. Bind collection structural children:
   - if
   - foreach
   - fragment
   <!-- IMPLEMENTED -->
5. Bind single-child conditional values:
   - require else
   - reject invalid branch cardinality
   - reject foreach
   <!-- IMPLEMENTED -->
6. Add binder diagnostics.
   <!-- IMPLEMENTED: generic binder diagnostics with centralized helper methods. Dedicated diagnostic descriptors remain future work. -->
7. Update existing binder tests or add new binder tests.
   <!-- NOT IMPLEMENTED: dedicated structural binder tests remain pending. -->

## Remaining Work

<!-- NEXT: Implement parser / lexer syntax for `if`, `else`, standalone fragments, and any key/index foreach syntax that should be accepted by the DSL. -->

<!-- NEXT: Implement source generation for `ChildCollectionLowering.Blocks`, `QMIfNodeSymbol<T>`, `QMConditionalValueSymbol<T>`, `QMFragmentNodeSymbol`, and reactive `QMForNodeSymbol<T>` using the infra block types. -->

<!-- NEXT: Add structural binder tests. Best options are either parser-backed tests after syntax exists, or a small test-facing binder harness / InternalsVisibleTo path that can construct AST nodes directly. -->

<!-- NEXT: Decide whether structural binder diagnostics should remain generic `QM1003` errors or get dedicated `QMBinderError` record types and diagnostic descriptors. -->
