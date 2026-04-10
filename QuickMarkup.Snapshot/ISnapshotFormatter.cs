namespace QuickMarkup.Snapshot;

public interface ISnapshotFormatter<TNode>
{
    TNode NewKVNode();
    void FreeKVNode(TNode node);
    void AppendKey<T>(TNode keyValueNode, string key, T value);
    T ReadKey<T>(TNode keyValueNode, string key);
}
