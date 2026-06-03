using Get.LangSupport;
using QuickMarkup.Parser;

var generatorDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
var repoRoot = Path.GetFullPath(Path.Combine(generatorDir, "..", "..", "..", ".."));
var outputPath = args.Length > 0 ? args[0]
    : Path.Combine(repoRoot, "QuickMarkup.VSCode.Extension", "syntaxes", "qmui.tmGrammar.json");

outputPath = Path.GetFullPath(outputPath);

var metadata = new TextmateGrammarMetadata
{
    LanguageId = "quickmarkup",
    LanguageExtensions = [".qmui"]
};

var repository = TextmateGrammarGenerator.GenerateRepository<QuickMarkupLexer>();
var grammar = metadata.GetGrammarJSON(
    repository,
    additionalEntries: null,
    repositoryIncludeOrder: ["comments", "strings", "main"]);

File.WriteAllText(outputPath, grammar);
Console.WriteLine($"Grammar written to: {outputPath}");
