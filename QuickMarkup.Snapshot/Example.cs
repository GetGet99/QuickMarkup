using System.Text.Json.Nodes;

namespace QuickMarkup.Snapshot;

// user code

[SnapshotComponent(typeof(JsonNodeSnapshotFormatter), "$type", SnapshotPresetMode.Persistence)]
partial interface IMySnapshotComponent : ISnapshotComponent;

[SnapshotInclude("ABC")]
[QuickMarkup("""
    [SnapshotInclude("abc")]
    int abc;
    [SnapshotInclude("def")]
    string def;
    [SnapshotInclude("ghi")]
    GHI ghi;
    """)]
partial class ABC : IMySnapshotComponent;
[SnapshotInclude("GHI")]
[QuickMarkup("""
    [SnapshotInclude("jkl")]
    bool jkl;
    [SnapshotInclude("mno")]
    float mno;
    """)]
partial class GHI : IMySnapshotComponent;

class QuickMarkupAttribute(string markup) : Attribute;

// generated code mockup

/*
partial interface IMySnapshotComponent
    // source generator should add this typed generic inteface
    : ISnapshotComponent<JsonNodeSnapshotFormatter, JsonNode>
{
    // and also these methods
    public static IMySnapshotComponent LoadFromSnapshot(JsonNodeSnapshotFormatter formatter, JsonNode jsonNode)
    {
        IMySnapshotComponent component = formatter.ReadKey<string>(jsonNode, "$type") switch
        {
            "ABC" => new ABC(),
            "GHI" => new GHI(),
            _ => throw new NotImplementedException()
        };
        component.LoadSnapshot(formatter, jsonNode);
        return component;
    }
}

partial class ABC
{
    public void LoadSnapshot(JsonNodeSnapshotFormatter formatter, JsonNode saved)
    {
        abc = formatter.ReadKey<int>(saved, "abc");
        def = formatter.ReadKey<string>(saved, "def");
        // use LoadFromSnapshot instead of `new GHI().Load()` in case other class may have inhirted GHI during saving.
        ghi = IMySnapshotComponent.LoadFromSnapshot(formatter, formatter.ReadKey<JsonNode>(saved, "ghi"));
    }

    public JsonNode SaveSnapshot(JsonNodeSnapshotFormatter formatter)
    {
        var kv = formatter.NewKVNode();
        formatter.AppendKey(kv, "$type", "ABC");
        formatter.AppendKey(kv, "abc", abc);
        formatter.AppendKey(kv, "def", def);
        formatter.AppendKey(kv, "ghi", ghi.SaveSnapshot(formatter));
        return kv;
    }
}
partial class GHI
{
    public void LoadSnapshot(JsonNodeSnapshotFormatter formatter, JsonNode saved)
    {
        abc = formatter.ReadKey<int>(saved, "abc");
        def = formatter.ReadKey<string>(saved, "def");
    }

    public JsonNode SaveSnapshot(JsonNodeSnapshotFormatter formatter)
    {
        var kv = formatter.NewKVNode();
        formatter.AppendKey(kv, "$type", "GHI");
        formatter.AppendKey(kv, "abc", abc);
        formatter.AppendKey(kv, "def", def);
        return kv;
    }
}
*/
