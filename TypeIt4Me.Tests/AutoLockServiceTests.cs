using System;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class FakeTimer : Services.ITimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }

        public event EventHandler? Tick;

        public void Start() => IsEnabled = true;
        public void Stop() => IsEnabled = false;

        public void TriggerTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    public class AutoLockServiceTests
    {
        [Fact]
        public void EvaluateTimerState_StartsTimer_WhenAutoLockMinutesGreaterThanZero()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 5;
            var timer = new FakeTimer();
            var service = new AutoLockService(settingsManager, timer, () => DateTime.Now);

            // Act
            service.EvaluateTimerState();

            // Assert
            Assert.True(timer.IsEnabled);
        }

        [Fact]
        public void EvaluateTimerState_StopsTimer_WhenAutoLockMinutesIsZero()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 0;
            var timer = new FakeTimer();
            timer.Start(); // Pre-start timer
            var service = new AutoLockService(settingsManager, timer, () => DateTime.Now);

            // Act
            service.EvaluateTimerState();

            // Assert
            Assert.False(timer.IsEnabled);
        }

        [Fact]
        public void TimerTick_TriggersLock_WhenTimePassedAndPinSet()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 5;
            settingsManager.Settings.PinHash = "somehash";

            var timer = new FakeTimer();

            var currentTime = new DateTime(2023, 1, 1, 12, 0, 0); // 12:00:00

            var service = new AutoLockService(settingsManager, timer, () => currentTime);
            // Internal lastActivity is now 12:00:00

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Act
            currentTime = currentTime.AddMinutes(6); // 12:06:00
            timer.TriggerTick();

            // Assert
            Assert.True(lockTriggered);
        }

        [Fact]
        public void TimerTick_DoesNotTriggerLock_WhenTimePassedButPinEmpty()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 5;
            settingsManager.Settings.PinHash = ""; // No PIN

            var timer = new FakeTimer();
            var currentTime = new DateTime(2023, 1, 1, 12, 0, 0);

            var service = new AutoLockService(settingsManager, timer, () => currentTime);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Act
            currentTime = currentTime.AddMinutes(6);
            timer.TriggerTick();

            // Assert
            Assert.False(lockTriggered);
        }

        [Fact]
        public void TimerTick_DoesNotTriggerLock_WhenTimeNotPassed()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 5;
            settingsManager.Settings.PinHash = "somehash";

            var timer = new FakeTimer();
            var currentTime = new DateTime(2023, 1, 1, 12, 0, 0);

            var service = new AutoLockService(settingsManager, timer, () => currentTime);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Act
            currentTime = currentTime.AddMinutes(3); // Only 3 mins passed
            timer.TriggerTick();

            // Assert
            Assert.False(lockTriggered);
        }

        [Fact]
        public void UpdateLastActivity_DelaysLockTrigger()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 5;
            settingsManager.Settings.PinHash = "somehash";

            var timer = new FakeTimer();
            var currentTime = new DateTime(2023, 1, 1, 12, 0, 0); // initial LastActivity = 12:00:00

            var service = new AutoLockService(settingsManager, timer, () => currentTime);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Act
            currentTime = currentTime.AddMinutes(3); // 12:03:00
            service.UpdateLastActivity(); // LastActivity is now 12:03:00

            currentTime = currentTime.AddMinutes(3); // 12:06:00
            // 12:06:00 - 12:03:00 = 3 mins passed, which is < 5 mins

            timer.TriggerTick();

            // Assert
            Assert.False(lockTriggered);
        }

        [Fact]
        public void TimerTick_DoesNotTriggerLock_WhenAutoLockMinutesIsZero()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            settingsManager.Settings.AutoLockMinutes = 0;
            settingsManager.Settings.PinHash = "somehash";

            var timer = new FakeTimer();
            var currentTime = new DateTime(2023, 1, 1, 12, 0, 0);

            var service = new AutoLockService(settingsManager, timer, () => currentTime);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Act
            currentTime = currentTime.AddMinutes(6);
            timer.TriggerTick();

            // Assert
            Assert.False(lockTriggered);
        }

        [Fact]
        public void Stop_StopsTimer()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            var timer = new FakeTimer();
            timer.Start(); // Ensure it's running
            var service = new AutoLockService(settingsManager, timer, () => DateTime.Now);

            // Act
            service.Stop();

            // Assert
            Assert.False(timer.IsEnabled);
        }

        [Fact]
        public void Dispose_StopsTimer()
        {
            // Arrange
            var settingsManager = new FakeSettingsManager();
            var timer = new FakeTimer();
            timer.Start(); // Ensure it's running
            var service = new AutoLockService(settingsManager, timer, () => DateTime.Now);

            // Act
            service.Dispose();

            // Assert
            Assert.False(timer.IsEnabled);
        }
    }
}
