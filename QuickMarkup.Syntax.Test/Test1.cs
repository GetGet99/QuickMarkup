using Get.Lexer;
using Get.PLShared;
using Get.RegexMachine;
using Mono.Cecil.Cil;
using QuickMarkup.AST;
using QuickMarkup.Parser;
using System.Text;

namespace QuickMarkup.Syntax.Test
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestSyntax()
        {
            var output = Parse("""
                <props>
                int Minimum
                int Maximum
                </props>
                <script>
                Console.WriteLine("Hello World");
                </script>
                <template>
                    <ABC PropInt=1 PropBool=true PropTrue !PropFalse PropStr="Hello" PropScript=/-1 + 1-/ />
                </template>
                """);
            Assert.AreEqual($"""
                {""}
                int Minimum
                int Maximum
                {""}
                """, output.Props.RawScript);
            Assert.AreEqual($"""
                {""}
                Console.WriteLine("Hello World");
                {""}
                """, output.Scirpt.RawScript);
            Assert.HasCount(1, output.Template.Children);
            var ABC = (QuickMarkupQMNode)output.Template.Children[0];
            Assert.HasCount(6, ABC.Properties);
            Assert.AreEqual("PropInt", ABC.Properties[0].Key);
            Assert.AreEqual(1, ((QuickMarkupQMPropertiesKeyInt32)ABC.Properties[0]).Value);
            Assert.AreEqual("PropBool", ABC.Properties[1].Key);
            Assert.IsTrue(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[1]).Value);
            Assert.AreEqual("PropTrue", ABC.Properties[2].Key);
            Assert.IsTrue(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[2]).Value);
            Assert.AreEqual("PropFalse", ABC.Properties[3].Key);
            Assert.IsFalse(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[3]).Value);
            Assert.AreEqual("PropStr", ABC.Properties[4].Key);
            Assert.AreEqual("Hello", ((QuickMarkupQMPropertiesKeyString)ABC.Properties[4]).Value);
            Assert.AreEqual("PropScript", ABC.Properties[5].Key);
            Assert.AreEqual("1 + 1", ((QuickMarkupQMPropertyKeyForeign)ABC.Properties[5]).ForeignAsString);
        }

        [TestMethod]
        public void TestDecimal()
        {
            var output = Lex("<Test Double=0.01 />", QuickMarkupLexer.LexerStates.Default).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.QMOpenTagOpen, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[1].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[2].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Equal, output[3].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Double, output[4].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.QMOpenTagCloseAuto, output[5].TokenType);
        }

        [TestMethod]
        public void ForLoopRange()
        {
            var output = Lex("for (i in ..3) { }", QuickMarkupLexer.LexerStates.Default).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.For, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenBracket, output[1].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[2].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.In, output[3].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Range, output[4].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Integer, output[5].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseBracket, output[6].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenCuryBracket, output[7].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseCuryBracket, output[8].TokenType);
        }

        [TestMethod]
        public void ForLoopForeign()
        {
            var output = Lex("for (i in /-(string[])[\"1\"]-/) { }", QuickMarkupLexer.LexerStates.Default).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.For, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenBracket, output[1].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[2].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.In, output[3].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Foreign, output[4].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseBracket, output[5].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenCuryBracket, output[6].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseCuryBracket, output[7].TokenType);
        }

        IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code, QuickMarkupLexer.LexerStates initState = QuickMarkupLexer.LexerStates.Start)
        {
            return new QuickMarkupLexer(new StreamSeeker(new MemoryStream(Encoding.UTF8.GetBytes(code))), initState).GetTokens();
        }
        QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
        {
            return new QuickMarkupParser().Parse(tokens);
        }
        QuickMarkupSFC Parse(string code)
        {
            return Parse(Lex(code));
        }
    }
}
