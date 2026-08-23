using System;
using System.IO;
using System.Threading.Tasks;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class FileLoggerTests : IDisposable
    {
        private readonly string _tempFile;

        public FileLoggerTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        }

        public void Dispose()
        {
            foreach (var path in new[] { _tempFile, _tempFile + ".1" })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch { }
                }
            }
        }

        [Fact]
        public void LogInfo_WritesToFile()
        {
            using (var logger = new FileLogger(_tempFile))
            {
                logger.LogInfo("Test Info Message");
            } // Dispose will wait for the write to complete

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("[INFO] Test Info Message", content);
        }

        [Fact]
        public void LogError_WritesToFile_WithoutException()
        {
            using (var logger = new FileLogger(_tempFile))
            {
                logger.LogError("Test Error Message");
            }

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("[ERROR] Test Error Message", content);
            Assert.DoesNotContain("Exception:", content);
        }

        [Fact]
        public void LogError_WritesToFile_WithException()
        {
            using (var logger = new FileLogger(_tempFile))
            {
                var ex = new InvalidOperationException("Test exception");
                logger.LogError("Test Error Message", ex);
            }

            Assert.True(File.Exists(_tempFile));
            var content = File.ReadAllText(_tempFile);
            Assert.Contains("[ERROR] Test Error Message", content);
            Assert.Contains("Exception: System.InvalidOperationException", content);
        }

        [Fact]
        public async Task Log_HandlesFileLock_Gracefully()
        {
            // Simulate file lock
            using (var fileStream = new FileStream(_tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var logger = new FileLogger(_tempFile))
                {
                    // This should not throw, even though the file is locked
                    logger.LogInfo("Should not crash");
                }
            } // Release lock

            // FileStream creates an empty file
            var content = await File.ReadAllTextAsync(_tempFile);
            Assert.DoesNotContain("Should not crash", content);
        }

        [Fact]
        public void Log_QueuesWritesSequentially()
        {
            using (var logger = new FileLogger(_tempFile))
            {
                for (int i = 0; i < 100; i++)
                {
                    logger.LogInfo($"Message {i}");
                }
            }

            var content = File.ReadAllText(_tempFile);
            for (int i = 0; i < 100; i++)
            {
                Assert.Contains($"Message {i}", content);
            }
        }

        [Fact]
        public void Log_RotatesOversizedLog_PreservingHistory()
        {
            // Seed a log that has already crossed the 1 MB rollover threshold.
            const string oldMarker = "OLD HISTORY MARKER";
            var seeded = new string('x', 1024 * 1024) + "\n" + oldMarker + "\n";
            File.WriteAllText(_tempFile, seeded);

            using (var logger = new FileLogger(_tempFile))
            {
                logger.LogInfo("Entry after rotation");
            }

            var rotatedPath = _tempFile + ".1";

            // The old log is rolled aside, not deleted: the crash trail is still readable.
            Assert.True(File.Exists(rotatedPath));
            var rotated = File.ReadAllText(rotatedPath);
            Assert.Contains(oldMarker, rotated);
            Assert.DoesNotContain("Entry after rotation", rotated);

            // The live log restarts with just the new entry.
            var current = File.ReadAllText(_tempFile);
            Assert.Contains("[INFO] Entry after rotation", current);
            Assert.DoesNotContain(oldMarker, current);
        }

        [Fact]
        public void Log_DoesNotRotate_WhenLogIsUnderThreshold()
        {
            using (var logger = new FileLogger(_tempFile))
            {
                logger.LogInfo("Small entry");
            }

            Assert.False(File.Exists(_tempFile + ".1"));
            Assert.Contains("[INFO] Small entry", File.ReadAllText(_tempFile));
        }
    }
}
