using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests
{
    public class SettingsManagerTests : IDisposable
    {
        private class TestLogger : ILogger
        {
            public bool ErrorLogged { get; private set; }
            public Exception? LoggedException { get; private set; }

            public void LogInfo(string message) { }

            public void LogError(string message, Exception? ex = null)
            {
                ErrorLogged = true;
                LoggedException = ex;
            }
        }

        private class TestSettingsManager : SettingsManager
        {
            private readonly string _testPath;

            public TestSettingsManager(ILogger logger, string testPath) : base(logger)
            {
                _testPath = testPath;
            }

            protected override string GetFilePath()
            {
                return _testPath;
            }
        }

        private readonly string _testDirectory;

        public SettingsManagerTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "SettingsManagerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch { /* Ignore */ }
            }
        }

        [Fact]
        public async Task SaveSettingsAsync_ExceptionDuringFileCreate_LogsError()
        {
            // Arrange
            var logger = new TestLogger();

            // Using a directory that doesn't exist to force DirectoryNotFoundException during File.Create
            string invalidPath = Path.Combine(_testDirectory, "NonExistentDirectory", "settings.json");
            var manager = new TestSettingsManager(logger, invalidPath);

            // Act
            await manager.SaveSettingsAsync();

            // Assert
            Assert.True(logger.ErrorLogged);
            Assert.NotNull(logger.LoggedException);
            Assert.IsType<DirectoryNotFoundException>(logger.LoggedException);
        }

        [Fact]
        public async Task SaveSettingsAsync_ExceptionDuringFileMove_LogsError()
        {
            // Arrange
            var logger = new TestLogger();
            string testPath = Path.Combine(_testDirectory, "settings.json");
            var manager = new TestSettingsManager(logger, testPath);

            // Create the destination file and lock it so File.Move fails
            using (var stream = new FileStream(testPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Act
                await manager.SaveSettingsAsync();
            }

            // Assert
            Assert.True(logger.ErrorLogged);
            Assert.NotNull(logger.LoggedException);
            Assert.True(logger.LoggedException is IOException || logger.LoggedException is UnauthorizedAccessException);
        }
    }
}
