# Structural Codegen Plan

This plan covers source generation for structural child symbols that are already produced by the binder:

- `ChildCollectionLowering.Blocks`
- `QMIfNodeSymbol<T>`
- `QMConditionalValueSymbol<T>`
- `QMForNodeSymbol<T>`
- `QMFragmentNodeSymbol`

Out of scope for this plan:

- Parser / lexer syntax work
- Changing structural infra semantics
- Key/index DSL design beyond consuming the existing symbol fields

## Current State

Current direct child generation in `QuickMarkup.SourceGen/CodeGen/CodeGenContext.cs` assumes `QMAddChildMember.ChildPropertyPath` is a collection property name and emits:

```csharp
target.Children.Add(child);
```

Current foreach generation emits a C# `foreach` / range `for` loop directly. This is still valid for static range loops that do not require UI block lowering, but it is not valid for reactive `foreach`, `if`, or fragment grouping.

The binder already marks sibling child collections:

```csharp
ChildCollectionLowering.DirectAdd
ChildCollectionLowering.Blocks
```

When one sibling requires blocks, all `QMAddChildMember`s in that sibling collection are marked `Blocks`.

## Required Symbol Metadata

Block codegen needs to construct:

```csharp
UIBlockHost<TElement>
IUIBlock<TElement>
StaticBlock<TElement>
ConditionalBlock<TElement>
ForBlock<TSrc, TElement>
FragmentBlock<TElement>
```

So each child collection add member should expose the element type.

Recommended symbol change:

```csharp
public record class QMAddChildMember<T>(
    string ChildPropertyPath,
    IQMNodeChildSymbol Child,
    ChildCollectionLowering CollectionLowering = ChildCollectionLowering.DirectAdd,
    T? ChildElementType = default
) : IQMMemberSymbol;
```

Use the generic form and update existing binder/codegen pattern matches. The binder should emit `QMAddChildMember<ITypeSymbol?>` with `tagInfo.ChildrenType`.

This keeps element type information explicit and avoids inferring it later from child values or collection property metadata.

## Codegen Shape

### Direct Add Path

For `CollectionLowering.DirectAdd`, preserve current behavior:

```csharp
target.Children.Add(CGen(child));
```

Static range foreach may continue to generate a direct C# loop when its `QMAddChildMember.CollectionLowering` is `DirectAdd`.

### Block Path

For `CollectionLowering.Blocks`, generate one host for the whole sibling child collection:

```csharp
var QUICKMARKUP_HOST_0 =
    new global::QuickMarkup.Infra.UIBlockHost<TElement>(
        new global::QuickMarkup.Infra.TargetUICollection<TElement>(target.Children));
```

Then add each sibling as a block:

```csharp
QUICKMARKUP_HOST_0.AddBlock(/* block */);
```

The host variable must be stable for the lifetime of the generated UI instance. For constructor-mode generation, a local variable is enough if nothing needs later access. If future reinitialization/disposal needs to clear hosts, then generated host/root block references should be stored in fields or a generated disposal list.

V1 recommendation:

- Generate the host as a local inside the constructor / `Init`.
- Generate disposable root blocks or slots into `QUICKMARKUP_DISPOSABLES`.
- On reinitialize, dispose every item in `QUICKMARKUP_DISPOSABLES`, clear the list, then rebuild UI.
- `UIBlockHost<T>` itself does not implement `IDisposable`, so add root blocks or wrapper disposables that call `Clear()` where needed.

## Block Factories

Add codegen helpers that return C# expressions or write factory bodies:

```text
CGenBlock(child, elementType, targetContext) -> expression returning IUIBlock<TElement>
CGenBlockBody(members, hostVar, scopeVar, elementType)
CGenScopedValue(child, valueType) -> expression returning ScopedValue<T>
```

The important distinction:

- Direct generation writes elements immediately to `target.Children`.
- Block generation writes blocks to a `UIBlockHost<TElement>`.
- Static blocks build concrete child elements into a local list.

## Static Child Block

Plain node/value children inside a block-lowered sibling collection should become `StaticBlock<TElement>`.

Example generated shape:

```csharp
new global::QuickMarkup.Infra.StaticBlock<TElement>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    (QUICKMARKUP_ITEMS, QUICKMARKUP_SCOPE) => {
        TElement QUICKMARKUP_NODE_1 = ...;
        // property effects should be added to QUICKMARKUP_SCOPE
        QUICKMARKUP_ITEMS.Add(QUICKMARKUP_NODE_1);
    })
```

Required codegen adjustment:

- Replace generated `QUICKMARKUP_EFFECTS` with a broader `QUICKMARKUP_DISPOSABLES`.
- Existing property binding generation should add returned effects to `QUICKMARKUP_DISPOSABLES`.
- Structural block scopes still own effects that are created inside their factories when those effects are added to the block scope by infra or generated block code.
- For V1, codegen does not need a separate generated scope sink if every directly generated effect is tracked in `QUICKMARKUP_DISPOSABLES`, and block/slot objects are also tracked there.

Potential future refinement:

- Add a generated current-scope sink only if we want effects inside structural branch factories to be disposed immediately on branch/item removal instead of waiting for the parent structural disposable to dispose its owned `ReactiveScope`.

## ConditionalBlock Codegen

For collection `if`:

```csharp
new global::QuickMarkup.Infra.ConditionalBlock<TElement>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    () => condition,
    () => /* true block */,
    () => /* false block */)
```

If there is no `else`, pass `null` or omit the false factory:

```csharp
new ConditionalBlock<TElement>(scope, () => condition, trueFactory)
```

Branch bodies should be generated as `FragmentBlock<TElement>` or `StaticBlock<TElement>` depending on content:

- Empty branch: `StaticBlock<TElement>` with no elements.
- One plain child: `StaticBlock<TElement>`.
- Multiple / nested structural children: `FragmentBlock<TElement>`.

Simpler V1:

- Always generate branch bodies as `FragmentBlock<TElement>` for consistency.

## ForBlock Codegen

### Static Range

If `QMForNodeSymbol.Kind == StaticRange` and the add member is `DirectAdd`, preserve current direct loop generation:

```csharp
for (var i = start; i < end; i++) {
    ...
}
```

If `Kind == StaticRange` but the add member is `Blocks`, generate a `FragmentBlock<TElement>` whose build action emits the range loop and adds child blocks into the nested host.

### Reactive Collection

For `QMForKind.ReactiveCollection`, generate:

```csharp
global::QuickMarkup.Infra.ForBlock.Create<TSrc, TElement>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    source,
    itemRef => /* item block */)
```

If `IndexVarName` is present:

```csharp
global::QuickMarkup.Infra.ForBlock.Create<TSrc, TElement>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    source,
    (indexRef, itemRef) => /* item block */)
```

The body must expose ref-backed loop variables to generated expressions:

- `VarName` should refer to `itemRef.Value` when used in body expressions.
- `IndexVarName` should refer to `indexRef.Value`.

Do not rewrite expression strings. Rewriting breaks strings, `nameof(...)`, comments, and other C# constructs.

V1 recommendation:

- Keep the user expression text intact.
- Generate a closure around forward-binding expressions when the expression may reference foreach variables.
- Define local variables from refs inside that closure.

Example for a forward expression:

```csharp
() => {
    var item = itemRef.Value;
    var index = indexRef.Value;
    return $"{index + 1}. {item}";
}
```

The generated property binding can then track that closure:

```csharp
ReferenceTracker.RunAndRerunOnReferenceChange(() => {
    var item = itemRef.Value;
    var index = indexRef.Value;
    return userExpression;
}, value => {
    target.Text = value;
});
```

Use a conservative `\bname\b` check only to avoid emitting unused local definitions. This detection should be done in the binder as optimization metadata, not in codegen. It must not rewrite the expression.

Recommended binder metadata:

```csharp
public record class QMValueSymbol<T>(
    T? Type,
    string ValueInFinalCode,
    IReadOnlySet<string> CapturedLocalNames
) : IQMValueSymbol;
```

or an equivalent scoped/captured-name sidecar on bound values.

Rules:

- Binder records whole-word mentions of in-scope foreach locals.
- Binder aggregates captured names through nested node/member symbols.
- Codegen consumes `CapturedLocalNames` only to decide whether to emit local definitions like `var item = itemRef.Value;`.
- If usage is ambiguous or metadata is unavailable, codegen should prefer emitting the locals rather than risking uncompilable generated code.
- The metadata is an optimization only. It must not affect binding semantics.

For bindback expressions, generate a closure that reads the current item without capturing the reference tracking dependency, mutates it, then writes it back:

```csharp
() => {
    var item = ReferenceTracker.NoCapture(() => itemRef.Value);
    item.Name = newValue;
    itemRef.Value = item;
}
```

The exact generated variable names should be made collision-resistant. Undefined behavior for non-pure expressions still exists, but behavior is defined for string contents and `nameof(...)` because the user expression is not rewritten.

### Keyed For

`QuickMarkupParsedForNode.Key` exists in AST, but `QMForNodeSymbol<T>` currently does not carry a bound key expression.

V1 codegen can support implicit keys immediately:

```csharp
ForBlock.Create<TSrc, TElement>(scope, source, itemFactory)
```

V1 can also support explicit keys once the binder carries a bound key expression on `QMForNodeSymbol<T>`. Prefer omitting type parameters and relying on C# inference:

```csharp
ForBlock.Create(scope, source, keyFactory, itemFactory)
```

For key factories, use the same local-variable closure approach as body expressions:

```csharp
(itemRefValue, indexValue) => {
    var item = itemRefValue;
    var index = indexValue;
    return userKeyExpression;
}
```

Future / required binder work for explicit keys:

- bind `Key` into `QMForNodeSymbol`
- preserve whether the key expression references the index variable
- generate `Func<TSrc, TKey>` or `Func<TSrc, int, TKey>`
- do not rewrite key expression strings

## FragmentBlock Codegen

For `QMFragmentNodeSymbol`:

```csharp
new global::QuickMarkup.Infra.FragmentBlock<TElement>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    (QUICKMARKUP_HOST, QUICKMARKUP_SCOPE) => {
        QUICKMARKUP_HOST.AddBlock(...);
        QUICKMARKUP_HOST.AddBlock(...);
    })
```

Fragments preserve grouping and sibling offsets through their nested host.

In direct-add lowering, fragments should normally not appear because binder currently treats fragments as requiring `Blocks`. If codegen sees a direct-add fragment anyway, it should throw `NotSupportedException` until flattening is explicitly designed.

## ConditionalSlot Codegen

For `QMAssignChildMember` whose child is `QMConditionalValueSymbol<T>`:

```csharp
new global::QuickMarkup.Infra.ConditionalSlot<T>(
    new global::QuickMarkup.Infra.ReactiveScope(),
    () => condition,
    value => target.Content = value,
    () => new global::QuickMarkup.Infra.ScopedValue<T>(trueValue, trueScope),
    () => new global::QuickMarkup.Infra.ScopedValue<T>(falseValue, falseScope));
```

The generated `ConditionalSlot<T>` must be stored/disposed somewhere.

Use one generated disposable list:

```csharp
global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];
```

This is a breaking cleanup from the old generated `QUICKMARKUP_EFFECTS` list. Effects, slots, and structural root blocks should all be stored in `QUICKMARKUP_DISPOSABLES`. On reinitialize, dispose disposables before clearing/rebuilding UI.

## Disposable Handling

Replace generated:

```csharp
global::System.Collections.Generic.List<global::QuickMarkup.Infra.RefEffect> QUICKMARKUP_EFFECTS { get; } = [];
```

with:

```csharp
global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];
```

Then update all generated effect code from:

```csharp
QUICKMARKUP_EFFECTS.Add(effect);
```

to:

```csharp
QUICKMARKUP_DISPOSABLES.Add(effect);
```

On generated reinitialize:

```csharp
foreach (var disposable in QUICKMARKUP_DISPOSABLES)
    disposable.Dispose();
QUICKMARKUP_DISPOSABLES.Clear();
```

This list should also receive `ConditionalSlot<T>` instances and structural root block disposables.

## Suggested Implementation Order

1. Replace `QMAddChildMember` with generic `QMAddChildMember<T>` and include child element type.
2. Update binder to pass `tagInfo.ChildrenType` into `QMAddChildMember`.
3. Update existing direct-add codegen pattern matches for the new generic symbol.
4. Replace generated `QUICKMARKUP_EFFECTS` with `QUICKMARKUP_DISPOSABLES`.
5. Add direct-add compatibility path and ensure existing sourcegen output remains unchanged except `QUICKMARKUP_DISPOSABLES` naming/type.
6. Add block host generation for sibling collections marked `Blocks`.
7. Add `StaticBlock<TElement>` generation for plain node/value children in block context.
8. Add `QMFragmentNodeSymbol` generation.
9. Add `QMIfNodeSymbol<T>` generation via `ConditionalBlock<TElement>`.
10. Add binder metadata for captured foreach local names as an optimization-only signal.
11. Add reactive `QMForNodeSymbol<T>` generation via `ForBlock.Create`, using local-variable closures for foreach body expressions.
12. Preserve direct static range loop generation for direct-add static range loops.
13. Add block-lowered static range loop generation when its body requires blocks.
14. Add `QMConditionalValueSymbol<T>` generation via `ConditionalSlot<T>`.
15. Add explicit-key `ForBlock.Create(scope, source, keyFactory, itemFactory)` codegen after `QMForNodeSymbol<T>` carries the bound key expression.
16. Add sourcegen tests for direct-add preservation and structural generated code shape.

## Tests To Add

Direct-add preservation:

- Plain children still emit `.Children.Add(...)`.
- Static range loop without structural body still emits direct C# loop.

Block lowering:

- Sibling list with `if` emits one `UIBlockHost<TElement>` and all siblings become blocks.
- `if` without else emits `ConditionalBlock<TElement>` with no false factory.
- `if` with else emits true and false factories.
- `fragment` emits `FragmentBlock<TElement>`.
- Reactive `foreach` emits `ForBlock.Create`.
- Static range with structural body emits block-lowered range body.

Single-child conditional:

- Assignment conditional emits `ConditionalSlot<T>`.
- Slot branch scopes dispose when branch switches.

Scope behavior:

- Generated effects are added to `QUICKMARKUP_DISPOSABLES`.
- Structural slots/root blocks are added to `QUICKMARKUP_DISPOSABLES`.
- Reinitialize disposes `QUICKMARKUP_DISPOSABLES` before rebuilding.

Captured local metadata:

- Binder marks foreach-local usage on bound expressions.
- Codegen omits unused closure locals when metadata says they are unused.
- Codegen emits locals conservatively when metadata is missing or ambiguous.

Regression:

- Existing sourcegen test still builds.
- `dotnet build` succeeds.
- `dotnet test QuickMarkup.slnx` succeeds.
