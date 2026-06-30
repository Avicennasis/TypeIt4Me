using System;
using System.Threading.Tasks;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class FocusTrackerTests
    {
        [Fact]
        public async Task TrackFocusLoop_UpdatesLastExternalWindowHandle_WhenDifferentFromMyWindow()
        {
            // Arrange
            IntPtr myWindow = new IntPtr(123);
            IntPtr externalWindow = new IntPtr(456);
            IntPtr currentForeground = externalWindow;

            using var focusTracker = new FocusTracker(() => currentForeground);

            // Act
            focusTracker.Start(myWindow);

            // Give the loop time to run at least once
            await Task.Delay(250);

            // Assert
            Assert.Equal(externalWindow, focusTracker.LastExternalWindowHandle);
        }

        [Fact]
        public async Task TrackFocusLoop_DoesNotUpdate_WhenForegroundIsMyWindow()
        {
            // Arrange
            IntPtr myWindow = new IntPtr(123);
            IntPtr currentForeground = myWindow;

            using var focusTracker = new FocusTracker(() => currentForeground);

            // Act
            focusTracker.Start(myWindow);

            // Give the loop time to run at least once
            await Task.Delay(250);

            // Assert - Should remain IntPtr.Zero (default)
            Assert.Equal(IntPtr.Zero, focusTracker.LastExternalWindowHandle);
        }

        [Fact]
        public async Task TrackFocusLoop_DoesNotUpdate_WhenForegroundIsZero()
        {
            // Arrange
            IntPtr myWindow = new IntPtr(123);
            IntPtr currentForeground = IntPtr.Zero;

            using var focusTracker = new FocusTracker(() => currentForeground);

            // Act
            focusTracker.Start(myWindow);

            // Give the loop time to run at least once
            await Task.Delay(250);

            // Assert - Should remain IntPtr.Zero
            Assert.Equal(IntPtr.Zero, focusTracker.LastExternalWindowHandle);
        }

        [Fact]
        public async Task Stop_PreventsFurtherUpdates()
        {
            // Arrange
            IntPtr myWindow = new IntPtr(123);
            IntPtr externalWindow1 = new IntPtr(456);
            IntPtr externalWindow2 = new IntPtr(789);
            IntPtr currentForeground = externalWindow1;

            using var focusTracker = new FocusTracker(() => currentForeground);

            // Act
            focusTracker.Start(myWindow);

            // Give the loop time to run and capture externalWindow1
            await Task.Delay(250);

            focusTracker.Stop();

            // Change the simulated foreground window
            currentForeground = externalWindow2;

            // Give the loop time if it were still running (which it shouldn't be)
            await Task.Delay(250);

            // Assert - Should still be the first external window since tracking was stopped
            Assert.Equal(externalWindow1, focusTracker.LastExternalWindowHandle);
        }

        [Fact]
        public void Constructor_Default_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() =>
            {
                using var tracker = new FocusTracker();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Stop_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            using var focusTracker = new FocusTracker(() => IntPtr.Zero);

            // Act & Assert
            var exception = Record.Exception(() => focusTracker.Stop());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_WhenNotStarted_DoesNotThrow()
        {
            // Arrange
            var focusTracker = new FocusTracker(() => IntPtr.Zero);

            // Act & Assert
            var exception = Record.Exception(() => focusTracker.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public async Task Dispose_PreventsFurtherUpdates()
        {
            // Arrange
            IntPtr myWindow = new IntPtr(123);
            IntPtr externalWindow1 = new IntPtr(456);
            IntPtr externalWindow2 = new IntPtr(789);
            IntPtr currentForeground = externalWindow1;

            var focusTracker = new FocusTracker(() => currentForeground);

            // Act
            focusTracker.Start(myWindow);

            // Give the loop time to run and capture externalWindow1
            await Task.Delay(250);

            focusTracker.Dispose();

            // Change the simulated foreground window
            currentForeground = externalWindow2;

            // Give the loop time if it were still running (which it shouldn't be)
            await Task.Delay(250);

            // Assert - Should still be the first external window since tracking was disposed
            Assert.Equal(externalWindow1, focusTracker.LastExternalWindowHandle);
        }
    }
}
