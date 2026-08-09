# Reactive Structural Scope Scheduling

## Goal

Add structural scope awareness to QuickMarkup's reactive scheduler so that reactive UI effects execute in a deterministic parent-before-descendant order.

The primary problem is that `if`, `foreach`, and nested structural blocks currently establish ordering only during initial rendering. After references change, their reactive callbacks can execute in arbitrary order because `ReactiveScheduler` currently stores pending `RefEffect`s in a `HashSet`.

This can cause a descendant UI effect to run before an ancestor `if`/`foreach` has reconciled the UI tree that contains that descendant.

The goal is to guarantee:

> An active ancestor structural scope is reconciled before reactive UI effects belonging to its descendants.

This should work across nested `if`, `foreach`, fragments, and components.

---

## Important existing behavior to preserve

### Computed values

`Computed<T>.Value` must continue to return an up-to-date value even when its dependencies changed but the scheduler has not reached the computed's scheduled effect yet.

`Computed<T>` currently achieves this through:

```csharp
ReactiveScheduler.DoNowIfScheduled(effect);
```

Do not remove or weaken this behavior.

`DoNowIfScheduled` exists primarily for computed read-time stabilization, not as a general mechanism for running arbitrary UI effects immediately.

A computed being read must be able to synchronously update itself and return the correct value.

### Tick behavior

The scheduler currently:

* deduplicates scheduled effects;
* snapshots pending effects when a tick begins;
* allows effects scheduled during a tick to run on a later tick;
* supports `DoNowIfScheduled`;
* separately handles scheduled callbacks.

Preserve these semantics unless a change is required for structural ordering.

---

# 1. Introduce structural reactive scopes

Use a `ReactiveScope` to represent the structural lifetime/order of a portion of the generated UI tree.

Scopes should form a hierarchy:

```text
Parent scope
    Child scope
        Grandchild scope
```

A scope should know its parent and whether it is still active/disposed.

The scope is primarily for UI structural lifetime and scheduling order.

Do not make every reactive value automatically become structurally scoped merely because it happened to be created while a scope was active.

In particular, ordinary `Computed<T>` instances should not automatically become invalid merely because their creation happened inside a UI scope.

---

# 2. Track the current structural scope during rendering

Add a mechanism analogous to the existing reference capture mechanism for temporarily setting the current structural scope.

Conceptually:

```csharp
using (ReferenceTracker.EnterStructuralScope(scope))
{
    // render/create descendants
}
```

The current structural scope should be ambient/thread-local in the same general manner as the existing reference capture state.

When a UI reactive effect is created while a structural scope is active, it captures that scope at creation time.

The effect must retain the scope it was created under.

Do NOT determine an effect's scope later when it executes. The scope belongs to the effect for its lifetime.

---

# 3. Scope inheritance

Structural scopes should naturally propagate down the generated UI tree.

For example:

```text
Parent
└── if
    └── Child component
        └── if
            └── foreach
                └── Text
```

should conceptually become:

```text
ParentScope
└── ChildScope / structural child scope
    └── NestedStructuralScope
        └── ForScope
```

A child component created inside a parent's structural scope initially belongs to the parent's structural context.

When the child component starts rendering its own UI through its own `UIBlockHost`, it can establish its own child scope.

This means component internals naturally inherit the parent structural context while being created, then establish their own structural scope for their descendants.

The generated QuickMarkup code should not need explicit integer priority arguments everywhere.

Prefer establishing scope through the existing `UIBlockHost` / `UIBlock` infrastructure.

---

# 4. Structural ordering

The scheduler needs to execute structural/UI effects in ancestor-before-descendant order.

Do not rely on a single global distinction such as:

```text
structural effects first
property effects second
```

because nested structural structures require more ordering information:

```text
if
    foreach
        if
            component/property
```

A useful ordering model is based on the structural scope hierarchy/depth.

For example:

```text
Parent structural scope       depth 0
Nested structural scope      depth 1
Nested nested scope          depth 2
Descendant UI effects        deeper
```

Effects at the same structural level do not need an arbitrary semantic ordering unless an actual parent/child relationship exists.

The scheduler may use a sequence number as a deterministic tie-breaker.

Do not require unrelated sibling branches to execute in a specific semantic order.

---

# 5. Scope lifetime/disposal

Structural scope must also represent lifetime.

If an ancestor `if` becomes false:

```text
if (Show)
    <Child />
```

then the child's scope becomes inactive/disposed when the branch is removed.

Any effects belonging to that scope or descendant scopes that were already queued for the current tick must not subsequently mutate the destroyed UI.

For example:

```text
Show = false
Value = newValue
```

must not result in:

```text
Child property effect
    ↓
update destroyed Child
    ↓
Parent if effect
    ↓
destroy Child
```

Instead:

```text
Parent structural effect
    ↓
destroy Child scope
    ↓
queued descendant effects become invalid/skipped
```

The scheduler should check scope validity before executing a queued UI effect, or otherwise ensure disposed scopes are removed from pending work.

Prefer a mechanism that makes disposed descendant work harmless rather than requiring every UI effect to individually check whether its element still exists.

---

# 6. Do not apply structural scope blindly to Computed<T>

Keep computed reactivity conceptually separate from UI structural lifetime.

There are two different concerns:

```text
Computed:
    "Is this reactive value currently up to date?"

Structural scope:
    "Does this UI effect still belong to an active part of the UI tree?"
```

`Computed<T>.Value` must still be able to call:

```csharp
ReactiveScheduler.DoNowIfScheduled(effect);
```

and synchronously stabilize itself.

Do not make `DoNowIfScheduled` obey UI structural ordering in a way that prevents computed values from being read correctly.

However, avoid allowing arbitrary UI effects to use `DoNowIfScheduled` as a way to bypass structural ordering.

The current known use of `DoNowIfScheduled` is `Computed<T>.Value`; preserve that intended use.

---

# 7. Scheduler data structure

The current scheduler uses:

```csharp
HashSet<RefEffect>
```

for deduplication and arbitrary execution order.

Replace or augment this with a scheduler representation that provides:

1. deduplication by `RefEffect`;
2. access to the effect's captured structural scope;
3. deterministic ancestor-before-descendant scheduling;
4. efficient removal/skipping of disposed effects;
5. compatibility with effects being scheduled while a tick is running.

A dictionary plus an ordered work queue/priority queue is one possible implementation.

Do not require `RefEffect` itself to be globally comparable.

The scheduler should own scheduling metadata.

---

# 8. Important tick scenario to test

Test this case explicitly:

```text
if (Show)
{
    <Child />
}
```

where `Child` has:

```text
Text=`ParentValue`
```

Then change both:

```text
Show
ParentValue
```

before the next tick.

The result must never allow the child property effect to mutate the child before the ancestor `if` has decided whether the child remains in the tree.

---

# 9. Nested structural scenarios

Add tests for all combinations such as:

```text
if
    foreach
        if
            property
```

and:

```text
foreach
    if
        foreach
            property
```

Also test:

```text
if
    ChildComponent
```

where the child contains:

```text
if
foreach
property effects
```

The expected invariant is always:

```text
ancestor structural reconciliation
    ↓
descendant structural reconciliation
    ↓
descendant UI/property effects
```

---

# 10. Same-tick creation

Preserve the existing behavior that effects created/scheduled while a tick is already running do not unexpectedly execute as arbitrary new work in the middle of the current tree reconciliation.

In particular:

```text
Parent structural effect
    ↓
creates Child
    ↓
Child creates reactive effects
```

should not cause newly created descendant UI effects to execute before the parent structural operation has completed.

The existing scheduler behavior of separating the current tick's effects from newly scheduled effects should be considered when implementing this.

---

# 11. Generated code/API impact

Avoid requiring generated code to explicitly pass numeric priorities through every component/property expression.

Prefer infrastructure-level behavior.

The desired generated-code shape should remain approximately:

```csharp
host.AddBlock(new ConditionalBlock(...));
```

rather than becoming:

```csharp
host.AddBlock(new ConditionalBlock(..., priority: parentPriority + 1));
```

The UI block hierarchy should establish the structural scope automatically.

Components should therefore work without special syntax or generated priority plumbing.

---

# 12. Suggested implementation order

1. Inspect the existing `ReactiveScope`, `UIBlockHost`, `IUIBlock`, `ConditionalBlock`, `ForBlock`, and `RefEffect` relationships.

2. Define the exact ownership/lifetime semantics of `ReactiveScope`.

3. Add current-scope capture/restore functionality to `ReferenceTracker`.

4. Make UI/block-created reactive effects capture the current structural scope.

5. Make structural blocks/hosts establish child scopes appropriately.

6. Modify scheduler pending-effect storage to support deterministic scope-aware ordering while retaining deduplication.

7. Add disposal/invalidation handling for effects belonging to destroyed scopes.

8. Verify `Computed<T>.Value` and `DoNowIfScheduled` behavior remains unchanged.

9. Add tests for nested structural updates, parent/child components, simultaneous reference changes, branch removal, and effects created during a tick.

10. Inspect generated code to ensure the implementation does not introduce unnecessary priority/context plumbing into generated markup.

---

# Core invariants

The implementation should ultimately guarantee these invariants:

### Invariant 1 — Computed consistency

Reading `Computed<T>.Value` always returns the current computed value, even if its dependency changed earlier in the current scheduler cycle.

### Invariant 2 — Structural ordering

An ancestor structural effect runs before descendant UI effects that depend on the ancestor's existence.

### Invariant 3 — Dead subtree safety

Once a structural scope is removed/disposed, queued effects belonging to that scope or its descendants cannot mutate the removed UI.

### Invariant 4 — Scope capture

An effect's structural scope is captured when the effect is created, not inferred later from the currently rendering scope.

### Invariant 5 — Component nesting

A child component rendered inside a parent's structural scope correctly participates in the parent's structural ordering, while the child's own UI descendants can establish their own nested structural scopes.

### Invariant 6 — No unnecessary generated plumbing

The source generator should not need to manually propagate numeric priorities through every generated expression/component call. Structural context should primarily be an infrastructure concern.
