# QuickMarkup for React Developers

If you already know React, QuickMarkup will feel familiar in some ways — but very different in others.

QuickMarkup is a reactive UI language for .NET native desktop apps (WinUI/UWP) built around:

* reactive state
* declarative UI
* dependency tracking
* compile-time source generation

Like React, UI updates are driven by state changes.

But unlike React:

* there is no virtual DOM
* there are no rerender passes
* there are no hooks
* there are no dependency arrays
* components do not re-execute as functions

Instead, QuickMarkup tracks reactive dependencies directly and updates only the affected bindings.

If you think of it as:

> “fine-grained reactive UI for native .NET apps”

...you’ll already be very close to the intended mental model.

---

# A complete minimal example

```csharp
[QuickMarkup("""
    int Count = 0;

    <StackPanel Spacing=12>

        <TextBlock
            Text=`$"Count: {Count}"`
        />

        <Button
            Content="Increment"
            @Click+=`Count++`
        />

    </StackPanel>
    """)]
public partial class CounterPage : Page;
```

A few things stand out immediately:

* markup is embedded directly inside a C# attribute
* variables become reactive automatically
* backticks contain reactive C# expressions
* updating `Count` updates the UI automatically
* there is no `setState`
* there is no `useState`
* there is no JSX runtime
* there is no `INotifyPropertyChanged`

The compiler generates the reactive infrastructure automatically.

---

# The biggest mental shift from React

In React, components rerender.

In QuickMarkup, bindings update directly.

This distinction changes almost everything.

In React:

```jsx
function Counter() {
  const [count, setCount] = useState(0);

  return <p>{count}</p>;
}
```

When state changes:

* the component function runs again
* JSX gets recreated
* React diffs trees
* the DOM updates afterward

In QuickMarkup:

```csharp
int Count = 0;

<TextBlock Text=`$"Count: {Count}"` />
```

Changing:

```csharp
Count++;
```

does *not* rerun the component.

Only the `Text` binding updates.

There is no component rerender pipeline.

QuickMarkup behaves much closer to fine-grained reactive systems like SolidJS than React's render-cycle model.

---

# Reactive state

React state:

```jsx
const [count, setCount] = useState(0);
```

QuickMarkup state:

```csharp
int Count = 0;
```

That variable becomes a generated reactive reference automatically.

You read and write it directly:

```csharp
Count++;
Count = 10;
```

No setter function is required.

Under the hood, QuickMarkup generates reactive backing infrastructure similar to:

```csharp
Reference<int> CountProp;
```

...but normally you work with the generated property directly.

---

# Computed values

React commonly uses `useMemo`:

```jsx
const total = useMemo(
  () => price * quantity,
  [price, quantity]
);
```

QuickMarkup uses computed values:

```csharp
double Total => `Price * Quantity`;
```

Computed values:

* automatically track dependencies
* cache results
* reevaluate only when needed

There are no dependency arrays to maintain manually.

This is one of the biggest ergonomic differences from React.

---

# Effects and watchers

React effects:

```jsx
useEffect(() => {
  console.log(count);
}, [count]);
```

QuickMarkup:

```csharp
CountProp.Watch(v => Console.WriteLine(v));
```

or:

```csharp
Effect(
    () => Console.WriteLine(Count),
    CountProp
);
```

This is an important conceptual difference:

React `useEffect` is tied to the render lifecycle.

QuickMarkup effects are not.

They are simply reactive subscriptions.

There is no mount/update/unmount effect cycle.

No render scheduling.

No dependency-array semantics.

Just:

> “rerun this when these references change.”

For UI lifecycle events, use the native framework lifecycle instead:

```csharp
Loaded
Unloaded
```

---

# Conditional rendering

React:

```jsx
{isLoggedIn
  ? <Logout />
  : <Login />
}
```

QuickMarkup:

```csharp
if (`IsLoggedIn`) {
    <Logout />
}
else {
    <Login />
}
```

QuickMarkup uses actual control flow syntax instead of template directives or JSX expressions.

---

# List rendering

React:

```jsx
{items.map(item =>
  <TodoItem
    key={item.id}
    item={item}
  />
)}
```

QuickMarkup:

```csharp
foreach (var item in `Items`; `item.Id`) {
    <TodoItem Item=`item` />
}
```

With index access:

```csharp
foreach (index; var item in `Items`; `item.Id`) {
    <TextBlock
        Text=`$"{index + 1}. {item.Name}"`
    />
}
```

Like React, stable keys are important.

However, QuickMarkup does not rerender lists virtually.

It incrementally updates the native UI tree directly.

When using `ObservableCollection<T>`, collection updates reconcile automatically.

---

# Event handling

React:

```jsx
<button
  onClick={() => setCount(c => c + 1)}
>
  +
</button>
```

QuickMarkup:

```csharp
<Button
    @Click+=`Count++`
    Content="+"
/>
```

Events are native .NET events.

The `@` shorthand automatically wraps the expression in a delegate.

You can also use `await` in the shorthand syntax:

```csharp
<Button
    @Click+=`await DisplayDialog("Click")`
/>
```

This automatically generates an `async` delegate wrapper.

You can also write full handlers:

```csharp
<Button
    Click+=`(sender, args) => Count++`
/>
```

Unlike React, these are not synthetic browser events.

---

# Two-way binding

React usually requires explicit state synchronization:

```jsx
<input
  value={name}
  onChange={e => setName(e.target.value)}
/>
```

QuickMarkup has built-in two-way binding syntax:

```csharp
<TextBox Text<=>`Name` />
```

Binding directions are explicit:

```csharp
// source -> UI
Text=`Name`

// UI -> source
Text=>`Name`

// two-way
Text<=>`Name`
```

Unlike React controlled inputs, the reactive plumbing is generated automatically.

---

# Element references

React:

```jsx
const buttonRef = useRef(null);

<button ref={buttonRef} />
```

QuickMarkup:

```csharp
myButton = <Button />
```

The captured variable becomes a generated field accessible from code-behind.

For backward or cross references, use `ref`:

```csharp
<TextBox
    AutomationProperties.LabeledBy=`InputLabel`
/>

ref InputLabel =
    <TextBlock Text="Subtitle" />
```

Initialization happens top-to-bottom, so declaration order matters.

---

# Components

React components are functions:

```jsx
function Label({ text }) {
  return <p>{text}</p>;
}
```

QuickMarkup components are generated partial classes:

```csharp
[QuickMarkup("""
    string Text = "";

    <TextBlock
        Text=`Text`
        FontSize=16
    />
    """)]
public partial class Label
    : IQuickMarkupComponent<TextBlock>;
```

Usage:

```csharp
<Label Text="Hello" />
```

Unlike React:

* components are not rerun
* there is no hook ordering
* there is no reconciliation step
* there is no runtime JSX evaluation

The compiler generates native UI initialization code directly.

---

# Sharing State Between Components

React applications commonly share values using Context to avoid passing props through multiple levels of components.

QuickMarkup provides `provide` and `inject` declarations for the same purpose.

```cs
[QuickMarkup("""
    provide string Theme = "Dark";

    <root>
        <SettingsPanel />
    </root>
    """)]
public partial class MainPage : Page;

[QuickMarkup("""
    inject string Theme;

    <root>
        <TextBlock Text=`Theme` />
    </root>
    """)]
public partial class SettingsPanel : StackPanel;
```

`provide` exposes a reactive value to descendant components. `inject` binds to that same reactive reference, so both components observe and update the same value.

If you're familiar with React Context, the programming model is similar. The main difference is that QuickMarkup generates the context plumbing automatically, so there is no separate context object, provider component, or `useContext` call.

### Optional Injection

Use `inject?` when a provider may not exist.

```cs
inject? string Theme;
```

If no matching provider exists, the injected property is initialized to `default(T)` instead of throwing.

### How It Works

Each component owns a context containing its provided values. Child components inherit that context, and `inject` looks up the requested value by type and name.

Because injected properties reference the same reactive object as the provider, changes made from either component are immediately visible to the other.

---

# No hooks

QuickMarkup intentionally does not have an equivalent to:

* `useState`
* `useEffect`
* `useMemo`
* `useCallback`
* `useReducer`
* hook call ordering rules

Reactive dependencies are tracked directly instead.

This removes entire categories of React problems:

* stale closures
* missing dependency arrays
* hook ordering bugs
* unnecessary rerenders
* memoization boilerplate

The tradeoff is that QuickMarkup operates with a very different execution model.

---

# Mental model comparison

| Concept             | React                    | QuickMarkup                   |
| ------------------- | ------------------------ | ----------------------------- |
| Rendering           | Component rerendering    | Direct binding updates        |
| UI diffing          | Virtual DOM              | Native UI updates             |
| State               | Hooks                    | Generated references          |
| Effects             | Render lifecycle effects | Reactive subscriptions        |
| Memoization         | `useMemo`                | Computed values               |
| Event system        | Synthetic events         | Native .NET events            |
| Components          | Functions                | Generated partial classes     |
| Dependency tracking | Dependency arrays        | Automatic reactive tracking   |
| Runtime model       | Runtime reconciliation   | Compile-time generated wiring |
| UI target           | Browser / React Native   | WinUI / UWP native UI         |

QuickMarkup borrows many ideas React developers already understand:

* state-driven UI
* declarative rendering
* reactive updates
* reusable components

...but the underlying execution model is fundamentally different.

Once you stop thinking in terms of rerenders and start thinking in terms of reactive dependency propagation, the model becomes much easier to reason about.
