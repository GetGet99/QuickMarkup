namespace QuickMarkup.Infra;

public interface IQuickMarkupContextAware
{
    QuickMarkupContext? Context { get; set; }
}
