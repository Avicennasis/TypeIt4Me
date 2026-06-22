using System;
using Xunit;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using System.Reflection;
using System.Windows.Threading;
using TypeIt4Me.Models;

namespace TypeIt4Me.Tests
{
    public class AutoLockServiceTests
    {
        [Fact]
        public void EvaluateTimerState_StartsTimer_WhenAutoLockMinutesGreaterThanZero()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            fakeSettingsManager.Settings.AutoLockMinutes = 5;
            using var service = new AutoLockService(fakeSettingsManager);

            service.EvaluateTimerState();

            var timerField = typeof(AutoLockService).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance);
            var timer = (DispatcherTimer)timerField!.GetValue(service)!;

            Assert.True(timer.IsEnabled);
        }

        [Fact]
        public void EvaluateTimerState_StopsTimer_WhenAutoLockMinutesIsZero()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            fakeSettingsManager.Settings.AutoLockMinutes = 5;
            using var service = new AutoLockService(fakeSettingsManager);

            service.EvaluateTimerState(); // Starts it

            fakeSettingsManager.Settings.AutoLockMinutes = 0;
            service.EvaluateTimerState(); // Should stop it

            var timerField = typeof(AutoLockService).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance);
            var timer = (DispatcherTimer)timerField!.GetValue(service)!;

            Assert.False(timer.IsEnabled);
        }

        [Fact]
        public void TimerTick_TriggersLock_WhenConditionsMet()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            fakeSettingsManager.Settings.AutoLockMinutes = 1;
            fakeSettingsManager.Settings.PinHash = "somehash";
            using var service = new AutoLockService(fakeSettingsManager);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Set last activity to 2 minutes ago
            var lastActivityField = typeof(AutoLockService).GetField("_lastActivity", BindingFlags.NonPublic | BindingFlags.Instance);
            lastActivityField!.SetValue(service, DateTime.Now.AddMinutes(-2));

            // Invoke Timer_Tick manually
            var tickMethod = typeof(AutoLockService).GetMethod("Timer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            tickMethod!.Invoke(service, new object[] { null!, EventArgs.Empty });

            Assert.True(lockTriggered);
        }

        [Fact]
        public void TimerTick_DoesNotTriggerLock_WhenTimeNotElapsed()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            fakeSettingsManager.Settings.AutoLockMinutes = 5;
            fakeSettingsManager.Settings.PinHash = "somehash";
            using var service = new AutoLockService(fakeSettingsManager);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Set last activity to just 1 minute ago (not elapsed)
            var lastActivityField = typeof(AutoLockService).GetField("_lastActivity", BindingFlags.NonPublic | BindingFlags.Instance);
            lastActivityField!.SetValue(service, DateTime.Now.AddMinutes(-1));

            // Invoke Timer_Tick manually
            var tickMethod = typeof(AutoLockService).GetMethod("Timer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            tickMethod!.Invoke(service, new object[] { null!, EventArgs.Empty });

            Assert.False(lockTriggered);
        }

        [Fact]
        public void TimerTick_DoesNotTriggerLock_WhenPinNotSet()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            fakeSettingsManager.Settings.AutoLockMinutes = 1;
            fakeSettingsManager.Settings.PinHash = ""; // Pin not set
            using var service = new AutoLockService(fakeSettingsManager);

            bool lockTriggered = false;
            service.OnLockTriggered += () => lockTriggered = true;

            // Set last activity to 2 minutes ago
            var lastActivityField = typeof(AutoLockService).GetField("_lastActivity", BindingFlags.NonPublic | BindingFlags.Instance);
            lastActivityField!.SetValue(service, DateTime.Now.AddMinutes(-2));

            // Invoke Timer_Tick manually
            var tickMethod = typeof(AutoLockService).GetMethod("Timer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            tickMethod!.Invoke(service, new object[] { null!, EventArgs.Empty });

            Assert.False(lockTriggered);
        }

        [Fact]
        public void UpdateLastActivity_ResetsTime()
        {
            var fakeSettingsManager = new FakeSettingsManager();
            using var service = new AutoLockService(fakeSettingsManager);

            // Set last activity to 2 minutes ago
            var lastActivityField = typeof(AutoLockService).GetField("_lastActivity", BindingFlags.NonPublic | BindingFlags.Instance);
            var initialTime = DateTime.Now.AddMinutes(-2);
            lastActivityField!.SetValue(service, initialTime);

            service.UpdateLastActivity();

            var newTime = (DateTime)lastActivityField.GetValue(service)!;
            Assert.True(newTime > initialTime);
            // newTime should be very close to DateTime.Now
            Assert.True((DateTime.Now - newTime).TotalSeconds < 1);
        }
    }
}
