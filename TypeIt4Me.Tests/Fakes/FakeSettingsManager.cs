using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeSettingsManager : ISettingsManager
    {
        public bool SaveSettingsCalled { get; private set; } = false;

        public AppSettings Settings { get; } = new AppSettings();
        public Task LoadSettingsAsync() => Task.CompletedTask;

        public Task SaveSettingsAsync()
        {
            SaveSettingsCalled = true;
            return Task.CompletedTask;
        }
    }
}
