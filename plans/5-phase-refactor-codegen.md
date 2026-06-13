# Refactor Plan: `CodeGenContext`

## Goal

Improve maintainability and safety of the QuickMarkup source generator without significantly changing generator architecture or emitted output.

The current generator is still fundamentally a recursive tree emitter, and this refactor should preserve that model.

Primary focus:

* eliminate fragile cloned mutable state synchronization
* improve navigability
* reduce accidental state bugs
* prepare for future growth

Avoid large architectural rewrites or over-abstraction.

---

# Current Problems

## 1. Fragile cloned state synchronization

The largest issue is the `Clone(StringBuilder)` pattern.

Current nested generation requires manual synchronization:

```csharp
counterRef = nestedContext.counterRef;
```

This is fragile because:

* synchronization is manual
* correctness depends on remembering sync-back
* future changes can silently break variable uniqueness
* ownership of mutable state is unclear

This is the highest-priority issue.

---

## 2. One large recursive emitter file

`CodeGenContext.cs` currently mixes:

* entry orchestration
* block lowering
* conditional lowering
* foreach lowering
* value lowering
* closure generation
* member routing
* helper utilities

The issue is primarily navigability, not necessarily architecture.

---

## 3. Nested builder emission boilerplate

Many methods repeat:

```csharp
var nested = new StringBuilder();
var nestedContext = Clone(nested);
```

This creates:

* repetitive code
* state propagation complexity
* inconsistent nested-generation handling

---

# Non-Goals

Do NOT:

* rewrite generator into compiler passes
* introduce visitor-pattern architecture
* split into many service classes
* redesign emitted output
* redesign AST model
* optimize performance unless needed for correctness

Keep the recursive emitter model intact.

---

# Refactor Strategy

Perform refactor incrementally in small safe phases.

Each phase should preserve byte-identical generated output where possible.

---

# Phase 1 — Extract Shared Mutable State

## Goal

Remove manual synchronization hazards.

## Add `CodeGenState`

Create a new shared mutable state type:

```csharp
sealed class CodeGenState
{
    public int CounterRef;

    public Stack<ForScope> ForScopes = [];

    public StringBuilder MembersBuilder;

    public CodeGenState(StringBuilder membersBuilder)
    {
        MembersBuilder = membersBuilder;
    }
}
```

The exact contents may vary, but at minimum:

* `counterRef`
* `forScopes`

must move here.

---

## Update `CodeGenContext`

Convert `CodeGenContext` into a lightweight scoped emission context.

Suggested direction:

```csharp
sealed partial class CodeGenContext
{
    readonly CodeGenState state;

    readonly StringBuilder codeBuilder;

    string disposableAddTarget;
}
```

The context should become:

* cheap to create
* safe to nest
* no manual sync-back required

---

## Remove sync-back logic

Eliminate patterns like:

```csharp
counterRef = nestedContext.counterRef;
```

after nested emission.

All nested contexts should share the same counter state automatically.

This is the most important outcome of the refactor.

---

# Phase 2 — Replace `Clone()` With Scoped Nested Context Helpers

## Goal

Centralize nested emission behavior.

---

## Replace `Clone(StringBuilder)` usage

Introduce a helper API for nested generation.

Possible direction:

```csharp
CodeGenContext CreateNestedContext(StringBuilder builder)
```

or:

```csharp
string EmitNested(Action<CodeGenContext> emit)
```

or similar.

Exact API shape is flexible.

---

## Desired properties

Nested generation should:

* automatically inherit shared state
* inherit `forScopes`
* inherit current `disposableAddTarget`
* avoid manual synchronization
* reduce repeated boilerplate

---

## Avoid builder ownership confusion

The helper should make it obvious:

* which builder is active
* which state is shared
* which state is scoped

---

# Phase 3 — Split Into Partial Files

## Goal

Improve readability without changing runtime architecture.

Do NOT split into independent generator services yet.

The generator is still highly recursive and state-coupled.

Using partial files preserves:

* locality
* easy shared-state access
* recursive readability

---

## Suggested file layout

```text
CodeGenContext.cs
CodeGenContext.Blocks.cs
CodeGenContext.Values.cs
CodeGenContext.Members.cs
CodeGenContext.Helpers.cs
```

Possible grouping:

### `CodeGenContext.Blocks.cs`

* `CGenBlock`
* `CGenFragmentBlock`
* `CGenConditionalBlock`
* `CGenForBlock`
* `CGenStaticBlock`
* block-host generation helpers

### `CodeGenContext.Values.cs`

* `CGenValue`
* captured local handling
* closure generation
* conditional slot generation

### `CodeGenContext.Members.cs`

* `CGenWrite`
* property/event/attached-property generation
* component-root generation

### `CodeGenContext.Helpers.cs`

* type helpers
* target-path helpers
* builder helpers
* nested emission helpers

---

# Phase 4 — Improve Nested Emission Ergonomics

## Goal

Reduce ceremony around temporary builders.

Current code repeatedly does:

```csharp
var nested = new StringBuilder();
```

Introduce reusable helper patterns where appropriate.

Possible helpers:

* nested builder creation
* scoped emission
* lambda block emission
* indentation helpers

Keep helpers lightweight.

Avoid creating a mini codegen framework.

---

# Phase 5 — Optional Cleanup Pass

Only after previous phases stabilize.

Potential improvements:

* reduce duplicated `BindingModes` switch logic
* extract common conditional-slot emission pieces
* simplify event handler generation
* centralize repeated `ReactiveScope` creation patterns

These are lower priority than state safety.

---

# Important Constraints

## Preserve emitted behavior

Generated code should remain functionally identical.

Prefer preserving exact generated output formatting where practical.

---

## Preserve recursive emitter model

Do not attempt to transform generator into:

* compiler passes
* immutable IR pipeline
* visitor-heavy architecture

Current architecture is acceptable for the project stage.

---

## Avoid premature service decomposition

Avoid creating:

* `BlockCodeGen`
* `ValueCodeGen`
* `MemberCodeGen`

unless future growth clearly justifies it.

Current methods are too interdependent for clean separation.

Partial files are preferred.

---

# Success Criteria

The refactor is successful if:

* no manual counter synchronization remains
* nested contexts are cheap and safe
* state ownership becomes obvious
* file navigation improves substantially
* generated output remains stable
* recursive emitter readability is preserved

---

# Recommended Order

1. Introduce `CodeGenState`
2. Remove sync-back patterns
3. Replace `Clone()` usage
4. Split into partial files
5. Cleanup/ergonomics pass

Do not combine all phases into one large commit.
