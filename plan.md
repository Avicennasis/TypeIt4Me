1. **Change `IInputSender.cs` to use `ReadOnlySpan<char>`**
   - Update `SendInputBatch(string text)` to `SendInputBatch(ReadOnlySpan<char> text)`.
2. **Update `WindowsInputSender.cs`**
   - Change `SendInputBatch` signature to match the interface.
   - The method logic already iterates over `char c in text`, which will work identically with `ReadOnlySpan<char>`.
3. **Update `InputInjector.cs`**
   - In `TypePlainTextAsync`, replace `string batch = text.Substring(i, Math.Min(BatchSize, text.Length - i));` with `ReadOnlySpan<char> batch = text.AsSpan(i, Math.Min(BatchSize, text.Length - i));`.
4. **Update `MockInputSender.cs` in `TypeIt4Me.Tests/Fakes`**
   - Update `SendInputBatch` signature to match the interface.
   - Adjust logging since `text` will be a `ReadOnlySpan<char>`, e.g. `Log.Add($"SEND_BATCH:{text.ToString()}");`.
5. **Run the tests**
   - Ensure `dotnet test --no-restore` passes.
6. **Pre-commit checks**
   - Run `pre_commit_instructions` and follow testing, verification, and code review steps.
7. **Submit the PR**
   - Call `submit` with the appropriate branch name and description.
