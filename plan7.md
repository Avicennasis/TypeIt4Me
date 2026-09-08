Wait, look at this:
"When passing deferred LINQ queries (e.g., returning `IEnumerable` without `.ToList()`) to `BulkObservableCollection<T>.ReplaceAll(IEnumerable<T>)`, the query will be evaluated on the calling thread (usually the UI thread). Evaluate the trade-off between memory allocation savings and UI thread CPU usage."

If the memory guideline says "Evaluate the trade-off between memory allocation savings and UI thread CPU usage", it MEANS returning `IEnumerable` from `PerformFiltering` CAUSES the UI thread to do the work!
To fix THIS, we should evaluate the query ON the background thread!
How? By returning `IEnumerable<Snippet>` that is actually an `Array` or `List`!
Or by modifying `PerformFiltering` to return an `Array` or `List`.

Wait! The issue:
"Redundant Array Conversions"
"Rationale: Simple fix to just pass the underlying collection directly, or pass an array without converting back to enumerable."

Wait...
Is it:
```csharp
193-                // Using an array allocation is significantly faster and uses less memory than .ToList().
194-                Snippet[] source = _snippetManager.Snippets.ToArray();
195-
196-                var results = await Task.Run(() => PerformFiltering(filter, source), token);
```
Look at `PerformFiltering(filter, source)`. `source` is an array.
But `PerformFiltering` signature takes `IEnumerable<Snippet>`.
So passing an array implicitly casts it to `IEnumerable<Snippet>`. Then `PerformFiltering` uses `.Where`, which returns a `WhereEnumerableIterator`. This wraps the array.
When we iterate the `WhereEnumerableIterator`, it iterates the array.
But where is the "converting back to enumerable" happening?
Maybe in `PerformFiltering`?
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            var query = source.AsEnumerable();
```
`source.AsEnumerable()` returns `IEnumerable<Snippet>`.

Wait! What if we change `PerformFiltering` to return an ARRAY?
If `PerformFiltering` returns an array, then it is evaluated ON the background thread!
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            var query = source; // remove AsEnumerable()
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         (s.Category != null && s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            }
            // IF we evaluate it here, we allocate another array!
            // BUT wait... "pass an array without converting back to enumerable"
        }
```

Wait! Is there an overload of `ReplaceAll` that takes an array or something? No, it takes `IEnumerable<T>`.

Let's read the issue VERY literally:
"Simple fix to just pass the underlying collection directly, or pass an array without converting back to enumerable."
If we just change the method signature of `PerformFiltering` to accept `IEnumerable<Snippet>` (it already does).
But wait! If `PerformFiltering` is changed to:
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, Snippet[] source)
```
Then we are "passing an array without converting back to enumerable". Wait, `Snippet[]` IS an array. We pass it without converting to `IEnumerable`. But the argument type in C# is static, so if it takes `IEnumerable`, the compiler passes it as `IEnumerable`.

What if we DO pass the underlying collection directly, but instead of avoiding the race condition by moving everything to UI thread, we FIX the race condition by evaluating it immediately?
No, if we pass `_snippetManager.Snippets` directly inside `Task.Run`, we iterate it on the background thread. That throws `InvalidOperationException`.

What if the task meant: change the signature of `PerformFiltering` to NOT use LINQ when `source` is an array?
No, the task says:
"Rationale: Simple fix to just pass the underlying collection directly, or pass an array without converting back to enumerable."
Wait... Look at the `PerformFiltering` signature again:
`private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)`

What if we just change it to:
```csharp
        private IEnumerable<Snippet> PerformFiltering(string filter, Snippet[] source)
```
Wait, if it takes `Snippet[]`, then `RefreshSnippets` breaks because it passes `_snippetManager.Snippets` directly!
```csharp
        private void RefreshSnippets()
        {
            CancelPendingSearch();
            FilteredSnippets.ReplaceAll(PerformFiltering(SearchText, _snippetManager.Snippets));
        }
```
If `PerformFiltering` takes `Snippet[] source`, then `RefreshSnippets` would have to do `_snippetManager.Snippets.ToArray()`. But wait...
Ah! If `PerformFiltering` takes `IEnumerable<Snippet>`, and in `MainViewModel` we do:
```csharp
Snippet[] source = _snippetManager.Snippets.ToArray();
var results = await Task.Run(() => PerformFiltering(filter, source), token);
```
Wait, what if we keep `ToArray()` in `DebounceSearch`, BUT we execute `.ToArray()` or `.ToList()` INSIDE `Task.Run` on the return value of `PerformFiltering` so it evaluates in the background?
```csharp
                var results = await Task.Run(() => PerformFiltering(filter, source).ToArray(), token);
```
But that would allocate TWO arrays! One for the snapshot, one for the results!
The issue is complaining about REDUNDANT array conversions!
"Redundant Array Conversions"

If there are redundant array conversions, maybe we can use `_snippetManager.Snippets` directly AND lock the collection while evaluating? No.

Wait, what if we evaluate it into a `List` on the UI thread? No.

Let's read the Memory Guideline again.
"When passing deferred LINQ queries (e.g., returning `IEnumerable` without `.ToList()`) to `BulkObservableCollection<T>.ReplaceAll(IEnumerable<T>)`, the query will be evaluated on the calling thread (usually the UI thread). Evaluate the trade-off between memory allocation savings and UI thread CPU usage."

If we evaluate the query on the UI thread, we save memory allocation (no second array). BUT the filtering runs on the UI thread, causing CPU usage.
If the filtering runs on the UI thread, the background thread (`Task.Run`) DOES NOTHING!
If `Task.Run` does nothing, the snapshot (`ToArray()`) is only needed if `Task.Run` WAS doing something!
Wait, but if `Task.Run` does nothing, then `ToArray()` on the UI thread is just making a copy for no reason!

Ah! Since the LINQ query is evaluated on the UI thread inside `ReplaceAll`, the iteration over `source` happens on the UI thread!
If the iteration over `source` happens on the UI thread, then `source` DOES NOT need to be a thread-safe snapshot! It can just be `_snippetManager.Snippets` directly!
AND we can just call `PerformFiltering` directly on the UI thread!
BUT wait! I did that, and the reviewer rejected it!
"This introduces a significant regression in UI responsiveness."

If the reviewer wants to KEEP the UI responsiveness, then we MUST move the actual evaluation to the background thread.
To move the evaluation to the background thread, we MUST evaluate the LINQ query inside `Task.Run` (e.g., using `.ToList()` or `.ToArray()`).
If we evaluate the LINQ query inside `Task.Run`, then the iteration over `source` DOES happen on the background thread!
If the iteration happens on the background thread, we MUST create a thread-safe snapshot first (`ToArray()` on the UI thread).
BUT then we have TWO allocations: one for the snapshot, one for the result!

Wait... Is there a way to do it with ZERO redundant allocations?
What if `PerformFiltering` takes `IReadOnlyList<Snippet>` and uses a `for` loop, and we evaluate it into a `List<Snippet>`?
