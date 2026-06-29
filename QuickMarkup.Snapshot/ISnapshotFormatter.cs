namespace QuickMarkup.Snapshot;

public interface ISnapshotFormatter<TNode>
{
    TNode NewKVNode();
    void FreeNode(TNode node);
    void AppendKey<T>(TNode keyValueNode, string key, T value);
    T ReadKey<T>(TNode keyValueNode, string key);
    TNode NewList();
    // if node is not a list, return -1
    int ListCount(TNode node);
    void AppendList<T>(TNode node, T value);
    void InsertList<T>(TNode node, int index, T value);
    T ReadList<T>(TNode node, int index);
    void RemoveAtList(TNode node, int index);
}
