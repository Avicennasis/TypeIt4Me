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
        private readonly TestLogger _logger;
        private readonly string _testPath;
        private readonly TestSettingsManager _manager;

        public SettingsManagerTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "SettingsManagerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _logger = new TestLogger();
            _testPath = Path.Combine(_testDirectory, "settings.json");
            _manager = new TestSettingsManager(_logger, _testPath);
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
            // Using a directory that doesn't exist to force DirectoryNotFoundException during File.Create
            string invalidPath = Path.Combine(_testDirectory, "NonExistentDirectory", "settings.json");
            var manager = new TestSettingsManager(_logger, invalidPath);

            // Act
            await manager.SaveSettingsAsync();

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.NotNull(_logger.LoggedException);
            Assert.IsType<DirectoryNotFoundException>(_logger.LoggedException);
        }

        [Fact]
        public async Task SaveSettingsAsync_ExceptionDuringFileMove_LogsError()
        {
            // Arrange
            // Create the destination file and lock it so File.Move fails
            using (var stream = new FileStream(_testPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Act
                await _manager.SaveSettingsAsync();
            }

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.NotNull(_logger.LoggedException);
            Assert.True(_logger.LoggedException is IOException || _logger.LoggedException is UnauthorizedAccessException);
        }

        [Fact]
        public async Task SaveSettingsAsync_TempFileLocked_LogsError()
        {
            // Arrange
            string tempPath = _testPath + ".tmp";

            // Create the temporary file and lock it so File.Create fails
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Act
                await _manager.SaveSettingsAsync();
            }

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.NotNull(_logger.LoggedException);
            Assert.True(_logger.LoggedException is IOException || _logger.LoggedException is UnauthorizedAccessException);
        }

        // ===================================================================
        // Load error tests (2 methods)
        // ===================================================================

        [Fact]
        public async Task LoadSettingsAsync_DeserializationError_LogsError()
        {
            // Arrange
            File.WriteAllText(_testPath, "{ invalid json");

            // Act
            await _manager.LoadSettingsAsync();

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.Contains("Error loading settings", _logger.LastErrorMessage);
            Assert.NotNull(_logger.LoggedException);
            Assert.IsType<System.Text.Json.JsonException>(_logger.LoggedException);
        }

        [Fact]
        public async Task LoadSettingsAsync_FileAccessError_LogsError()
        {
            // Arrange
            File.WriteAllText(_testPath, "{}");

            // Lock the file exclusively
            using (var lockStream = new FileStream(_testPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                await _manager.LoadSettingsAsync();
            }

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.Contains("Error loading settings", _logger.LastErrorMessage);
            Assert.NotNull(_logger.LoggedException);
            Assert.True(_logger.LoggedException is IOException || _logger.LoggedException is UnauthorizedAccessException);
        }

        [Fact]
        public async Task LoadSettingsAsync_FileNotFound_DoesNotLogError()
        {
            // Arrange
            // File does not exist
            string nonExistentPath = Path.Combine(_testDirectory, "non_existent_settings.json");
            var manager = new TestSettingsManager(_logger, nonExistentPath);

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.False(_logger.ErrorLogged);
            Assert.Null(_logger.LoggedException);
        }

        [Fact]
        public async Task LoadSettingsAsync_DirectoryNotFound_DoesNotLogError()
        {
            // Arrange
            // Directory does not exist
            string nonExistentPath = Path.Combine(_testDirectory, "NonExistentDir", "settings.json");
            var manager = new TestSettingsManager(_logger, nonExistentPath);

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.False(_logger.ErrorLogged);
            Assert.Null(_logger.LoggedException);
        }

        private class ThrowingSettingsManager : SettingsManager
        {
            public ThrowingSettingsManager(ILogger logger) : base(logger)
            {
            }

            protected override string GetFilePath()
            {
                throw new InvalidOperationException("Simulated unexpected error");
            }
        }

        [Fact]
        public async Task LoadSettingsAsync_UnexpectedException_LogsError()
        {
            // Arrange
            var manager = new ThrowingSettingsManager(_logger);

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.True(_logger.ErrorLogged);
            Assert.Contains("Error loading settings", _logger.LastErrorMessage);
            Assert.NotNull(_logger.LoggedException);
            Assert.IsType<InvalidOperationException>(_logger.LoggedException);
        }
    }
}
