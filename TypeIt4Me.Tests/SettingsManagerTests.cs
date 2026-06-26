using System;
using System.IO;
using System.Threading.Tasks;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class SettingsManagerTests : IDisposable
    {
        private readonly string _tempFile;

        public SettingsManagerTests()
        {
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                try { File.Delete(_tempFile); } catch { }
            }
        }

        private class TestableSettingsManager : SettingsManager
        {
            private readonly string _testPath;

            public TestableSettingsManager(ILogger logger, string testPath) : base(logger)
            {
                _testPath = testPath;
            }

            protected override string GetFilePath()
            {
                return _testPath;
            }
        }

        private class FakeLogger : ILogger
        {
            public bool LogErrorCalled { get; private set; }
            public string? LastErrorMessage { get; private set; }

            public void LogInfo(string message) { }

            public void LogError(string message, Exception? ex = null)
            {
                LogErrorCalled = true;
                LastErrorMessage = message;
            }
        }

        [Fact]
        public async Task LoadSettingsAsync_DeserializationError_LogsError()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _tempFile);
            File.WriteAllText(_tempFile, "{ invalid json");

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.True(logger.LogErrorCalled);
            Assert.Contains("Error loading settings", logger.LastErrorMessage);
        }

        [Fact]
        public async Task LoadSettingsAsync_FileAccessError_LogsError()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _tempFile);
            File.WriteAllText(_tempFile, "{}");

            // Lock the file exclusively
            using (var lockStream = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                await manager.LoadSettingsAsync();
            }

            // Assert
            Assert.True(logger.LogErrorCalled);
            Assert.Contains("Error loading settings", logger.LastErrorMessage);
        }
    }
}
