using Get.Lexer;
using Get.Parser;
using Get.PLShared;
using QuickMarkup.AST;
using QuickMarkup.Parser;

namespace QuickMarkup.CodeAnalysis.Helpers;


partial class QuickMarkupProviderExtension
{
    internal static IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
    {
        // retry as it is flaky
        QuickMarkupLexer? lexer = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                lexer = new QuickMarkupLexer(new StringTextSeeker(code));
                break;
            } catch
            {

            }
        }
        lexer ??= new QuickMarkupLexer(new StringTextSeeker(code));
        return lexer.GetTokens();
    }
    internal static ThreadLocal<QuickMarkupParser> ParserPerThread { get; } = new(static () =>
    {
        // retry as it is flaky
        for (int i = 0; i < 10; i++)
        {
            try
            {
                return new QuickMarkupParser();
            }
            catch
            {

            }
        }
        return new QuickMarkupParser();
    });
    static QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
    {
        return ParserPerThread.Value!.Parse(tokens, out _);
    }
    internal static QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }

    /// <summary>
    /// Parses QuickMarkup code and returns both the result and any parse errors.
    /// Used by the Language Server for diagnostic conversion.
    /// </summary>
    internal static (QuickMarkupSFC? sfc, List<ErrorTerminalValue> errors) ParseWithErrors(string code)
    {
        try
        {
            return ParseWithErrorsCore(code);
        }
        catch
        {
            return (null, []);
        }
    }

    /// <summary>
    /// Parses QuickMarkup code and returns errors without catching parse exceptions.
    /// Used by the analyzer which handles parse exceptions separately via <c>TryHandleParseException</c>.
    /// </summary>
    internal static (QuickMarkupSFC Sfc, List<ErrorTerminalValue> Errors) ParseWithErrorsCore(string code)
    {
        var tokens = Lex(code);
        var sfc = ParserPerThread.Value!.Parse(tokens, out var errors);
        return (sfc, errors);
    }
}