namespace QuickMarkup.SourceGen.Test;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <setup>
    Console.WriteLine("Test");
    </setup>
    <root>
        <Class1 A=/-A + 1-/ />
        <Class1   />
    </root>
    """)]
public partial class Class1
{
    public int A;
    public Class1 Child { get; set; } = null!;
}
