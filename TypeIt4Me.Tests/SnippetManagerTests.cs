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
            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, json);

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

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
            finally
            {
                if (File.Exists(importFilePath)) File.Delete(importFilePath);
            }
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

            string importFilePath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(importFilePath, encrypted);

                // Act
                bool result = await _manager.ImportSnippetsAsync(importFilePath, pin);

                // Assert
                Assert.True(result);
                Assert.Single(_manager.Snippets);
                Assert.Equal("enc1", _manager.Snippets[0].Name);
                Assert.Equal("secret1", _manager.Snippets[0].Content);
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

                // Set the session PIN on the manager
                _manager.SetPin(sessionPin);

                // Act - import without explicit PIN
                bool result = await _manager.ImportSnippetsAsync(importFilePath);

                // Assert
                Assert.True(result);
                Assert.Single(_manager.Snippets);
                Assert.Equal("enc2", _manager.Snippets[0].Name);
                Assert.Equal("secret2", _manager.Snippets[0].Content);
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
    }
}
