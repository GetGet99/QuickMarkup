using Get.Lexer;
using Get.PLShared;
using QuickMarkup.AST;
using QuickMarkup.Parser;

namespace QuickMarkup.CodeAnalysis.Helpers;


partial class QuickMarkupProviderExtension
{
    static IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
    {
        // retry as it is flaky
        QuickMarkupLexer? lexer = null;
        for (int i = 0; i < 10; i++)
        {
            lexer = new QuickMarkupLexer(new StringTextSeeker(code));
            break;
        }
        lexer ??= new QuickMarkupLexer(new StringTextSeeker(code));
        return lexer.GetTokens();
    }
    static ThreadLocal<QuickMarkupParser> ParserPerThread { get; } = new(static () =>
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
        return ParserPerThread.Value.Parse(tokens, out _);
    }
    static QuickMarkupSFC Parse(string code)
    {
        return Parse(Lex(code));
    }
}