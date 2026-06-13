---
name: snapshot-testing
description: How to run, update, or add snapshot tests for source generator output in Get.Parser. Snapshot tests verify generated C# code (`.g.cs`) against stored reference files to catch regressions.
---

# Snapshot Testing for Source Generators

Snapshot tests live in `Parser/Get.SourceGenerator.Test/`. They compile a small C# source that uses a source generator attribute (e.g. `[Parser]`, `[Lexer]`), run the generator in-process via Roslyn APIs, and compare the output against stored `.cs.snap` files.

## Quick Reference

| Action | Command |
|--------|---------|
| Run all snapshot tests | `dotnet test Parser/Get.SourceGenerator.Test` |
| Update all snapshots | `$env:UPDATE_SNAPSHOTS=1; dotnet test Parser/Get.SourceGenerator.Test` |
| Add a new snapshot test | see below |

## How It Works

- `SnapshotTestBase` (in `Parser/Get.SourceGenerator.Test/SnapshotTestBase.cs`) provides:
  - `CreateCompilation(source)` — builds a Roslyn `CSharpCompilation` with proper metadata references
  - `LoadParserGenerator()` / `LoadLexerGenerator()` — loads the internal source generator DLL via reflection
  - `RunGenerator(compilation, generator)` — executes the generator and returns results
  - `MatchSnapshot(actual, name)` — compares output against `Snapshots/{name}.cs.snap`

- Snapshot files are stored at `Parser/Get.SourceGenerator.Test/Snapshots/*.cs.snap`
  - The `.cs.snap` extension prevents MSBuild from treating them as compile sources
  - Line endings are normalized (CRLF → LF) before comparison

## Updating Snapshots

When you intentionally change a source generator's output:

```powershell
# From the Parser/ directory:
$env:UPDATE_SNAPSHOTS=1
dotnet test Get.SourceGenerator.Test
Remove-Item Env:\UPDATE_SNAPSHOTS
```

When `UPDATE_SNAPSHOTS=1` is set, `MatchSnapshot` **writes** the file instead of asserting. After updating, run the tests again without the env var to confirm they pass.

## Adding a New Snapshot Test

1. In `ParserGeneratorSnapshotTests.cs` or `LexerGeneratorSnapshotTests.cs`, write a test that:
   - Defines inline C# source using the generator attribute
   - Creates a compilation and runs the generator
   - Asserts 0 diagnostics and exactly 1 generated source
   - Calls `MatchSnapshot(generatedCode, "UniqueName")`

2. Run with `UPDATE_SNAPSHOTS=1` to create the initial `.cs.snap` file

3. Run without the env var to confirm the test passes

## CI / Automation

On CI, `UPDATE_SNAPSHOTS` is not set, so any mismatch between generated output and the stored snapshot will fail the test with a diff. To update snapshots in CI, commit the `.cs.snap` changes and push.
