# Plan: Ctor Args Dropped When Child Has Provide/Inject in DeferredInit Mode

## Bug Summary

When a parent uses DeferredInit mode (the default for `[QuickMarkup]` classes) and a child component has both:
1. A `[QuickMarkupConstructor]` with parameters, AND
2. `provide`/`inject` declarations

...the source generator drops the ctor args from the generated child construction code.

## Reproduction

```csharp
// Child: has inject + ctor param
[QuickMarkup("""
    inject string Label;
    string Extra = "";
    <TestText Text=`$"{Label}-{Extra}"` />
    """)]
public partial class MyChild : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string extra)
    {
        Extra = extra;
        Init(extra);
    }
}

// Parent: uses ctor arg syntax
[QuickMarkup("""
    public provide string Label = "hello";
    <root>
        <MyChild("world") />
    </root>
    """)]
public partial class MyParent : TestRoot;
```

### Expected generated code (parent's Init method)

```csharp
QUICKMARKUP_NODE_0 = new MyChild("world", QUICKMARKUP_NODE_1 => {
    QUICKMARKUP_NODE_1.Context = new QuickMarkupContext(Context);
});
```

### Actual generated code

```csharp
QUICKMARKUP_NODE_0 = new MyChild(QUICKMARKUP_NODE_1 => {
    QUICKMARKUP_NODE_1.Context = new QuickMarkupContext(Context);
});
```

The `"world"` ctor arg is silently dropped. This causes a compile error because the child's action constructor expects `(string extra, Action<MyChild>)` but only receives `(Action<MyChild>)`.

## Root Cause

The bug is in `CGenDeferredInit` in `QuickMarkup.SourceGen/CodeGen/CodeGenContext.cs`, lines 151-157.

`CGenDeferredInit` has a three-way branch for constructing child components:

1. **`if (initMembers.Count > 0)`** (line 117) -- When there ARE properties to set before init. This path **correctly** checks for ctor args at line 139 and prepends them.

2. **`else if (node.SupportsContext)`** (line 151) -- When there are NO properties to set, but the type supports context. This path **ignores** `node.Constructor.Parameters` entirely and only emits `new Type(lambda => { ... })`.

3. **`else`** (line 159) -- Fallback. Uses `constructorExpr` which includes ctor args. Unreachable for DeferredInit types because they always have `SupportsContext = true`.

### Why the working case works

`CtorArgWithRefsConsumerCase` uses `<CtorArgWithRefsTarget("hello") Extra="world" />`. The `Extra="world"` attribute creates a `QMAddPropertyMember`, so `initMembers.Count > 0` and path 1 is taken. Path 1 correctly handles ctor args.

### Why the broken case fails

`<MyChild("world") />` has no attributes besides the ctor arg. `initMembers` is empty. Path 2 is taken (`SupportsContext == true` for all DeferredInit types). Path 2 does not check or emit ctor args.

## All Relevant Code Locations

| File | Lines | Role |
|------|-------|------|
| `CodeGenContext.cs` | 151-157 | **BUG**: `else if (node.SupportsContext)` path drops ctor args |
| `CodeGenContext.cs` | 139-147 | Correct ctor arg handling in path 1 (reference for fix) |
| `CodeGenContext.cs` | 64-169 | Full `CGenDeferredInit` method |
| `CodeGenContext.cs` | 91-114 | Init member classification (determines which path is taken) |
| `QuickMarkupGeneratedMemberTableBuilder.cs` | 85-89 | `InitMode` determination (DeferredInit if no explicit constructors) |
| `QuickMarkupGeneratedMemberTableBuilder.cs` | 91-94 | `SupportsContext` implication (DeferredInit implies `true`) |
| `QuickMarkupBinder.cs` | 70-97 | Child binding: sets `initMode` and `supportsContext` on node |
| `QuickMarkupBinder.cs` | 164-179 | Constructor binding: binds markup ctor args to `QMConstructor` |
| `QuickMarkupSymbols.cs` | 63-67 | `QMConstructor` record with `Parameters` list |
| `QuickMarkupSymbols.cs` | 69-80 | `QMNodeSymbol` type with `Constructor`, `InitMode`, `SupportsContext` |

## Fix Direction

The fix should be in `CodeGenContext.cs` at the `else if (node.SupportsContext)` branch (line 151). It needs to check `node.Constructor.Parameters.Count > 0` the same way path 1 does, and prepend ctor args when present.

The corrected branch should look something like:

```csharp
else if (node.SupportsContext)
{
    var lambdaParam = NewVariable();
    if (node.Constructor.Parameters.Count > 0)
    {
        var ctorArgs = string.Join(", ", node.Constructor.Parameters.Select(p => CGen(p)));
        codeBuilder.AppendLine($"{varTarget} = new {typeName}({ctorArgs}, {lambdaParam} => {{");
    }
    else
    {
        codeBuilder.AppendLine($"{varTarget} = new {typeName}({lambdaParam} => {{");
    }
    codeBuilder.AppendLine($"    {lambdaParam}.Context = new global::QuickMarkup.Infra.QuickMarkupContext(Context);");
    codeBuilder.AppendLine("});");
}
```

## Existing Working Tests (for reference)

- `CtorArgWithRefs_ConstructorArgAndPropertySetBeforeInit` (`SourceGenBehaviorTests.cs:796`) -- ctor args + property set (path 1)
- `ProvideInjectBasic_ParentProvidesChildInjects` (`SourceGenBehaviorTests.cs:850`) -- provide/inject without ctor args (path 2, works because no ctor args needed)

## Test to Add After Fix

A test combining ctor args + provide/inject on the same child:

```csharp
// Child with inject + ctor param
[QuickMarkup("""
    inject string Label;
    string Extra = "";
    <TestText Text=`$"{Label}-{Extra}"` />
    """)]
public partial class ProvideInjectCtorArgsTarget : IQuickMarkupComponent<TestText>
{
    [QuickMarkupConstructor]
    private void MyInit(string extra)
    {
        Extra = extra;
        Init(extra);
    }
}

// Parent with provide + ctor arg syntax
[QuickMarkup("""
    public provide string Label = "hello";
    <root>
        <ProvideInjectCtorArgsTarget("world") />
    </root>
    """)]
public partial class ProvideInjectCtorArgsCase : TestRoot;
```

```csharp
[TestMethod]
public void ProvideInjectCtorArgs_BothAvailableInCtor()
{
    var page = new ProvideInjectCtorArgsCase();
    var text = TestTreeAssert.Child<TestText>(page.Children, 0);
    Assert.AreEqual("hello-world", text.Text);
}
```
