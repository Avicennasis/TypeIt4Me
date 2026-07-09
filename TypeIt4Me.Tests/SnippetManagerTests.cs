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
        private readonly string _importFile;
        private readonly FakeLogger _logger;
        private readonly TestableSnippetManager _manager;

        public SnippetManagerTests()
        {
            _tempFile = Path.GetTempFileName();
            _importFile = Path.GetTempFileName();
            _logger = new FakeLogger();
            _manager = new TestableSnippetManager(_logger, _tempFile);
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
            if (File.Exists(_importFile))
            {
                File.Delete(_importFile);
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

            // Act
            await manager.ExportSnippetsAsync(_importFile);

            // Assert
            string fileContent = await File.ReadAllTextAsync(_importFile);
            var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);

            Assert.NotNull(deserialized);
            Assert.Single(deserialized);
            Assert.Equal("Test", deserialized[0].Name);
            Assert.Equal("Content", deserialized[0].Content);
            Assert.Equal(snippet.Id, deserialized[0].Id);

            // Ensure it's not encrypted (doesn't start with V3|)
            Assert.False(fileContent.StartsWith("V3|"));

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

            // Act
            await manager.ExportSnippetsAsync(_importFile);

            // Assert
            string fileContent = await File.ReadAllTextAsync(_importFile);

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

        [Fact]
        public async Task ExportSnippetsAsync_ExistingFile_OverwritesSuccessfully()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            var snippet = new Snippet { Name = "OverwriteTest", Content = "NewData" };
            manager.Snippets.Add(snippet);

            await File.WriteAllTextAsync(_importFile, "Old Garbage Data");
            // Act
            await manager.ExportSnippetsAsync(_importFile);

            // Assert
            string fileContent = await File.ReadAllTextAsync(_importFile);
            var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);

            Assert.NotNull(deserialized);
            Assert.Single(deserialized);
            Assert.Equal("OverwriteTest", deserialized[0].Name);
            Assert.Equal("NewData", deserialized[0].Content);

            Assert.DoesNotContain("Old Garbage Data", fileContent);

        }


        [Fact]
        public async Task ExportSnippetsAsync_FileAccessError_ThrowsExceptionAndReleasesLock()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            var snippet = new Snippet { Name = "Test", Content = "Content", Id = Guid.NewGuid() };
            manager.Snippets.Add(snippet);

            // Lock the file to cause an IOException
            using (var stream = new FileStream(_importFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                // Act & Assert
                await Assert.ThrowsAnyAsync<IOException>(() => manager.ExportSnippetsAsync(_importFile));
            }

            // Verify lock was released by trying again
            await manager.ExportSnippetsAsync(_importFile);

            string fileContent = await File.ReadAllTextAsync(_importFile);
            var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);
            Assert.NotNull(deserialized);
            Assert.Single(deserialized);

        }

        [Fact]
        public async Task ExportSnippetsAsync_EmptySnippets_ExportsEmptyJsonArray()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new SnippetManager(logger);

            // Act
            await manager.ExportSnippetsAsync(_importFile);

            // Assert
            string fileContent = await File.ReadAllTextAsync(_importFile);
            var deserialized = JsonSerializer.Deserialize<List<Snippet>>(fileContent);

            Assert.NotNull(deserialized);
            Assert.Empty(deserialized);

        }

        // ===================================================================
        // Import tests (6 methods)
        // ===================================================================

        [Fact]
        public async Task ImportSnippetsAsync_PlainJson_Success()
        {
            // Arrange
            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "test1", Content = "content1" },
                new Snippet { Id = Guid.NewGuid(), Name = "test2", Content = "content2" }
            };
            string json = JsonSerializer.Serialize(snippets);
            await File.WriteAllTextAsync(_importFile, json);

            // Act
            bool result = await _manager.ImportSnippetsAsync(_importFile);

            // Assert
            Assert.True(result);
            Assert.Equal(2, _manager.Snippets.Count);

            // Verify IDs are regenerated
            Assert.NotEqual(snippets[0].Id, _manager.Snippets[0].Id);
            Assert.NotEqual(snippets[1].Id, _manager.Snippets[1].Id);

            // Verify content
            Assert.Equal("test1", _manager.Snippets[0].Name);
            Assert.Equal("content1", _manager.Snippets[0].Content);

            // Verify saved to test file path (app data)
            string savedContent = await File.ReadAllTextAsync(_tempFile);
            var savedSnippets = JsonSerializer.Deserialize<List<Snippet>>(savedContent);
            Assert.NotNull(savedSnippets);
            Assert.Equal(2, savedSnippets.Count);

        }

        [Fact]
        public async Task ImportSnippetsAsync_EncryptedJson_ExplicitPin_Success()
        {
            // Arrange
            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "enc1", Content = "secret1" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string pin = "mysecretpin";
            string encrypted = CryptoService.Encrypt(json, pin);

            await File.WriteAllTextAsync(_importFile, encrypted);

            // Act
            bool result = await _manager.ImportSnippetsAsync(_importFile, pin.ToCharArray());

            // Assert
            Assert.True(result);
            Assert.Single(_manager.Snippets);
            Assert.Equal("enc1", _manager.Snippets[0].Name);
            Assert.Equal("secret1", _manager.Snippets[0].Content);

            // Verify saved to test file path (app data)
            string savedContent = await File.ReadAllTextAsync(_tempFile);
            var savedSnippets = JsonSerializer.Deserialize<List<Snippet>>(savedContent);
            Assert.NotNull(savedSnippets);
            Assert.Single(savedSnippets);

        }

        [Fact]
        public async Task ImportSnippetsAsync_EncryptedJson_SessionPin_Success()
        {
            // Arrange
            var snippets = new List<Snippet>
            {
                new Snippet { Id = Guid.NewGuid(), Name = "enc2", Content = "secret2" }
            };
            string json = JsonSerializer.Serialize(snippets);
            string sessionPin = "sessionpin";
            string encrypted = CryptoService.Encrypt(json, sessionPin);

            await File.WriteAllTextAsync(_importFile, encrypted);

            // Set the session PIN on the manager
            _manager.SetPin(sessionPin.AsSpan());

            // Act - import without explicit PIN
            bool result = await _manager.ImportSnippetsAsync(_importFile);

            // Assert
            Assert.True(result);
            Assert.Single(_manager.Snippets);
            Assert.Equal("enc2", _manager.Snippets[0].Name);
            Assert.Equal("secret2", _manager.Snippets[0].Content);

            // Verify saved to test file path (app data) as encrypted
            string savedContent = await File.ReadAllTextAsync(_tempFile);
            Assert.StartsWith("V3|", savedContent);

        }

        [Fact]
        public async Task ImportSnippetsAsync_InvalidJson_ReturnsFalse()
        {
            // Arrange
            await File.WriteAllTextAsync(_importFile, "{ not valid json ]");

            // Act
            bool result = await _manager.ImportSnippetsAsync(_importFile);

            // Assert
            Assert.False(result);
            Assert.Empty(_manager.Snippets);

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

            await File.WriteAllTextAsync(_importFile, encrypted);

            // Act - try with wrong PIN
            bool result = await _manager.ImportSnippetsAsync(_importFile, "wrongpin".ToCharArray());

            // Assert
            Assert.False(result);
            Assert.Empty(_manager.Snippets);

        }

        [Fact]
        public async Task ImportSnippetsAsync_MissingFile_ReturnsFalse()
        {
            // Act
            bool result = await _manager.ImportSnippetsAsync("non_existent_file.json");

            // Assert
            Assert.False(result);
            Assert.Empty(_manager.Snippets);
            Assert.Contains(_logger.ErrorLogs, log => log.Message == "Error importing snippets");
            Assert.NotNull(_logger.ErrorLogs[0].Exception);
            Assert.IsType<FileNotFoundException>(_logger.ErrorLogs[0].Exception);
        }

        [Fact]
        public async Task ImportSnippetsAsync_FileAccessError_LogsErrorAndReturnsFalse()
        {
            // Arrange
            await File.WriteAllTextAsync(_importFile, "[]");

            // Lock the target file so ImportSnippetsAsync fails
            using (var fs = new FileStream(_importFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = await _manager.ImportSnippetsAsync(_importFile);

                // Assert
                Assert.False(result);
                Assert.Empty(_manager.Snippets);
                Assert.Contains(_logger.ErrorLogs, log => log.Message == "Error importing snippets");
                Assert.NotNull(_logger.ErrorLogs[0].Exception);
                Assert.True(_logger.ErrorLogs[0].Exception is IOException || _logger.ErrorLogs[0].Exception is UnauthorizedAccessException);
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

            // Ensure the temporary file exists before trying to lock it
            await File.WriteAllTextAsync(_importFile, "[]");

            var snippetManager = new TestableSnippetManager(logger, _importFile);
            var snippet = new Snippet { Name = "Test" };

            var tcs = new TaskCompletionSource<bool>();

            logger.ErrorLogged += (msg, ex) =>
            {
                if (msg == "Background save failed after AddSnippet")
                {
                    tcs.TrySetResult(true);
                }
            };

            // Lock the target file so SaveSnippetsAsync fails
            using (var fs = new FileStream(_importFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                snippetManager.AddSnippet(snippet);

                // Assert
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
                Assert.Equal(tcs.Task, completedTask);
            }

            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after AddSnippet");

        }

        [Fact]
        public async Task RemoveSnippet_BackgroundSaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();

            // Ensure the temporary file exists before trying to lock it
            await File.WriteAllTextAsync(_importFile, "[]");

            var snippetManager = new TestableSnippetManager(logger, _importFile);
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

            // Lock the target file so SaveSnippetsAsync fails
            using (var fs = new FileStream(_importFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                snippetManager.RemoveSnippet(snippet);

                // Assert
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
                Assert.Equal(tcs.Task, completedTask);
            }

            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after RemoveSnippet");

        }
    }
}
