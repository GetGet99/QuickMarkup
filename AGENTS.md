The rules below applies to NEW CODE and NEW CONTRIBUTIONS.

Old codes may not currently following this guideline, but they must be modernized if the relevant code will be changed.

# Comments should be minimal

Do not add unnecessary comments. Key: code is documentation. Only add if something is complex and not understandable.

## Bad examples (Don't add these kinds of comments)

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

## What is OK: Public API documnetation

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

### But do not explain how it works internally

Bad Example:

```csharp
/// <summary>
/// Initialize Reactive Scheduler by adding dispatcher to reactive scheduler tick
/// </summary>
/// <remarks>How this method is implemented is that we are doing XXXX and YYYY.</remarks>
public static void InitReactiveScheduler()
{
    ...
}
```

## Bad Example: Context leaking

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

# AVOID duplicating code

You should find ways to extract common logics into shared helpers if you need the same path code.

In the future, it is easy to miss the fact that there are duplicated codes. There are codes in two different places. And one ended up being changed while leaving another one unchanged, causing unexpected bugs.

If extracting IS really hard and not worth the effort. Document it as a comment that there is another copy of the code.

It is almost always okay to extract shared helpers. If you need behavior to be a bit different than what we had, use `if` statements with more parameters for example and branch out.

# Contribution to the skills and documentation

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
