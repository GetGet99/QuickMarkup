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

Grammar and lexer use **Get.Parser** / **Get.Lexer** (usually under `Parser/` submodule). For framework mechanics, see `Parser/Language-Authoring-Guide.md` in that tree.

## Binders and language-side analysis

- **Source-gen binders** (how QM binds to C#): `QuickMarkup.SourceGen/Analyzers/`
- **Shared symbols / language model hooks**: `QuickMarkup.Language/Symbols/`
- **Type resolution for codegen**: `QuickMarkup.SourceGen/CodeGenTypeResolver.cs` (shared with codegen; change here when binding or typing rules affect both)

## Code generation (emitted C#)

- **Generators and snippets**: `QuickMarkup.SourceGen/CodeGen/`
- **Resolving types for generated code**: `QuickMarkup.SourceGen/CodeGenTypeResolver.cs`

## Source generator entry points

- **User-facing attribute**: `QuickMarkup.SourceGen/QuickMarkupAttribute.cs`
- **Main generator pipeline / refactor driver**: `QuickMarkup.SourceGen/QuickMarkupGeneratorRefactor.cs`

## Roslyn analyzers (IDE diagnostics on user C#)

- **Diagnostic IDs and analyzer logic**: `QuickMarkup.SourceGen/QuickMarkupAnalyzer.cs`
- **Attribute the analyzer keys off** (same surface as users): `QuickMarkup.SourceGen/QuickMarkupAttribute.cs`

## Workflow hint

1. Grammar/lexer/AST first if the feature is new surface syntax or tree shape.
2. Then binders/symbols/resolver so the generator knows types and members.
3. Then `CodeGen/` output and generator wiring in `QuickMarkupGeneratorRefactor.cs`.
4. Add or extend `QuickMarkupAnalyzer.cs` when users need compile-time feedback on invalid QuickMarkup usage.

Keep changes scoped to the layer that actually needs to move; resolver and binders often change together with new AST or binding rules.
