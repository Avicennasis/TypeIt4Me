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
    public class SnippetManagerTests
    {
        private class TestSnippetManager : SnippetManager
        {
            public TestSnippetManager(ILogger logger) : base(logger)
            {
            }

            protected override string GetFilePath()
            {
                // Return a path with invalid characters to force an exception
                // Path.GetInvalidPathChars() usually doesn't work well on all OSes
                // for directory creation, but a bad root or just a non-existent drive letter works on Windows.
                // Or simply throwing inside a custom method might be better, but we need to trigger the exception in SaveSnippetsAsync.
                // We'll return an empty string, which throws ArgumentException in File operations.
                return string.Empty;
            }
        }

        [Fact]
        public async Task AddSnippet_SaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();
            var tcs = new TaskCompletionSource<bool>();
            logger.ErrorLogged += msg =>
            {
                if (msg == "Background save failed after AddSnippet")
                    tcs.TrySetResult(true);
            };

            var manager = new TestSnippetManager(logger);
            var snippet = new Snippet { Id = Guid.NewGuid(), Name = "Test", Content = "Test", Category = "Test" };

            // Act
            manager.AddSnippet(snippet);

            // Assert
            // The AddSnippet method runs SaveSnippetsAsync in a Task.Run background thread.
            // We await the TaskCompletionSource to know when the exception was caught and logged.
            // Add a timeout to prevent hanging the test if it fails.
            var timeoutTask = Task.Delay(1000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            Assert.Equal(tcs.Task, completedTask); // Ensure it didn't timeout
            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after AddSnippet");
        }

        [Fact]
        public async Task RemoveSnippet_SaveFails_LogsError()
        {
            // Arrange
            var logger = new MockLogger();
            var tcs = new TaskCompletionSource<bool>();
            logger.ErrorLogged += msg =>
            {
                if (msg == "Background save failed after RemoveSnippet")
                    tcs.TrySetResult(true);
            };

            var manager = new TestSnippetManager(logger);
            var snippet = new Snippet { Id = Guid.NewGuid(), Name = "Test", Content = "Test", Category = "Test" };

            manager.Snippets.Add(snippet);

            // Act
            manager.RemoveSnippet(snippet);

            // Assert
            var timeoutTask = Task.Delay(1000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            Assert.Equal(tcs.Task, completedTask); // Ensure it didn't timeout
            Assert.Contains(logger.ErrorLogs, log => log.Message == "Background save failed after RemoveSnippet");
        }
    }
}
