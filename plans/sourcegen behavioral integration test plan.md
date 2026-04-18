# Sourcegen Behavioral Integration Test Plan

This plan replaces generated-text integration tests with behavioral tests over real generated code.

The goal is to test QuickMarkup the way users experience it:

```text
[QuickMarkup(...)] class -> source generator emits code -> test constructs class -> mutate refs/collections -> tick scheduler -> assert object tree
```

Generated C# text should remain inspectable for debugging, but tests should not compare generated strings.

## Current State

Relevant files:

- `QuickMarkup.SourceGen.Test/QuickMarkup.SourceGen.Test.csproj`
- `QuickMarkup.SourceGen.Test/Class1.cs`
- `QuickMarkup.IntegrationTest/QuickMarkup.IntegrationTest.csproj`
- `QuickMarkup.IntegrationTest/Test1.cs`
- `QuickMarkup.Infra.Test/Test1.cs`

Current problems:

- `QuickMarkup.SourceGen.Test` is a compile-only smoke project. It can catch generated code that does not compile, but it does not assert behavior.
- `QuickMarkup.IntegrationTest` contains ignored tests that compare generated code strings from an outdated codegen API.
- Generated text comparisons are brittle and become less useful as sourcegen formatting and helper names evolve.
- There is no reusable fake UI object model for sourcegen tests.
- There is no test helper for constructing generated QuickMarkup classes, ticking `ReactiveScheduler`, and asserting the resulting tree.
- Content-property resolution is heuristic in `CodeTypeResolver.TryGetContentProperty`, so sourcegen behavior tests need to cover where child content actually lands.

## Target Shape

Use a real MSTest project that references `QuickMarkup.SourceGen` as an analyzer and tests generated classes directly.

Recommendation:

- Replace `QuickMarkup.SourceGen.Test` with a proper behavioral sourcegen test project.
- Delete or repurpose the old ignored `QuickMarkup.IntegrationTest` generated-text tests.
- Keep `QuickMarkup.Syntax.Test` for lexer/parser shape tests.
- Keep `QuickMarkup.Infra.Test` for infrastructure-only tests.
- Use sourcegen behavioral tests for end-to-end generated-code behavior.

Possible project layout:

```text
QuickMarkup.SourceGen.Test/
  QuickMarkup.SourceGen.Test.csproj
  QuickMarkupAttribute.cs
  TestControls.cs
  TestTreeAssert.cs
  ReactiveTestBase.cs
  Generated/
    StaticTreeCases.cs
    BindingCases.cs
    ConditionalCases.cs
    ForeachCases.cs
    FragmentCases.cs
  Tests/
    StaticTreeTests.cs
    BindingTests.cs
    ConditionalTests.cs
    ForeachTests.cs
    FragmentTests.cs
```

The `Generated/` folder contains partial classes decorated with `[QuickMarkup(...)]`.

The `Tests/` folder contains MSTest classes that instantiate those generated classes and assert behavior.

## Test UI Model

Add small fake controls inside `QuickMarkup.SourceGen.Test`.

Keep them intentionally boring and framework-neutral. Prefer focused controls over a catch-all base type with every possible content property.

```csharp
public sealed class TestRoot
{
    public TestElementCollection Children { get; } = [];
}

public abstract class TestElement
{
    public string? Name { get; set; }
}

public sealed class TestText : TestElement
{
    public string? Text { get; set; }
    public int Number { get; set; }
    public bool Flag { get; set; }
}

public sealed class TestPanel : TestElement
{
    public TestElementCollection Children { get; } = [];
}

public sealed class TestButton : TestElement
{
    public TestElement? Content { get; set; }
}
```

Collection:

```csharp
public sealed class TestElementCollection : Collection<TestElement>
{
    public void Move(int oldIndex, int newIndex)
    {
        var item = this[oldIndex];
        RemoveAt(oldIndex);
        Insert(newIndex, item);
    }
}
```

Notes:

- Use simple class names in markup, such as `<TestPanel>`, `<TestText Text="A" />`.
- Use `TestPanel` for multi-child content tests.
- Use `TestButton` for single-child content tests.
- Avoid putting both `Children` and `Content` on the common base type. That keeps tests clear and avoids accidentally depending on heuristic resolution order in most fixtures.
- Add specialized controls to test resolver heuristics deliberately:
  - `ChildrenOnlyElement` with only `Children`.
  - `ItemsOnlyElement` with only `Items`.
  - `ChildOnlyElement` with only `Child`.
  - `ContentOnlyElement` with only `Content`.
  - `AmbiguousElement` with multiple candidate content properties, used only for resolver-order tests.
  - `AttributedContentElement` with a test content-property attribute if we decide to extend the resolver for test attributes.
- Optionally add `Items` later if item collection conventions need coverage.
- Do not use real WinUI/UWP controls in this test layer.
- If we later decide it is more convenient for shared base controls to expose more properties, add a content-property attribute to disambiguate that behavior. Do not let broad base-type properties leak into normal tests accidentally.

## Content Property Resolution

`CodeTypeResolver.TryGetContentProperty` currently resolves content in this order:

1. recognized content-property attribute:
   - `global::Windows.UI.Xaml.Markup.ContentPropertyAttribute`
   - `global::Microsoft.UI.Xaml.Markup.ContentPropertyAttribute`
2. `Children`
3. `Items`
4. `Child`
5. `Content`

The tests should verify this behavior explicitly because most sourcegen behavior depends on the chosen child target.

Recommended coverage:

- a type with `Children` receives nested children in `Children`.
- a type with `Items` and no `Children` receives nested children in `Items`.
- a type with `Child` and no `Children`/`Items` receives one nested child in `Child`.
- a type with `Content` and no `Children`/`Items`/`Child` receives one nested child in `Content`.
- an explicitly ambiguous type with both `Children` and `Content` uses `Children` for implicit child content.
- explicit property tags like `<.Content>` override the implicit heuristic and assign content to `Content`.
- explicit property tags like `<.Children>` override the implicit heuristic and add children to `Children`.

Test attribute option:

- For now, the resolver only recognizes WinUI/UWP content-property attributes. We can avoid test-only resolver changes by using heuristic test types.
- If attribute behavior needs coverage without real WinUI references, add a small test attribute and intentionally update `CodeTypeResolver.FindContentAttirbute` to recognize it. That would be a product behavior change, so keep it separate and deliberate.
- Future de-hardcoding could recognize attributes by simple contract rather than exact framework namespace, but this plan should not require that change.

## Alternate Child Syntaxes

Behavior tests should cover all currently supported ways to put children into properties.

### Implicit Content

```quickmarkup
<TestPanel>
    <TestText />
</TestPanel>
```

Expected:

- Uses `TryGetContentProperty` heuristic.
- If target property is a collection (`Children`, `Items`), children are added.
- If target property is single-child (`Child`, `Content`), exactly one child is assigned.

### Property Value Tag

```quickmarkup
<TestButton
    Content=<TestText />
/>
```

Expected:

- Binds as a normal property assignment.
- Should support a plain tag value.
- Structural `if`/`foreach` are not property values, so this syntax should not be expected to support structural children directly.

### Property Value List

```quickmarkup
<TestPanel
    Children=<>
        <TestText />
    </>
/>
```

Expected:

- Uses `QuickMarkupValueList` for old `<>...</>` syntax.
- Binds to an add-style child collection for the named property.
- Existing behavior should be preserved.
- Structural `{ ... }`, `if`, and `foreach` inside `<>...</>` should be tested before we rely on them. Binder currently routes `QuickMarkupValueList.Value` through normal child-list binding, so structural children may work after parser support, but sourcegen direct/block lowering for named property collections should be verified with behavior tests.

### Property Tag Assignment

```quickmarkup
<TestButton>
    <.Content>
        <TestText />
    </.Content>
</TestButton>
```

Expected:

- Explicitly targets the `Content` property.
- In assignment mode, supports single-child `if/else` through `ConditionalSlot<T>`.
- Should reject `foreach`.
- Should allow `{ ... }` only when the fragment contains exactly one valid single child.

### Property Tag Collection Add

```quickmarkup
<TestPanel>
    <.Children>
        <TestText />
    </.Children>
</TestPanel>
```

Expected:

- Explicitly targets the `Children` property.
- In add mode, should support normal children.
- Should support structural `{ ... }`, `if`, and reactive `foreach` if the binder marks that named child collection as `ChildCollectionLowering.Blocks`.
- This deserves dedicated behavior tests because it exercises block lowering for a named property collection rather than the implicit content property.
- Prefer using `TestPanel` for this test rather than writing long property-targeted markup against a generic element. Reserve explicit property-tag tests for behavior that cannot be expressed clearly through focused controls.

## Collection Move Semantics

The test collection and `TargetUICollection<T>.Move` should follow `ObservableCollection<T>.Move(oldIndex, newIndex)` semantics:

```text
[A, B, C, D]
Move(0, 2)
=> [B, C, A, D]
```

This is a move-to-final-index operation, not a swap.

Important note:

- The current `TargetUICollection<T>` default move implementation adjusts `newIndex` downward after removal when moving forward.
- That produces pre-removal insertion semantics, not `ObservableCollection<T>.Move` semantics.
- Current `ForBlock` reconciliation mostly detaches/remounts blocks, so this may be latent rather than visible in existing foreach tests.
- Before adding sourcegen behavior tests that rely on `Move`, add infra tests for `TargetUICollection<T>.Move` and fix the implementation if needed.

Suggested infra tests:

- `Move(0, 2)` on `[A, B, C, D]` produces `[B, C, A, D]`.
- `Move(2, 0)` on `[A, B, C, D]` produces `[C, A, B, D]`.
- `Move(i, i)` is a no-op.

## Test Base

Add a base class or helper:

```csharp
public abstract class ReactiveTestBase
{
    [TestInitialize]
    public void ResetReactiveScheduler()
    {
        ReactiveScheduler.ResetForCurrentThread();
        ReactiveScheduler.Instance.Value!.AutoTick = false;
        ReactiveScheduler.Instance.Value!.ContinueOnException = false;
    }

    protected static void Tick()
        => ReactiveScheduler.Tick();
}
```

This mirrors `QuickMarkup.Infra.Test` and keeps structural updates deterministic.

## Tree Assertions

Add helpers to assert object trees by behavior, not generated code:

```csharp
public static class TestTreeAssert
{
    public static void Texts(TestElementCollection children, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, children.Select(x => x.Text).ToArray());
    }

    public static T Child<T>(TestElement parent, int index)
        where T : TestElement
        => Assert.IsInstanceOfType<T>(parent.Children[index]);
}
```

Useful helpers:

- `Texts(collection, ...)`
- `Numbers(collection, ...)`
- `Names(collection, ...)`
- `AssertSameInstance(expected, actual)`
- `AssertChildCount(element, count)`
- `AssertContent<T>(element)`

Avoid snapshot-style full tree strings unless a failure message helper is useful. Assertions should stay focused.

## Generated Test Classes

Each generated class should be a small fixture with public refs or source collections that tests can mutate.

Example static tree fixture:

```csharp
[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <root>
        <TestPanel>
            <TestText Text="A" />
            <TestText Text="B" />
        </TestPanel>
    </root>
    """)]
public partial class StaticTreeCase : TestRoot;
```

Test:

```csharp
[TestMethod]
public void StaticTree_CreatesChildren()
{
    var page = new StaticTreeCase();
    var panel = Assert.IsInstanceOfType<TestPanel>(page.Children[0]);

    TestTreeAssert.Texts(panel.Children, "A", "B");
}
```

## Coverage Plan

### Static Markup

Test:

- root child is created
- nested children are appended in order
- properties are assigned
- old `<>...</>` value-list property assignment still works if a fake collection property supports it
- content-property heuristic chooses the expected target property
- explicit property tags override the implicit content-property heuristic

### Reactive Property Binding

Fixture:

```quickmarkup
string Label = "A";
<root>
    <TestText Text=`Label` />
</root>
```

Test:

- initial `Text == "A"`
- set `Label = "B"`
- before tick: still `"A"`
- after `ReactiveScheduler.Tick()`: `"B"`

### Single-Child Assignment

Fixture:

```quickmarkup
bool UseA = true;
<root>
    <TestButton>
        <.Content>
            if (`UseA`) <TestText Text="A" /> else <TestText Text="B" />
        </.Content>
    </TestButton>
</root>
```

Test:

- initial content is `A`
- toggling condition switches content to `B`
- disposed old branch stops reacting
- property tag assignment `<.Content>` behaves the same as implicit single-child assignment when targeting `Content`

### Nested Conditional Slot

Fixture:

```quickmarkup
bool Outer = true;
bool Inner = false;
string InnerTrue = "inner true";
string InnerFalse = "inner false";

<root>
    <TestButton>
        <.Content>
            if (`Outer`)
                if (`Inner`) <TestText Text=`InnerTrue` /> else <TestText Text=`InnerFalse` />
            else
                <TestText Text="outer false" />
        </.Content>
    </TestButton>
</root>
```

Tests:

- initial content is inner false
- toggling `Inner` switches content to inner true
- toggling `Outer` switches content to outer false
- after outer false, changes to `InnerTrue` / `InnerFalse` do not update stale inner branch controls
- toggling `Outer` back recreates a fresh nested slot

This directly covers the sourcegen path added for nested `QMConditionalValueSymbol<T>`.

### Collection If

Fixture:

```quickmarkup
bool Show = true;
<root>
    <TestPanel>
        <TestText Text="before" />
        if (`Show`) {
            <TestText Text="A" />
            <TestText Text="B" />
        } else {
            <TestText Text="C" />
        }
        <TestText Text="after" />
    </TestPanel>
</root>
```

Tests:

- initial order: `before, A, B, after`
- after toggle: `before, C, after`
- toggling back: `before, A, B, after`

This validates structural sibling offsets.

### Foreach

Fixture:

```quickmarkup
ObservableCollection<Item> Items = [...];
<root>
    <TestPanel>
        foreach (var item in `Items`) {
            <TestText Text=`item.Text` />
        }
    </TestPanel>
</root>
```

Tests:

- initial item render
- add/remove updates after tick
- item replacement updates after tick
- move preserves block instance for implicit operation identity

### Foreach Index

Fixture:

```quickmarkup
foreach (index; var item in `Items`) {
    <TestText Text=`$"{index}:{item.Text}"` />
}
```

Tests:

- initial indexes are correct
- moving items updates index-dependent text
- inserted items shift later indexes

### Foreach Key

Fixture:

```quickmarkup
foreach (var item in `Items`; `item.Id`) {
    <TestText Text=`item.Text` />
}
```

Tests:

- reset-like remove/add with same keys preserves instances
- duplicate keys surface an exception or diagnostic, depending on where the failure is expected

### Structural Fragment

Fixture:

```quickmarkup
<root>
    <TestPanel>
        {
            <TestText Text="A" />
            <TestText Text="B" />
        }
    </TestPanel>
</root>
```

Tests:

- fragment contributes both children in order
- fragment nested under `if`/`foreach` preserves grouping and offsets

### Named Property Collection Structural Children

Fixture:

```quickmarkup
<root>
    <TestPanel>
        <.Children>
            <TestText Text="before" />
            if (`Show`) {
                <TestText Text="A" />
                <TestText Text="B" />
            } else {
                <TestText Text="C" />
            }
            <TestText Text="after" />
        </.Children>
    </TestPanel>
</root>
```

Tests:

- initial order is correct.
- toggling condition updates only the named `Children` collection.
- sibling offsets are correct inside the named collection.

### Value-List Property Collection Structural Children

Fixture:

```quickmarkup
<root>
    <TestPanel Children=<>
        <TestText Text="before" />
        if (`Show`) <TestText Text="A" /> else <TestText Text="B" />
        <TestText Text="after" />
    </> />
</root>
```

Tests:

- if supported by current binder/sourcegen, assert behavior.
- if not supported, document the gap and decide whether `QuickMarkupValueList` should allow structural children or remain static/value-only.

Current expectation:

- Since `QuickMarkupValueList.Value` is bound through normal child-list binding, it should be possible to support structural children here. The behavior test will confirm whether sourcegen already handles `ChildCollectionLowering.Blocks` for named property collection members.

## Compile-Time Negative Tests

Behavioral tests are best for valid markup. Some invalid markup still needs compile-time diagnostics.

Options:

1. Keep simple analyzer/generator diagnostic tests later using Roslyn test utilities.
2. For now, avoid negative sourcegen tests unless the current test infrastructure already supports expected compile failures.

Suggested future diagnostics:

- `foreach` in single-child assignment
- single-child fragment with zero or multiple children
- explicit foreach key that is not foreign
- single-child `if` without `else`

## Project Migration

Recommended approach:

1. Convert `QuickMarkup.SourceGen.Test` into an MSTest project.
2. Keep the existing `QuickMarkupAttribute.cs` test attribute.
3. Add `MSTest` package and test SDK settings matching the other test projects.
4. Keep the source generator analyzer reference:

```xml
<ProjectReference Include="..\QuickMarkup.SourceGen\QuickMarkup.SourceGen.csproj"
                  ReferenceOutputAssembly="False"
                  OutputItemType="Analyzer" />
```

5. Add `QuickMarkup.Infra` project reference for `Reference<T>`, `ReactiveScheduler`, etc.
6. Add fake controls and assertion helpers.
7. Add generated fixture classes and behavior tests.
8. Delete the recursive `Class1` smoke fixture once replacement fixtures exist.

After this, decide whether to:

- delete `QuickMarkup.IntegrationTest`, or
- keep it only for parser-level integration tests if it still has a unique role.

Current recommendation:

- Remove the ignored generated-text tests from `QuickMarkup.IntegrationTest`.
- Put all end-to-end sourcegen behavior in `QuickMarkup.SourceGen.Test`.

## Implementation Order

1. Convert `QuickMarkup.SourceGen.Test` to MSTest.
2. Add fake UI controls.
3. Add `ReactiveTestBase`.
4. Add `TestTreeAssert`.
5. Add a minimal static tree fixture and test.
6. Add content-property heuristic fixtures and tests.
7. Add alternate child syntax tests:
   - `Content=<TestText />`
   - `Children=<>...</>`
   - `<.Content>...</.Content>`
   - `<.Children>...</.Children>`
8. Add reactive property binding fixture and test.
9. Add single-child conditional fixture and tests.
10. Add nested conditional slot fixture and tests.
11. Add collection `if` fixture and tests.
12. Add named-property collection structural tests.
13. Add value-list property collection structural tests or document the support gap.
14. Add fragment fixture and tests.
15. Add foreach fixture and tests.
16. Add foreach index/key fixtures and tests.
17. Remove obsolete recursive `Class1` smoke fixture.
18. Remove or rewrite old ignored `QuickMarkup.IntegrationTest` generated-text tests.
19. Run:

```powershell
dotnet build --no-restore -m:1 -v:minimal
dotnet test --no-build -m:1 -v:minimal
```

Use `-m:1` while the local machine is sensitive to many concurrent .NET hosts.

## Acceptance Criteria

- Tests instantiate generated `[QuickMarkup]` classes directly.
- Tests assert object tree state and object identity, not generated source text.
- Tests cover reactive property updates after scheduler ticks.
- Tests cover structural `if`, fragments, foreach add/remove/move, index, and key behavior.
- Tests cover nested conditional slots end to end.
- `QuickMarkup.SourceGen.Test` fails if generated code does not compile.
- `dotnet test --no-build -m:1 -v:minimal` passes.
