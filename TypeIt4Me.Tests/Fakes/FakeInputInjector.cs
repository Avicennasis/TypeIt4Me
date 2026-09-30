using System.Threading.Tasks;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeInputInjector : IInputInjector
    {
        public string? LastText { get; private set; }
        public Task TypeTextAsync(string text) { LastText = text; return Task.CompletedTask; }
    }
}
