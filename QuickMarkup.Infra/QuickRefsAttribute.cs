namespace QuickMarkup.SourceGen;


#pragma warning disable CS9113 // Parameter is unread.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class QuickRefsAttribute(string markup) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.
