using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeSettingsManager : ISettingsManager
    {
        public AppSettings Settings { get; } = new AppSettings();
        public Task LoadSettingsAsync() => Task.CompletedTask;
        public Task SaveSettingsAsync() => Task.CompletedTask;
    }
}
