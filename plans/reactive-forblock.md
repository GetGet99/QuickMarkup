# Plan: `ReactiveList` support in `ForBlock` / `foreach`

Status: design agreed, pending implementation
Date: 2026-08-08

## Goal

Make the new `ReactiveList<T>` work with `ForBlock` and the `foreach` QuickMarkup keyword. The
`foreach` iterable should react to changes through the reference system, not only through
`INotifyCollectionChanged`.

## Current state

`ForBlock<TSrc, TElement, TKey>` (`QuickMarkup.Infra/ForBlock.cs`) today:

- Takes `IReadOnlyList<TSrc> source`; subscribes to `INotifyCollectionChanged` when the source
  implements it.
- Each `CollectionChanged` event → key manager incremental update → mark dirty → scheduler tick →
  full reconcile (diff by key, reusing `ForItemState` blocks for identity).

`ReactiveList<T>` (`QuickMarkup.Infra/Collections/ReactiveList.cs`) breaks for two reasons:

1. **Type**: it implements `IList<T>` but not `IReadOnlyList<T>` (`IList<T>` is not
   `IReadOnlyList<T>` in .NET), so `ForBlock.Create(scope, list, ...)` does not compile against the
   current `IReadOnlyList<TSrc>` parameter.
2. **Signal**: it is not `INotifyCollectionChanged`, so ForBlock subscribes to nothing and never
   reconciles. It *is* an `IReference`; every read (`Count`, `this[i]`, `GetEnumerator`) already
   calls `ReferenceTracker.NotifyRefernceRead`, and every mutation fires `ValueChanged` — so it
   composes with `RunAndRerunOnReferenceChange`.

## Decisions (locked)

1. **New `ForBlock`** in namespace `QuickMarkup.Infra.Blocks`
   (`QuickMarkup.Infra/Blocks/ForBlock.cs`). The remaining blocks will be migrated to this
   namespace at a later stable version.
2. **Old `QuickMarkup.Infra.ForBlock`** gets `[Obsolete("Use QuickMarkup.Infra.Blocks.ForBlock
   instead.")]` and stays for compatibility. Existing tests stay on the old class with
   `#pragma warning disable CS0618`; tests are **duplicated** against the new class to prove no
   regression after switching.
3. **Both paths** in the new class:
   - `INotifyCollectionChanged` incremental path (`OperationForKeyManager`, unchanged) for
     `ObservableCollection<T>` — exact slot-history identity, existing behavior preserved.
   - Reference-based path (`RunAndRerunOnReferenceChange`) for reactive sources / reactive getters.
4. **Implicit keys + non-INCC reactive source** → new `IdentityForKeyManager` (stable key per item
   instance). **Explicit keys** → tracked key evaluation (see "Reactive keys").
5. **No new ObservableCollection signal.** `ObservableCollection` reads are untracked, so the
   reference effect never fires for it; INCC handles it. Scope item for a version reference is
   dropped.
6. Codegen points at `global::QuickMarkup.Infra.Blocks.ForBlock.Create`.

## Mechanism

### Source getter

The primary source form is a getter `Func<IReadOnlyList<TSrc>>`. Convenience overloads accept a
plain source and wrap it as `() => source`.

### Mount

- Subscribe `INotifyCollectionChanged` when the source implements it →
  `keyManager.ApplyCollectionChanged(e, source)` + `MarkDirty`.
- Register the reference effect and add it to `controllerScope`:

  ```csharp
  ReferenceTracker.RunAndRerunOnReferenceChange(
      () =>
      {
          var s = sourceGetter();
          _ = s.Count;                // tracked read for IReference sources (e.g. ReactiveList)
          keyManager.RecomputeKeys(s); // explicit keys evaluated in tracking scope (see Reactive keys)
          return s;
      },
      _ => MarkDirty());               // batch on scheduler tick, same as INCC
  ```

  `_ = s.Count` registers `ReactiveList` as a dependency. For `ObservableCollection`/`List<T>` the
  read is untracked → no dependency → the effect never fires → INCC (or nothing) handles it.

### Reconcile

- `ReconcileOnTick` reads the fresh source via the getter.
- For non-incremental managers, call `keyManager.Refresh(source)` before the existing by-key diff.
- Existing by-key diff reuses `ForItemState` blocks by key (unchanged logic).

### Source-instance swap (`Reference<ObservableCollection<T>>`)

The iterable may be an expression that resolves to a different collection instance over time, e.g.
`foreach (var item in `CollectionRef.Value`)`. Requirements:

- The getter read `CollectionRef.Value` is tracked by the reference effect, so reassigning
  `CollectionRef` fires the effect and re-reconciles.
- ForBlock tracks the currently-subscribed `INotifyCollectionChanged` instance. On reconcile, if the
  fresh source instance differs (`!ReferenceEquals`), it **re-subscribes** to the new source's
  `CollectionChanged` (unsubscribing the old), re-ensures the key manager against the new source's
  INCC-ness, and recomputes keys (`Initialize`). A source swap is treated like a reset (blocks are
  rebuilt), which is consistent with implicit-key reset semantics.
- Null sources are treated as empty (`Reference<T> = new()` starts as `null`).

### Key manager selection (runtime, decided at Mount)

- explicit `keyFn` → explicit-key manager (tracked evaluation)
- implicit + source is `INotifyCollectionChanged` → `OperationForKeyManager` (incremental, unchanged)
- implicit + otherwise → `IdentityForKeyManager`

### IdentityForKeyManager (implicit keys, reactive path)

- Keeps `(item, key)` history.
- `Refresh(source)` recomputes keys, reusing a key only for a matching prior item:
  `ReferenceEquals` for reference types, else `EqualityComparer<T>`.
- Mints `nextId++` for new items; purges unmatched history entries.
- `ApplyCollectionChanged` degrades to `Refresh(source)` (reactive sources produce no ops).

Rationale: without per-op event args we must re-evaluate every key, but keys can be **stable per
item instance** — a new key is only minted when the item genuinely changes. So Add/Remove/Move/
Replace preserve blocks for untouched items; only genuine item changes recreate blocks. This is
better than a full rebuild (which would recreate every block on every mutation).

### Reactive keys (explicit keys)

The key expression may read reactive references, e.g. `item.key.Value`. For these to trigger
reconciliation, the new ForBlock evaluates explicit keys **inside the reference effect's tracking
scope** (no `NoCapture`), so key-reference changes re-run the effect and re-reconcile.

- The public `ExplicitForKeyManager` keeps its `NoCapture` wrapping and its test
  (`ExplicitKeyManagerDoesNotCaptureReactiveDependencies`) — tracked evaluation is internal to the
  new ForBlock's own effect and does not leak into surrounding user scopes.

Semantics: mutating a key in place changes key identity → that item's block is recreated on the
next reconcile. `myItems[0].key.Value = 4` after `myItems.Add(new(3))` coalesces into one tick and
renders the final correct tree.

## Layered changes

### QuickMarkup.Infra

- `QuickMarkup.Infra/Blocks/ForBlock.cs` — new `ForBlock<TSrc, TElement, TKey>`,
  `ForBlock<TSrc, TElement>`, static `ForBlock` (mirror old shape).
- `QuickMarkup.Infra/Blocks/ForKeyManager.cs` — `IdentityForKeyManager<TSrc>` + tracked explicit-key
  manager.
- `ReactiveList<T>`: add `IReadOnlyList<T>`/`IReadOnlyCollection<T>` (trivial — members exist).
- `QuickMarkup.Infra/ForBlock.cs`: add `[Obsolete]` to the three types.

### QuickMarkup.SourceGen

- `CGenForBlock` (`CodeGen/CodeGenContext.cs:634`): emit
  `global::QuickMarkup.Infra.Blocks.ForBlock.Create(new ReactiveScope(), () => <source>, ...)`.
  Key-factory and body codegen unchanged; static-range foreach unchanged.
- Update `sample-generated-code/TodoPage.INIT.cs` to match (and fix the `Todos.Count` line).

### QuickMarkup.CodeAnalysis (binder)

- No changes: `GetCollectionElementType` already resolves `IEnumerable<T>` (ReactiveList qualifies);
  no foreach diagnostic requires `INotifyCollectionChanged`.

## Tests

- Existing ForBlock infra tests (`QuickMarkup.Infra.Test/Test1.cs`): add `#pragma warning disable
  CS0618` at the top of the affected test classes; assertions unchanged.
- Duplicate those tests against `QuickMarkup.Infra.Blocks.ForBlock` (e.g.
  `QuickMarkup.Infra.Test/Blocks/ForBlockTests.cs`) — same assertions, proves no regression.
- New infra tests for the reactive path (ForBlock + `ReactiveList`): render, Add/Insert/Remove/
  RemoveAt/Replace/Clear/Move, duplicate reference items, identity (add/move reuse; replace/re-clear
  recreate), index-aware factory, explicit keys, reactive key-ref mutation triggers reconcile and
  recreates that block, `Refresh`, unmount/remount, dispose.
- Integration: new **Shared** case `foreach (var item in `Items`)` over a `ReactiveList<TestItem>` +
  behavior test (runs both init modes + backcompat).
- Run `dotnet test` + `test-nativeaot.sh`.

## Docs

- `SKILL.md` foreach section (lines ~358-376): iterable is reactive for `INotifyCollectionChanged`
  **or** reference-tracked collections (`ReactiveList`); explicit keys now work for reactive sources.
- `infra.md`: ForBlock reconciliation (INCC + reference-based) and identity semantics.

## Notes / edge cases

- No warnings-as-errors in the repo, so `[Obsolete]` is safe during transition.
- A source that is both INCC and IReference (custom type): both signals fire; `MarkDirty` coalesces.
- Filtered/computed getters returning a fresh enumerable each pass cannot preserve identity → keys
  all regenerate → effectively a rebuild; users should supply explicit keys.
- Key computation for the standalone public `ExplicitForKeyManager` remains intentionally
  non-reactive (`NoCapture`); only the new ForBlock's internal key evaluation is tracked.
