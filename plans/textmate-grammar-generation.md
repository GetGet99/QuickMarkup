# TextMate Grammar Generation — Gaps & Improvements

## Goal

Upgrade `Get.LangSupport` and the QuickMarkup lexer annotations so the auto-generated
`qmui.tmGrammar.json` covers all language features properly — including foreign C#
sections, structured tags, and embedded language injection — eliminating the need for
any hand-written grammar maintenance.

---

## Current Architecture

```
QuickMarkupLexer.cs               Get.LangSupport/
  enum Tokens { ... }               TextmateScopeAttribute.cs
    [Textmate*Scope]                 TextmateGrammarGenerator.cs
    [Regex(...)]                          │
         │                                │
         └──────────┬─────────────────────┘
                    │
        TextmateGrammarGenerator
        .GenerateRepository<QuickMarkupLexer>()
                    │
                    ▼
            qmui.tmGrammar.json
```

The generator reads `[Textmate*Scope]` + `[Regex]` attributes from the lexer's
terminal enum fields and emits a flat `#main` repository with `match` and
`begin`/`end` rules ordered by priority.

---

## Current Gaps

### 1. Foreign C# sections (backtick) — missing

Backtick-delimited inline C# code:

```quickmarkup
<Button Text=`counter.ToString()` />
```

The lexer processes these via a **non-emitting state machine**:
- `` ` `` → `HandleCuryForeignStart` (`ShouldReturnToken = false`) → enters `InsideTickForeign`
- content chars → `ForeignHelperToken` (`ShouldReturnToken = false`) → appended to buffer
- `` ` `` → `HandleForeignEnd` → emits `Foreign` token with accumulated buffer as data

No token emits the backtick content itself as a visible token, so **no TextMate
rule is generated** for it. The C# code appears unhighlighted.

**Same issue** for `/- ... -/` foreign sections.

### 2. Tag structure flattened

Hand-written grammar had structured `begin`/`end` patterns matching the full
`<TagName ...>` with nested `#tag-attributes` inside. The generated grammar splits
`<`, tag-name, `>` into three separate flat rules with no nesting.

This means attributes inside tags can't be scoped differently from attributes
outside tags — the TextMate context has no awareness of "inside an open tag".

### 3. `HeaderIdentifier` dead code

Both `Identifier` and `HeaderIdentifier` use the same regex `[a-zA-Z_][a-zA-Z0-9_]*`
with the same priority (0). `Identifier` is declared first in the enum, so its
rule appears first in the pattern list and always wins. `entity.name.type.other`
is unreachable (`QuickMarkupLexer.cs:180-182`).

| Token | Scope | Regex | Priority |
|---|---|---|---|
| `Identifier` | `variable.other.other` | `[a-zA-Z_][a-zA-Z0-9_]*` | 0 |
| `HeaderIdentifier` | `entity.name.type.other` | `[a-zA-Z_][a-zA-Z0-9_]*` | 0 |

### 4. State-duplicated rules

The same regex often appears on the same enum field multiple times with different
`State` values (e.g., `String` token with two `[Regex]` for `Props` and
`InsideQMOpenTag`). The generator emits one TextMate rule per `[Regex]` attribute,
producing identical duplicates:

| Duplicate | Lines in generated JSON |
|---|---|
| `string.quoted.double` | 25-31 |
| `punctuation.terminator` (`;`) | 116-119, 196-199, 202-207 |
| `punctuation.separator.dot` | 132-135, 192-195 |
| `punctuation.definition.tag.end` (`>`) | 176-179, 188-191 |
| `punctuation.separator.colon` (`:`) | 148-151, 200-202 |

Harmless but wasteful.

### 5. `RawBaseTypes` broad regex

`[^;]+` matches everything between semicolons. In the lexer, this is only active
in `BeforeRefsBaseTypes` state, but TextMate has no state awareness — it applies
globally (at low priority 0). After keywords and identifiers consume their matches,
this rule eats whatever's left. The `storage.type` scope is semantically wrong
outside base-type declarations.

### 6. No language injection for embedded C#

Foreign C# content can't embed `source.cs` highlighting. The generated grammar
has no mechanism for VS Code's `embeddedLanguages` or `include` of other language
grammars. C# inside `` ` `` stays plain text.

### 7. Captures not supported

The generator only produces `name` + `match`/`begin`/`end`. No `beginCaptures`,
`endCaptures`, or `captures` are emitted. This prevents marking sub-parts of a
pattern with different scopes (e.g., distinguishing the `.` of `entity.name.function`
from the function name).

### 8. Single flat `#main` repository

`GenerateRepository` always creates just `["main"] = ...`. All rules — comments,
strings, tags, operators — are dumped into one flat list. Structured decomposition
into named sections (`#comments`, `#strings`, `#tag-attributes`) is impossible.

---

## Proposed `Get.LangSupport` Improvements

### A. Multi-repository entries

**Problem**: All rules go into `#main`. Can't organize or selectively `include`
sub-sections.

**Solution**: Add `RepositoryKey` to `TextmateScopeAttribute`:

```csharp
public string? RepositoryKey { get; set; } = "main";
```

Rules with the same `RepositoryKey` are grouped together. `GenerateRepository`
produces multiple entries:

```csharp
{
  "main": { "patterns": [/* keywords, identifiers, etc. */] },
  "comments": { "patterns": [/* comment rules */] },
  "strings": { "patterns": [/* string rules */] },
  "tags": { "patterns": [/* tag rules */] },
  "tag-attributes": { "patterns": [/* attribute rules */] }
}
```

`GetGrammarJSON` generates top-level patterns that include each repository key
in a defined order:

```json
{
  "patterns": [
    { "include": "#comments" },
    { "include": "#strings" },
    { "include": "#main" }
  ],
  "repository": { ... }
}
```

### B. Capture support

**Problem**: Can't mark sub-parts of a match with different scopes.

**Solution**: Add capture dictionaries to `TextmateScopeAttribute`:

```csharp
public Dictionary<string, string>? BeginCaptures { get; set; }
public Dictionary<string, string>? EndCaptures { get; set; }
public Dictionary<string, string>? MatchCaptures { get; set; }
```

Usage:

```csharp
[TextmateScope("meta.tag.quickmarkup",
    Begin = "(<)(\\w[\\w.]*)",
    BeginCaptures = { ["1"] = "punctuation.definition.tag.begin",
                      ["2"] = "entity.name.tag" },
    End = "(>)",
    EndCaptures = { ["1"] = "punctuation.definition.tag.end" })]
```

Generated:

```json
{
  "name": "meta.tag.quickmarkup",
  "begin": "(<)(\\w[\\w.]*)",
  "beginCaptures": {
    "1": { "name": "punctuation.definition.tag.begin" },
    "2": { "name": "entity.name.tag" }
  },
  "end": "(>)",
  "endCaptures": {
    "1": { "name": "punctuation.definition.tag.end" }
  }
}
```

Sub-captures in `match` rules are also supported via `MatchCaptures`.

### C. Embedded language injection

**Problem**: Foreign C# sections can't delegate to `source.cs` for highlighting.

**Solution**: Add `EmbeddedLanguage` and `ContentScope` properties:

```csharp
public string? EmbeddedLanguage { get; set; }
public string? ContentScope { get; set; }
```

When `EmbeddedLanguage` is set on a begin/end rule, the generator emits:

```json
{
  "name": "meta.embedded.csharp",
  "begin": "`",
  "end": "`",
  "patterns": [{ "include": "source.cs" }]
}
```

When `ContentScope` is set instead (no language injection, just a scope for the
content between begin/end):

```json
{
  "name": "string.quoted.backtick",
  "begin": "`",
  "end": "`",
  "patterns": [
    { "match": "\\\\.",
      "name": "constant.character.escape" }
  ]
}
```

### D. State-aware `begin`/`end` inference

**Problem**: The lexer's state machine for foreign sections (non-emitting helper
tokens + emitting delimiter tokens) can't be expressed in the current attribute
model. The delimiter tokens (`Foreign` with regexes `` ` `` and `-/`) exist but
have no `[TextmateScope]` because they'd match globally without context.

**Solution**: Allow annotating the delimiter tokens with `BeginFor` / `EndFor`
to link them into a begin/end pair:

```csharp
// On the foreign entry token (non-emitting)
[Regex(@"`", nameof(HandleCuryForeignStart), ShouldReturnToken = false)]
[TextmateScope("string.quoted.backtick",
    Begin = "`", End = "`",
    EmbeddedLanguage = "csharp",
    Priority = (int)TextmateOrder.StringChar)]
ForeignHelperEntry,

// The Foreign token naturally matches the closing delimiter via its regex
```

Alternatively, introduce a dedicated attribute pair:

```csharp
[TextmateBeginScope("string.quoted.backtick", Begin = "`", End = "`",
    EmbeddedLanguage = "csharp")]
[Regex(@"`", nameof(HandleCuryForeignStart), ShouldReturnToken = false)]
```

The generator would check whether `Begin`/`End` regexes match any `[Regex]`
attribute on the field or any other field in the same enum, and optionally merge
them.

### E. De-duplication

**Problem**: Same regex on the same field with different `State` produces
duplicate TextMate rules.

**Solution**: In `GenerateRepository`, when iterating `[Regex]` attributes for a
field, skip duplicates by `InputRegex`. Only emit one TextMate rule per unique
regex per enum field. Optionally controlled by a flag:

```csharp
public bool DeduplicateRegexes { get; set; } = true;
```

### F. Smart `AddBoundary`

**Problem**: `AddBoundary = true` wraps any regex with `\b...\b`, but operators
like `=` are non-word characters where `\b` never matches.

**Solution**: When `AddBoundary` is true, auto-detect whether the regex starts
and ends with `\w`. Only emit `\b` if both sides are word characters:

```csharp
// Auto-skip \b for non-word regex bounds
if (scopeAttr.AddBoundary)
{
    var trimmedRegex = regex.TrimStart('\\').TrimEnd('\\');
    bool startsWithWord = char.IsLetterOrDigit(trimmedRegex[0]);
    bool endsWithWord = char.IsLetterOrDigit(trimmedRegex[^1]);
    if (startsWithWord && endsWithWord)
        rule["match"] = $@"\b{regex}\b";
    else
        rule["match"] = regex;
}
```

### G. User-supplied repository merging

**Problem**: Some patterns (like backtick foreign sections with nested C#
injection) can't be auto-generated from lexer attributes because the lexer doesn't
emit tokens for them.

**Solution**: Add an overload of `GetGrammarJSON` that accepts additional
repository entries:

```csharp
public string GetGrammarJSON<T>(
    StringDict<T> autoRepository,
    StringDict<T>? additionalEntries = null)
```

Or expose `GenerateRepository` to accept add-ons:

```csharp
var repo = TextmateGrammarGenerator.GenerateRepository<QuickMarkupLexer>();
repo["strings"] = new StringDict<List<StringDict<object>>>
{
    ["patterns"] = new List<StringDict<object>>
    {
        new() { ["name"] = "string.quoted.backtick",
                ["begin"] = "`",
                ["end"] = "`",
                ["patterns"] = new[] { new Dictionary<string, object>
                    { ["include"] = "source.cs" } } }
    }
};
```

---

## QuickMarkup-Specific Action Items

### Lexer annotation changes

| Token | Current scope | Proposed change |
|---|---|---|
| `Identifier` (in QMTag state) | `variable.other.other` | Change to `entity.name.tag` when tag context is deducible, or accept limitation |
| `HeaderIdentifier` | `entity.name.type.other` | Remove annotation (dead code) or raise priority above `Identifier` |
| `Foreign` / `ForeignHelperToken` | None | Add `Begin`/`End` with `EmbeddedLanguage = "csharp"` for backtick variant |

### Generator project changes

- `QuickMarkup.GrammarGenerator/Program.cs` — after improvements land, add
  hand-written repository entries for foreign sections that the auto-generator
  can't produce, and merge them into the final grammar.

---

## How to test independently

1. Run the generator: `dotnet run --project QuickMarkup.GrammarGenerator`
2. Open a `.qmui` file in VS Code with the extension loaded
3. Verify syntax highlighting for:
   - Keywords (`var`, `if`, `foreach`, `namespace`, `class`)
   - Tag delimiters (`<`, `>`, `</`, `/>`)
   - Backtick C# expressions: <code>`expression`</code>
   - Foreign sections: `/- ... -/`
   - Comments (`//`, `/* */`)
   - Strings (`"..."`)
   - Numbers (decimal, hex, binary)
   - Operators (`=`, `=>`, `+=`)
4. Open `qmui.tmGrammar.json` and verify no duplicate identical rules remain
5. Verify `HeaderIdentifier` either produces visible `entity.name.type` highlighting
   or is removed

---

## Files referenced

| File | Purpose |
|---|---|
| `Parser/Get.LangSupport/TextmateScopeAttribute.cs` | Scope attribute types |
| `Parser/Get.LangSupport/TextmateGrammarGenerator.cs` | Grammar generation engine |
| `Parser/Get.LangSupport/Get.LangSupport.csproj` | Library project |
| `QuickMarkup.Language/Parser/QuickMarkupLexer.cs` | Lexer with `[Textmate*Scope]` annotations |
| `QuickMarkup.GrammarGenerator/Program.cs` | Generator console app |
| `QuickMarkup.GrammarGenerator/QuickMarkup.GrammarGenerator.csproj` | Generator project |
| `QuickMarkup.VSCode.Extension/syntaxes/qmui.tmGrammar.json` | Generated output |
