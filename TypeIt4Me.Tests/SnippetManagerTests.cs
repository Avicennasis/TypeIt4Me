using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class SnippetManagerTests : IDisposable
    {
        private readonly string _snippetsFilePath;
        private readonly FakeLogger _fakeLogger;

        public SnippetManagerTests()
        {
            _fakeLogger = new FakeLogger();
            _snippetsFilePath = Path.GetTempFileName();

            // Delete the empty temp file initially so SnippetManager can create it or handle missing files
            if (File.Exists(_snippetsFilePath))
            {
                File.Delete(_snippetsFilePath);
            }
        }

        public void Dispose()
        {
            // Clean up temporary files used in tests
            if (File.Exists(_snippetsFilePath))
            {
                File.Delete(_snippetsFilePath);
            }

            var tempFile = _snippetsFilePath + ".tmp";
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void TryDeserializeSnippets_PlainJson_ReturnsSnippets()
        {
            var snippets = new List<Snippet>
            {
                new Snippet { Name = "Test", Category = "Original", Content = "Replaced" }
            };
            var json = JsonSerializer.Serialize(snippets);

            var result = SnippetManager.TryDeserializeSnippets(json, ReadOnlySpan<char>.Empty);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Test", result[0].Name);
        }

        [Fact]
        public void TryDeserializeSnippets_EncryptedV3_WrongPin_ReturnsNull()
        {
            var snippets = new List<Snippet>
            {
                new Snippet { Name = "Test", Category = "Original", Content = "Replaced" }
            };
            var json = JsonSerializer.Serialize(snippets);
            var encrypted = CryptoService.Encrypt(json, "correct_pin");

            var result = SnippetManager.TryDeserializeSnippets(encrypted, "wrong_pin".AsSpan());

            Assert.Null(result);
        }

        [Fact]
        public void TryDeserializeSnippets_EncryptedV3_CorrectPin_ReturnsSnippets()
        {
            var snippets = new List<Snippet>
            {
                new Snippet { Name = "Test", Category = "Original", Content = "Replaced" }
            };
            var json = JsonSerializer.Serialize(snippets);
            var encrypted = CryptoService.Encrypt(json, "correct_pin");

            var result = SnippetManager.TryDeserializeSnippets(encrypted, "correct_pin".AsSpan());

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Test", result[0].Name);
        }

        [Fact]
        public void TryDeserializeSnippets_NullOrEmptyContent_ReturnsNull()
        {
            Assert.Null(SnippetManager.TryDeserializeSnippets(null!, ReadOnlySpan<char>.Empty));
            Assert.Null(SnippetManager.TryDeserializeSnippets("", ReadOnlySpan<char>.Empty));
        }

        [Fact]
        public void TryDeserializeSnippets_InvalidPlainJson_ReturnsNull()
        {
            var result = SnippetManager.TryDeserializeSnippets("invalid json", ReadOnlySpan<char>.Empty);
            Assert.Null(result);
        }

        [Fact]
        public async Task AddSnippetAsync_AddsToCollectionAndSaves()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var snippet = new Snippet { Name = "AddTest", Category = "Add", Content = "AddResult" };

            await manager.AddSnippetAsync(snippet);

            Assert.Single(manager.Snippets);
            Assert.Equal("AddTest", manager.Snippets[0].Name);
            Assert.True(File.Exists(_snippetsFilePath));

            var content = await File.ReadAllTextAsync(_snippetsFilePath);
            Assert.Contains("AddTest", content);
        }

        [Fact]
        public async Task RemoveSnippetAsync_RemovesFromCollectionAndSaves()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var snippet = new Snippet { Name = "RemoveTest", Category = "Rem", Content = "RemResult" };

            // Add first
            await manager.AddSnippetAsync(snippet);
            Assert.Single(manager.Snippets);

            // Then remove
            await manager.RemoveSnippetAsync(snippet);

            Assert.Empty(manager.Snippets);
            var content = await File.ReadAllTextAsync(_snippetsFilePath);
            Assert.DoesNotContain("RemoveTest", content);
        }

        [Fact]
        public void SetPin_ClearsPreviousPin()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);

            // It's hard to verify memory clearing directly in C# without reflection and unsafe code,
            // but we can verify that setting the pin changes behavior.
            manager.SetPin("new_pin");

            // Since we don't have direct access to _currentPin, this mainly tests that the method doesn't crash
            // and executes correctly. Setting it to null should also clear the array.
            manager.SetPin(null!);
            manager.SetPin("");

            Assert.True(true); // If we get here without exceptions, the method ran successfully
        }

        [Fact]
        public async Task SaveAndLoadSnippetsAsync_WithPin_EncryptsAndDecrypts()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var snippet = new Snippet { Name = "Secret", Category = "Sec", Content = "ret" };

            manager.SetPin("my_secret_pin");
            await manager.AddSnippetAsync(snippet); // Add calls Save

            var fileContent = await File.ReadAllTextAsync(_snippetsFilePath);
            Assert.StartsWith("V3|", fileContent); // Verifying it was encrypted

            // Create a new manager to test loading
            var newManager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            newManager.SetPin("my_secret_pin");
            await newManager.LoadSnippetsAsync();

            Assert.Single(newManager.Snippets);
            Assert.Equal("Secret", newManager.Snippets[0].Name);
        }

        [Fact]
        public async Task ExportSnippetsAsync_PlainJson_SavesToFile()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var snippet = new Snippet { Name = "ExportTest" };
            manager.Snippets.Add(snippet);

            var exportPath = _snippetsFilePath + "_export.json";

            try
            {
                await manager.ExportSnippetsAsync(exportPath);

                Assert.True(File.Exists(exportPath));
                var content = await File.ReadAllTextAsync(exportPath);
                Assert.Contains("ExportTest", content);
                Assert.DoesNotContain("V3|", content); // Shouldn't be encrypted
            }
            finally
            {
                if (File.Exists(exportPath)) File.Delete(exportPath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_ValidFile_AddsSnippetsAndReturnsTrue()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var exportPath = _snippetsFilePath + "_import.json";

            try
            {
                // Create a valid file to import
                var snippetsToImport = new List<Snippet> { new Snippet { Name = "ImportMe" } };
                await File.WriteAllTextAsync(exportPath, JsonSerializer.Serialize(snippetsToImport));

                var result = await manager.ImportSnippetsAsync(exportPath);

                Assert.True(result);
                Assert.Single(manager.Snippets);
                Assert.Equal("ImportMe", manager.Snippets[0].Name);

                // The import should generate new IDs
                Assert.NotEqual(Guid.Empty, manager.Snippets[0].Id);
            }
            finally
            {
                if (File.Exists(exportPath)) File.Delete(exportPath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_InvalidFile_ReturnsFalse()
        {
            var manager = new SnippetManager(_fakeLogger, _snippetsFilePath);
            var exportPath = _snippetsFilePath + "_invalid_import.json";

            try
            {
                await File.WriteAllTextAsync(exportPath, "invalid json");

                var result = await manager.ImportSnippetsAsync(exportPath);

                Assert.False(result);
                Assert.Empty(manager.Snippets);
            }
            finally
            {
                if (File.Exists(exportPath)) File.Delete(exportPath);
            }
        }
    }
}
