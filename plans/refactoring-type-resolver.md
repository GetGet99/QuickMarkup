# Refactor: CodeTypeResolver.GetTypeSymbol Performance

**File:** `QuickMarkup.CodeAnalysis/CodeTypeResolver.cs`

## Problem

`CodeTypeResolver.GetTypeSymbol(string typeName)` resolves a C# type name by:
1. Creating a fake C# source file with a field of that type
2. Parsing it into a syntax tree
3. Adding it to the compilation (`compilation.AddSyntaxTrees(tree)`)
4. Getting the semantic model
5. Extracting the field's declared symbol

This creates a **new compilation** on every call, which is expensive. `AddSyntaxTrees` returns a new `Compilation` instance each time. The method also creates a new `CSharpParseOptions` from `compilation.SyntaxTrees.First()` on every call.

## Symptoms

- Each type resolution creates: new parse options, new source text, new syntax tree, new compilation, new semantic model
- No caching across calls beyond the `Dictionary<string, INamedTypeSymbol?>` result cache (which helps for repeated lookups of the same type name, but not for different names)
- The `First()` call to get parse options assumes at least one syntax tree exists in the compilation

## Suggested Approach

1. Cache the `CSharpParseOptions` once instead of fetching from `SyntaxTrees.First()` each time.
2. Create a reusable `SyntaxTree` template with a placeholder that gets substituted, rather than parsing from scratch each time.
3. Use `SyntaxFactory` to build and resolve the type symbol without creating a full compilation:
   - Create `IdentifierNameSyntax` 
   - Use `SemanticModel.GetSpeculativeSymbolInfo` or `GetSemanticModel` on an already-added tree
4. If creating a new compilation is unavoidable, batch type resolutions so multiple types are resolved in a single compilation round.

## Risk

- Medium: speculative semantic model APIs have limitations with complex types
- Low: behavior must remain identical for all existing callers
