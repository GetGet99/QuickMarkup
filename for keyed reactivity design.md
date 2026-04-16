# Keyed `for` Reactivity Design

This document extends the infrastructure-only `ForBlock` design with keyed identity.

The goal is to replace index-based reuse with identity-based reconciliation while still batching structural UI updates to the next scheduler tick.

## Goals

- Preserve item UI identity across collection moves.
- Support explicit user keys.
- Support implicit operation identity when no explicit key is provided.
- Keep source generation ergonomic through static factory methods.
- Provide manual `Refresh()` behavior.
- Detect duplicate explicit keys.
- Do not make key expressions reactive by default.

## Identity Modes

`ForBlock` should support two identity modes.

<!-- Implemented in QuickMarkup.Infra/ForBlock.cs and QuickMarkup.Infra/ForKeyManager.cs. -->

### Explicit Key Identity

<!-- Implemented in ExplicitForKeyManager<TSrc, TKey> and ForBlock<TSrc, TElement, TKey>. -->

The user provides a key function:

```csharp
key item.Id
```

Infrastructure receives this as one of:

```csharp
Func<TSrc, TKey> keyFn
Func<TSrc, int, TKey> keyFn
```

The index-aware overload also supports intentionally index-keyed rendering:

```csharp
(item, index) => index
```

The framework treats index keys as explicit keys. There is no special index-key mode.

Explicit key behavior:

```text
initial mount:
  compute keys from source

collection changed:
  mark dirty
  reconcile on next tick using recomputed keys

manual Refresh():
  mark dirty
  reconcile on next tick using recomputed keys

same key:
  preserve existing item block
  update item Reference<TSrc>.Value

new key:
  create new item block

missing old key:
  dispose old item block

duplicate key:
  throw during reconcile
```

### Implicit Operation Identity

<!-- Implemented in OperationForKeyManager<TSrc>. -->

If the user does not provide a key, the default identity should come from collection operations.

The key manager assigns internal integer IDs to source positions:

```text
initial mount:
  assign fresh IDs

Add:
  insert fresh IDs

Remove:
  remove IDs

Move:
  move existing IDs

Replace:
  replace old IDs with fresh IDs

Reset:
  assign fresh IDs for all current source items

manual Refresh():
  same as Reset
```

This preserves identity across real `Move` notifications but does not preserve identity across `Reset` or manual refresh.

These IDs are internal operation identities, not public user keys.

## Key Reactivity

<!-- Implemented: explicit key recomputation uses ReferenceTracker.NoCapture and keys are sampled only during initialize, collection change handling, or Refresh(). -->

Key expressions should not be reactive by default.

Keys are sampled during structural reconciliation:

```text
initial mount
collection-changed reconcile
manual Refresh()
```

If an object property used by a key changes but the collection does not change, the key is not automatically reevaluated.

Example:

```csharp
key item.Text
```

If `item.Text` changes without collection mutation:

```text
no structural update happens
```

The user can call `Refresh()` to recompute explicit keys.

Key evaluation should avoid dependency capture:

```csharp
ReferenceTracker.NoCapture(() => keyFn(item, index))
```

This prevents key computation from accidentally subscribing an unrelated reactive effect to every referenced key dependency.

Reactive keys can be considered later as a separate explicit feature.

## Key Manager Interface

<!-- Implemented as IForKeyManager<TSrc, TKey>. -->

Use a generic key type instead of erasing to `object?`.

```csharp
public interface IForKeyManager<TSrc, TKey>
{
    IReadOnlyList<TKey> Keys { get; }

    void Initialize(IReadOnlyList<TSrc> source);
    void ApplyCollectionChanged(NotifyCollectionChangedEventArgs e, IReadOnlyList<TSrc> source);
    void Refresh(IReadOnlyList<TSrc> source);
}
```

The key list must always match source order and source count after initialization or collection-change processing:

```text
Keys.Count == source.Count
```

Do not store keys in a dictionary keyed by `TSrc`. Source values may be duplicated or mutable.

## Operation Key Manager

<!-- Implemented as OperationForKeyManager<TSrc>. -->

```csharp
public sealed class OperationForKeyManager<TSrc> : IForKeyManager<TSrc, int>
{
    readonly List<int> keys = [];
    int nextId;

    public IReadOnlyList<int> Keys => keys;
}
```

Behavior:

```text
Initialize(source):
  clear keys
  add one fresh ID for each source item

ApplyCollectionChanged(Add):
  insert fresh IDs at NewStartingIndex

ApplyCollectionChanged(Remove):
  remove IDs at OldStartingIndex

ApplyCollectionChanged(Move):
  move existing IDs from OldStartingIndex to NewStartingIndex

ApplyCollectionChanged(Replace):
  remove old IDs
  insert fresh IDs for new items

ApplyCollectionChanged(Reset):
  Initialize(source)

Refresh(source):
  Initialize(source)
```

Range changes should be handled when `NotifyCollectionChangedEventArgs` contains multiple items.

## Explicit Key Manager

<!-- Implemented as ExplicitForKeyManager<TSrc, TKey>. -->

```csharp
public sealed class ExplicitForKeyManager<TSrc, TKey> : IForKeyManager<TSrc, TKey>
{
    readonly Func<TSrc, int, TKey> keyFn;
    readonly List<TKey> keys = [];

    public IReadOnlyList<TKey> Keys => keys;
}
```

Behavior:

```text
Initialize(source):
  Recompute(source)

ApplyCollectionChanged(any):
  Recompute(source)

Refresh(source):
  Recompute(source)
```

Recompute:

```text
clear keys
for each source item and index:
  key = ReferenceTracker.NoCapture(() => keyFn(item, index))
  add key
```

Duplicate validation can happen in `ForBlock` during reconciliation.

## Factories

<!-- Implemented as ForKeyManager and ForBlock static factories. -->

Use static factories so generated code does not need to name `TKey` directly in common cases.

```csharp
public static class ForKeyManager
{
    public static IForKeyManager<TSrc, int> CreateImplicit<TSrc>()
        => new OperationForKeyManager<TSrc>();

    public static IForKeyManager<TSrc, TKey> Create<TSrc, TKey>(
        Func<TSrc, TKey> keyFn)
        => new ExplicitForKeyManager<TSrc, TKey>((item, _) => keyFn(item));

    public static IForKeyManager<TSrc, TKey> Create<TSrc, TKey>(
        Func<TSrc, int, TKey> keyFn)
        => new ExplicitForKeyManager<TSrc, TKey>(keyFn);
}
```

`ForBlock` should also have static factory methods:

```csharp
public static class ForBlock
{
    public static ForBlock<TSrc, TElement, int> Create<TSrc, TElement>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory);

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory);

    public static ForBlock<TSrc, TElement, TKey> Create<TSrc, TElement, TKey>(
        ReactiveScope controllerScope,
        IReadOnlyList<TSrc> source,
        Func<TSrc, int, TKey> keyFn,
        Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory);
}
```

The existing `ForBlock<TSrc, TElement>` can remain as a convenience wrapper for implicit operation identity, or it can be replaced by the static factory in generated code later.

## ForBlock Shape

<!-- Implemented as ForBlock<TSrc, TElement, TKey>, with ForBlock<TSrc, TElement> as the implicit-operation convenience subclass. -->

Introduce the keyed implementation:

```csharp
public sealed class ForBlock<TSrc, TElement, TKey> : IUIBlock<TElement>
{
    readonly ReactiveScope controllerScope;
    readonly IReadOnlyList<TSrc> source;
    readonly INotifyCollectionChanged? collectionChanged;
    readonly IForKeyManager<TSrc, TKey> keyManager;
    readonly Func<Reference<TSrc>, IUIBlock<TElement>> itemFactory;

    readonly List<ForItemState<TSrc, TElement, TKey>> items = [];
}
```

Item state:

```csharp
public sealed record ForItemState<TSrc, TElement, TKey>(
    TKey Key,
    Reference<TSrc> ItemRef,
    IUIBlock<TElement> Block
);
```

The non-keyed convenience type can be:

```csharp
public sealed class ForBlock<TSrc, TElement> : IUIBlock<TElement>
{
    readonly ForBlock<TSrc, TElement, int> inner;
}
```

or it can be removed once sourcegen uses `ForBlock.Create(...)`.

## Dirty Scheduling

<!-- Implemented in ForBlock<TSrc, TElement, TKey>. -->

Keep the existing next-tick structural scheduling.

```text
CollectionChanged:
  keyManager.ApplyCollectionChanged(e, source)
  MarkDirty()

Refresh():
  keyManager.Refresh(source)
  MarkDirty()
```

Do not mutate UI immediately from `CollectionChanged`.

## Reconciliation

<!-- Implemented in ForBlock<TSrc, TElement, TKey>. -->

On next tick:

```text
ReconcileToCurrentSource:
  validate keyManager.Keys.Count == source.Count
  validate keys are unique

  oldByKey = map current item states by Key
  newItems = []

  for each index in source:
    key = keyManager.Keys[index]
    item = source[index]

    if oldByKey contains key:
      state = oldByKey[key]
      state.ItemRef.Value = item
      newItems.Add(state)
    else:
      create new item state for key and item
      newItems.Add(state)

  dispose old states not present in newItems
  reorder mounted blocks to match newItems
  items = newItems
```

Duplicate keys:

```text
throw InvalidOperationException
```

Use `EqualityComparer<TKey>.Default`.

## Reordering Blocks

<!-- Implemented with detach/remount through UIBlockHost.DetachBlock. -->

The first implementation can preserve block instances while using unmount/remount for position changes.

Rules:

```text
new item:
  create block
  mount at final position

removed item:
  unmount and dispose block

reused item whose position changed:
  unmount block
  keep scope and block alive
  remount block at final position

reused item whose position did not change:
  keep mounted
```

This preserves effects/control instances for reused keys even if the physical collection operation is remove+insert internally.

Later, `UIBlockHost` can grow a block-level move operation if needed.

## Manual Refresh

<!-- Implemented as Refresh() on ForBlock<TSrc, TElement> and ForBlock<TSrc, TElement, TKey>. -->

```csharp
public void Refresh();
```

Behavior:

```text
implicit operation identity:
  regenerate internal IDs
  next reconcile disposes old blocks and creates new blocks

explicit key identity:
  recompute keys
  next reconcile preserves blocks with matching keys
```

`Refresh()` should be safe to call before mount, after mount, and multiple times before one tick.

## Tests

Add tests for:

- implicit identity preserves block instance across `Move`
- implicit identity creates new block on `Replace`
- implicit identity recreates all blocks on `Reset`
- implicit `Refresh()` recreates all blocks
- explicit keys preserve block instance across `Reset` when keys match
- explicit keys preserve block instance across remove/add reorder when keys match after `Refresh()`
- explicit duplicate keys throw
- key function is not reactively tracked by default
- multiple collection changes before one tick reconcile once to final keyed order
- source values with duplicates still work in implicit operation identity mode

## Implementation Order

1. Add `IForKeyManager<TSrc, TKey>`.
2. Add `OperationForKeyManager<TSrc>`.
3. Add `ExplicitForKeyManager<TSrc, TKey>`.
4. Add `ForKeyManager` static factories.
5. Introduce `ForBlock<TSrc, TElement, TKey>`.
6. Rework existing `ForBlock<TSrc, TElement>` into an implicit-operation wrapper or keep it as a forwarding convenience class.
7. Add `ForBlock` static factory methods.
8. Add `Refresh()`.
9. Add keyed reconciliation and duplicate-key validation.
10. Add tests.
