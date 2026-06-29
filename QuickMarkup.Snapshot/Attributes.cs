using System.Text.Json.Nodes;

namespace QuickMarkup.Snapshot;

public enum SnapshotPresetMode
{
    // Use recommended recommendation and setup presets for persistence-ready snapshot
    // default: SnapshotStateMode.AllExplicit, DiagnosticMode = Public | NoName
    Persistence,
    // Use recommended recommendation and setup presets for runtime snapshot
    // default: SnapshotStateMode.IncludesPublic, DiagnosticMode = Public
    Runtime
}

[Flags]
public enum SnapshotStateMode
{
    // User must explicitly include states. Recommended if snapshotting for persistence to avoid state changes
    AllExplicit = 0,
    // Public members will be automatically included. Recommended for serializing
    IncludesPublic = 0b1
}

[Flags]
public enum SnapshotDiagnosticMode
{
    None = 0,
    // Error if the snapshotting state does not have explicit state name, useful to ensure against rename tolerance
    NoName = 0b1,
    // Warns if public refernce does not have [SnapshotInclude] or [SnapshotIgnore] in markup
    Public = 0b10,
}
[AttributeUsage(AttributeTargets.Interface)]
public class SnapshotComponentAttribute(Type formatterType, string typeKey, SnapshotPresetMode preset) : Attribute
{
    // overridable configuration
    public SnapshotStateMode? SnapshotMode { get; set; }
    public SnapshotDiagnosticMode? DiagnosticMode { get; set; }
}

public class SnapshotIncludeAttribute(string key = "") : System.Attribute;
public class SnapshotIgnoreAttribute() : System.Attribute;
public class SnapshotManualAttribute(string key = "") : SnapshotIncludeAttribute(key);

record SnasphostConfiguration(SnapshotStateMode SnapshotMode, SnapshotDiagnosticMode DiagnosticMode);
