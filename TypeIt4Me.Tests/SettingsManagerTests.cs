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
            public string? LastErrorMessage { get; private set; }

            public void LogInfo(string message) { }

            public void LogError(string message, Exception? ex = null)
            {
                ErrorLogged = true;
                LoggedException = ex;
                LastErrorMessage = message;
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

        // ===================================================================
        // Save error tests (2 methods)
        // ===================================================================

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

        [Fact]
        public async Task SaveSettingsAsync_TempFileLocked_LogsError()
        {
            // Arrange
            var logger = new TestLogger();
            string testPath = Path.Combine(_testDirectory, "settings.json");
            string tempPath = testPath + ".tmp";
            var manager = new TestSettingsManager(logger, testPath);

            // Create the temporary file and lock it so File.Create fails
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Act
                await manager.SaveSettingsAsync();
            }

            // Assert
            Assert.True(logger.ErrorLogged);
            Assert.NotNull(logger.LoggedException);
            Assert.True(logger.LoggedException is IOException || logger.LoggedException is UnauthorizedAccessException);
        }

        // ===================================================================
        // Load error tests (2 methods)
        // ===================================================================

        [Fact]
        public async Task LoadSettingsAsync_DeserializationError_LogsError()
        {
            // Arrange
            var logger = new TestLogger();
            string testPath = Path.Combine(_testDirectory, "settings_deser.json");
            var manager = new TestSettingsManager(logger, testPath);
            File.WriteAllText(testPath, "{ invalid json");

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.True(logger.ErrorLogged);
            Assert.Contains("Error loading settings", logger.LastErrorMessage);
        }

        [Fact]
        public async Task LoadSettingsAsync_FileAccessError_LogsError()
        {
            // Arrange
            var logger = new TestLogger();
            string testPath = Path.Combine(_testDirectory, "settings_lock.json");
            var manager = new TestSettingsManager(logger, testPath);
            File.WriteAllText(testPath, "{}");

            // Lock the file exclusively
            using (var lockStream = new FileStream(testPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                await manager.LoadSettingsAsync();
            }

            // Assert
            Assert.True(logger.ErrorLogged);
            Assert.Contains("Error loading settings", logger.LastErrorMessage);
        }
    }
}
