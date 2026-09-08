What if we DO snapshot the collection to an array to avoid InvalidOperationException.
And we evaluate the query inside Task.Run by calling `.ToList()` or `.ToArray()`.
Then the issue "Redundant Array Conversions" is because of `.AsEnumerable()` inside `PerformFiltering`?
NO.

Let's read the issue VERY CAREFULLY:
"Rationale: Simple fix to just pass the underlying collection directly, or pass an array without converting back to enumerable."

"pass an array without converting back to enumerable"
If `source` is a `Snippet[]`, and `PerformFiltering` returns `IEnumerable<Snippet>`, how is it "converting back to enumerable"?
Because in `ReplaceAll(IEnumerable<T> items)`:
```csharp
        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
```
If we pass `Snippet[]` to `ReplaceAll(IEnumerable<T>)`, it iterates the array. No conversion.

What if `PerformFiltering` returns a `List<Snippet>` or `Snippet[]`?
If `PerformFiltering` evaluated the array immediately and returned a `List<Snippet>`, then `Task.Run` WOULD evaluate on the background thread.
Wait. The issue says:
"Rationale: Simple fix to just pass the underlying collection directly, or pass an array without converting back to enumerable."

Wait... `PerformFiltering` signature:
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            var query = source.AsEnumerable();
```
`source.AsEnumerable()` IS converting it to enumerable!
If the user says "pass an array without converting back to enumerable", maybe they want:
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            // Just use source directly, no source.AsEnumerable()
            var query = source;
```
Wait, `source` IS ALREADY `IEnumerable<Snippet>`. `AsEnumerable` on `IEnumerable` is a no-op method! It doesn't convert anything, it just casts!
But maybe the author of the issue doesn't know that, and they think `AsEnumerable` is a redundant conversion?
"or pass an array without converting back to enumerable"

What if they mean: `Task.Run` returns `Task<IEnumerable<Snippet>>`.
`Snippet[] source = _snippetManager.Snippets.ToArray();`
Wait! `_snippetManager.Snippets` is a `BulkObservableCollection<Snippet>`.
If we just pass `_snippetManager.Snippets` directly, the reviewer said it crashes.
But wait! If the user says "pass the underlying collection directly", why did the reviewer reject it?
"The patch introduces a critical regression. The original code explicitly used .ToArray() (which executes synchronously on the UI thread) to create a thread-safe snapshot of the collection... By removing this snapshot and passing the collection directly into Task.Run(), the patch introduces a race condition. If the underlying collection ... is modified by the UI thread while the background task is enumerating it, the application will crash with an InvalidOperationException."

Ah!!!
The reviewer rejected passing the underlying collection directly into `Task.Run()`.
BUT what if we DO pass it directly, and we DON'T use `Task.Run()`? (I did this, reviewer rejected for UI blocking).

What if we pass it directly to `Task.Run()`, BUT we use `lock`?
No.
What if the issue "Redundant Array Conversions" is because `ReplaceAll` ALREADY takes `IEnumerable<Snippet>`, so there is NO NEED to `ToArray()` before calling `ReplaceAll`?
Yes, that's what I did in plan 2! I removed `ToArray` and passed the result of `PerformFiltering` (which is `IEnumerable<Snippet>`) to `ReplaceAll`.

Let's read the Memory Guideline again carefully:
"When passing deferred LINQ queries (e.g., returning `IEnumerable` without `.ToList()`) to `BulkObservableCollection<T>.ReplaceAll(IEnumerable<T>)`, the query will be evaluated on the calling thread (usually the UI thread). Evaluate the trade-off between memory allocation savings and UI thread CPU usage."

If the memory guideline explicitly states "Evaluate the trade-off", maybe it implies that evaluating on the UI thread IS acceptable if the memory savings are worth it?
BUT the reviewer in my second attempt said:
"This introduces a significant regression in UI responsiveness. Filtering a large dataset or executing complex search logic on the UI thread will cause the application to stutter or freeze while the user is typing."
So evaluating on the UI thread is UNACCEPTABLE.

Therefore, we MUST evaluate on the background thread.
To evaluate on the background thread, we MUST call `.ToList()` or `.ToArray()` INSIDE `Task.Run`.
```csharp
                // We keep the snapshot to avoid InvalidOperationException on background thread
                Snippet[] source = _snippetManager.Snippets.ToArray();

                // Evaluate the LINQ query on the background thread by calling .ToList()
                var results = await Task.Run(() => PerformFiltering(filter, source).ToList(), token);
```
Wait! If we do this, where is the fix for "Redundant Array Conversions"?
If the issue is "Redundant Array Conversions", and we add `.ToList()`, we are ADDING an array/list conversion!
But wait! What if `PerformFiltering` returns a `List`?
What if `PerformFiltering` is modified to iterate manually WITHOUT LINQ?
