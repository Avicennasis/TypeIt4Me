using System.Collections.Generic;
using System.Threading.Tasks;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class MockInputSender : IInputSender
    {
        public List<string> Log { get; } = new List<string>();
        public int TotalDelayMs { get; private set; }

        public Task DelayAsync(int milliseconds)
        {
            Log.Add($"DELAY:{milliseconds}");
            TotalDelayMs += milliseconds;
            return Task.CompletedTask;
        }

        public void ReleaseModifiers()
        {
            Log.Add("RELEASE_MODIFIERS");
        }

        public void SendInputBatch(string text)
        {
            Log.Add($"SEND_BATCH:{text}");
        }

        public void SendVirtualKey(ushort vkCode)
        {
            Log.Add($"SEND_VK:0x{vkCode:X2}");
        }
    }
}
