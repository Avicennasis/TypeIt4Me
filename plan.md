1. **Fix Vulnerability in `Services/CryptoService.cs`:**
   - Modify the `SecureStringToCharArray` method in `Services/CryptoService.cs` to prevent the string from sitting around in memory without being cleared.
   - Use `GC.AllocateUninitializedArray<char>(secureString.Length, pinned: true)` to ensure that the unmanaged memory backing the array cannot be relocated by the Garbage Collector, making sure when we clear the array, the memory is definitely zeroed out.
   - We should use `pinned: true` so the garbage collector does not move the `char[]` around in memory and leave copies.
   - Verify that this is supported in .NET 8 (the framework used by this project).
2. **Review Codebase for Calls to `SecureStringToCharArray`:**
   - I have checked `App.xaml.cs` and seen that `pinChars` returned from this method is appropriately cleared using `Array.Clear` inside a `finally` block everywhere it is called. No modifications are necessary in `App.xaml.cs`.
3. **Execute Pre-commit instructions:**
   - Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.
   - Execute `dotnet test` (via `run_in_bash_session`) to ensure no regressions. The test might fail on Linux due to WPF native issues, but I will do it.
4. **Submit PR:**
   - Push and submit with proper title and description.
