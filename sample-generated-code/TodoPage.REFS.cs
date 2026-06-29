using static QuickMarkup.Infra.QuickRefs;
#nullable enable
namespace MyNamespace;

partial class TodoPage {
    public global::QuickMarkup.Infra.Reference<string> InputProp => field ??= new global::QuickMarkup.Infra.Reference<string>("", "global::PocketPic.TodoPage.Input");
    public string Input {
        get {
            return this.InputProp.Value;
        }
        set {
            this.InputProp.Value = value;
        }
    }
    
}