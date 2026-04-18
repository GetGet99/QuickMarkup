# Structural Grammar Plan

This plan covers the grammar and AST migration for QuickMarkup structural children:

- structural fragments: `{ ... }`
- `if (...) child`
- `if (...) child else child`
- `foreach (var item in source) child`
- `foreach (index; var item in source) child`
- `foreach (var item in source; key) child`

The goal is to finish the Vue-like structural syntax on top of the infrastructure, binder, and sourcegen work that already exists.

This plan intentionally preserves the existing `<>...</>` value-list feature. Historically it was also called a fragment, but this plan reserves "fragment" for the new structural child syntax `{ ... }`.

## Current State

Relevant files:

- `QuickMarkup.Language/Parser/QuickMarkupLexer.cs`
- `QuickMarkup.Language/Parser/QuickMarkupParser.cs`
- `QuickMarkup.Language/AST/QuickMarkupSFC.cs`
- `QuickMarkup.Language/Symbols/QuickMarkupSymbols.cs`
- `QuickMarkup.CodeAnalysis/Binders/QuickMarkupBinder.cs`
- `QuickMarkup.SourceGen/CodeGen/CodeGenContext.cs`
- `QuickMarkup.Syntax.Test/Test1.cs`

Already implemented:

- Lexer tokens exist for `foreach`, `if`, `else`, `in`, `var`, parentheses, braces, and `..` in `BeforeRoot`.
- AST nodes exist for `QuickMarkupParsedForNode`, `QuickMarkupParsedIfNode`, and `QuickMarkupParsedFragmentNode`.
- `QuickMarkupQMs` exists for the old `<>...</>` nested value-list syntax used in places like property values.
- Binder already binds collection `if`, `foreach`, fragments, single-child conditional values, foreach index names, and foreach keys.
- Symbols already carry `QMForKind`, optional `IndexVarName`, optional bound `Key`, `QMIfNodeSymbol<T>`, `QMConditionalValueSymbol<T>`, `QMFragmentNodeSymbol`, and `ChildCollectionLowering`.
- Sourcegen already has block-lowered paths for `ConditionalBlock`, `ForBlock`, `FragmentBlock`, `StaticBlock`, keyed `ForBlock.Create`, index-aware loop factories, and conditional slots.

Known grammar gaps:

- `ParsedIfNode` has no parser rules.
- `QuickMarkupParsedFragmentNode` has no parser rules.
- `QMChild` only recognizes `ParsedForNode` and `QMValue`, so `if` and standalone fragments are not parseable children.
- Current `ParsedForNode` accepts only the existing basic foreach header and stores block bodies as `ListAST<IQMNodeChild>`.
- Current `QuickMarkupParsedIfNode` and `QuickMarkupParsedForNode` store branch/body child lists directly, while the desired model is one child body where multiple children are represented by a fragment.
- `Semicolon` is only lexed in `Props`, but advanced foreach headers need semicolons in `BeforeRoot`.
- `QuestionMark` is only lexed in `Props`, so nullable explicit loop types in `BeforeRoot` are incomplete.

Naming gap:

- `QuickMarkupQMs` is the existing `<>...</>` value-list construct. It should be renamed to `QuickMarkupValueList` before or during grammar work so "fragment" clearly means the new structural `{ ... }` child node.

## Target AST Shape

Move structural bodies from child lists to single child nodes.

Recommended final shape:

```csharp
public record class QuickMarkupParsedIfNode(
    QuickMarkupValue Condition,
    IQMNodeChild BodyWhenTrue,
    IQMNodeChild? BodyWhenFalse
) : AST, IQMNodeChild;

public record class QuickMarkupParsedForNode(
    TypeDeclaration? VarType,
    string VarName,
    QuickMarkupValue Iterable,
    IQMNodeChild Body,
    string? IndexVarName = null,
    QuickMarkupValue? Key = null
) : AST, IQMNodeChild;

public record class QuickMarkupParsedFragmentNode(
    ListAST<IQMNodeChild> Children
) : AST, IQMNodeChild;
```

Reasoning:

- `if`, `else`, and `foreach` semantically have one body child.
- A block with multiple children is just a `QuickMarkupParsedFragmentNode`.
- A single tag/value/structural statement stays a direct child and does not need a one-item list wrapper.
- Binder and sourcegen already understand fragments as structural grouping, so this aligns the parser model with the runtime model.
- `QuickMarkupQMs` remains a value form and should not become a node child fragment. It is still needed for property value lists such as `RowDefinitions=<>...</>`.

Rename old `<>...</>` concept:

- Rename `QuickMarkupQMs` to `QuickMarkupValueList`.
- Rename local parser non-terminals/comments from "QMs" where they refer specifically to `<>...</>` value lists, if practical.
- Preserve the existing grammar and behavior for:

```quickmarkup
<Grid RowDefinitions=<>
       <RowDefinition />
       <RowDefinition />
       <RowDefinition />
   </>
>
</Grid>
```

- Do not route `<>...</>` through `QuickMarkupParsedFragmentNode`.
- Do not change assignment-property binding behavior for `<>...</>` as part of this grammar pass.

Migration option to minimize diffs:

- Change the AST constructors first.
- Add small compatibility helpers in the binder that convert a structural body child into the existing member list shape:

```csharp
List<IQMMemberSymbol> BindStructuralBody(IQMNodeChild body, QMBinderTagInfo tagInfo)
```

- For a fragment body, bind `fragment.Children`.
- For a non-fragment body, bind a one-item `ListAST<IQMNodeChild>` or bind it directly through existing collection-child helpers.

Avoid keeping long-term compatibility constructors that accept lists. They would preserve the old mental model and make the parser migration less clear.

## Target Grammar

Conceptual grammar:

```text
QMChildren
  := empty
   | QMChildren QMChild

QMChild
  := ParsedIfNode
   | ParsedForNode
   | ParsedFragmentNode
   | QMValue

ParsedFragmentNode
  := "{" QMChildren "}"

StructuralBody
  := ParsedFragmentNode
   | ParsedIfNode
   | ParsedForNode
   | QMValue

ParsedIfNode
  := "if" "(" QMValue ")" StructuralBody
   | "if" "(" QMValue ")" StructuralBody "else" StructuralBody

ParsedForNode
  := "foreach" "(" ForHeader ")" StructuralBody

ForHeader
  := TypeDeclOrVarKeyword Identifier "in" QMIterable
   | Identifier ";" TypeDeclOrVarKeyword Identifier "in" QMIterable
   | TypeDeclOrVarKeyword Identifier "in" QMIterable ";" QMValue
   | Identifier ";" TypeDeclOrVarKeyword Identifier "in" QMIterable ";" QMValue
```

Notes:

- `QMValue` already includes parsed tags, foreign expressions, primitive values, and the old `<>...</>` nested value-list construct.
- A brace body always produces `QuickMarkupParsedFragmentNode`.
- `if (...) <A /> else <B />` should associate the `else` with the nearest unmatched `if`.
- `if (a) <A /> if (b) <B /> else <C />` is two sibling children. The `else <C />` belongs to the second/nearest `if (b)`, matching C/C++/C#/Java-style dangling-else behavior.
- The parser generator is LR(1), so if the direct `ParsedIfNode` rules create a dangling-else shift/reduce conflict, split the grammar into matched/unmatched if forms rather than relying on fragile parser behavior.

Matched/unmatched fallback shape:

```text
StructuralBody
  := MatchedStructuralBody
   | UnmatchedIf

MatchedStructuralBody
  := ParsedFragmentNode
   | ParsedForNode
   | QMValue
   | MatchedIf

MatchedIf
  := "if" "(" QMValue ")" MatchedStructuralBody "else" MatchedStructuralBody

UnmatchedIf
  := "if" "(" QMValue ")" StructuralBody
   | "if" "(" QMValue ")" MatchedStructuralBody "else" UnmatchedIf
```

This keeps `else` binding local and deterministic.

## Lexer Changes

Small lexer edits:

- Add `BeforeRoot` state to `Semicolon`.
- Add `BeforeRoot` state to `QuestionMark` so nullable explicit loop types work in structural headers.
- Keep `{` and `}` as structural tokens in `BeforeRoot`.
- Do not re-enable `{ ... }` as foreign syntax. Backtick foreign already covers C# expressions and avoids ambiguity with fragments.

Naming cleanup:

- Rename `Terminal.For` to `Terminal.Foreach`.
- Update parser rules, syntax tests, and any generated/source references accordingly.
- This is slightly noisier than keeping `For`, but it makes future parser diagnostics and grammar maintenance clearer.
- Keep this cleanup in the same grammar pass because `ParsedForNode` rules are being rewritten anyway.

## Parser Changes

Recommended order:

1. Add `ParsedFragmentNode` non-terminal.
2. Add `StructuralBody` non-terminal.
3. Add `ParsedIfNode` rules.
4. Add `ParsedIfNode` and `ParsedFragmentNode` to `QMChild`.
5. Change `ParsedForNode` to use `StructuralBody` instead of separate braced/single-child list rules.
6. Add advanced foreach header non-terminals.
7. Rename old `QuickMarkupQMs` usages to `QuickMarkupValueList` without changing its grammar.

Suggested helper constructors/reducers:

```csharp
static QuickMarkupParsedForNode CreateFor(
    TypeDeclaration? varType,
    string varName,
    QuickMarkupValue iterable,
    IQMNodeChild body,
    string? indexVarName,
    QuickMarkupValue? key)
    => new(varType, varName, iterable, body, indexVarName, key);
```

Use a small `ParsedForHeader` record if the `[Rule]` constructor mapping gets awkward:

```csharp
record class ParsedForHeader(
    TypeDeclaration? VarType,
    string VarName,
    QuickMarkupValue Iterable,
    string? IndexVarName,
    QuickMarkupValue? Key);
```

Then `ParsedForNode` can be:

```text
"foreach" "(" ParsedForHeader ")" StructuralBody
```

This keeps the foreach grammar readable and reduces duplicated parser rules.

## Binder Changes

Current binder expects:

- `QuickMarkupParsedForNode.Body` as `ListAST<IQMNodeChild>`
- `QuickMarkupParsedIfNode.BodyWhenTrue` as `ListAST<IQMNodeChild>`
- `QuickMarkupParsedIfNode.BodyWhenFalse` as `ListAST<IQMNodeChild>?`

Change it to bind one body child:

- Collection `if`:
  - true body: `BindStructuralBody(ifNode.BodyWhenTrue, tagInfo)`
  - false body: `null` or `BindStructuralBody(ifNode.BodyWhenFalse, tagInfo)`
- Single-child `if`:
  - true value: `BindSingleChildNode(ifNode.BodyWhenTrue, tagInfo)`
  - false value: `BindSingleChildNode(ifNode.BodyWhenFalse, tagInfo)`
  - missing `else` remains a binder error in assignment context.
  - `foreach` is rejected anywhere under the single-child assignment path.
  - a structural fragment `{ ... }` is allowed only if it contains exactly one valid single-child body.
  - nested `if` is allowed only if every branch recursively satisfies the same single-child rules.
  - any branch or fragment with zero children or more than one content child is a binder error.
- `foreach`:
  - push item/index scoped names as today.
  - bind `Body` through `BindStructuralBody`.
  - bind `Key` while item/index names are in scope, as today.
- Fragment:
  - collection context binds children exactly as today.
  - single-child assignment context validates the fragment as a grouping construct and unwraps only if it contains exactly one valid single-child body.

Important detail:

- `BindStructuralBody` must treat a non-fragment body as one child in collection context. That keeps `if (cond) <A />` and `foreach (...) <A />` equivalent to a one-child fragment without changing the AST.
- The existing old `<>...</>` value-list construct should remain handled by value binding, not fragment binding.

## Sourcegen Changes

Sourcegen mostly consumes symbols, so the parser/AST migration should not require large sourcegen rewrites if the binder keeps producing the same symbols.

Areas to verify after the AST migration:

- `QMIfNodeSymbol<T>.BodyWhenTrue` and `.BodyWhenFalse` should still be member lists.
- `QMForNodeSymbol<T>.Body` should still be a member list.
- `QMFragmentNodeSymbol.Body` should still be a member list.
- `QMForNodeSymbol<T>.Key` should still be bound when explicit key syntax is parsed.
- `QMValueSymbol<T>.CapturedLocalNames` should still include item/index names for loop body expressions and key expressions.

Nested conditional slot follow-up:

- `CGenScopedValueFactoryBody` currently throws for nested `QMConditionalValueSymbol<T>` values. The clarified binder rules allow nested single-child conditionals, so this gap should be filled as part of this work.
- Start by adding focused tests in `QuickMarkup.Infra.Test` that manually compose nested `ConditionalSlot<T>` / `ScopedValue<T>` structures and prove the desired lifecycle:
  - nested branch value updates the outer assignment target correctly.
  - switching the outer slot disposes the active inner slot/scope.
  - switching the inner slot disposes only the replaced inner branch.
  - effects in disposed nested branches stop reacting.
- After the infra shape is proven, update sourcegen so nested assignment conditionals lower to that structure instead of hitting the current not-supported path.
- This is not a parser gap, but it is required for the accepted single-child grammar to be fully supported end to end.

## Foreach Header Semantics

Basic:

```quickmarkup
foreach (var item in list) <A />
```

Bind as:

- `VarType = null`
- `VarName = "item"`
- `Iterable = list`
- `IndexVarName = null`
- `Key = null`

Index:

```quickmarkup
foreach (index; var item in list) <A />
```

Bind as:

- `IndexVarName = "index"`
- `VarName = "item"`

Key:

```quickmarkup
foreach (var item in list; `item.Id`) <A />
```

Bind as:

- `Key = QuickMarkupForeign("item.Id")`

Index and key together:

```quickmarkup
foreach (index; var item in list; `item.Id`) <A />
```

Key validation:

- Parser should accept any `QMValue` after the trailing semicolon.
- Binder should enforce key expression requirements.
- Per the requested design, explicit keys should be `QuickMarkupForeign`. If `Key` is not `QuickMarkupForeign`, report a binder diagnostic and continue with recovery.

## Fragment Semantics

Standalone fragment:

```quickmarkup
{
    <A />
    <B />
}
```

Rules:

- A fragment is an `IQMNodeChild`.
- It is allowed anywhere collection children are allowed.
- It can contain tags, values, callbacks, `if`, `foreach`, and nested fragments.
- In single-child assignment context, it is a grouping construct only. It must contain exactly one valid single child after recursively applying nested `if`/fragment rules.
- It must not be confused with old `<>...</>` value lists.

## Tests

Add parser-focused tests first in `QuickMarkup.Syntax.Test/Test1.cs`:

- parse standalone fragment under a tag child list.
- parse old `<>...</>` value lists for property assignment and verify the AST still uses the renamed value-list node, not `QuickMarkupParsedFragmentNode`.
- parse `` if (`condition`) <A /> ``.
- parse `` if (`condition`) <A /> else <B /> ``.
- parse `` if (`a`) if (`b`) <B /> else <C /> `` and verify `else` binds to the inner `if`.
- parse `` if (`condition`) { <A /> <B /> } ``.
- parse nested `foreach` and `if` inside a fragment.
- parse `` foreach (var item in `items`) <A /> ``.
- parse `` foreach (var item in `items`) { <A /> <B /> } ``.
- parse `` foreach (index; var item in `items`) <A /> ``.
- parse `` foreach (var item in `items`; `item.Id`) <A /> ``.
- parse `` foreach (index; var item in `items`; `item.Id`) <A /> ``.

Add lexer tests:

- semicolon token in `BeforeRoot`.
- nullable type punctuation in `BeforeRoot`, if supported.

Add binder/sourcegen regression tests after parser tests pass:

- collection `if` still produces block lowering.
- standalone fragment produces block lowering.
- reactive `foreach` with index has `IndexVarName`.
- reactive `foreach` with key has bound `Key`.
- non-foreign key reports a binder error.
- single-child assignment `if` without `else` still reports an error.
- single-child assignment `if` rejects any branch/fragment with multiple content children.
- single-child assignment `if` rejects any `foreach`.
- single-child assignment fragment unwraps exactly one valid child and rejects zero/multiple children.
- nested assignment `if` generates valid nested conditional slots.

Add infra tests before sourcegen implementation:

- nested `ConditionalSlot<T>` assigns the currently selected nested branch value.
- outer condition switch disposes the previously active nested slot and its branch scope.
- inner condition switch disposes only the previous inner branch scope and keeps the outer slot alive.
- branch effects stop after the branch scope is disposed.

## Implementation Order

1. Rename old `QuickMarkupQMs` / `<>...</>` terminology to `QuickMarkupValueList` without changing behavior.
2. Rename `Terminal.For` to `Terminal.Foreach`.
3. Update lexer punctuation for advanced foreach headers.
4. Change structural AST body properties from `ListAST<IQMNodeChild>` to `IQMNodeChild`.
5. Update binder body binding helpers so symbols keep the current shape.
6. Add parser `ParsedFragmentNode` and use it for brace bodies.
7. Add parser `ParsedIfNode`, including the dangling-else-safe form if needed.
8. Rewrite `ParsedForNode` around a `ParsedForHeader` plus `StructuralBody`.
9. Add advanced foreach header forms for index and key.
10. Add binder validation that explicit foreach keys must be foreign.
11. Tighten single-child assignment binding for fragments, nested conditionals, and foreach rejection.
12. Add infra tests for nested `ConditionalSlot<T>` / `ScopedValue<T>` composition.
13. Implement sourcegen for nested `QMConditionalValueSymbol<T>` inside conditional slot branch factories.
14. Add syntax tests for all accepted forms and old `<>...</>` preservation.
15. Add binder/sourcegen regression tests for index, key, fragments, and if/else.
16. Run `dotnet test QuickMarkup.slnx`.

## Open Decisions

No remaining grammar-level open decisions from this plan.

Settled decisions:

- Non-foreign bool literal conditions such as `if (true)` and `if (false)` should be valid. Plain identifiers should still be rejected unless/until identifier condition semantics are deliberately designed.
- Nullable explicit loop types are included in this pass by lexing `?` in `BeforeRoot`.
- The old `<>...</>` AST/value construct will be renamed from `QuickMarkupQMs` to `QuickMarkupValueList`.
- Nested single-child conditionals are valid and should be supported end to end. The plan includes infra tests first, then sourcegen support for nested `QMConditionalValueSymbol<T>` under conditional slots.
