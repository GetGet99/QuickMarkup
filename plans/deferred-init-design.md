# Design: Deferred Initialization for QuickMarkup Components

## Problem

QuickMarkup currently initializes components immediately inside their constructor:

```csharp
public TitleText()
{
    Init();
}
```

This prevents several future features:

* Context injection before initialization
* Required component properties
* Dependency injection
* Other pre-initialization setup

The issue is that by the time the caller assigns properties, initialization has already occurred.

---

## Proposed Design

Introduce a secondary constructor that performs property assignment first, then initializes the component.

Generated code may use:

```csharp
new TitleText(quickMarkupInitializer: x =>
{
    x.Text = "Hello";
    x.Context = currentContext;
});
```

Generated constructor:

```csharp
public TitleText(Action<TitleText> quickMarkupInitializer)
{
    quickMarkupInitializer(this);
    InternalInit();
}
```

Existing constructor remains unchanged:

```csharp
public TitleText()
{
    InternalInit();
}
```

---

## Initialization Order

Normal usage:

```csharp
var component = new TitleText();
```

Execution:

```text
Allocate object
↓
InternalInit()
```

Generated QuickMarkup usage:

```csharp
new TitleText(x =>
{
    x.Text = "Hello";
    x.Context = ctx;
});
```

Execution:

```text
Allocate object
↓
Assign generated properties
↓
Assign context
↓
InternalInit()
```

---

## Goals

* Preserve existing API compatibility.
* Avoid exposing explicit `Init()` calls.
* Prevent users from forgetting initialization.
* Enable future features requiring pre-init setup.
* Preserve existing property evaluation ordering semantics.

---

## Notes

The lambda constructor is intended primarily for generated code.

Optionally:

```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public TitleText(Action<TitleText> quickMarkupInitializer)
```

may be used to reduce IntelliSense visibility.
