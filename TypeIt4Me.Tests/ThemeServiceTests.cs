using System;
using System.IO.Packaging;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class ThemeServiceTests
    {
        static ThemeServiceTests()
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                // Register the pack scheme so unit tests don't throw UriFormatException
                // System.Windows.Application usually does this, but we don't have it here.
                _ = PackUriHelper.UriSchemePack;
            }
        }

        [Fact]
        public void SetTheme_DarkTheme_UsesDarkThemeUri()
        {
            // Arrange
            Uri? appliedUri = null;
            var service = new ThemeService(uri => appliedUri = uri);

            // Act
            service.SetTheme(true);

            // Assert
            Assert.NotNull(appliedUri);
            Assert.Equal("pack://application:,,,/TypeIt4Me;component/Views/DarkTheme.xaml", appliedUri.ToString());
        }

        [Fact]
        public void SetTheme_LightTheme_UsesLightThemeUri()
        {
            // Arrange
            Uri? appliedUri = null;
            var service = new ThemeService(uri => appliedUri = uri);

            // Act
            service.SetTheme(false);

            // Assert
            Assert.NotNull(appliedUri);
            Assert.Equal("pack://application:,,,/TypeIt4Me;component/Views/LightTheme.xaml", appliedUri.ToString());
        }
    }
}
