using System.ComponentModel;
using TypeIt4Me.Models;
using Xunit;

namespace TypeIt4Me.Tests;

public class AppSettingsModelTests
{
    [Fact]
    public void NewSettings_HasExpectedDefaults()
    {
        var s = new AppSettings();
        Assert.True(s.AlwaysOnTop);
        Assert.Equal(string.Empty, s.PinHash);
        Assert.Equal(string.Empty, s.PinSalt);
        Assert.False(s.IsMiniMode);
        Assert.True(s.MinimizeToTray);
        Assert.False(s.IsDarkMode);
        Assert.Equal(0, s.AutoLockMinutes);
        Assert.False(s.LockOnRestore);
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged()
    {
        var s = new AppSettings();
        var changed = new List<string>();
        ((INotifyPropertyChanged)s).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changed.Add(e.PropertyName);
        };

        s.AlwaysOnTop = false;
        s.IsDarkMode = true;
        s.AutoLockMinutes = 15;

        Assert.Contains(nameof(AppSettings.AlwaysOnTop), changed);
        Assert.Contains(nameof(AppSettings.IsDarkMode), changed);
        Assert.Contains(nameof(AppSettings.AutoLockMinutes), changed);
    }

    [Fact]
    public void SetSameValue_DoesNotRaisePropertyChanged()
    {
        var s = new AppSettings();
        var raised = false;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, _) => raised = true;

        s.AlwaysOnTop = true; // Same as default
        Assert.False(raised);
    }
}
