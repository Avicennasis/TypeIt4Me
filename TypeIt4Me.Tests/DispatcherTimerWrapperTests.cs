using System;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class DispatcherTimerWrapperTests
    {
        [Fact]
        public void InitialState_IsCorrect()
        {
            var timer = new DispatcherTimerWrapper();
            Assert.False(timer.IsEnabled);
        }

        [Fact]
        public void Interval_GetAndSet_WorksCorrectly()
        {
            var timer = new DispatcherTimerWrapper();
            var newInterval = TimeSpan.FromSeconds(5);

            timer.Interval = newInterval;

            Assert.Equal(newInterval, timer.Interval);
        }

        [Fact]
        public void StartStop_ChangesIsEnabledProperty()
        {
            var timer = new DispatcherTimerWrapper();

            timer.Start();
            Assert.True(timer.IsEnabled);

            timer.Stop();
            Assert.False(timer.IsEnabled);
        }

        [Fact]
        public void TickEvent_CanSubscribeAndUnsubscribe()
        {
            var timer = new DispatcherTimerWrapper();
            var eventHandled = false;

            EventHandler handler = (s, e) => eventHandled = true;

            // Just verifying we can subscribe/unsubscribe without exceptions
            timer.Tick += handler;
            timer.Tick -= handler;

            Assert.False(eventHandled);
        }
    }
}
