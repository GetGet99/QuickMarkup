namespace QuickMarkup.SourceGen.Test;

[QuickMarkup("""
    using QuickMarkup.SourceGen.Test;
    <script>
    Console.WriteLine("Test");
    </script>
    <template>
        <Class1 A=/-A + 1-/ />
    </template>
    """)]
public partial class Class1
{
    public int A;
    public Class1 Child { get; set; } = null!;
}