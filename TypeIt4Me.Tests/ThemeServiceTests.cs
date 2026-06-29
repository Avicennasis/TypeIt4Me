using System;
using Xunit;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests
{
    public class ThemeServiceTests
    {
        [Fact]
        public void SetTheme_DarkTheme_AppliesDarkUri()
        {
            // Arrange
            Uri? appliedUri = null;
            bool uiInvoked = false;

            Action<Action> mockInvokeOnUI = (action) =>
            {
                uiInvoked = true;
                action();
            };

            Action<Uri> mockApplyTheme = (uri) =>
            {
                appliedUri = uri;
            };

            var themeService = new ThemeService(mockInvokeOnUI, mockApplyTheme);

            // Act
            themeService.SetTheme(isDark: true);

            // Assert
            Assert.True(uiInvoked, "Expected SetTheme to invoke the UI dispatcher delegate.");
            Assert.NotNull(appliedUri);
            Assert.Equal("pack://application:,,,/TypeIt4Me;component/Views/DarkTheme.xaml", appliedUri.ToString());
        }

        [Fact]
        public void SetTheme_LightTheme_AppliesLightUri()
        {
            // Arrange
            Uri? appliedUri = null;
            bool uiInvoked = false;

            Action<Action> mockInvokeOnUI = (action) =>
            {
                uiInvoked = true;
                action();
            };

            Action<Uri> mockApplyTheme = (uri) =>
            {
                appliedUri = uri;
            };

            var themeService = new ThemeService(mockInvokeOnUI, mockApplyTheme);

            // Act
            themeService.SetTheme(isDark: false);

            // Assert
            Assert.True(uiInvoked, "Expected SetTheme to invoke the UI dispatcher delegate.");
            Assert.NotNull(appliedUri);
            Assert.Equal("pack://application:,,,/TypeIt4Me;component/Views/LightTheme.xaml", appliedUri.ToString());
        }
    }
}
