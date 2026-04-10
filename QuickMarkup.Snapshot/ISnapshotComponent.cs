using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuickMarkup.Snapshot;

public interface ISnapshotComponent<TFormatter, TNode> : ISnapshotComponent where TFormatter : ISnapshotFormatter<TNode>
{
    TNode SaveSnapshot(TFormatter formatter);
    void LoadSnapshot(TFormatter formatter, TNode saved);
}

public interface ISnapshotComponent;