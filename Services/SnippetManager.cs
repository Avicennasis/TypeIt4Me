using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.Services
{
    /// <summary>
    /// Manages the collection of snippets, including loading, saving, and thread-safe access.
    /// Handles encryption if a PIN is provided.
    /// </summary>
    public class SnippetManager : ISnippetManager
    {
        private readonly ILogger _logger;
        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        private char[]? _currentPin; // Store PIN in memory (mutable char[] so it can be cleared)

        public BulkObservableCollection<Snippet> Snippets { get; private set; } = new BulkObservableCollection<Snippet>();

        public SnippetManager(ILogger logger)
        {
            _logger = logger;
        }

        public void SetPin(ReadOnlySpan<char> pin)
        {
            // Zero the previous PIN before replacing it so it doesn't linger in memory.
            if (_currentPin != null)
            {
                Array.Clear(_currentPin, 0, _currentPin.Length);
            }
            _currentPin = pin.IsEmpty ? null : pin.ToArray();
        }

        protected virtual string GetFilePath()
        {
            return Constants.GetAppDataPath(Constants.SnippetsFileName);
        }

        /// <summary>
        /// Deserializes snippet file content, transparently handling both plain JSON and
        /// V3-encrypted payloads. Encryption is detected by the "V3|" prefix that
        /// <see cref="CryptoService.Encrypt"/> always writes, rather than by catching a
        /// deserialization exception. Returns null when the content cannot be read with the
        /// supplied <paramref name="pin"/>. Pure and side-effect-free for testability.
        /// </summary>
        public static List<Snippet>? TryDeserializeSnippets(string content, ReadOnlySpan<char> pin)
        {
            if (string.IsNullOrEmpty(content)) return null;

            if (content.StartsWith("V3|"))
            {
                if (pin.IsEmpty) return null;
                try
                {
                    string decrypted = CryptoService.Decrypt(content, pin);
                    if (!string.IsNullOrEmpty(decrypted))
                    {
                        return JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Decrypt/deserialize of encrypted snippets failed: {ex.GetType().FullName}");
                }
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<List<Snippet>>(content);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Plain-text snippet deserialization failed: {ex.GetType().FullName}");
                return null;
            }
        }

        public async Task LoadSnippetsAsync()
        {
            string path = GetFilePath();
            if (!File.Exists(path)) return;

            try
            {
                await _fileLock.WaitAsync();
                try
                {
                    string content = await File.ReadAllTextAsync(path);

                    // Detect plain vs V3-encrypted by prefix and deserialize off the UI thread.
                    var loaded = await Task.Run(() => TryDeserializeSnippets(content, _currentPin));

                    if (loaded != null)
                    {
                        Snippets.ReplaceAll(loaded);
                    }
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error loading snippets", ex);
            }
        }

        public async Task SaveSnippetsAsync()
        {
            string? tempPath = null;
            try
            {
                string path = GetFilePath();
                tempPath = path + ".tmp";

                await _fileLock.WaitAsync();

                string json = JsonSerializer.Serialize(Snippets);

                if (_currentPin != null && _currentPin.Length > 0)
                {
                    // Encrypt with V3 (AES-256 + HMAC)
                    string encrypted = await Task.Run(() => CryptoService.Encrypt(json, _currentPin));
                    await File.WriteAllTextAsync(tempPath, encrypted);
                }
                else
                {
                    // Plain Text (no encryption)
                    await File.WriteAllTextAsync(tempPath, json);
                }

                // Atomic move operation
                File.Move(tempPath, path, overwrite: true);
                tempPath = null; // Successfully moved, don't delete
            }
            catch (Exception ex)
            {
                _logger.LogError("Error saving snippets", ex);
                throw; // Re-throw to allow caller to handle
            }
            finally
            {
                // Clean up temp file if it still exists (operation failed)
                if (tempPath != null && File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete temporary file {tempPath}: {ex.GetType().FullName}");
                    }
                }

                _fileLock.Release();
            }
        }
        
        public async Task ExportSnippetsAsync(string filePath)
        {
             try 
             {
                 await _fileLock.WaitAsync();
                 string json = JsonSerializer.Serialize(Snippets);
                 
                 if (_currentPin != null && _currentPin.Length > 0)
                 {
                     string encrypted = CryptoService.Encrypt(json, _currentPin);
                     await File.WriteAllTextAsync(filePath, encrypted);
                 }
                 else
                 {
                     await File.WriteAllTextAsync(filePath, json);
                 }
             }
             finally
             {
                 _fileLock.Release();
             }
        }

        public async Task<bool> ImportSnippetsAsync(string filePath, char[]? importPin = null)
        {
            try
            {
                string content = await File.ReadAllTextAsync(filePath);

                // Plain JSON, or V3-encrypted: try the supplied import PIN first, then fall
                // back to the active session PIN. Detection is prefix-based (see
                // TryDeserializeSnippets) and runs off the UI thread.
                List<Snippet>? loaded = await Task.Run(() => TryDeserializeSnippets(content, importPin));
                if (loaded == null && _currentPin != null && _currentPin.Length > 0)
                {
                    loaded = await Task.Run(() => TryDeserializeSnippets(content, _currentPin));
                }

                if (loaded != null)
                {
                     foreach (var snippet in loaded)
                         snippet.Id = Guid.NewGuid();
                     Snippets.AddRange(loaded);
                     await SaveSnippetsAsync();
                     return true;
                }
                return false; // Failed to decrypt or deserialize
            }
            catch (Exception ex)
            {
                _logger.LogError("Error importing snippets", ex);
                return false;
            }
        }

        public async Task AddSnippetAsync(Snippet snippet)
        {
            Snippets.Add(snippet);
            await SaveSnippetsAsync();
        }

        public async Task RemoveSnippetAsync(Snippet snippet)
        {
            Snippets.Remove(snippet);
            await SaveSnippetsAsync();
        }

        // Keep synchronous versions for compatibility, but log errors
        public void AddSnippet(Snippet snippet)
        {
            Snippets.Add(snippet);
            Task.Run(async () =>
            {
                try
                {
                    await SaveSnippetsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Background save failed after AddSnippet", ex);
                }
            });
        }

        public void RemoveSnippet(Snippet snippet)
        {
            Snippets.Remove(snippet);
            Task.Run(async () =>
            {
                try
                {
                    await SaveSnippetsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Background save failed after RemoveSnippet", ex);
                }
            });
        }
    }
}
