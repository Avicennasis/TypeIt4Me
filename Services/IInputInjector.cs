using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public interface IInputInjector
    {
        Task TypeTextAsync(string text);
    }
}
