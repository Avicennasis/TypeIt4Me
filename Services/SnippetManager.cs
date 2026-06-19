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
    public class SnippetManager
    {
        private readonly ILogger _logger;
        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        private string? _currentPin = ""; // Store PIN in memory for crypto operations

        public BulkObservableCollection<Snippet> Snippets { get; private set; } = new BulkObservableCollection<Snippet>();

        public SnippetManager(ILogger logger)
        {
            _logger = logger;
        }

        public void SetPin(string pin)
        {
            _currentPin = pin;
        }

        private string GetFilePath()
        {
            return Constants.GetAppDataPath(Constants.SnippetsFileName);
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
                    
                    // Try to deserialize as plain text first (migration path or no PIN)
                    // If it fails, try decrypting if we have a PIN.
                    
                    List<Snippet>? loaded = null;
                    try 
                    {
                         loaded = JsonSerializer.Deserialize<List<Snippet>>(content);
                    }
                    catch
                    {
                        // Not plain text JSON. Try decrypting.
                        if (!string.IsNullOrEmpty(_currentPin))
                        {
                            string decrypted = await Task.Run(() => CryptoService.Decrypt(content, _currentPin));
                            if (!string.IsNullOrEmpty(decrypted))
                            {
                                loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                            }
                        }
                    }

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

                if (!string.IsNullOrEmpty(_currentPin))
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
                        Debug.WriteLine($"Failed to delete temporary file {tempPath}: {ex.Message}");
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
                 
                 if (!string.IsNullOrEmpty(_currentPin))
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

        public async Task<bool> ImportSnippetsAsync(string filePath, string? importPin = null)
        {
            try
            {
                string content = await File.ReadAllTextAsync(filePath);
                List<Snippet>? loaded = null;
                
                 try 
                {
                     // Try plain text first
                     loaded = JsonSerializer.Deserialize<List<Snippet>>(content);
                }
                catch
                {
                    // Decrypt logic
                    // 1. Try provided importPin if any
                    // 2. Try _currentPin (active session PIN)
                    
                    if (!string.IsNullOrEmpty(importPin))
                    {
                         string decrypted = await Task.Run(() => CryptoService.Decrypt(content, importPin));
                         if (!string.IsNullOrEmpty(decrypted))
                         {
                             try
                             {
                                 loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                             }
                             catch (Exception ex)
                             {
                                 Debug.WriteLine($"Error deserializing imported snippets with provided PIN: {ex.Message}");
                             }
                         }
                    }
                    
                    if (loaded == null && !string.IsNullOrEmpty(_currentPin))
                    {
                        string decrypted = await Task.Run(() => CryptoService.Decrypt(content, _currentPin));
                        if (!string.IsNullOrEmpty(decrypted))
                        {
                             try
                             {
                                 loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                             }
                             catch (Exception ex)
                             {
                                 Debug.WriteLine($"Error deserializing imported snippets with session PIN: {ex.Message}");
                             }
                        }
                    }
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
