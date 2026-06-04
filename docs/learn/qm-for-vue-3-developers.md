# QuickMarkup for Vue 3 Developers

If you already know Vue 3, QuickMarkup will feel surprisingly familiar.

QuickMarkup is a reactive UI language for WinUI/UWP that brings Vue-style reactive programming into native C# UI development.

Instead of writing:

* XAML for UI
* `INotifyPropertyChanged` boilerplate
* converters
* view models
* dependency-property glue

...you write reactive UI directly in C#-based markup.

The developer experience is often closer to Vue's Composition API than traditional XAML/MVVM.

However, QuickMarkup is *not* a web framework and it does *not* work like a virtual DOM renderer. The UI updates individual properties directly instead of rerendering component trees.

If you think of it as:

> “Vue-style reactivity for native WinUI apps”

...you'll be very close to the intended model.

---

## A complete minimal example

A QuickMarkup component usually looks like this:

```csharp
[QuickMarkup("""
    // reactive state
    int Count = 0;

    <StackPanel Spacing=12>
        <TextBlock Text=`$"Count: {Count}"` />

        <Button
            Content="Increment"
            @Click+=`Count++`
        />
    </StackPanel>
    """)]
public partial class CounterPage : Page
{
    public CounterPage()
    {
        Init();
    }
}
```

Things to notice immediately:

* Markup is embedded directly inside a C# string attribute
* Variables declared at the top become reactive references automatically
* Backticks `` `...` `` contain reactive C# expressions
* Updating `Count` automatically updates the UI
* The UI is declared using native WinUI/UWP controls
* No `INotifyPropertyChanged`
* No `setState`
* No `.value`

The generated code handles the reactive plumbing automatically.

QuickMarkup markup itself intentionally resembles a mix of:

* XML/XAML-style UI declaration
* C# expressions
- reactive dependency tracking

However, the syntax is still fundamentally C#-oriented.

For example, comments use standard C-style syntax:

```csharp
// single-line comment

/* multi-line
   comment */
```

Unlike Vue templates or HTML, `<!-- -->` comments are not supported inside QuickMarkup markup.

## Important differences from Vue

Although the mental model is similar, QuickMarkup behaves very differently internally.

### No virtual DOM

<!-- recheck -->
Vue rerenders component subtrees.

QuickMarkup performs targeted property updates generated at compile time.

Changing:

```csharp
Count++;
```

does not rerender the entire component tree. Only expressions depending on `Count` are reevaluated.

---

### No proxy-based deep reactivity

Vue tracks nested object mutations through proxies.

QuickMarkup tracks explicit reactive references and computed values.

This works:

```csharp
int Count = 0;
```

But mutating arbitrary nested object state will not automatically become reactive unless the reactive references themselves change.

Reactive updates are driven by reference changes, computed values, bindings, and observable collections rather than arbitrary deep mutation tracking.

---

### No component lifecycle hooks

There is no equivalent to:

```js
onMounted()
onUnmounted()
```

QuickMarkup relies on the underlying UI framework lifecycle instead.

For WinUI/UWP, use native events like:

```csharp
Loaded
Unloaded
```

---

### No hot reload

QuickMarkup relies heavily on compile-time source generation.

The tradeoff is:

* much less runtime overhead
* smaller runtime infrastructure
* direct native UI updates

...but currently without the hot reload workflow common in Vue tooling.

---

## Reactive state → References

Vue's `ref()` maps very naturally to QuickMarkup references.

However, unlike Vue, QuickMarkup references are generated at compile time as C# properties and reactive backing objects.

```vue
<!-- Vue 3 -->
<script setup>
const count = ref(0);
</script>

<template>
  <p>Count: {{ count }}</p>
</template>
```

```csharp
// QuickMarkup
int Count = 0;

<TextBlock Text=`$"Count: {Count}"` />
```

| Vue 3           | QuickMarkup                              |
| --------------- | ---------------------------------------- |
| `ref(0)`        | `int Count = 0;`                         |
| `count.value`   | `Count`                                  |
| `computed(...)` | `` ComputedName => `expr` ``             |
| `v-model`       | `<=>` (two-way binding)                  |
| template        | markup inside `[QuickMarkup("""...""")]` |

Unlike Vue refs, QuickMarkup references are accessed directly as properties — there is no `.value`.

Under the hood, the compiler generates reactive backing fields and dependency tracking automatically.

This means:

```csharp
Count++;
```

would propagate updates to any dependent UI expressions or computed values.

No `INotifyPropertyChanged`.
No setter boilerplate.
No manual dependency arrays.

## Computed values

QuickMarkup computed values behave similarly to Vue's `computed()`.

They:

* cache their results
* automatically track dependencies
* reevaluate only when dependencies change

```vue
<!-- Vue 3 -->
const total = computed(() => price.value * quantity.value);
```

```csharp
// QuickMarkup
double Total => `Price * Quantity`;
```

Like Vue, computed values are declarative and dependency-driven.

However, QuickMarkup performs dependency tracking through generated reactive infrastructure rather than runtime proxy interception.

Computed values are also lazily evaluated and cached until invalidated by a dependency change.

Because updates are targeted, QuickMarkup does not rerender an entire component when a computed value changes. Only dependent bindings are updated.

## Binding directions

Vue primarily exposes two common binding patterns:

* source → UI
* two-way (`v-model`)

QuickMarkup exposes binding direction explicitly in the syntax.

```vue
<!-- Vue 3 -->
<input v-model="name" />

<input
  :value="name"
  @input="name = $event.target.value"
/>
```

```csharp
// QuickMarkup

// two-way binding
<TextBox Text<=>`Name` />

// source → UI
<TextBox Text=`Name` />

// UI → source
<TextBox Text=>`Name` />
```

This explicit directionality becomes especially useful in native desktop UI, where one-way, bind-back, and two-way flows are often mixed within the same view.

Unlike XAML bindings, QuickMarkup bindings are still just reactive C# expressions — not string-based binding paths.


## Conditional rendering

Vue's `v-if` maps directly to regular control flow in QuickMarkup.

```vue
<!-- Vue 3 -->
<p v-if="visible">Hello</p>
<p v-else>World</p>
```

```csharp
// QuickMarkup
if (`Visible`) {
    <TextBlock Text="Hello" />
}
else {
    <TextBlock Text="World" />
}
```

Instead of template directives, QuickMarkup uses actual control-flow syntax.

This makes conditional UI behave more like normal C# code while still participating in the reactive system.

Whenever `Visible` changes, only the affected generated bindings are reevaluated.

## List rendering

Vue's `v-for` maps closely to QuickMarkup's `foreach` blocks.

```vue
<!-- Vue 3 -->
<li v-for="(item, index) in items" :key="item.id">
  {{ index + 1 }}. {{ item.name }}
</li>
```

```csharp
// QuickMarkup
foreach (index; var item in `Items`; `item.Id`) {
    <TextBlock Text=`$"{index + 1}. {item.Name}"` />
}
```

The concepts map closely:

| Vue        | QuickMarkup             |
| ---------- | ----------------------- |
| `v-for`    | `foreach`               |
| `:key`     | key expression          |
| loop index | optional index variable |

Like Vue, stable keys are important for preserving UI identity during collection updates.

When the source collection implements `INotifyCollectionChanged` (such as `ObservableCollection<T>`), QuickMarkup incrementally reconciles the generated UI as items are added, removed, or moved.

```csharp
ObservableCollection<TodoItem> Items = [];
```

Unlike React or Vue, QuickMarkup does not rerender the loop body virtually. The generated code updates the native UI tree directly.

## Event handling

Vue event listeners map naturally to QuickMarkup event bindings.

```vue
<!-- Vue 3 -->
<button @click="count++">+</button>
```

```csharp
// QuickMarkup
<Button @Click+=`Count++` Content="+" />
```

QuickMarkup uses native .NET event syntax:

```csharp
<Button Click+=`(sender, args) => Count++` />
```

The `@` shorthand automatically wraps the expression in a delegate, allowing concise inline expressions without manually writing the lambda.

```csharp
<Button @Click+=`Count++` />
```

Because QuickMarkup targets native WinUI/UWP controls directly, these are regular platform events — not synthetic DOM events.

## Element references

Vue's template refs have a close equivalent in QuickMarkup.

```vue
<!-- Vue 3 -->
<template>
  <button ref="myButton">Click</button>
</template>
```

```csharp
// QuickMarkup
myButton = <Button Content="Click" />
```

The captured variable becomes a field on the generated class and can be accessed from C# code-behind after initialization.

```csharp
myButton.Content = "Updated";
```

For forward or cross references, use `ref`:

```csharp
<TextBox AutomationProperties.LabeledBy=`InputLabel` />

ref InputLabel = <TextBlock Text="Subtitle" />
```

Without `ref`, the variable remains `null` until the element has been created.

The `ref` keyword creates a reactive reference that updates once initialization completes. Unlike Vue's `ref()`, this `ref` keyword is specifically for forward element references.

Since initialization happens top-to-bottom, declaration order matters.

## Watchers and effects

Vue's `watch()` concept maps closely to QuickMarkup watchers and effects.

```vue
<!-- Vue 3 -->
watch(count, (val) => {
  console.log(val);
});
```

```csharp
// QuickMarkup
CountProp.Watch(v => Console.WriteLine(v));
```

You can also react to multiple dependencies using `Effect`:

```csharp
Effect(
    () => Console.WriteLine($"{First} {Last}"),
    FirstProp,
    LastProp
);
```

Unlike Vue's `watchEffect()`, QuickMarkup effects are not tied to component render cycles or lifecycle phases.

They are purely reactive dependency subscriptions.

This distinction is important:

* no rerender lifecycle
* no mount/unmount semantics
* no render pass scheduling

QuickMarkup reacts to reference changes directly through generated reactive infrastructure.

## Components

Vue and QuickMarkup both support reusable declarative components.

```vue
<!-- Vue 3 -->
<script setup>
defineProps(['text']);
</script>

<template>
  <p>{{ text }}</p>
</template>
```

```csharp
// QuickMarkup
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

Properties declared at the top of the markup become reactive component properties automatically.

Usage looks very similar to Vue:

```vue
<!-- Vue 3 -->
<Label text="Hello" />
```

```csharp
// QuickMarkup
<Label Text="Hello" />
```

Unlike Vue components, QuickMarkup components compile directly into generated C# and native UI initialization logic.

There is no virtual DOM, template interpreter, or runtime component renderer involved.

Additional properties placed on the component tag are forwarded to the component's root markup node:

```csharp
<Label
    Text="Hello"
    HorizontalAlignment=Center
/>
```

Conceptually similar to Vue's fallthrough attributes.

## Mental model comparison

| Concept                | Vue 3                       | QuickMarkup                          |
| ---------------------- | --------------------------- | ------------------------------------ |
| Reactivity             | Runtime proxy tracking      | Compile-time generated references    |
| Template               | `.vue` SFC                  | C# markup attribute                  |
| Dependency tracking    | Runtime                     | Compile-time generated               |
| Rendering              | Virtual DOM diffing         | Direct native property updates       |
| Script                 | `<script setup>`            | `partial class`                      |
| Styling                | CSS / scoped styles         | Native platform APIs                 |
| UI target              | Browser / SSR               | WinUI / UWP native UI                |
| Runtime overhead       | Framework runtime required  | Minimal reactive infrastructure      |
| Reactivity granularity | Component/render-tree level | Individual bindings/properties       |
| UI update mechanism    | Virtual DOM rerender + diff | Scheduled direct binding propagation |

QuickMarkup intentionally borrows many of Vue's reactive ideas while targeting an entirely different runtime environment.

The result feels conceptually similar to Vue's Composition API, but optimized for native .NET desktop UI instead of the browser.

If you already think in terms of:

* reactive state
* computed values
* declarative UI
* dependency-driven updates

...then most of QuickMarkup will feel immediately familiar.
