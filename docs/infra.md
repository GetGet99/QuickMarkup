# QuickMarkup Infrastructure

This description will go through the infrastructure of QuickMarkup.

> [!CAUTION]  
> QuickMarkup infrastructure is currently designed for a single-threaded application. Attempting to use reactivity system outside the main thread may result in undefined behavior. Support for multi-thread may come in the future. 

## References-based reactivity

In QuickMarkup, the reactivity system is the system where if a value to the reference changes, all their dependencies will be re-evaluated. For example:

```cs
Reference<int> numberRef = new(0);

Computed<int> computedVar = new(() => numberRef.Value + 1);

// watching for changes
computedVar.Watch(x => Console.WriteLine(x));

// will print "2" to the console
number.Value = 1;

// will print "5" to the console
number.Value = 4;
```

## References

Refernces are a value container that, when written, will send signal to all dependencies.

```cs
Reference<int> numberRef = new(0);
```

### Intercepting changes manually in references

Callbacks can be added to watch changes from references by using `Watch` method.

```cs
// prints numberRef's value whenever numberRef changes
numberRef.Watch(x => Console.WriteLine(x));
```

With `immediete: true`, it will also runs the callback the first time as well.

```cs
// prints numberRef's value when the line below is executed and whenever numberRef changes
numberRef.Watch(x => Console.WriteLine(x), immediete: true);
```



## Computed Variables

Computed variables are a special, cached value that would track all references. Reading computed variable twice will only evaluates the logic once, effectively caching the output value until any input references are changed.

```cs
Computed<int> computedVar = new(() => numberRef.Value + 1);
```

### Intercepting changes manually in computed variables

Callbacks can be added to watch changes to its computed value by using `Watch` method.

```cs
// prints computedVar's new value whenever computedVar's dependency changes
computedVar.Watch(x => Console.WriteLine(x));
```

With `immediete: true`, it will also runs the callback the first time as well.

```cs
// prints computedVar's value when the line below is executed and whenever computedVar's dependency changes
computedVar.Watch(x => Console.WriteLine(x), immediete: true);
```

## Reactive Scheduler

We have simplified a bit earlier about how the callback is executed whenever the changes happened. In reality, it does not get re-evaluated immedietely. It waits for the next "Tick" to happen.

```cs
Reference<int> numberRef = new(0);

Computed<int> computedVar = new(() => numberRef.Value + 1);

// watching for changes
computedVar.Watch(x => Console.WriteLine(x));

// does not trigger Console.WriteLine yet
number.Value = 1;

// will print "2" to the console
ReactiveScheduler.Tick();

// does not trigger Console.WriteLine yet
number.Value = 2;

// does not trigger Console.WriteLine yet
number.Value = 3;

// will print "4" to the console
// "3" will not be printed
ReactiveScheduler.Tick();
```

"Tick" is when all changed references' dependencies will all get re-executed.

### Automatically scheduling Tick

In most UI frameworks, there will be a way to schedule a call to be running later. In most rael world use cases, there will be no need to call Tick manually. With a setup call on the program starup, you can setup reactivity with a few lines and the system will be ready to go.

```cs
ReactiveScheduler.AddTickCallbackForCurrentThread(delegate
{
    _ = Dispatcher.TryRunAsync(CoreDispatcherPriority.High, ReactiveScheduler.Tick);
});
```

## Additional Helpers

### QuickRefs

QuickRefs static class defines many helpful helper methods to declare refs and computed variables easily.

```cs
public static Reference<T> Ref<T>(T initialValue)
{
    return new(initialValue);
}
public static Computed<T> Computed<T>(Func<T> initialValue)
{
    return new(initialValue);
}
```

`Effect` helper is useful when you'd like to schedule a callback when any of several selected references change.

```cs
public static RefEffect Effect(Action callback, params IReference[] references);
```

### ReferenceTracker Low-level helpers

`NoCapture` provides a scope that the references will not be tracked.

```cs
public static T NoCapture<T>(Func<T> action);
```

`RunAndRerunOnReferenceChange` is a lower level handler where if any references used in `func` is changed, the function will be rerun.

This is lower level API than `Computed<T>` API, which is used very often in generated code of QuickMarkup language.

```cs
public static RefEffect RunAndRerunOnReferenceChange<T>(Func<T> func, Action<T> continueAction);
```


