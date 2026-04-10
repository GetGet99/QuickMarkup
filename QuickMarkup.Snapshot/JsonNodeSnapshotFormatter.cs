using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuickMarkup.Snapshot;

// sample impl
class JsonNodeSnapshotFormatter : ISnapshotFormatter<JsonNode>
{
    public void AppendKey<T>(JsonNode keyValueNode, string key, T value)
    {
        if (keyValueNode is not JsonObject jObj)
            throw new InvalidCastException();
        if (value is JsonNode node)
        {
            jObj[key] = node;
        } else
        {
            jObj[key] = JsonNode.Parse(JsonSerializer.Serialize(value));
        }
    }

    public void FreeKVNode(JsonNode node)
    {
        // garbage collection
    }

    public JsonNode NewKVNode() => new JsonObject();

    public T ReadKey<T>(JsonNode keyValueNode, string key)
    {
        var k = keyValueNode[key];
        if (k is null)
        {
            if (default(T) == null)
            {
                return default!;
            } else
            {
                throw new InvalidCastException();
            }
        } else
        {
            return JsonSerializer.Deserialize<T>(k)!;
        }
    }
}
