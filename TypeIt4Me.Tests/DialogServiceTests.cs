using System;
using System.Windows;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class DialogServiceTests
    {
        [Fact]
        public void OpenFileDialog_ReturnsFileName_WhenDialogResultIsTrue()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.OpenFileDialogFunc = (filter, defaultExt, title, owner) =>
            {
                Assert.Equal("test_filter", filter);
                Assert.Equal("test_ext", defaultExt);
                Assert.Equal("Import snippets", title);
                return (true, "selected_file.txt");
            };

            var result = service.OpenFileDialog("test_filter", "test_ext");

            Assert.Equal("selected_file.txt", result);
        }

        [Fact]
        public void OpenFileDialog_ReturnsNull_WhenDialogResultIsFalse()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.OpenFileDialogFunc = (filter, defaultExt, title, owner) => (false, "ignored.txt");

            var result = service.OpenFileDialog("test_filter", "test_ext");

            Assert.Null(result);
        }

        [Fact]
        public void SaveFileDialog_ReturnsFileName_WhenDialogResultIsTrue()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.SaveFileDialogFunc = (filter, defaultExt, fileName, title, owner) =>
            {
                Assert.Equal("test_filter", filter);
                Assert.Equal("test_ext", defaultExt);
                Assert.Equal("initial_name.txt", fileName);
                Assert.Equal("Export snippets", title);
                return (true, "saved_file.txt");
            };

            var result = service.SaveFileDialog("test_filter", "test_ext", "initial_name.txt");

            Assert.Equal("saved_file.txt", result);
        }

        [Fact]
        public void SaveFileDialog_ReturnsNull_WhenDialogResultIsFalse()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.SaveFileDialogFunc = (filter, defaultExt, fileName, title, owner) => (false, "ignored.txt");

            var result = service.SaveFileDialog("test_filter", "test_ext", "initial_name.txt");

            Assert.Null(result);
        }

        [Fact]
        public void ShowConfirmation_ReturnsTrue_WhenDialogResultIsTrue()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.ShowMessageWindowFunc = (message, title, confirmation, destructive, owner) =>
            {
                Assert.Equal("Are you sure?", message);
                Assert.Equal("Confirmation", title);
                Assert.True(confirmation);
                Assert.False(destructive);
                return true;
            };

            var result = service.ShowConfirmation("Are you sure?", "Confirmation");

            Assert.True(result);
        }

        [Fact]
        public void ShowConfirmation_ReturnsFalse_WhenDialogResultIsFalse()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.ShowMessageWindowFunc = (message, title, confirmation, destructive, owner) => false;

            var result = service.ShowConfirmation("Are you sure?", "Confirmation");

            Assert.False(result);
        }

        [Fact]
        public void ShowConfirmation_SetsDestructive_WhenTitleIsDeleteSnippet()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            service.ShowMessageWindowFunc = (message, title, confirmation, destructive, owner) =>
            {
                Assert.True(destructive);
                return true;
            };

            service.ShowConfirmation("Delete it?", "Delete snippet");
        }

        [Fact]
        public void ShowInformation_CallsShowMessageWindowFunc()
        {
            var service = new DialogService();
            service.GetActiveOwner = () => null;
            bool called = false;
            service.ShowMessageWindowFunc = (message, title, confirmation, destructive, owner) =>
            {
                Assert.Equal("Info", message);
                Assert.Equal("Information", title);
                Assert.False(confirmation);
                Assert.False(destructive);
                called = true;
                return true;
            };

            service.ShowInformation("Info", "Information");

            Assert.True(called);
        }
    }
}
