namespace QuickMarkup.SourceGen.Test;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <setup>
    Console.WriteLine("Test");
    </setup>
    <root>
        if (true)
            if (false) <Class1 A=1 /> else <Class1 A=2 />
        else
            <Class1 A=3 />
    </root>
    """)]
public partial class Class1
{
    public int A;
    public Class1 Child { get; set; } = null!;
}
