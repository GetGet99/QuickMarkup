//using System.Collections.ObjectModel;
//using System.markup.Linq;

//namespace QuickMarkup.Infra;

//class StructuralChildren<T>(IUICollection<T> uiChildren)
//{
//    public readonly List<StructNode> Nodes = [];

//    int GetUIIndexBefore(AnchorNode anchor)
//    {
//        int index = 0;
//        foreach (var node in Nodes)
//        {
//            if (ReferenceEquals(node, anchor))
//                return index;

//            if (node is ChildNode<T>)
//                index++;
//        }
//        throw new InvalidOperationException("Anchor not found");
//    }
//    public void MoveChild(
//        ChildNode<T> child,
//        AnchorNode beforeAnchor
//    )
//    {
//        int oldUI = uiChildren.IndexOf(child.Child);
//        int newUI = GetUIIndexBefore(beforeAnchor);

//        if (oldUI != newUI)
//        {
//            uiChildren.Move((uint)oldUI, (uint)newUI);
//        }

//        Nodes.Remove(child);
//        int structIndex = Nodes.IndexOf(beforeAnchor);
//        Nodes.Insert(structIndex, child);
//    }
//    public void Remove(ChildNode<T> child)
//    {
//        int uiIndex = uiChildren.IndexOf(child.Child);
//        uiChildren.RemoveAt(uiIndex);
//        Nodes.Remove(child);
//    }
//    int id = 0;
//    int GetNewId() => id++;
//    public AnchorNode CreateAnchor() => new(GetNewId());
//    public ChildRange<T> CreateRange() => new(this);
//}
//class ChildRange<T>
//{
//    public AnchorNode Start;
//    public AnchorNode End;
//    public StructuralChildren<T> Owner;

//    public ChildRange(StructuralChildren<T> owner)
//    {
//        Owner = owner;
//        Start = owner.CreateAnchor();
//        End = owner.CreateAnchor();

//        var nodes = owner.Nodes;
//        nodes.Add(Start);
//        nodes.Add(End);
//    }

//    void InsertBeforeEnd(ChildNode<T> child)
//    {
//        var nodes = Owner.Nodes;

//        int structIndex = nodes.IndexOf(End);
//        nodes.Insert(structIndex, child);

//        int uiIndex = Owner.GetUIIndexBefore(End);
//        Owner.UIChildren.Insert(uiIndex, child.Child);
//    }
//    public void Remove(ChildNode<T> child) => Owner.Remove(child);
//}


//abstract record class StructNode;

//sealed record class AnchorNode(int Id) : StructNode;

//sealed record class ChildNode<T>(T Child, object? Key = null) : StructNode;

//class AdvancedNodes
//{
//    public void Foreach<T>(ObservableCollection<T> collection, ChildRange<T> range)
//    {

//    }
//    class ChildrenTracker<TCurrent, TKey, TList, TChild> where TKey : notnull
//    {
//        public readonly Dictionary<TKey, TChild> Children = [];
//        public List<TKey> Order = [];
//    }
//}