using System;
using System.IO;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests.Services
{
    // A testable version of SnippetManager that overrides the file path
    public class TestableSnippetManager : SnippetManager
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

    public class SnippetManagerTests
    {
        [Fact]
        public async Task RemoveSnippet_BackgroundSaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();
            // Provide a fundamentally invalid path that will fail gracefully and quickly
            // such as an invalid drive or path containing invalid characters.
            var invalidPath = Path.Combine("InvalidDrive:\\", "nonexistent", "snippets.json");

            var snippetManager = new TestableSnippetManager(logger, invalidPath);
            var snippet = new Snippet { Name = "Test" };
            snippetManager.Snippets.Add(snippet); // Add it directly to bypass AddSnippet's background task

            var tcs = new TaskCompletionSource<bool>();

            // Set up our event handler to capture the log
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
            // Wait up to 1 second for the background task to log the error
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.Equal(tcs.Task, completedTask); // If Task.Delay wins, this will fail

            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after RemoveSnippet");
        }

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
    }
}
