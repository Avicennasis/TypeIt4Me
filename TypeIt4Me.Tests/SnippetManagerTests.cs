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
        /// <summary>
        /// SnippetManager subclass that overrides GetFilePath for test isolation.
        /// Used by import tests (valid temp path) and background save error tests (invalid path).
        /// </summary>
        private class TestableSnippetManager : SnippetManager
        {
            private readonly string _testFilePath;

            public TestableSnippetManager(ILogger logger, string testFilePath) : base(logger)
            {
                _testFilePath = testFilePath;
            }

            protected override string GetFilePath()
            {
                return _testFilePath;
            }
        }

        private readonly string _tempFile;
        private readonly FakeLogger _logger;
        private readonly TestableSnippetManager _manager;

        public SnippetManagerTests()
        {
            _tempFile = Path.GetTempFileName();
            _logger = new FakeLogger();
            _manager = new TestableSnippetManager(_logger, _tempFile);
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        // ===================================================================
        // Export tests (3 methods)
        // ===================================================================

        [Fact]
        public async Task ExportSnippetsAsync_NoPin_ExportsPlaintextJson()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            var snippet = new Snippet { Name = "Test", Content = "Content", Id = Guid.NewGuid() };
            manager.Snippets.Add(snippet);

            string tempFile = Path.GetTempFileName();

            try
            {
                // Act
                await manager.ExportSnippetsAsync(tempFile);

                // Assert
                string fileContent = await File.ReadAllTextAsync(tempFile);
                var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);

                Assert.NotNull(deserialized);
                Assert.Single(deserialized);
                Assert.Equal("Test", deserialized[0].Name);
                Assert.Equal("Content", deserialized[0].Content);
                Assert.Equal(snippet.Id, deserialized[0].Id);

                // Ensure it's not encrypted (doesn't start with V3|)
                Assert.False(fileContent.StartsWith("V3|"));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExportSnippetsAsync_WithPin_ExportsEncryptedV3()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            var snippet = new Snippet { Name = "Secret", Content = "Confidential", Id = Guid.NewGuid() };
            manager.Snippets.Add(snippet);

            manager.SetPin("1234".AsSpan());

            string tempFile = Path.GetTempFileName();

            try
            {
                // Act
                await manager.ExportSnippetsAsync(tempFile);

                // Assert
                string fileContent = await File.ReadAllTextAsync(tempFile);

                // Should be encrypted
                Assert.True(fileContent.StartsWith("V3|"));

                // Decrypt manually
                string decrypted = CryptoService.Decrypt(fileContent, "1234".AsSpan());
                Assert.NotNull(decrypted);

                var deserialized = JsonSerializer.Deserialize<List<Snippet>>(decrypted);
                Assert.NotNull(deserialized);
                Assert.Single(deserialized);
                Assert.Equal("Secret", deserialized[0].Name);
                Assert.Equal("Confidential", deserialized[0].Content);
                Assert.Equal(snippet.Id, deserialized[0].Id);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExportSnippetsAsync_ExistingFile_OverwritesSuccessfully()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            var snippet = new Snippet { Name = "OverwriteTest", Content = "NewData" };
            manager.Snippets.Add(snippet);

            string tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "Old Garbage Data");

            try
            {
                // Act
                await manager.ExportSnippetsAsync(tempFile);

                // Assert
                string fileContent = await File.ReadAllTextAsync(tempFile);
                var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);

                Assert.NotNull(deserialized);
                Assert.Single(deserialized);
                Assert.Equal("OverwriteTest", deserialized[0].Name);
                Assert.Equal("NewData", deserialized[0].Content);

                Assert.DoesNotContain("Old Garbage Data", fileContent);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        // ===================================================================
        // Import tests (6 methods)
        // ===================================================================

        [Fact]
        public async Task ImportSnippetsAsync_PlainJson_Success()
        {
            // Arrange
            var existingSnippet = new Snippet { Id = Guid.NewGuid(), Name = "existing", Content = "oldContent" };
            _manager.Snippets.Add(existingSnippet); // Pre-populate

            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "test1", Content = "content1" },
                new Snippet { Id = Guid.NewGuid(), Name = "test2", Content = "content2" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, json);

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

                // Assert
                Assert.True(result);
                Assert.Equal(3, _manager.Snippets.Count); // 1 existing + 2 imported

                // Verify existing remains intact
                Assert.Equal("existing", _manager.Snippets[0].Name);

                // Verify IDs are regenerated for imported
                Assert.NotEqual(snippets[0].Id, _manager.Snippets[1].Id);
                Assert.NotEqual(snippets[1].Id, _manager.Snippets[2].Id);

                // Verify content
                Assert.Equal("test1", _manager.Snippets[1].Name);
                Assert.Equal("content1", _manager.Snippets[1].Content);

                // Verify saved to test file path (app data) in plain text
                string savedContent = await File.ReadAllTextAsync(_tempFile);
                var savedSnippets = JsonSerializer.Deserialize<List<Snippet>>(savedContent);
                Assert.NotNull(savedSnippets);
                Assert.Equal(3, savedSnippets.Count);
                Assert.Equal("existing", savedSnippets[0].Name);
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_EncryptedJson_ExplicitPin_Success()
        {
            // Arrange
            var existingSnippet = new Snippet { Id = Guid.NewGuid(), Name = "existing", Content = "oldContent" };
            _manager.Snippets.Add(existingSnippet); // Pre-populate

            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "enc1", Content = "secret1" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string pin = "mysecretpin";
            string encrypted = CryptoService.Encrypt(json, pin);

            // Note: Since _manager doesn't have a session PIN set, it will save in plain text.

            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, encrypted);

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath, pin);

                // Assert
                Assert.True(result);
                Assert.Equal(2, _manager.Snippets.Count);
                Assert.Equal("existing", _manager.Snippets[0].Name);
                Assert.Equal("enc1", _manager.Snippets[1].Name);
                Assert.Equal("secret1", _manager.Snippets[1].Content);

                // Verify saved to test file path in plain text (because session pin is not set)
                string savedContent = await File.ReadAllTextAsync(_tempFile);
                var savedSnippets = JsonSerializer.Deserialize<List<Snippet>>(savedContent);
                Assert.NotNull(savedSnippets);
                Assert.Equal(2, savedSnippets.Count);
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_EncryptedJson_SessionPin_Success()
        {
            // Arrange
            var existingSnippet = new Snippet { Id = Guid.NewGuid(), Name = "existing", Content = "oldContent" };
            _manager.Snippets.Add(existingSnippet);

            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "enc2", Content = "secret2" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string sessionPin = "sessionpin";
            string encrypted = CryptoService.Encrypt(json, sessionPin);

            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, encrypted);

                // Set the session PIN on the manager so it attempts to save encrypted
                _manager.SetPin(sessionPin.AsSpan());

                // Act - import without explicit PIN
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

                // Assert
                Assert.True(result);
                Assert.Equal(2, _manager.Snippets.Count);
                Assert.Equal("existing", _manager.Snippets[0].Name);
                Assert.Equal("enc2", _manager.Snippets[1].Name);
                Assert.Equal("secret2", _manager.Snippets[1].Content);

                // Verify saved file is encrypted
                string savedContent = await File.ReadAllTextAsync(_tempFile);
                // It should fail plain text deserialization
                Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<List<Snippet>>(savedContent));

                // Decrypt saved content to verify
                string decryptedContent = CryptoService.Decrypt(savedContent, sessionPin);
                var savedSnippets = JsonSerializer.Deserialize<List<Snippet>>(decryptedContent);
                Assert.NotNull(savedSnippets);
                Assert.Equal(2, savedSnippets.Count);
                Assert.Equal("existing", savedSnippets[0].Name);
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_InvalidJson_ReturnsFalse()
        {
            // Arrange
            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, "{ not valid json ]");

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

                // Assert
                Assert.False(result);
                Assert.Empty(_manager.Snippets);
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_EncryptedJson_WrongPin_ReturnsFalse()
        {
            // Arrange
            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "enc3", Content = "secret3" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string encrypted = CryptoService.Encrypt(json, "correctpin");

            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, encrypted);

                // Act - try with wrong PIN
                bool result = await _manager.ImportSnippetsAsync(importFilePath, "wrongpin");

                // Assert
                Assert.False(result);
                Assert.Empty(_manager.Snippets);
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        [Fact]
        public async Task ImportSnippetsAsync_MissingFile_ReturnsFalse()
        {
            // Act
            bool result = await _manager.ImportSnippetsAsync("non_existent_file.json");

            // Assert
            Assert.False(result);
            Assert.Empty(_manager.Snippets);
        }

        [Fact]
        public async Task ImportSnippetsAsync_FileReadFails_LogsError_ReturnsFalse()
        {
            // Arrange
            string importFilePath = Path.GetTempFileName();
            try
            {
                // Lock the file exclusively to simulate an access/read error
                using var lockStream = new FileStream(importFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

                // Assert
                Assert.False(result);
                Assert.Contains(_logger.ErrorLogs, log => log.Message == "Error importing snippets");
            }
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
        }

        // ===================================================================
        // Background save error tests (2 methods)
        // ===================================================================

        [Fact]
        public async Task AddSnippet_BackgroundSaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();
            var invalidPath = Path.Combine("InvalidDrive:\\", "nonexistent", "snippets.json");

            var snippetManager = new TestableSnippetManager(logger, invalidPath);
            var snippet = new Snippet { Name = "Test" };

            var tcs = new TaskCompletionSource<bool>();

            logger.ErrorLogged += (msg, ex) =>
            {
                if (msg == "Background save failed after AddSnippet")
                {
                    tcs.TrySetResult(true);
                }
            };

            // Act
            snippetManager.AddSnippet(snippet);

            // Assert
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.Equal(tcs.Task, completedTask);

            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after AddSnippet");
        }

        [Fact]
        public async Task RemoveSnippet_BackgroundSaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();
            var invalidPath = Path.Combine("InvalidDrive:\\", "nonexistent", "snippets.json");

            var snippetManager = new TestableSnippetManager(logger, invalidPath);
            var snippet = new Snippet { Name = "Test" };
            snippetManager.Snippets.Add(snippet); // Add directly to bypass AddSnippet's background task

            var tcs = new TaskCompletionSource<bool>();

            logger.ErrorLogged += (msg, ex) =>
            {
                if (msg == "Background save failed after RemoveSnippet")
                {
                    tcs.TrySetResult(true);
                }
            };

            // Act
            snippetManager.RemoveSnippet(snippet);

            // Assert
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.Equal(tcs.Task, completedTask);

            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after RemoveSnippet");
        }
    }
}
