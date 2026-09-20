# QuickMarkup LSP

QuickMarkup language support for Visual Studio Code with real-time diagnostics, syntax highlighting, and language server integration.

## Features

- Syntax highlighting for `.qmui` files
- Real-time diagnostics via LSP
- Code analysis integration

## Requirements

- .NET 10.0+ Runtime (if using framework-dependent mode)
- Or use the self-contained build (no runtime required)

## Build & Package

```powershell
# Build the language server (self-contained, win-x64)
pnpm run build:server

# Package into VSIX
pnpm run package
```
