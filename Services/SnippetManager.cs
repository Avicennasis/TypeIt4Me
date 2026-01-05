using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        private string? _currentPin = ""; // Store PIN in memory for crypto operations

        public ObservableCollection<Snippet> Snippets { get; private set; } = new ObservableCollection<Snippet>();

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
                            string decrypted = CryptoService.Decrypt(content, _currentPin);
                            if (!string.IsNullOrEmpty(decrypted))
                            {
                                loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                            }
                        }
                    }

                    if (loaded != null)
                    {
                        Snippets.Clear();
                        foreach (var snippet in loaded)
                        {
                            Snippets.Add(snippet);
                        }
                    }
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading snippets: {ex.Message}");
            }
        }

        public async Task SaveSnippetsAsync()
        {
            try
            {
                string path = GetFilePath();
                string tempPath = path + ".tmp";
                
                await _fileLock.WaitAsync();
                
                string json = JsonSerializer.Serialize(Snippets);
                
                if (!string.IsNullOrEmpty(_currentPin))
                {
                    // Encrypt
                    string encrypted = CryptoService.Encrypt(json, _currentPin);
                    await File.WriteAllTextAsync(tempPath, encrypted);
                }
                else
                {
                    // Plain Text
                    await File.WriteAllTextAsync(tempPath, json);
                }
                
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving snippets: {ex.Message}");
            }
            finally
            {
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
                         string decrypted = CryptoService.Decrypt(content, importPin);
                         if (!string.IsNullOrEmpty(decrypted))
                         {
                             try { loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted); } catch { }
                         }
                    }
                    
                    if (loaded == null && !string.IsNullOrEmpty(_currentPin))
                    {
                        string decrypted = CryptoService.Decrypt(content, _currentPin);
                        if (!string.IsNullOrEmpty(decrypted))
                        {
                             try { loaded = JsonSerializer.Deserialize<List<Snippet>>(decrypted); } catch { }
                        }
                    }
                }

                if (loaded != null)
                {
                     foreach (var snippet in loaded)
                     {
                         snippet.Id = Guid.NewGuid();
                         Snippets.Add(snippet);
                     }
                     await SaveSnippetsAsync();
                     return true;
                }
                return false; // Failed to decrypt or deserialize
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing snippets: {ex.Message}");
                return false;
            }
        }

        public void AddSnippet(Snippet snippet)
        {
            Snippets.Add(snippet);
            _ = SaveSnippetsAsync();
        }

        public void RemoveSnippet(Snippet snippet)
        {
            Snippets.Remove(snippet);
            _ = SaveSnippetsAsync();
        }
    }
}
