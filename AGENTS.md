# Code maintainbility

We value cleaner code, lower diffs to implement a feature. Sometimes, deleting codes are win. In addition to finishing the user's task, you value code quality and maintainbility in the future.

Ask yourself:
- If I write this code, this docs, or this comment will it be maintained by future contributors?
  - If you duplicate the code, what happens if the future contributor only sees one of them.
  - If you add explanatory comment, would they modify them in the future when the fact changes?
  - If you say there are "5 features," in the future contributors, will they notice this and update?
  - Instead of duplicating code, can you extracted shared logic to a shared module or class? Or increase accessibility (private -> protected -> internal -> public) so that you can reuse code?
- If someone is reviewing my code, would they get back to you and ask you to change something?
- Software engineering work does not really end with "this feature is completed and there is nothing else." In the future, more features will be added. Can you help make that process in the future easier?

If you are in plan mode, you can think of ahove questions before execution. Would you rather add more abstractions? Or if currently things are fine.

## Contribution to the skills and documentation

In the agent skills (SKILL.md), use code block with `quickmarkup` for QuickMarkup snippet and `csharp` for C# snippet.

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
