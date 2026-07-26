# Contribution Guidelines

The rules below applies to NEW CODE and NEW CONTRIBUTIONS.

Old codes may not currently following this guideline, but they must be modernized if the relevant code will be changed.

## QuickMarkup Pipeline Codebase standards

Here are the responsibilities on each layer of QuickMarkup pasing to code generation.

Parser - A parser that does pasing. Validation and typing DOES NOT happen on this layer.

Binder or type checking - QuickMarkup code validation DOES happen on this layer. It must do all preprocessing before giving to code generation stage. All the typing information, all of "should codegen does A or B due to the semantics"? Binder should be the one deciding on default behaviors of what happens if things are not provided, whether to error out or we have to provide default behavior, like does omitting accessibility mean private, protected, or public.

Code generation - Generates code based on what was provided by the binder. Code generation SHOULD NOT try to guess types or do type lookups. Code generation SHOULD NOT try to guess syntax, visibility, accesssibility, or to infer non-explicit meaning. Code generation should consume explicit decisions and information from binder. Binder should be responsible for providing acessibility, visibility, typing, and other values. Binder should be the one deciding on default behaviors, not code generation.

## QuickMarkup Test standards

### Running tests

Run all tests

```sh
dotnet test
```

Run tests in Native AOT environment (note: Linux only)

```sh
test-nativeaot.sh # run all
test-nativeaot.sh -- no-build # run without building (good for testing flakiness without waiting for rebuild)
test-nativeaot.sh QuickMarkup.SourceGen.IntegrationTest # run specific project
test-nativeaot.sh QuickMarkup.SourceGen.IntegrationTest --no-build # can specify both

# To run more than one specific project, run as separate terminal commands or without parameter to run all
```

To ensure that the work is done well for NativeAOT users as well, please run tests for NativeAOT too.

### Project and path specific tests

QuickMarkup.Infra.Test - tests on infrastructure level without source generator component. 

QuickMarkup.Syntax.Test - tests for lexer and parser (AST generation).

QuickMarkup.SourceGen.Test - unit tests for source generator components (code type resolver, framework configuration reader). Tests against in-memory Roslyn compilations.

QuickMarkup.LanguageServer.Test - unit tests for language server services (semantic resolution, diagnostics, symbol location, document store).

### Integration Tests

Integration tests test the flow from input QuickMarkup code into seeing the actual runtime behavior, effectively testing through infra, lexer, parser, binder, and code generation. We have 3 types of integration tests.

One that only works ONLY in the new DeferredInit behavior - must go under [DeferredInit](QuickMarkup.SourceGen.IntegrationTest/DeferredInit) cases.

One that only works ONLY in the old BackCompat behavior - must go under [BackCompat](QuickMarkup.SourceGen.BackCompatIntegrationTest) cases.

One that works in BOTH behavior - must go under [Shared](QuickMarkup.SourceGen.IntegrationTest/Shared) cases. Backcompat project will run these tests too.

Integration tests only works with correct QuickMarkup syntax, usually used with happy paths. Incorrect QuickMarkup syntax will cause compilation error to the integration test project.

#### Note

Integration tests do not cover diagnostics, language server, real UI frameworks integration, and most unhappy flows. Integration tests are the current standard of testing main QuickMarkup pipeline. Because it does not integrate with real UI frameworks and do not actually draw UI, the costs of running integration tests are very cheap and is a recommended way to test the QuickMarkup behavior.

Regardless, if the tests can also be added into other tests, do so too!

## Comments should be minimal

Do not add unnecessary comments. Key: code is documentation. Only add if something is complex and not understandable.

### Bad examples (Don't add these kinds of comments)

```csharp
int a = 0;

// Write variable `a` code to console
Console.WriteLine($"a = {a}");
```

```csharp
List<int> list = new() { 1, 2, 3 };

// Double each elements (guareded by shouldDoubleItems)
if (shouldDoubleItems) {
  for (int i = 0; i < list.Length; i++) {
    list[i] = list[i] * 2;
  }
}
```

### What is OK: Public API documnetation

```csharp
/// <summary>
/// Initialize Reactive Scheduler
/// </summary>
/// <remarks>Must be call on UI thread with dispatcher</remarks>
public static void InitReactiveScheduler()
{
    ...
}
```

#### But do not explain how it works internally

Bad Example:

```csharp
/// <summary>
/// Initialize Reactive Scheduler by adding dispatcher to reactive scheduler tick
/// </summary>
/// <remarks>How this method is implemented is that we are doing XXXX and
    ...
}
```

### Bad Example: Context leaking

If user is asking you to change from `X()` to `Y()`. Don't write comments.

Good example: `Y()`. Plain. Simple.

Bad examples

```csharp
// Calling Y() instead of X()
Y();
```

```csharp
// Aligning with the new behavior to call Y() instead of X() because ...
Y();
```

```csharp
// Old code is X()
Y();
```

## AVOID duplicating code

You should find ways to extract common logics into shared helpers if you need the same path code.

In the future, it is easy to miss the fact that there are duplicated codes. There are codes in two different places. And one ended up being changed while leaving another one unchanged, causing unexpected bugs.

If extracting IS really hard and not worth the effort. Document it as a comment that there is another copy of the code.

It is almost always okay to extract shared helpers. If you need behavior to be a bit different than what we had, use `if` statements with more parameters for example and branch out.

## Contribution to the skills and documentation

When making changes to the codebase, it is critical that we maintain the docs to be proper. For new changes in codebase, should also reflect in the documentation. This includes [quickmarkup](.agents/skills/quickmarkup/SKILL.md) skill - other agents will be referencing this skill for writing QuickMarkup code.

Note: in most cases README.md does not have to be updated. README.md is designed to showcase minimally, so unless how the basic onboarding or getting started flow changes, you don't need to update in README.md.

### Writing Guideline

You should make changes as minimal as possible to the existing skills and documentations while preserving the intent of documenting new changes or features. More writing does not mean better in terms of guidelines.

### Code Blocks

In the agent skills or rules (SKILL.md or AGENTS.md), use code block with `quickmarkup` for QuickMarkup snippet and `csharp` for C# snippet.

```quickmarkup
<TextBlock Text="My textblock" />
```
```csharp
Console.WriteLine("My C# code");
```

In oter documentation, use `csharp` with the `//` comment to denote whether it is QuickMarkup or C# code.

```csharp
// QuickMarkup
<TextBlock Text="My textblock" />
```
```csharp
// C#
Console.WriteLine("My C# code");
```

Reason: In other user facing documentation, `csharp` provides a better syntax highlgihting and the language name is not usually shown directly to the user. However, syntax highlighting information is not given to the agent, so using `quickmarkup` and `csharp` directly will help the agent understand better.
