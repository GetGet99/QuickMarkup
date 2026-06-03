using Get.Parser;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using QuickMarkup.AST;
using QuickMarkup.CodeAnalysis.Binders;
using QuickMarkup.Language.Symbols;
using AstNode = QuickMarkup.AST.AST;

namespace QuickMarkup.LanguageServer.Diagnostics.Test;

[TestClass]
public sealed class LspDiagnosticConverterTests
{
    [TestMethod]
    public void ConvertParseError_UnexpectedInput_ReturnsErrorDiagnostic()
    {
        var errors = new List<ErrorTerminalValue>
        {
            new(new LRParserRuntimeUnexpectedInputException(new TestSyntaxElement("<")))
            {
                Start = new(0, 0),
                End = new(0, 1)
            }
        };

        var result = LspDiagnosticConverter.ConvertParseErrors(errors, "<Button");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1001", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertParseError_UnexpectedEnding_ReturnsErrorDiagnostic()
    {
        var errors = new List<ErrorTerminalValue>
        {
            new(new LRParserRuntimeUnexpectedEndingException([]))
            {
                Start = new(1, 0),
                End = new(1, 5)
            }
        };

        var result = LspDiagnosticConverter.ConvertParseErrors(errors, "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1002", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_PropertyUnknown_ReturnsWarning()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderPropertyUnknownError(node, "Button", "Color", ["Foreground"])
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Warning, result[0].Severity!.Value);
        Assert.AreEqual("QM1006", (string)result[0].Code!);
        Assert.Contains("Color", result[0].Message);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_TypeUnknown_ReturnsError()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderTypeUnknownError(node, "NonExistentType")
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1008", (string)result[0].Code!);
        Assert.Contains("NonExistentType", result[0].Message);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_EnumMemberUnknown_ReturnsWarning()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderEnumMemberUnknownError(node, "Visibility", "Collapsed", ["Visible"])
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Warning, result[0].Severity!.Value);
        Assert.AreEqual("QM1007", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_ChildrenTooMany_ReturnsError()
    {
        var node = new TestAst();
        var tagInfo = new QMBinderTagInfo(null, "StackPanel", null, null, ChildrenModes.None);
        var diags = new List<QMDiagnostic>
        {
            new QMBinderChildrenTooMany(node, tagInfo)
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1004", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_TagMismatched_ReturnsError()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderTagMismatchedError(node, "Button", "StackPanel")
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1009", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_TagUnexpected_ReturnsError()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderTagUnexpectedError(node, "Button", "StackPanel")
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1010", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_TypeMismatch_ReturnsError()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderTypeMismatchError(node, "int", "string")
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1011", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertBinderDiagnostic_GeneralFallback_ReturnsError()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderError(node, "Something went wrong")
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, [], "");

        Assert.HasCount(1, result);
        Assert.AreEqual(DiagnosticSeverity.Error, result[0].Severity!.Value);
        Assert.AreEqual("QM1003", (string)result[0].Code!);
    }

    [TestMethod]
    public void ConvertAll_MixedDiagnostics_ReturnsAll()
    {
        var node = new TestAst();
        var diags = new List<QMDiagnostic>
        {
            new QMBinderError(node, "Error 1"),
            new QMBinderWarning(node, "Warning 1")
        };

        var parseErrors = new List<ErrorTerminalValue>
        {
            new(new LRParserRuntimeUnexpectedInputException(new TestSyntaxElement("x")))
            {
                Start = new(0, 0),
                End = new(0, 1)
            }
        };

        var result = LspDiagnosticConverter.ConvertAll(diags, parseErrors, "");

        Assert.HasCount(3, result);
    }

    record TestAst : AstNode;
}

class TestSyntaxElement(string text) : Get.Parser.ISyntaxElementValue
{
    public Get.PLShared.Position Start { get; set; }
    public Get.PLShared.Position End { get; set; }
    public Get.Parser.ISyntaxElement WithoutValue => throw new NotImplementedException();
    public override string ToString() => text;
}
