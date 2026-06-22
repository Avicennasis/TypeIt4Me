using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    // A subclass of SnippetManager that overrides the file path to use a temporary file for tests.
    public class TestableSnippetManager : SnippetManager, IDisposable
    {
        private readonly string _tempFilePath;

        public TestableSnippetManager(ILogger logger, string tempFilePath) : base(logger)
        {
            _tempFilePath = tempFilePath;
        }

        protected override string GetFilePath()
        {
            return _tempFilePath;
        }

        public string TempFilePath => _tempFilePath;

        public void Dispose()
        {
            // Do not delete file here so it can be shared between instances if needed, or caller deletes
        }
    }

    public class SnippetManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _tempFilePath;

        public SnippetManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _tempFilePath = Path.Combine(_tempDir, Constants.SnippetsFileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public async Task AddSnippetAsync_AddsSnippetAndSavesToFile()
        {
            var logger = new FakeLogger();
            using var manager = new TestableSnippetManager(logger, _tempFilePath);

            var snippet = new Snippet { Name = "Test Snippet", Content = "Test Content" };
            await manager.AddSnippetAsync(snippet);

            Assert.Single(manager.Snippets);
            Assert.Equal("Test Snippet", manager.Snippets[0].Name);

            // Verify file was created
            Assert.True(File.Exists(manager.TempFilePath));

            // Read file content
            var fileContent = await File.ReadAllTextAsync(manager.TempFilePath);
            Assert.Contains("Test Content", fileContent);
        }

        [Fact]
        public async Task RemoveSnippetAsync_RemovesSnippetAndSavesToFile()
        {
            var logger = new FakeLogger();
            using var manager = new TestableSnippetManager(logger, _tempFilePath);

            var snippet = new Snippet { Name = "Test Snippet", Content = "Test Content" };
            await manager.AddSnippetAsync(snippet);
            Assert.Single(manager.Snippets);

            await manager.RemoveSnippetAsync(snippet);
            Assert.Empty(manager.Snippets);

            var fileContent = await File.ReadAllTextAsync(manager.TempFilePath);
            Assert.DoesNotContain("Test Content", fileContent);
        }

        [Fact]
        public async Task LoadSnippetsAsync_LoadsSnippetsFromFile()
        {
            var logger = new FakeLogger();
            using var manager1 = new TestableSnippetManager(logger, _tempFilePath);

            var snippet = new Snippet { Name = "Test Snippet", Content = "Test Content" };
            await manager1.AddSnippetAsync(snippet);

            using var manager2 = new TestableSnippetManager(logger, _tempFilePath);
            await manager2.LoadSnippetsAsync();

            Assert.Single(manager2.Snippets);
            Assert.Equal("Test Snippet", manager2.Snippets[0].Name);
            Assert.Equal("Test Content", manager2.Snippets[0].Content);
        }

        [Fact]
        public async Task SaveSnippetsAsync_WithPin_EncryptsData()
        {
            var logger = new FakeLogger();
            using var manager = new TestableSnippetManager(logger, _tempFilePath);

            manager.SetPin("mypassword");

            var snippet = new Snippet { Name = "Secret Snippet", Content = "Secret Content" };
            await manager.AddSnippetAsync(snippet);

            var fileContent = await File.ReadAllTextAsync(manager.TempFilePath);

            // File should be encrypted (starts with V3|) and should NOT contain the plain text
            Assert.StartsWith("V3|", fileContent);
            Assert.DoesNotContain("Secret Content", fileContent);
        }

        [Fact]
        public async Task LoadSnippetsAsync_WithCorrectPin_DecryptsData()
        {
            var logger = new FakeLogger();
            using var manager1 = new TestableSnippetManager(logger, _tempFilePath);

            manager1.SetPin("mypassword");
            var snippet = new Snippet { Name = "Secret Snippet", Content = "Secret Content" };
            await manager1.AddSnippetAsync(snippet);

            using var manager2 = new TestableSnippetManager(logger, _tempFilePath);
            manager2.SetPin("mypassword");
            await manager2.LoadSnippetsAsync();

            Assert.Single(manager2.Snippets);
            Assert.Equal("Secret Content", manager2.Snippets[0].Content);
        }

        [Fact]
        public async Task LoadSnippetsAsync_WithIncorrectPin_DoesNotLoadData()
        {
            var logger = new FakeLogger();
            using var manager1 = new TestableSnippetManager(logger, _tempFilePath);

            manager1.SetPin("mypassword");
            var snippet = new Snippet { Name = "Secret Snippet", Content = "Secret Content" };
            await manager1.AddSnippetAsync(snippet);

            using var manager2 = new TestableSnippetManager(logger, _tempFilePath);
            manager2.SetPin("wrongpassword");
            await manager2.LoadSnippetsAsync();

            Assert.Empty(manager2.Snippets);
        }

        [Fact]
        public async Task ExportSnippetsAsync_ExportsToSpecifiedFile()
        {
            var logger = new FakeLogger();
            using var manager = new TestableSnippetManager(logger, _tempFilePath);

            var snippet = new Snippet { Name = "Export Snippet", Content = "Export Content" };
            await manager.AddSnippetAsync(snippet);

            string exportPath = Path.Combine(_tempDir, "export.json");
            await manager.ExportSnippetsAsync(exportPath);

            Assert.True(File.Exists(exportPath));
            var fileContent = await File.ReadAllTextAsync(exportPath);
            Assert.Contains("Export Content", fileContent);
        }

        [Fact]
        public async Task ImportSnippetsAsync_ImportsFromSpecifiedFile()
        {
            var logger = new FakeLogger();
            using var manager1 = new TestableSnippetManager(logger, _tempFilePath);

            var snippet = new Snippet { Name = "Import Snippet", Content = "Import Content" };
            await manager1.AddSnippetAsync(snippet);

            string importPath = Path.Combine(_tempDir, "import.json");
            File.Copy(manager1.TempFilePath, importPath);

            using var manager2 = new TestableSnippetManager(logger, Path.Combine(_tempDir, "other.json"));
            bool success = await manager2.ImportSnippetsAsync(importPath);

            Assert.True(success);
            Assert.Single(manager2.Snippets);
            Assert.Equal("Import Content", manager2.Snippets[0].Content);
        }

        [Fact]
        public async Task SetPin_EmptyString_ClearsPin()
        {
            var logger = new FakeLogger();
            using var manager = new TestableSnippetManager(logger, _tempFilePath);

            manager.SetPin("mypassword");
            manager.SetPin(""); // Should clear pin

            // To verify it's cleared, we can save a snippet and check if it's plain text
            var snippet = new Snippet { Name = "Plain Snippet", Content = "Plain Content" };
            await manager.AddSnippetAsync(snippet);

            var fileContent = File.ReadAllText(manager.TempFilePath);
            Assert.DoesNotContain("V3|", fileContent);
            Assert.Contains("Plain Content", fileContent);
        }
    }
}
