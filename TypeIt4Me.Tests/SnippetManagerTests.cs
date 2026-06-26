using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class SnippetManagerTests
    {
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

            manager.SetPin("1234");

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
    }
}
