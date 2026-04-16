# Reactive `if` / `for` Design

This design adds structural reactivity to QuickMarkup without a virtual DOM.

The generated code should stop treating child creation as only:

```csharp
parent.Children.Add(child);
```

For structural markup, generated code should instead create logical UI blocks that can mount, unmount, reconcile, and dispose their own effects.

## Goals

Support markup shaped like:

```csharp
if (`condition`) {
    <A />
} else {
    <B />
}

for (var item in items) {
    <A item=`item` />
}
```

Required behavior:

- Removed UI must stop reacting to references.
- Dynamic blocks must insert at correct positions even when earlier sibling blocks change size.
- `if` and `for` structural updates should run on the next scheduler tick, not immediately inside reference or collection change callbacks.
- Multiple `for` collection changes before one tick should collapse into one reconcile pass.
- First version can be unkeyed and index-based. Keyed reconciliation can be added later.

## Core Concepts

- `IUICollection<T>` adapts framework child collections.
- `ReactiveScope` owns effects and disposes them when a block is destroyed.
- `IUIBlock<TElement>` represents a logical fragment of UI.
- `UIBlockHost<TElement>` owns sibling block order and maps block-local indexes to the flat target UI collection.
- `ConditionalBlock<TElement>` owns the currently selected branch block.
- `ForBlock<TItem, TElement>` owns rendered item blocks and reconciles them to the current source collection on the next tick.

## Collection Adapter

<!-- Implemented in QuickMarkup.Infra/IUICollection.cs and QuickMarkup.Infra/TargetUICollection.cs. -->

```csharp
public interface IUICollection<T> : IList<T>
{
    void Move(int oldIndex, int newIndex);
}
```

`IUICollection<T>` is the framework boundary. WinUI, WPF, Avalonia, Uno, and custom UI collections can be wrapped behind this interface.

Use `int`, not `uint`, because `IList<T>`, `NotifyCollectionChangedEventArgs`, and most .NET collection indexes are `int`.

## ReactiveScope

<!-- Implemented in QuickMarkup.Infra/ReactiveScope.cs. -->

```csharp
public sealed class ReactiveScope : IDisposable
{
    readonly List<RefEffect> effects = [];
    bool disposed;

    public void Add(RefEffect effect)
    {
        if (disposed)
        {
            effect.Dispose();
            return;
        }

        effects.Add(effect);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        foreach (var effect in effects)
            effect.Dispose();

        effects.Clear();
    }
}
```

`ReactiveScope` does not make effects run. Effects run because `RefEffect` subscribes to references through `ReferenceTracker.RunAndRerunOnReferenceChange`.

The scope is only an ownership and cleanup mechanism.

Generated binding code should add effects to the nearest active scope:

```csharp
scope.Add(ReferenceTracker.RunAndRerunOnReferenceChange(
    () => SomeValue,
    value => target.SomeProperty = value
));
```

Ownership rule:

```text
root component UI       -> component/root scope
if branch UI            -> branch scope
for item UI             -> item scope
for controller watcher  -> for block controller scope
if controller watcher   -> conditional block controller scope
```

Disposed effects are never re-enabled. If UI is destroyed and later shown again, generated code creates new UI and new effects.

## Scheduler Support

<!-- Implemented in QuickMarkup.Infra/ReactiveScheduler.cs as ScheduleCallback(Action). -->

Structural updates should be scheduled to the next tick.

The existing scheduler schedules `RefEffect`. For `ForBlock`, it is useful to also schedule one-shot callbacks:

```csharp
public static void ScheduleCallback(Action callback);
```

The callback API should:

- run callbacks during `ReactiveScheduler.Tick()`
- run callbacks before the effect queue snapshot so effects scheduled by callbacks can run in the same tick
- coalesce only when the caller coalesces; the scheduler can simply enqueue callbacks
- avoid running callbacks synchronously from `ScheduleCallback`
- preserve current scheduler exception behavior

`ForBlock` should coalesce with its own flags:

```text
dirty
scheduled
disposed
```

If callback cancellation is not supported, scheduled callbacks should check `disposed` and return.

## UI Blocks

<!-- Implemented in QuickMarkup.Infra/IUIBlock.cs. -->

```csharp
public interface IUIBlock<TElement> : IDisposable
{
    int Count { get; }

    void Mount(UIBlockHost<TElement> host);
    void Unmount();
}
```

`Count` is the number of real UI elements currently contributed to the parent target collection.

A block can represent:

- one element
- multiple sibling elements
- an `if` branch
- a `for` loop
- a nested fragment

Blocks should not permanently cache absolute indexes in the target UI collection. Their current global position is derived from their host.

## UIBlockHost

<!-- Implemented in QuickMarkup.Infra/UIBlockHost.cs. -->

`UIBlockHost<TElement>` owns the logical order of sibling blocks and writes to the flat UI target collection.

The host supports two modes:

- root host: writes directly to an `IUICollection<TElement>`
- child host: maps its local element operations through a parent host and owner block

```csharp
public sealed class UIBlockHost<TElement>
{
    readonly IUICollection<TElement>? target;
    readonly UIBlockHost<TElement>? parentHost;
    readonly IUIBlock<TElement>? parentOwner;
    readonly List<IUIBlock<TElement>> blocks = [];

    public UIBlockHost(IUICollection<TElement> target)
    {
        this.target = target;
    }

    public UIBlockHost(UIBlockHost<TElement> parentHost, IUIBlock<TElement> parentOwner)
    {
        this.parentHost = parentHost;
        this.parentOwner = parentOwner;
    }

    public int Count => blocks.Sum(x => x.Count);

    public int GetStartIndex(IUIBlock<TElement> block)
    {
        var index = 0;

        foreach (var current in blocks)
        {
            if (ReferenceEquals(current, block))
                return index;

            index += current.Count;
        }

        throw new InvalidOperationException("Block is not mounted in this host.");
    }

    public void AddBlock(IUIBlock<TElement> block)
    {
        InsertBlock(blocks.Count, block);
    }

    public void InsertBlock(int index, IUIBlock<TElement> block)
    {
        blocks.Insert(index, block);
        block.Mount(this);
    }

    public void RemoveBlock(IUIBlock<TElement> block)
    {
        block.Unmount();
        blocks.Remove(block);
        block.Dispose();
    }

    public void InsertElement(IUIBlock<TElement> owner, int localIndex, TElement element)
    {
        var index = GetStartIndex(owner) + localIndex;

        if (target is not null)
            target.Insert(index, element);
        else
            parentHost!.InsertElement(parentOwner!, index, element);
    }

    public void RemoveElement(IUIBlock<TElement> owner, int localIndex)
    {
        var index = GetStartIndex(owner) + localIndex;

        if (target is not null)
            target.RemoveAt(index);
        else
            parentHost!.RemoveElement(parentOwner!, index);
    }
}
```

Important invariant:

```text
The host stores sibling block order.
The host computes offsets from current sibling Count values.
Blocks do not store permanent absolute parent indexes.
```

This solves offset drift:

```csharp
for (var item in items1) { <A item=`item` /> }
for (var item in items2) { <A item=`item` /> }
```

If `items1` grows, `items2` does not need to be notified. When `items2` later mutates, its start index is recomputed from the current `items1` block count.

## StaticBlock

<!-- Implemented in QuickMarkup.Infra/StaticBlock.cs. -->

A static block owns ordinary generated elements and their effects.

```csharp
sealed class StaticBlock<TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope scope;
    readonly List<TElement> elements = [];

    UIBlockHost<TElement>? host;

    public int Count => elements.Count;

    public StaticBlock(ReactiveScope scope)
    {
        this.scope = scope;

        // generated code creates elements here
        // generated bindings add effects to scope
    }

    public void Mount(UIBlockHost<TElement> host)
    {
        this.host = host;

        for (var i = 0; i < elements.Count; i++)
            host.InsertElement(this, i, elements[i]);
    }

    public void Unmount()
    {
        if (host is null)
            return;

        for (var i = 0; i < elements.Count; i++)
            host.RemoveElement(this, 0);

        host = null;
    }

    public void Dispose()
    {
        Unmount();
        scope.Dispose();
    }
}
```

For permanent root UI, the root block may use a component-level scope. For dynamic UI, each branch or item should get its own scope.

## ConditionalBlock

<!-- Implemented in QuickMarkup.Infra/ConditionalBlock.cs. -->

A conditional block has two lifetimes:

- controller lifetime: watches the condition and lives with the parent block
- branch lifetime: owns the currently mounted true or false branch

```csharp
sealed class ConditionalBlock<TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope controllerScope;
    readonly Func<bool> condition;
    readonly Func<IUIBlock<TElement>> trueFactory;
    readonly Func<IUIBlock<TElement>>? falseFactory;

    UIBlockHost<TElement>? host;
    IUIBlock<TElement>? current;
    bool? currentConditionValue;
    bool disposed;

    public int Count => current?.Count ?? 0;
}
```

Mount behavior:

```text
mount:
  store host
  create condition effect in controller scope
  condition effect reads condition
  if selected branch changed:
    switch branch during the condition effect run
```

The condition effect already runs through `ReactiveScheduler`, so condition changes naturally collapse to the latest value by the next tick. Initial mount may evaluate immediately as part of construction; later reference changes should use the normal scheduled effect rerun.

Branch switch behavior:

```text
switch branch:
  unmount and dispose current branch, if any
  create selected branch, if any
  mount selected branch
```

For no `else`, the false branch is empty.

Lifecycle:

```text
initial false without else:
  no branch mounted

false -> true:
  create true branch
  mount true branch

true -> false:
  dispose true branch
  create false branch, if any
  mount false branch, if any

false -> true again:
  dispose false branch, if any
  create a new true branch
  mount true branch
```

Disposed branch effects are never re-enabled.

## ForBlock

<!-- Implemented in QuickMarkup.Infra/ForBlock.cs with next-tick dirty reconciliation. -->

A `ForBlock` owns:

- a controller scope
- the source collection subscription
- the rendered item states
- a dirty/scheduled reconcile loop

```csharp
sealed class ForBlock<TItem, TElement> : IUIBlock<TElement>
{
    readonly ReactiveScope controllerScope;
    readonly IReadOnlyList<TItem> source;
    readonly Func<Reference<TItem>, IUIBlock<TElement>> itemFactory;

    readonly List<ForItemState<TItem, TElement>> items = [];

    UIBlockHost<TElement>? host;
    bool dirty;
    bool scheduled;
    bool disposed;

    public int Count => items.Sum(x => x.Block.Count);
}
```

For mutable collection notifications, the source should usually be `INotifyCollectionChanged` plus an indexable collection such as `IList<TItem>` or `IReadOnlyList<TItem>`.

Item state:

```csharp
sealed record ForItemState<TItem, TElement>(
    Reference<TItem> ItemRef,
    IUIBlock<TElement> Block
);
```

### Mount

```text
mount:
  store host
  subscribe to source.CollectionChanged, if supported
  create rendered item states for current source items
  mount item blocks in source order
```

Initial mount can render immediately. It does not need to wait one tick because it is part of component construction.

### Collection Changed

Do not mutate UI immediately in `CollectionChanged`.

```text
on source.CollectionChanged:
  MarkDirty()
```

```text
MarkDirty:
  dirty = true

  if scheduled:
    return

  scheduled = true
  ReactiveScheduler.ScheduleCallback(ReconcileOnTick)
```

```text
ReconcileOnTick:
  scheduled = false

  if disposed:
    return

  if host is null:
    return

  if !dirty:
    return

  dirty = false
  ReconcileToCurrentSource()
```

This collapses multiple collection operations before one tick into one reconcile pass.

### V1 Reconcile: Unkeyed Index-Based

<!-- Implemented in QuickMarkup.Infra/ForBlock.cs. -->

The first version should not replay queued collection events. Treat the current source collection as the truth.

Index-based reconcile:

```text
ReconcileToCurrentSource:
  commonCount = min(items.Count, source.Count)

  for each i where 0 <= i < commonCount:
    items[i].ItemRef.Value = source[i]

  while items.Count > source.Count:
    dispose item block at end
    remove item state at end

  while items.Count < source.Count:
    create item state for source[items.Count]
    append item state
    mount item block
```

This gives correct final visual contents after any number of adds, removes, replaces, moves, or resets before the tick.

Tradeoff:

```text
Unkeyed moves preserve UI by index, not by item identity.
```

Example:

```text
old source: [A, B, C]
new source: [C, A, B]
```

Unkeyed reconcile keeps three existing blocks but updates their item references:

```text
block 0 item ref = C
block 1 item ref = A
block 2 item ref = B
```

The UI is visually correct, but the controls are reused by position. This is acceptable for the first implementation and should be documented.

### Optional Event-Specific Fast Path

After the basic dirty/reconcile version works, individual event fast paths can be added.

However, they should still be scheduled to the next tick and coalesced carefully. If any complicated sequence occurs, falling back to full reconcile is preferred.

V1 should prefer correctness and simplicity:

```text
any CollectionChanged event -> dirty reconcile
```

### Disposal

```text
dispose ForBlock:
  set disposed = true
  unsubscribe from source.CollectionChanged
  dispose every item block
  clear item states
  dispose controller scope
```

If a scheduled reconcile callback runs after disposal, it should see `disposed == true` and return.

## Generated Code Shape

Current generated code emits direct child additions:

```csharp
parent.Children.Add(child);
```

Structural reactivity should emit block factories.

Root constructor conceptually becomes:

```csharp
var rootCollection = new TargetUICollection<UIElement>(this.Children);
var rootHost = new UIBlockHost<UIElement>(rootCollection);
var rootScope = new ReactiveScope();

rootHost.AddBlock(CreateRootBlock(rootScope));
```

A normal child becomes part of a `StaticBlock`.

An `if` becomes a `ConditionalBlock`:

```csharp
new ConditionalBlock<UIElement>(
    controllerScope,
    condition: () => IsActive,
    trueFactory: () => CreateTrueBranchBlock(new ReactiveScope()),
    falseFactory: () => CreateFalseBranchBlock(new ReactiveScope())
);
```

A `for` becomes a `ForBlock`:

```csharp
new ForBlock<TItem, UIElement>(
    controllerScope,
    source: Items,
    itemFactory: itemRef => CreateItemBlock(itemRef, new ReactiveScope())
);
```

Generated binding code changes from component-global ownership:

```csharp
QUICKMARKUP_EFFECTS.Add(effect);
```

to nearest-scope ownership:

```csharp
scope.Add(effect);
```

The old `QUICKMARKUP_EFFECTS` list can be retained as the component/root scope backing store, but dynamic branches and loop items need their own scopes so individual blocks can be disposed.

## Nested Blocks

Every block can contain another host.

Example:

```csharp
for (var item in items) {
    if (`item.Value.IsVisible`) {
        <A />
        <B />
    }
}
```

Conceptual structure:

```text
RootHost
  ForBlock
    ItemBlock 0
      ItemHost
        ConditionalBlock
          StaticBlock(A, B)
    ItemBlock 1
      ItemHost
        ConditionalBlock
          empty
```

Each host only understands direct child blocks. Offsets are local and derived from current sibling counts.

## V1 Semantics

```text
if false without else:
  branch does not exist

if toggle:
  dispose old branch
  create new selected branch

for initial mount:
  render current source immediately

for collection change:
  mark dirty
  reconcile on next scheduler tick

for add/remove/move/replace/reset before next tick:
  collapse to one index-based reconcile against final source state

for item removed by reconcile:
  dispose removed item block and effects

for item retained by index:
  update item Reference<T>.Value

for unkeyed move:
  reuse blocks by index, not by item identity
```

## Future Extensions

### Keyed `for`

Keyed loops can preserve item identity across moves and resets:

```text
for (var item in items; key item.Id)
```

Keyed reconcile:

```text
build old key -> item state map
walk current source in order
  if key exists:
    reuse item block
    update ItemRef.Value
    move block if needed
  else:
    create item block
dispose old blocks whose keys disappeared
```

### `v-show` Equivalent

<!-- v1: not required -->

`if` should use destroy/recreate semantics.

A separate `show` feature can be added later:

```text
create UI once
keep effects alive
toggle visibility
```

This should remain separate because it has different lifecycle and state preservation behavior.

## Implementation Order

Recommended order:

1. Make `IUICollection<T>.Move` use `int`.
2. Add `ReactiveScope`.
3. Add scheduler support for one-shot callbacks.
4. Add `IUIBlock<TElement>` and `UIBlockHost<TElement>`.
5. Add `StaticBlock<TElement>`.
6. Add `ConditionalBlock<TElement>`.
7. Add `ForBlock<TItem, TElement>` with dirty next-tick index-based reconcile.
8. Update source generation to emit block factories and pass scopes.
9. Add tests for disposal, sibling offset drift, deferred collection reconciliation, and condition toggling.
