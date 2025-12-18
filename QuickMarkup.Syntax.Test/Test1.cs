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
            var ABC = (QuickMarkupXMLNode)output.Template.Children[0];
            Assert.HasCount(6, ABC.Properties);
            Assert.AreEqual("PropInt", ABC.Properties[0].Key);
            Assert.AreEqual(1, ((QuickMarkupXMLPropertiesKeyInt32)ABC.Properties[0]).Value);
            Assert.AreEqual("PropBool", ABC.Properties[1].Key);
            Assert.IsTrue(((QuickMarkupXMLPropertiesKeyBoolean)ABC.Properties[1]).Value);
            Assert.AreEqual("PropTrue", ABC.Properties[2].Key);
            Assert.IsTrue(((QuickMarkupXMLPropertiesKeyBoolean)ABC.Properties[2]).Value);
            Assert.AreEqual("PropFalse", ABC.Properties[3].Key);
            Assert.IsFalse(((QuickMarkupXMLPropertiesKeyBoolean)ABC.Properties[3]).Value);
            Assert.AreEqual("PropStr", ABC.Properties[4].Key);
            Assert.AreEqual("Hello", ((QuickMarkupXMLPropertiesKeyString)ABC.Properties[4]).Value);
            Assert.AreEqual("PropScript", ABC.Properties[5].Key);
            Assert.AreEqual("1 + 1", ((QuickMarkupXMLPropertiesKeyForeign)ABC.Properties[5]).ForeignAsString);
        }

        IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code)
        {
            return new QuickMarkupLexer(new StreamSeeker(new MemoryStream(Encoding.UTF8.GetBytes(code)))).GetTokens();
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
