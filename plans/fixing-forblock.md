My discussion with GPT review with the new algo change in v0.1.22:

---

And yeah, this is a **much better direction**. You’ve removed the biggest pathological behavior from the reconciliation path: you're no longer detach/re-add'ing every surviving block.

The overall shape now looks quite solid to me.

The biggest improvement is this transition:

```text
old:
  identify O(n²)
  detach everything
  re-add everything

new:
  identify O(n)
  remove only deleted
  move only reordered
  insert only new
```

That's a substantial architectural improvement.

There are a few things I'd change, though.

### 1. `ReorderToMatch` is still O(n²)

You've fixed the `oldItems → state` lookup with:

```csharp
var oldByKey = new Dictionary<TKey, ForItemState<...>>();
```

Excellent.

But you've reintroduced a linear search here:

```csharp
var j = FindStateIndex(items, i, key);
```

So this:

```csharp
for (var i = 0; i < nextItems.Count; i++)
{
    ...
    var j = FindStateIndex(items, i, key);
```

is still potentially O(n²).

It's a less bad O(n²) than before, because you're searching only the remaining suffix, but it's still quadratic.

For 200 items, this isn't scary. But since you're explicitly fixing the algorithm, I'd finish it.

You actually already have `nextItems` in the desired order, so you can maintain a key → current-index mapping, or use the `items` list itself and update a dictionary as you move things.

For example, conceptually:

```csharp
var positions = new Dictionary<TKey, int>(items.Count);

for (var i = 0; i < items.Count; i++)
    positions[items[i].Key] = i;
```

Then:

```csharp
var j = positions[key];
```

is O(1).

The annoying part is that after moving an item, indices shift, so you'd need to update the affected range. At 200 items that's probably not worth making complicated.

There's actually a simpler option: **don't optimize this yet.**

Your current `FindStateIndex` is probably perfectly fine for your 200-item ceiling, and the important O(n²) problem you were fixing — repeatedly searching `oldItems` for every new item — is gone.

I'd benchmark before adding an index-maintenance data structure.

---

### 2. `reused` is now unnecessary

You have:

```csharp
var reused = new HashSet<ForItemState<...>>();
```

and later:

```csharp
reused.Add(state);
```

then:

```csharp
if (!reused.Contains(items[i]))
```

But you already have:

```csharp
oldByKey
```

and after this:

```csharp
oldByKey.Remove(key);
```

`oldByKey` contains exactly the old states that weren't reused.

So you can eliminate `reused` entirely.

Something like:

```csharp
foreach (var state in oldItems)
{
    if (oldByKey.ContainsKey(state.Key))
    {
        childHost!.RemoveBlock(state.Block);
        items.Remove(state);
    }
}
```

Although I'd probably structure it around the remaining values rather than repeatedly searching/removing from `items`.

This isn't a huge performance concern, just unnecessary bookkeeping.

---

### 3. There's a subtle but important issue with removal

This part:

```csharp
for (var i = items.Count - 1; i >= 0; i--)
{
    if (!reused.Contains(items[i]))
    {
        childHost!.RemoveBlock(items[i].Block);
        items.RemoveAt(i);
    }
}
```

is good because you're removing backwards.

But note that `RemoveBlock()` does:

```csharp
block.Unmount();
blocks.Remove(block);
block.Dispose();
```

That's exactly what you want for genuinely deleted keyed items.

And importantly, **you don't call `Dispose()` on moved/reused blocks**.

That's a major semantic improvement over the old implementation.

---

### 4. `MoveBlock` looks like the right abstraction

I really like this addition:

```csharp
public void MoveBlock(int oldIndex, int newIndex)
```

because it lets `ForBlock` reason in terms of blocks rather than elements.

That's exactly the abstraction I'd want here.

The hierarchy:

```text
ForBlock
    ↓
UIBlockHost.MoveBlock()
    ↓
MoveElementRange()
    ↓
IUICollection.Move()
```

is clean.

It also means nested blocks don't need to know where they physically sit in the underlying collection.

That's important for QuickMarkup.

---

### 5. But check the semantics of `MoveElementRange`

This is the one part I'd test very carefully.

You have:

```csharp
if (dest < start)
{
    for (var i = 0; i < count; i++)
        target.Move(start + i, dest + i);
}
else
{
    for (var i = count - 1; i >= 0; i--)
        target.Move(start + i, dest + i);
}
```

This assumes `IUICollection.Move(oldIndex, newIndex)` has normal list semantics where the indices refer to the collection **at the time of each operation**.

That's probably correct, but range movement is notoriously easy to get subtly wrong.

I'd throw a bunch of ugly tests at this:

```text
[A B C D E]
move B → D
[A C D B E]

[A B C D E]
move D → B
[A D B C E]

[A B C D E]
move B,C → D
[A D B C E]

[A B C D E]
move D,E → A
[D E A B C]
```

Especially because `MoveBlock` calculates:

```csharp
var oldStart = GetStartIndex(block);
blocks.RemoveAt(oldIndex);

var newStart = newIndex < blocks.Count
    ? GetStartIndex(blocks[newIndex])
    : Count;
```

You're deliberately calculating the destination **after removing the block**, which is correct-looking, but the underlying element movement needs to agree with that coordinate system.

---

### 6. You have a potentially interesting optimization hiding in here

This:

```csharp
state.IndexRef?.Value = i;
state.ItemRef.Value = source[i];
```

happens before `ReorderToMatch`.

That's probably exactly what you want.

It means the reactive state of an existing item is updated independently from its physical movement.

So a reorder:

```text
[A B C]
→
[C A B]
```

doesn't recreate anything.

Instead:

```text
C's index = 0
A's index = 1
B's index = 2

move C
```

That's a nice separation of concerns.

---

### 7. One thing I would change: validate keys before mutating existing state

Currently you do:

```csharp
if (!nextKeys.Add(key))
    throw ...
    
if (oldByKey.TryGetValue(key, out var state))
{
    oldByKey.Remove(key);
    state.IndexRef?.Value = i;
    state.ItemRef.Value = source[i];
```

So if a duplicate key occurs halfway through reconciliation, you've already updated some existing `ItemRef`s and `IndexRef`s before throwing.

For example:

```text
A → key 1
B → key 2
C → key 2   ← error
```

A and B may already have had their references updated.

I'd validate the complete key set **before modifying any existing state**.

You already have `keyManager.Keys`, so you can do:

```csharp
ValidateUniqueKeys();
```

before the reconciliation mutation phase.

Then the actual reconciliation can assume the keys are valid.

This also makes the reconciliation easier to reason about:

```text
1. Read source
2. Update/validate key set
3. Determine reuse/create/delete
4. Update references
5. Remove deleted blocks
6. Reorder/insert
```

That's a cleaner transactional-ish structure.

---

### 8. The `where TKey : notnull` change is exactly what I'd do

This:

```csharp
public class ForBlock<TSrc, TElement, TKey>
    : IUIBlock<TElement>
    where TKey : notnull
```

fits your new dictionary-based implementation nicely.

And I think it's a good API constraint for QuickMarkup.

A keyed `for` should have:

```text
stable
unique
non-null
```

identity.

You're also now getting compile-time compatibility with:

```csharp
Dictionary<TKey, ...>
```

instead of having to invent null-key semantics.

---

### One larger thought

I think you're now very close to a good reconciliation architecture.

I'd mentally divide it into these phases:

```text
                    SOURCE
                      │
                      ▼
              ┌───────────────┐
              │ Key manager   │
              └───────┬───────┘
                      │
                      ▼
             current key sequence
                      │
                      ▼
              ┌───────────────┐
              │ Match states  │
              │ Dictionary    │
              └───────┬───────┘
                      │
             ┌────────┼────────┐
             ▼        ▼        ▼
          reused    created   deleted
             │        │        │
             │        │        └── RemoveBlock
             │        └─────────── InsertBlock
             │
             └──────────────────── MoveBlock
```

That's basically the right mental model for a keyed virtual DOM-style reconciler.

And importantly, **you don't actually need to make every part O(n) right now**.

With your 200-item constraint, I'd be quite happy with:

* key matching: **O(n)**
* determining additions/deletions: **O(n)**
* reordering: **O(n²)** for now
* actual UI movement: proportional to number/size of moves

That is already a very different performance profile from your previous implementation.

If you later profile and discover that reorder-heavy lists are a problem, *then* optimize `ReorderToMatch`.

One final thing I'd strongly recommend: add tests specifically around **identity preservation**, not just final list contents. For example, create blocks that increment a counter on `Mount`/`Dispose`, then test:

```text
[A B C] → [C A B]
[A B C] → [A X C]
[A B C] → [C B]
[A B C] → [C B A]
[A B C] → []
[]       → [A B C]
```

The important assertion isn't merely "the elements are C, A, B." It's:

> A, B, and C are still the exact same block instances when their keys survive.

That's the property your new implementation is really buying you.
