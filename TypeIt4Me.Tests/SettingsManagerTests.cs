using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class SettingsManagerTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _settingsFilePath;

        // A testable subclass that overrides GetFilePath so it doesn't try to use AppData
        private class TestableSettingsManager : SettingsManager
        {
            private readonly string _path;

            public TestableSettingsManager(ILogger logger, string path) : base(logger)
            {
                _path = path;
            }

            protected override string GetFilePath()
            {
                return _path;
            }
        }

        public SettingsManagerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
            _settingsFilePath = Path.Combine(_tempDirectory, "test_settings.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Fact]
        public async Task LoadSettingsAsync_FileDoesNotExist_KeepsDefaults()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _settingsFilePath);
            var initialSettings = new AppSettings();

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.Equal(initialSettings.AlwaysOnTop, manager.Settings.AlwaysOnTop);
            Assert.Equal(initialSettings.IsDarkMode, manager.Settings.IsDarkMode);
        }

        [Fact]
        public async Task LoadSettingsAsync_FileExists_LoadsSettings()
        {
            // Arrange
            var savedSettings = new AppSettings
            {
                AlwaysOnTop = false,
                IsDarkMode = true,
                AutoLockMinutes = 15,
                PinHash = "hash123",
                PinSalt = "salt123"
            };

            await File.WriteAllTextAsync(_settingsFilePath, JsonSerializer.Serialize(savedSettings));

            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _settingsFilePath);

            // Act
            await manager.LoadSettingsAsync();

            // Assert
            Assert.False(manager.Settings.AlwaysOnTop);
            Assert.True(manager.Settings.IsDarkMode);
            Assert.Equal(15, manager.Settings.AutoLockMinutes);
            Assert.Equal("hash123", manager.Settings.PinHash);
            Assert.Equal("salt123", manager.Settings.PinSalt);
        }

        [Fact]
        public async Task SaveSettingsAsync_WritesSettingsToFile()
        {
            // Arrange
            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _settingsFilePath);

            manager.Settings.IsDarkMode = true;
            manager.Settings.AlwaysOnTop = false;
            manager.Settings.MinimizeToTray = false;

            // Act
            await manager.SaveSettingsAsync();

            // Assert
            Assert.True(File.Exists(_settingsFilePath));

            var fileContent = await File.ReadAllTextAsync(_settingsFilePath);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(fileContent);

            Assert.NotNull(deserialized);
            Assert.True(deserialized!.IsDarkMode);
            Assert.False(deserialized.AlwaysOnTop);
            Assert.False(deserialized.MinimizeToTray);

            // Check that temp file is deleted (because SaveSettingsAsync renames it)
            Assert.False(File.Exists(_settingsFilePath + ".tmp"));
        }

        [Fact]
        public async Task LoadSettingsAsync_CorruptJson_HandlesGracefully()
        {
            // Arrange
            await File.WriteAllTextAsync(_settingsFilePath, "{ invalid json ]");

            var logger = new FakeLogger();
            var manager = new TestableSettingsManager(logger, _settingsFilePath);

            // Act & Assert
            // Shouldn't throw, should catch the error and keep default settings
            await manager.LoadSettingsAsync();

            Assert.False(manager.Settings.IsDarkMode); // Default
        }
    }
}
