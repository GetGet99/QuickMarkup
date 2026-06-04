---
name: quickmarkup-contributing
description: Maps QuickMarkup repository areas to files for contributors implementing language, binders, codegen, source generation, or Roslyn analyzers. Use when editing QuickMarkup itself (not when consuming QuickMarkup as a library), adding syntax, AST nodes, binders, diagnostics, or generated C# output.
---

# Contributing to QuickMarkup

Paths are relative to the **QuickMarkup repository root** (the folder containing `QuickMarkup.Language`, `QuickMarkup.SourceGen`, etc.).

## Defining or extending the language (grammar + AST)

- **Parser (LR rules, reductions)**: `QuickMarkup.Language/Parser/QuickMarkupParser.cs`
- **Lexer (tokens, states, regex actions)**: `QuickMarkup.Language/Parser/QuickMarkupLexer.cs`
- **AST shapes / node types**: `QuickMarkup.Language/AST/`
- **TextMate grammar scopes**: `QuickMarkup.Language/Textmate/`

Grammar and lexer use **Get.Parser** / **Get.Lexer** (usually under `Parser/` submodule). For framework mechanics, see `Parser/Language-Authoring-Guide.md` in that tree.

## Binders and language-side analysis

- **Source-gen binders** (how QM binds to C#): `QuickMarkup.CodeAnalysis/Binders/`
- **Shared symbols / language model hooks**: `QuickMarkup.Language/Symbols/`
- **Type resolution for codegen**: `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs` (shared with codegen; change here when binding or typing rules affect both)
- **Generated member tracking**: `QuickMarkup.CodeAnalysis/QuickMarkupGeneratedMemberTable.cs` and `QuickMarkupGeneratedMemberTableBuilder.cs`
- **Target type context**: `QuickMarkup.CodeAnalysis/Helpers/QuickMarkupTargetContext.cs`

## Shared analysis project (QuickMarkup.CodeAnalysis)

`QuickMarkup.CodeAnalysis/` is a **shared project** (`.shproj`) containing code used by both the source generator and the analyzer. Key files:

- **Binder base and main binder**: `Binders/Binder.cs`, `Binders/QuickMarkupBinder.cs`, `Binders/QuickMarkupBinder.Refs.cs`
- **Binder diagnostics**: `Binders/QMBinderError.cs`
- **Ref attribute binding**: `Binders/RefAttributeBinder.cs`
- **Type resolution**: `CodeTypeResolver.cs`
- **Member table**: `QuickMarkupGeneratedMemberTable.cs`
- **String similarity for suggestions**: `StringSimilarity.cs`
- **Provider extensions** (parsing + source-gen hooks): `Helpers/QuickMarkupProviderExtension.*.cs`

## Code generation (emitted C#)

- **Generators and snippets**: `QuickMarkup.SourceGen/CodeGen/`
  - `CodeGenContext.cs` — generates init/setup code from bound tree
  - `CodeSnippets.cs` — C# code snippet helpers (closures, property assign)
  - `RefsGenContext.cs` — generates ref/computed property declarations

## Source generator entry points

- **User-facing attribute**: `QuickMarkup.Infra/QuickMarkupAttribute.cs`
- **Main generator pipeline**: `QuickMarkup.SourceGen/QuickMarkupGenerator.cs`
- **.qmui file pipeline**: `QuickMarkup.SourceGen/QuickMarkupGenerator.Qmui.cs`
- **Diagnostic reporting**: `QuickMarkup.SourceGen/QuickMarkupDiagnosticReporter.cs`

## Roslyn analyzers (IDE diagnostics on user C#)

- **Diagnostic IDs and analyzer logic**: `QuickMarkup.SourceGen/QuickMarkupAnalyzer.cs`
- **.qmui file analysis**: `QuickMarkup.SourceGen/QuickMarkupAnalyzer.Qmui.cs`
- **Attribute the analyzer keys off** (same surface as users): `QuickMarkup.Infra/QuickMarkupAttribute.cs`
- **Diagnostic reporting helpers**: `QuickMarkup.SourceGen/QuickMarkupDiagnosticReporter.cs`

## Language Server (QuickMarkup.LanguageServer)

Full LSP server for real-time `.qmui` diagnostics and navigation:

- **Entry point / DI**: `QuickMarkup.LanguageServer/Program.cs`
- **Compilation enrichment**: `QuickMarkupLanguageServer/QuickMarkupCompilationEnricher.cs`
- **Diagnostic service**: `Diagnostics/QmuiDiagnosticService.cs`
- **Handlers**: `Handlers/QmuiDefinitionHandler.cs`, `QmuiHoverHandler.cs`, `QmuiDidOpenHandler.cs`, `QmuiDidChangeHandler.cs`, `QmuiDidCloseHandler.cs`
- **Navigation**: `Navigation/MarkupCursorResolver.cs`, `SymbolLocationResolver.cs`
- **Workspace management**: `Workspace/RoslynWorkspaceManager.cs`, `ProjectFinder.cs`, `SolutionParser.cs`

## Snapshot persistence (QuickMarkup.Snapshot)

- **Snapshot attributes**: `QuickMarkup.Snapshot/Attributes.cs`
- **Snapshot interfaces**: `QuickMarkup.Snapshot/ISnapshotComponent.cs`, `ISnapshotFormatter.cs`
- **Snapshot source generator**: `QuickMarkup.Snapshot.SourceGen/QuickMarkupSnapshotGenerator.cs`
- **Snapshot binder**: `QuickMarkup.Snapshot.SourceGen/Binders/SnapshotBinder.cs`

## Grammar generation and editor integration

- **TextMate grammar generator**: `QuickMarkup.GrammarGenerator/Program.cs` — generates grammar JSON from lexer tokens
- **VS Code extension**: `QuickMarkup.VSCode.Extension/` — syntax highlighting, LSP client for `.qmui` files

## Runtime infrastructure (QuickMarkup.Infra)

Runtime library with reactive infrastructure, UI block abstractions, and the user-facing `[QuickMarkup]` attribute.

## Workflow hint

1. Grammar/lexer/AST first if the feature is new surface syntax or tree shape.
2. Then binders/symbols/resolver (in `QuickMarkup.CodeAnalysis/`) so the generator knows types and members.
3. Then `CodeGen/` output and generator wiring in `QuickMarkupGenerator.cs`.
4. Add or extend `QuickMarkupAnalyzer.cs` when users need compile-time feedback on invalid QuickMarkup usage.
5. If the change affects `.qmui` files, update the Language Server handlers and diagnostic service.

Keep changes scoped to the layer that actually needs to move; resolver and binders often change together with new AST or binding rules.
