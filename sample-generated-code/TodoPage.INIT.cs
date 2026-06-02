using static QuickMarkup.Infra.QuickRefs;
#nullable enable
namespace MyNamespace;

partial class TodoPage {
    global::System.Collections.Generic.List<global::System.IDisposable> QUICKMARKUP_DISPOSABLES { get; } = [];
    
    public TodoPage() {
        // No raw scripts was provided
        global::Microsoft.UI.Xaml.Controls.StackPanel QUICKMARKUP_NODE_0 = new StackPanel();
        QUICKMARKUP_NODE_0.Spacing = 12;
        QUICKMARKUP_NODE_0.Padding = new global::Microsoft.UI.Xaml.Thickness(24);
        global::QuickMarkup.Infra.UIBlockHost<global::Microsoft.UI.Xaml.UIElement> QUICKMARKUP_NODE_1 = new global::QuickMarkup.Infra.UIBlockHost<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.TargetUICollection<global::Microsoft.UI.Xaml.UIElement>(QUICKMARKUP_NODE_0.Children));
        QUICKMARKUP_NODE_1.AddBlock(new global::QuickMarkup.Infra.StaticBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            (QUICKMARKUP_NODE_2, QUICKMARKUP_NODE_3) => {
                global::Microsoft.UI.Xaml.Controls.TextBlock QUICKMARKUP_NODE_4 = new TextBlock();
                QUICKMARKUP_NODE_4.Text = "QuickMarkup Todo App";
                QUICKMARKUP_NODE_4.FontSize = 28;
                QUICKMARKUP_NODE_4.FontWeight = global::Windows.UI.Text.FontWeight.SemiBold;
                QUICKMARKUP_NODE_2.Add(QUICKMARKUP_NODE_4);
                
            }));
        QUICKMARKUP_NODE_1.AddBlock(new global::QuickMarkup.Infra.StaticBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            (QUICKMARKUP_NODE_5, QUICKMARKUP_NODE_6) => {
                global::Microsoft.UI.Xaml.Controls.TextBox QUICKMARKUP_NODE_7 = new TextBox();
                QUICKMARKUP_NODE_7.PlaceholderText = "Add a todo...";
                QUICKMARKUP_NODE_6.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange<string> (() => {
                    return Input;
                }, QUICKMARUP_TEMPVALUE => {
                    QUICKMARKUP_NODE_7.Text = QUICKMARUP_TEMPVALUE;
                }));
                Input = QUICKMARKUP_NODE_7.Text;
                QUICKMARKUP_NODE_7.RegisterPropertyChangedCallback(
                    global::Microsoft.UI.Xaml.Controls.TextBox.TextProperty,
                    (_, _) => {
                        Input = QUICKMARKUP_NODE_7.Text;
                    }
                );
                QUICKMARKUP_NODE_5.Add(QUICKMARKUP_NODE_7);
                
            }));
        QUICKMARKUP_NODE_1.AddBlock(new global::QuickMarkup.Infra.StaticBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            (QUICKMARKUP_NODE_8, QUICKMARKUP_NODE_9) => {
                global::Microsoft.UI.Xaml.Controls.Button QUICKMARKUP_NODE_10 = new Button();
                QUICKMARKUP_NODE_10.Content = "Add Todo";
                QUICKMARKUP_NODE_10.Click += delegate { 
                                if (!string.IsNullOrWhiteSpace(Input))
                                {
                                    Todos.Add(Input);
                                    Input = "";
                                }
                            ; };
                QUICKMARKUP_NODE_8.Add(QUICKMARKUP_NODE_10);
                
            }));
        QUICKMARKUP_NODE_1.AddBlock(new global::QuickMarkup.Infra.ConditionalBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            () => Todos.Count == 0,
            () => new global::QuickMarkup.Infra.FragmentBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            (QUICKMARKUP_NODE_11, QUICKMARKUP_NODE_12) => {
                QUICKMARKUP_NODE_11.AddBlock(new global::QuickMarkup.Infra.StaticBlock<global::Microsoft.UI.Xaml.UIElement>(
                    new global::QuickMarkup.Infra.ReactiveScope(),
                    (QUICKMARKUP_NODE_13, QUICKMARKUP_NODE_14) => {
                        global::Microsoft.UI.Xaml.Controls.TextBlock QUICKMARKUP_NODE_15 = new TextBlock();
                        QUICKMARKUP_NODE_15.Text = "Nothing here yet.";
                        QUICKMARKUP_NODE_15.Opacity = 0.7;
                        QUICKMARKUP_NODE_13.Add(QUICKMARKUP_NODE_15);
                        
                    }));
                
            }),
            () => new global::QuickMarkup.Infra.FragmentBlock<global::Microsoft.UI.Xaml.UIElement>(
            new global::QuickMarkup.Infra.ReactiveScope(),
            (QUICKMARKUP_NODE_16, QUICKMARKUP_NODE_17) => {
                QUICKMARKUP_NODE_16.AddBlock(global::QuickMarkup.Infra.ForBlock.Create(
                    new global::QuickMarkup.Infra.ReactiveScope(),
                    Todos,
                    (QUICKMARKUP_NODE_19, QUICKMARKUP_NODE_18) => new global::QuickMarkup.Infra.FragmentBlock<global::Microsoft.UI.Xaml.UIElement>(
                    new global::QuickMarkup.Infra.ReactiveScope(),
                    (QUICKMARKUP_NODE_20, QUICKMARKUP_NODE_21) => {
                        QUICKMARKUP_NODE_20.AddBlock(new global::QuickMarkup.Infra.StaticBlock<global::Microsoft.UI.Xaml.UIElement>(
                            new global::QuickMarkup.Infra.ReactiveScope(),
                            (QUICKMARKUP_NODE_22, QUICKMARKUP_NODE_23) => {
                                global::Microsoft.UI.Xaml.Controls.Border QUICKMARKUP_NODE_24 = new Border();
                                QUICKMARKUP_NODE_24.CornerRadius = new global::Microsoft.UI.Xaml.CornerRadius(12);
                                QUICKMARKUP_NODE_24.Padding = new global::Microsoft.UI.Xaml.Thickness(12);
                                global::Microsoft.UI.Xaml.Controls.TextBlock QUICKMARKUP_NODE_25 = new TextBlock();
                                QUICKMARKUP_NODE_23.Add(global::QuickMarkup.Infra.ReferenceTracker.RunAndRerunOnReferenceChange<string> (() => {
                                    return global::QuickMarkup.Infra.CompilerHelpers.ClosureValue(
                                    QUICKMARKUP_NODE_18.Value,
                                    QUICKMARKUP_NODE_19.Value,
                                    (todo, index) => $"{index + 1}. {todo}");
                                }, QUICKMARUP_TEMPVALUE => {
                                    QUICKMARKUP_NODE_25.Text = QUICKMARUP_TEMPVALUE;
                                }));
                                QUICKMARKUP_NODE_24.Child = QUICKMARKUP_NODE_25;
                                QUICKMARKUP_NODE_22.Add(QUICKMARKUP_NODE_24);
                                
                            }));
                        
                    })));
                
            })));
        QUICKMARKUP_DISPOSABLES.Add(new global::QuickMarkup.Infra.DisposableAction(() => QUICKMARKUP_NODE_1.Clear()));
        this.Content = QUICKMARKUP_NODE_0;
        
    }
}