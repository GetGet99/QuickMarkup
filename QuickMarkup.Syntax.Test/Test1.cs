using Get.Lexer;
using Get.Parser;
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
        //[TestMethod]
        //public void TestSyntax()
        //{
        //    var output = Parse("""
        //        <props>
        //        int Minimum
        //        int Maximum
        //        </props>
        //        <script>
        //        Console.WriteLine("Hello World");
        //        </script>
        //        <template>
        //            <ABC PropInt=1 PropBool=true PropTrue !PropFalse PropStr="Hello" PropScript=/-1 + 1-/ />
        //        </template>
        //        """);
        //    Assert.AreEqual($"""
        //        {""}
        //        int Minimum
        //        int Maximum
        //        {""}
        //        """, output.Props.RawScript);
        //    Assert.AreEqual($"""
        //        {""}
        //        Console.WriteLine("Hello World");
        //        {""}
        //        """, output.Scirpt.RawScript);
        //    Assert.HasCount(1, output.Template.Children);
        //    var ABC = (QuickMarkupQMNode)output.Template.Children[0];
        //    Assert.HasCount(6, ABC.Properties);
        //    Assert.AreEqual("PropInt", ABC.Properties[0].Key);
        //    Assert.AreEqual(1, ((QuickMarkupQMPropertiesKeyInt32)ABC.Properties[0]).Value);
        //    Assert.AreEqual("PropBool", ABC.Properties[1].Key);
        //    Assert.IsTrue(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[1]).Value);
        //    Assert.AreEqual("PropTrue", ABC.Properties[2].Key);
        //    Assert.IsTrue(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[2]).Value);
        //    Assert.AreEqual("PropFalse", ABC.Properties[3].Key);
        //    Assert.IsFalse(((QuickMarkupQMPropertiesKeyBoolean)ABC.Properties[3]).Value);
        //    Assert.AreEqual("PropStr", ABC.Properties[4].Key);
        //    Assert.AreEqual("Hello", ((QuickMarkupQMPropertiesKeyString)ABC.Properties[4]).Value);
        //    Assert.AreEqual("PropScript", ABC.Properties[5].Key);
        //    Assert.AreEqual("1 + 1", ((QuickMarkupQMPropertyKeyForeign)ABC.Properties[5]).ForeignAsString);
        //}

        [TestMethod]
        public void TestDecimal()
        {
            var output = Lex("<Test Double=0.01 />", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
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
            var output = Lex("foreach (i in ..3) { }", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.Foreach, output[0].TokenType);
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
            var output = Lex("foreach (i in /-(string[])[\"1\"]-/) { }", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.Foreach, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenBracket, output[1].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[2].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.In, output[3].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Foreign, output[4].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseBracket, output[5].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.OpenCuryBracket, output[6].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.CloseCuryBracket, output[7].TokenType);
        }

        [TestMethod]
        public void Lexer_ForeachAdvancedHeaderPunctuation()
        {
            var output = Lex("foreach (index; string? item in `items`; `item.Id`) <A />", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
            CollectionAssert.Contains(output.Select(x => x.TokenType).ToArray(), QuickMarkupLexer.Tokens.Semicolon);
            CollectionAssert.Contains(output.Select(x => x.TokenType).ToArray(), QuickMarkupLexer.Tokens.QuestionMark);
        }

        [TestMethod]
        public void Parse_FragmentChild()
        {
            var sfc = Parse("""
                <root>
                    {
                        <A />
                        <B />
                    }
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var fragment = sfc.Template.Children[0] as QuickMarkupParsedFragmentNode;
            Assert.IsNotNull(fragment);
            Assert.HasCount(2, fragment.Children);
        }

        [TestMethod]
        public void Parse_ValueListIsNotFragment()
        {
            var sfc = Parse("""
                <root>
                    <Grid RowDefinitions=<>
                        <RowDefinition />
                        <RowDefinition />
                    </> />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var grid = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(grid);
            Assert.HasCount(1, grid.InlineMembers);
            var property = grid.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(property);
            Assert.IsInstanceOfType<QuickMarkupValueList>(property.Value);
        }

        [TestMethod]
        public void Parse_IfElseBindsElseToNearestIf()
        {
            var sfc = Parse("""
                <root>
                    if (`a`) <A />
                    if (`b`) <B /> else <C />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(2, sfc.Template.Children);
            var first = sfc.Template.Children[0] as QuickMarkupParsedIfNode;
            var second = sfc.Template.Children[1] as QuickMarkupParsedIfNode;
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.IsNull(first.BodyWhenFalse);
            Assert.IsNotNull(second.BodyWhenFalse);
        }

        [TestMethod]
        public void Parse_ForeachIndexAndKey()
        {
            var sfc = Parse("""
                <root>
                    foreach (index; var item in `items`; `item.Id`) {
                        <A Text=`item.Text` />
                    }
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var foreachNode = sfc.Template.Children[0] as QuickMarkupParsedForNode;
            Assert.IsNotNull(foreachNode);
            Assert.AreEqual("index", foreachNode.IndexVarName);
            Assert.AreEqual("item", foreachNode.VarName);
            Assert.IsInstanceOfType<QuickMarkupForeign>(foreachNode.Iterable);
            Assert.IsInstanceOfType<QuickMarkupForeign>(foreachNode.Key);
            Assert.IsInstanceOfType<QuickMarkupParsedFragmentNode>(foreachNode.Body);
        }

        [TestMethod]
        public void PropsLexer_RefAttributePunctuation()
        {
            var output = Lex("""
                [target: A(1, x = true)]
                int X;
                """, QuickMarkupLexer.LexerStates.Props).ToArray();
            var types = output.Select(t => t.TokenType).ToArray();
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.OpenSquareBracket);
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.CloseSquareBracket);
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.Colon);
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.OpenBracket);
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.CloseBracket);
            CollectionAssert.Contains(types, QuickMarkupLexer.Tokens.Comma);
        }

        [TestMethod]
        public void Parse_Ref_WithCompileTimeAttributes()
        {
            var sfc = Parse("""
                using System;

                [A][B(1), target: C(true, name = "x")]
                int Foo;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Foo", r.Name.Name);
            Assert.HasCount(3, r.Attributes);
            Assert.AreEqual("A", r.Attributes[0].AttributeName.Name);
            Assert.IsNull(r.Attributes[0].TargetSpecifier?.Name);
            Assert.AreEqual("B", r.Attributes[1].AttributeName.Name);
            Assert.AreEqual(1, r.Attributes[1].Arguments.Positionals.Count);
            Assert.AreEqual("C", r.Attributes[2].AttributeName.Name);
            Assert.AreEqual("target", r.Attributes[2].TargetSpecifier?.Name);
            Assert.AreEqual(1, r.Attributes[2].Arguments.Positionals.Count);
            Assert.HasCount(1, r.Attributes[2].Arguments.Named);
            Assert.AreEqual("name", r.Attributes[2].Arguments.Named[0].Name.Name);
        }

        [TestMethod]
        public void Parse_AttachedPropertyKey()
        {
            var sfc = Parse("""
                <root>
                    <Test Grid.Row=1 />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.HasCount(1, tag.InlineMembers);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.AreEqual("Grid.Row", prop.Key);
            Assert.AreEqual(ParsedPropertyOperator.Assign, prop.Operator);
            Assert.IsInstanceOfType<QuickMarkupInt32>(prop.Value);
            Assert.AreEqual(1, ((QuickMarkupInt32)prop.Value!).Value);
        }

        [TestMethod]
        public void Parse_AttachedPropertyWithForeignValue()
        {
            var sfc = Parse("""
                <root>
                    <Test Grid.Row=`expr` />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.AreEqual("Grid.Row", prop.Key);
            Assert.IsInstanceOfType<QuickMarkupForeign>(prop.Value);
        }

        [TestMethod]
        public void Parse_AttachedPropertyBindBack()
        {
            var sfc = Parse("""
                <root>
                    <Test Grid.Row=>`target` />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.AreEqual("Grid.Row", prop.Key);
            Assert.AreEqual(ParsedPropertyOperator.BindBack, prop.Operator);
            Assert.IsInstanceOfType<QuickMarkupForeign>(prop.Value);
        }

        [TestMethod]
        public void Parse_AttachedPropertyChildTag_IntValue()
        {
            var sfc = Parse("""
                <root>
                    <Grid.Row>1</Grid.Row>
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.IsInstanceOfType<QuickMarkupAttachedPropertyTagStart>(tag.TagStart);
            var att = (QuickMarkupAttachedPropertyTagStart)tag.TagStart;
            Assert.AreEqual("Grid", att.TypeName);
            Assert.AreEqual("Row", att.PropertyName);
            Assert.AreEqual(0, tag.InlineMembers.Count);
            Assert.IsNotNull(tag.Children);
            Assert.HasCount(1, tag.Children);
            Assert.IsInstanceOfType<QuickMarkupInt32>(tag.Children[0]);
            Assert.AreEqual(1, ((QuickMarkupInt32)tag.Children[0]).Value);
        }

        [TestMethod]
        public void Parse_AttachedPropertyChildTag_SelfClosing()
        {
            var sfc = Parse("""
                <root>
                    <Grid.Row />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.IsInstanceOfType<QuickMarkupAttachedPropertyTagStart>(tag.TagStart);
            Assert.IsTrue(tag.IsSelfClosing);
        }

        [TestMethod]
        public void Parse_AttachedPropertyChildTag_ForeignExpression()
        {
            var sfc = Parse("""
                <root>
                    <Grid.Row>`expr`</Grid.Row>
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.IsInstanceOfType<QuickMarkupAttachedPropertyTagStart>(tag.TagStart);
            Assert.IsNotNull(tag.Children);
            Assert.HasCount(1, tag.Children);
            Assert.IsInstanceOfType<QuickMarkupForeign>(tag.Children[0]);
        }

        [TestMethod]
        public void Parse_AttachedPropertyChildTag_NestedChildTag()
        {
            var sfc = Parse("""
                <root>
                    <Grid.Row>
                        <Test />
                    </Grid.Row>
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            Assert.HasCount(1, sfc.Template.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.IsInstanceOfType<QuickMarkupAttachedPropertyTagStart>(tag.TagStart);
            Assert.IsNotNull(tag.Children);
            Assert.HasCount(1, tag.Children);
            Assert.IsInstanceOfType<QuickMarkupParsedTag>(tag.Children[0]);
        }

        [TestMethod]
        public void Parse_Ref_StaticKeyword()
        {
            var sfc = Parse("""
                using System;

                static int X = 0;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("X", r.Name.Name);
            Assert.IsTrue(r.IsStatic);
            Assert.AreEqual(Accessibility.Default, r.Accessibility);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_PrivateStaticKeyword()
        {
            var sfc = Parse("""
                using System;

                private static int Y = 1;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Y", r.Name.Name);
            Assert.IsTrue(r.IsStatic);
            Assert.AreEqual(Accessibility.Private, r.Accessibility);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_StaticComputed()
        {
            var sfc = Parse("""
                using System;

                static int Z => `42`;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Z", r.Name.Name);
            Assert.IsTrue(r.IsStatic);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Computed, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_PublicKeyword()
        {
            var sfc = Parse("""
                using System;

                public int X = 0;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("X", r.Name.Name);
            Assert.AreEqual(Accessibility.Public, r.Accessibility);
            Assert.IsFalse(r.IsStatic);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_PublicStaticKeyword()
        {
            var sfc = Parse("""
                using System;

                public static int Y = 1;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Y", r.Name.Name);
            Assert.AreEqual(Accessibility.Public, r.Accessibility);
            Assert.IsTrue(r.IsStatic);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_PublicComputed()
        {
            var sfc = Parse("""
                using System;

                public int Z => `42`;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Z", r.Name.Name);
            Assert.AreEqual(Accessibility.Public, r.Accessibility);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Computed, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_RequiredKeyword()
        {
            var sfc = Parse("""
                using System;

                required int X = 0;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("X", r.Name.Name);
            Assert.IsTrue(r.IsRequired);
            Assert.AreEqual(Accessibility.Default, r.Accessibility);
            Assert.IsFalse(r.IsStatic);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_Ref_PublicRequiredKeyword()
        {
            var sfc = Parse("""
                using System;

                public required int Y = 1;
                """);
            Assert.HasCount(1, sfc.Refs);
            var r = sfc.Refs[0];
            Assert.AreEqual("Y", r.Name.Name);
            Assert.IsTrue(r.IsRequired);
            Assert.AreEqual(Accessibility.Public, r.Accessibility);
            Assert.IsFalse(r.IsStatic);
            Assert.IsNotNull(r.DefaultValue);
            Assert.AreEqual(DefaultValueKind.Assignment, r.DefaultValue.Kind);
        }

        [TestMethod]
        public void Parse_EmptyContent_ReturnsNullTemplate()
        {
            var sfc = Parse("");
            Assert.IsNull(sfc.Namespace);
            Assert.IsNull(sfc.ClassDeclaration);
        }

        [TestMethod]
        public void Parse_WhitespaceOnly_ReturnsNullTemplate()
        {
            var sfc = Parse("   \n  \t  ");
            Assert.IsNull(sfc.Template);
        }

        [TestMethod]
        [Ignore("Lexer does not currently support Unicode identifiers")]
        public void Lex_UnicodeIdentifier_TokenizesCorrectly()
        {
            var output = Lex("<按钮 Text=\"Hello\" />", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.QMOpenTagOpen, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[1].TokenType);
        }

        [TestMethod]
        public void Lex_MultipleIdentifiers_TokenizesCorrectly()
        {
            var output = Lex("<Test A B />", QuickMarkupLexer.LexerStates.BeforeRoot).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.QMOpenTagOpen, output[0].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[1].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[2].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.Identifier, output[3].TokenType);
            Assert.AreEqual(QuickMarkupLexer.Tokens.QMOpenTagCloseAuto, output[4].TokenType);
        }

        [TestMethod]
        public void Parse_ForeignExpressionAsPropertyValue()
        {
            var sfc = Parse("""
                <root>
                    <Test Text=`"Hello " + name` />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.HasCount(1, tag.InlineMembers);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.AreEqual("Text", prop.Key);
            Assert.IsInstanceOfType<QuickMarkupForeign>(prop.Value);
        }

        [TestMethod]
        public void Parse_InterpolatedStringPropertyValue()
        {
            var sfc = Parse("""
                <root>
                    <Test Text=`$"Hello {name}"` />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.IsInstanceOfType<QuickMarkupForeign>(prop.Value);
        }

        [TestMethod]
        public void Parse_NestedTags_DeepHierarchy()
        {
            var sfc = Parse("""
                <root>
                    <A>
                        <B>
                            <C>
                                <D />
                            </C>
                        </B>
                    </A>
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var a = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(a);
            var b = a.Children![0] as QuickMarkupParsedTag;
            Assert.IsNotNull(b);
            var c = b.Children![0] as QuickMarkupParsedTag;
            Assert.IsNotNull(c);
            var d = c.Children![0] as QuickMarkupParsedTag;
            Assert.IsNotNull(d);
        }

        [TestMethod]
        public void Parse_SelfClosingTag_HasNoChildren()
        {
            var sfc = Parse("""
                <root>
                    <Test />
                </root>
                """);

            var tag = sfc.Template!.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.IsTrue(tag.IsSelfClosing);
        }

        [TestMethod]
        public void Parse_MultiplePropertyValuesOnSameTag()
        {
            var sfc = Parse("""
                <root>
                    <Test A="1" B="2" C="3" />
                </root>
                """);

            var tag = sfc.Template!.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            Assert.HasCount(3, tag.InlineMembers);
        }

        [TestMethod]
        public void Parse_ForeignExpressionWithMethodCall()
        {
            var sfc = Parse("""
                <root>
                    <Test Text=`GetGreeting(name)` />
                </root>
                """);

            Assert.IsNotNull(sfc.Template?.Children);
            var tag = sfc.Template.Children[0] as QuickMarkupParsedTag;
            Assert.IsNotNull(tag);
            var prop = tag.InlineMembers[0] as QuickMarkupParsedProperty;
            Assert.IsNotNull(prop);
            Assert.IsInstanceOfType<QuickMarkupForeign>(prop.Value);
        }

        [TestMethod]
        [Ignore("Current parser error recovery throws before returning handled errors.")]
        public void Parse_Ref_NamedBeforePositional_YieldsErrors()
        {
            _ = new QuickMarkupParser().Parse(
                Lex("""
                    using System;

                    [A(b = 1, 2)]
                    int X;
                    """),
                out var errors);
            Assert.IsNotEmpty(errors, "named-then-positional in attribute args should not parse cleanly");
        }

        [TestMethod]
        public void Usings()
        {
            var output = Lex("""
                using CommunityToolkit.WinUI.Controls;
                using SymbolExIcon = Get.Symbols.SymbolExIcon;
                """, QuickMarkupLexer.LexerStates.Usings).ToArray();
            Assert.AreEqual(QuickMarkupLexer.Tokens.UsingStatement, output[0].TokenType);
            Assert.AreEqual("using CommunityToolkit.WinUI.Controls;", ((IToken<QuickMarkupLexer.Tokens, string>)output[0]).Data);
            Assert.AreEqual(QuickMarkupLexer.Tokens.UsingStatement, output[1].TokenType);
            Assert.AreEqual("using SymbolExIcon = Get.Symbols.SymbolExIcon;", ((IToken<QuickMarkupLexer.Tokens, string>)output[1]).Data);
        }

        IEnumerable<IToken<QuickMarkupLexer.Tokens>> Lex(string code, QuickMarkupLexer.LexerStates initState = QuickMarkupLexer.LexerStates.Usings)
        {
            return new QuickMarkupLexer(new StreamSeeker(new MemoryStream(Encoding.UTF8.GetBytes(code))), initState).GetTokens();
        }
        QuickMarkupSFC Parse(IEnumerable<IToken<QuickMarkupLexer.Tokens>> tokens)
        {
            return new QuickMarkupParser().Parse(tokens, out _);
        }
        QuickMarkupSFC Parse(string code)
        {
            return Parse(Lex(code));
        }
    }
}
