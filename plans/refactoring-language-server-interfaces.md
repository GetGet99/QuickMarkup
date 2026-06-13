# Refactor: Reduce Language Server Interface Indirection

**Location:** `QuickMarkup.LanguageServer/Contracts/` (11 interfaces)

## Problem

The Language Server has 11 single-implementation service interfaces, each with exactly one implementation class registered in DI:

| Interface | Implementation |
|---|---|
| `IQmuiDiagnosticService` | `QmuiDiagnosticService` |
| `ICatalogService` | `CatalogService` |
| `IMemberTableService` | `MemberTableService` |
| `IFileWatcherService` | `FileWatcherService` |
| `IQmuiWorkspaceService` | `QmuiWorkspaceService` |
| `IQmuiDocumentStore` | `QmuiDocumentStore` |
| `IQmuiSemanticService` | `QmuiSemanticService` |
| `IMarkupCursorResolver` | `MarkupCursorResolver` |
| `ISymbolLocationResolver` | `SymbolLocationResolver` |
| `ICompilationService` | `CompilationService` |
| `IFileProvider` | `FileSystemProvider` |

While interfaces are useful for testing, 11 interface-implementation pairs for a single production consumer is disproportionate. Some interfaces are near-empty wrappers (e.g., `IMarkupCursorResolver` is a 38-line shim, `IFileWatcherService` is 3 methods).

## Suggested Approach

1. **Remove `IMarkupCursorResolver`** — inline into `MarkupCursorResolver` (it's a 2-line delegation to `IQmuiSemanticService`). The handler can call `IQmuiSemanticService` directly.

2. **Remove `IFileWatcherService`/`FileWatcherService`** — replace with a simple `EventHandler` or integrate file watching directly into `QmuiWorkspaceService`.

3. **Remove `ICatalogService`/`IMemberTableService`** — merge their logic into `QmuiWorkspaceService` which is already the orchestrator.

4. **Keep** `IQmuiWorkspaceService`, `IQmuiDocumentStore`, `ICompilationService`, `IQmuiSemanticService`, `ISymbolLocationResolver`, `IQmuiDiagnosticService` (these have genuine orchestration responsibility).

This reduces from 11 to ~6 interfaces.

## Risk

- Low: DI registrations need updating
- Low: Tests that mock removed interfaces need to mock consolidated ones instead
