namespace QuickMarkup.WinUI;

/// <summary>
/// Creates <c>DataTemplate</c>s used by the source generator for QuickMarkup template values.
/// </summary>
/// <remarks>
/// On Uno targets the platform <c>DataTemplate(Func&lt;object&gt;)</c> constructor is used.
/// On WASDK and UWP that constructor is not public, so a template whose root is created by
/// <c>XamlReader</c> is used instead, with a delegator attached property running the postprocess
/// action for each materialized root.
/// </remarks>
public static class DataTemplateFactory
{
#if HAS_UNO
    public static DataTemplate CreateDataTemplate<T>(Action<T> postprocess) where T : FrameworkElement, new()
    {
        return new DataTemplate(() =>
        {
            var root = new T();
            postprocess(root);
            return root;
        });
    }
#else
    public static DataTemplate CreateDataTemplate<T>(Action<T> postprocess) where T : FrameworkElement
    {
        var id = DataTemplateDelegator.CreateId(element => postprocess((T)element));
        return (DataTemplate)LoadTemplate<T>(id);
    }

    static object LoadTemplate<T>(string id)
    {
        string xaml =
            $"""
            <DataTemplate
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:root="using:{typeof(T).Namespace}"
                xmlns:delegators="using:{typeof(DataTemplateDelegator).Namespace}">
                <root:{typeof(T).Name} delegators:{nameof(DataTemplateDelegator)}.Id="{id}" />
            </DataTemplate>
            """;
#if UWP
        return Windows.UI.Xaml.Markup.XamlReader.Load(xaml);
#else
        return Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
#endif
    }
#endif
}

/// <summary>
/// Invokes the postprocess action for a template root that was created by <c>XamlReader</c>.
/// </summary>
public static class DataTemplateDelegator
{
    static readonly Dictionary<string, Action<object>> Delegates = [];
    static int nextId;

    internal static string CreateId(Action<object> postprocess)
    {
        string id = "qm" + Interlocked.Increment(ref nextId);
        Delegates[id] = postprocess;
        return id;
    }

    public static readonly DependencyProperty IdProperty =
        DependencyProperty.RegisterAttached("Id", typeof(string), typeof(DataTemplateDelegator), new PropertyMetadata(null));

    public static string GetId(DependencyObject target) => (string)target.GetValue(IdProperty);

    public static void SetId(DependencyObject target, string id)
    {
        target.SetValue(IdProperty, id);
        if (!string.IsNullOrEmpty(id) && Delegates.TryGetValue(id, out var postprocess))
            postprocess(target);
    }
}
