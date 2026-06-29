using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace QuickMarkup.Snapshot;

public class JsonNodeSnapshotFormatter : ISnapshotFormatter<JsonNode>
{
    JsonNode Serialize<T>(T node)
    {
        if (node is JsonNode n)
        {
            return n;
        }
        return JsonNode.Parse(JsonSerializer.Serialize(node))!;
    }
    T Deserialize<T>(JsonNode node)
    {
        if (typeof(T) == typeof(JsonNode))
        {
            return (T)(object)node;
        }
        return JsonSerializer.Deserialize<T>(node)!;
    }
    public void AppendKey<T>(JsonNode keyValueNode, string key, T value)
    {
        if (keyValueNode is not JsonObject jObj)
            throw new InvalidCastException();
        jObj[key] = Serialize(value);
    }

    public void AppendList<T>(JsonNode node, T value)
    {
        if (node is not JsonArray array)
            throw new InvalidCastException();
        array.Add(value);
    }

    public void FreeNode(JsonNode node)
    {
        // garbage collection
    }

    public void InsertList<T>(JsonNode node, int index, T value)
    {
        throw new NotImplementedException();
    }

    public int ListCount(JsonNode node)
    {
        if (node is not JsonArray array)
            return -1;
        return array.Count;
    }

    public JsonNode NewKVNode() => new JsonObject();

    public JsonNode NewList() => new JsonArray();

    public T ReadKey<T>(JsonNode keyValueNode, string key)
        => Deserialize<T>(keyValueNode[key]!);

    public T ReadList<T>(JsonNode node, int index)
    {
        if (node is not JsonArray array)
            throw new InvalidCastException();
        return Deserialize<T>(array[index]!);
    }

    public void RemoveAtList(JsonNode node, int index)
    {
        if (node is not JsonArray array)
            throw new InvalidCastException();
        array.RemoveAt(index);
    }
}
