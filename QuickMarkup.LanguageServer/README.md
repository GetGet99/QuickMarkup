# QuickMarkup Language Server

LSP server providing real-time diagnostics for `.qmui` files in VS Code.

## How It Works

When you edit a `.qmui` file, the server:

1. Parses the markup using the same parser as `dotnet build`
2. Resolves types by loading the project's `.csproj` via Roslyn `MSBuildWorkspace`
3. Binds the markup against the resolved types
4. Publishes diagnostics to the editor (red/wavy underlines)

If no `.csproj` is found in the workspace root, the server falls back to
syntax-only diagnostics (no type resolution).

---

## Running

### From VS Code (F5 Debug)

1. Open `QuickMarkup.VSCode.Extension/` as the workspace folder in VS Code
2. Press **F5** — this launches a new Extension Development Host window
3. In that window, open any `.qmui` file (e.g. from `QuickMarkup.SourceGen.Test/`)
4. The extension starts the server via `dotnet run --project ../QuickMarkup.LanguageServer`
5. Diagnostics appear in the **Problems** panel as you type

### From Command Line (standalone server)

```pwsh
cd QuickMarkup.LanguageServer
dotnet run
```

This starts the server on stdin/stdout. It will wait for an LSP client to
connect. To test manually, you can pipe JSON-RPC messages or use a tool like
`lsp-test`.

### Building

```pwsh
dotnet build QuickMarkup.LanguageServer\QuickMarkup.LanguageServer.csproj
```

### Running Tests

```pwsh
dotnet test QuickMarkup.LanguageServer.Test\QuickMarkup.LanguageServer.Test.csproj
```

---

## What You Should See

| Scenario | Expected result |
|---|---|
| Open valid `.qmui` file | No diagnostics |
| Type invalid markup (e.g. `<FooBar>` where type doesn't exist) | Red squiggly: *"Unknown type 'FooBar'"* (QM1008) |
| Reference unknown property | Yellow squiggly: *"does not have a definition for..."* (QM1006) |
| No `.csproj` in workspace | Syntax-only diagnostics still work (parse errors) |
| Close a `.qmui` file | Squigglies clear immediately |

---

## Project Structure

```
QuickMarkup.LanguageServer/
├── Program.cs                     Entry point, DI registration, workspace init
├── Contracts/
│   ├── IRoslynWorkspaceManager.cs Interface for loading Compilation
│   └── IQmuiDiagnosticService.cs  Interface for getting diagnostics
├── Handlers/
│   ├── QmuiDidOpenHandler.cs      Diagnostics on file open
│   ├── QmuiDidChangeHandler.cs    Debounced diagnostics (300ms) on edit
│   └── QmuiDidCloseHandler.cs     Clear diagnostics on file close
├── Diagnostics/
│   ├── QmuiDiagnosticService.cs   Parses .qmui, binds against Roslyn, returns LSP diagnostics
│   ├── LspDiagnosticConverter.cs  Maps QMDiagnostic → OmniSharp Diagnostic
│   └── PositionConverter.cs       Maps Get.PLShared.Position → LSP Position/Range
├── Workspace/
│   ├── RoslynWorkspaceManager.cs  Loads .csproj via MSBuildWorkspace (primary)
│   ├── AdhocWorkspaceManager.cs   Fallback if MSBuildWorkspace fails
│   └── ProjectFinder.cs           Scans workspace root for .csproj

QuickMarkup.LanguageServer.Test/
├── LspDiagnosticConverterTests.cs     21 converter tests
├── QmuiDiagnosticServiceTests.cs      Service tests with mock workspace
└── HandlerSmokeTests.cs               Smoke tests for handler/service types

QuickMarkup.VSCode.Extension/
├── package.json                       Language registration, activation
├── src/extension.ts                   Launches LSP server process
└── syntaxes/qmui.tmGrammar.json       TextMate grammar for syntax highlighting
```

---

## Prerequisites

- .NET 10 SDK
- VS Code with the Extension Development Host
- For type resolution: a `.csproj` in the workspace root with NuGet packages restored

## Known Limitations (Phase 1)

- **Cross-file component resolution:** If component `A` is defined in `A.qmui`
  and used in `B.qmui`, the binder may report a false "unknown type" for `A`.
  This works correctly in the source generator pipeline but isn't ported to
  the LSP yet.
- **Stale diagnostics on C# changes:** Editing a C# type that a `.qmui` file
  depends on won't trigger re-diagnostics until the server re-loads the
  workspace (which happens on `.csproj` change or server restart).
