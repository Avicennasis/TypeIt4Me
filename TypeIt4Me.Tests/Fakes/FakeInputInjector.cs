using System.Threading.Tasks;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeInputInjector : IInputInjector
    {
        public Task TypeTextAsync(string text) => Task.CompletedTask;
    }
}
