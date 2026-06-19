using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public interface IInputSender
    {
        Task DelayAsync(int milliseconds);
        void SendVirtualKey(ushort vkCode);
        void SendInputBatch(string text);
        void ReleaseModifiers();
    }
}
