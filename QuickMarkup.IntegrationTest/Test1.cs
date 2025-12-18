using Get.Lexer;
using Get.PLShared;
using QuickMarkup.AST;
using QuickMarkup.CodeGen;
using QuickMarkup.Parser;
using System.Text;

namespace QuickMarkup.IntegrationTest
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestOneLevel()
        {
            var result = ParseAndGen("""
                <script>
                Console.WriteLine("Hello World");
                </script>
                <template>
                    <ABC PropInt=1 PropBool=true PropTrue !PropFalse PropStr="Hello" PropScript=/-1 + 1-/ />
                </template>
                """);
            Assert.AreEqual("""
                var QUICKMARKUP_EFFECTS = new List<IReference>();

                Console.WriteLine("Hello World");

                var QUICKMARKUP_NODE_0 = new ABC();
                QUICKMARKUP_NODE_0.PropInt = 1;
                QUICKMARKUP_NODE_0.PropBool = true;
                QUICKMARKUP_NODE_0.PropTrue = true;
                QUICKMARKUP_NODE_0.PropFalse = false;
                QUICKMARKUP_NODE_0.PropStr = "Hello";
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return 1 + 1;
                }, x => {
                    QUICKMARKUP_NODE_0.PropScript = x;
                }));

                return QUICKMARKUP_NODE_0;
                """, result);
        }
        [TestMethod]
        public void TestBasicMultiLevel()
        {
            var result = ParseAndGen("""
                <script>
                var clickTimes = new Reference<int>(0);
                </script>
                <template>
                    <StackPanel Orientation=/-Orientation.Vertical-/ >
                        <TextBox Text="Hello World!" />
                        <Button Content="Click Me!" Click=/-() => clickTimes.Value++-/ />
                        <TextBox Text=/-$"You've clicked {clickTimes.Value} time(s)."-/ />
                    </StackPanel>
                </template>
                """);
            Assert.AreEqual("""
                var QUICKMARKUP_EFFECTS = new List<IReference>();

                var clickTimes = new Reference<int>(0);

                var QUICKMARKUP_NODE_0 = new StackPanel();
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return Orientation.Vertical;
                }, x => {
                    QUICKMARKUP_NODE_0.Orientation = x;
                }));
                var QUICKMARKUP_NODE_1 = new TextBox();
                QUICKMARKUP_NODE_1.Text = "Hello World!";

                QUICKMARKUP_NODE_0.Children.Add(QUICKMARKUP_NODE_1);
                var QUICKMARKUP_NODE_2 = new Button();
                QUICKMARKUP_NODE_2.Content = "Click Me!";
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return () => clickTimes.Value++;
                }, x => {
                    QUICKMARKUP_NODE_2.Click = x;
                }));

                QUICKMARKUP_NODE_0.Children.Add(QUICKMARKUP_NODE_2);
                var QUICKMARKUP_NODE_3 = new TextBox();
                QUICKMARKUP_EFFECTS.Add(ReferenceTracker.RunAndRerunOnReferenceChange(() => {
                    return $"You've clicked {clickTimes.Value} time(s).";
                }, x => {
                    QUICKMARKUP_NODE_3.Text = x;
                }));

                QUICKMARKUP_NODE_0.Children.Add(QUICKMARKUP_NODE_3);

                return QUICKMARKUP_NODE_0;
                """, result);
        }
        [TestMethod]
        public void TestUsings()
        {
            var result = ParseAndGenAppendUsings("""
                using Microsoft.UI.Xaml.Controls;
                <script>
                Console.WriteLine("Hello World");
                </script>
                <template>
                    <TextBlock Text="Hello World" />
                </template>
                """);
            Assert.AreEqual("""
                using Microsoft.UI.Xaml.Controls;

                var QUICKMARKUP_EFFECTS = new List<IReference>();

                Console.WriteLine("Hello World");

                var QUICKMARKUP_NODE_0 = new TextBlock();
                QUICKMARKUP_NODE_0.Text = "Hello World";
                
                return QUICKMARKUP_NODE_0;
                """, result);
        }

        string ParseAndGen(string input)
        {
            return QuickMarkupCodeGen.GenInnerFromSFC(Parse(input));
        }

        string ParseAndGenAppendUsings(string input)
        {
            var parsed = Parse(input);
            return $"""
                {parsed.Usings.RawScript}
                {QuickMarkupCodeGen.GenInnerFromSFC(parsed)}
                """;
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
