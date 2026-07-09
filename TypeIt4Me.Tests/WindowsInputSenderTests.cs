using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class WindowsInputSenderTests
    {
        private (uint nInputs, NativeMethods.INPUT[]? array, NativeMethods.INPUT? singleRef, int cbSize) _lastArrayCall;
        private (uint nInputs, NativeMethods.INPUT? singleRef, int cbSize) _lastRefCall;

        private uint MockSendInputArray(uint nInputs, NativeMethods.INPUT[] pInputs, int cbSize)
        {
            var copy = new NativeMethods.INPUT[nInputs];
            Array.Copy(pInputs, copy, nInputs);
            _lastArrayCall = (nInputs, copy, null, cbSize);
            return nInputs;
        }

        private uint MockSendInputRef(uint nInputs, ref NativeMethods.INPUT pInputs, int cbSize)
        {
            _lastRefCall = (nInputs, pInputs, cbSize);
            return nInputs;
        }

        private IntPtr MockGetMessageExtraInfo()
        {
            return new IntPtr(12345);
        }

        [Fact]
        public void SendVirtualKey_ConstructsInputCorrectly()
        {
            var sender = new WindowsInputSender(MockSendInputRef, MockSendInputArray, MockGetMessageExtraInfo);

            sender.SendVirtualKey(0x41); // 'A'

            Assert.Equal(2u, _lastRefCall.nInputs);
            Assert.Equal(NativeMethods.INPUT.Size, _lastRefCall.cbSize);

            // The ref call only captures the first element of the span.
            // But we know it sends 2 inputs. Let's just verify the first one (KeyDown).
            Assert.True(_lastRefCall.singleRef.HasValue);
            var input = _lastRefCall.singleRef.Value;

            Assert.Equal(NativeMethods.INPUT_KEYBOARD, (int)input.type);
            Assert.Equal(0x41, input.U.ki.wVk);
            Assert.Equal(0u, input.U.ki.dwFlags);
            Assert.Equal(new IntPtr(12345), input.U.ki.dwExtraInfo);
        }

        [Fact]
        public void ReleaseModifiers_SendsKeyUpForAllModifiers()
        {
            var sender = new WindowsInputSender(MockSendInputRef, MockSendInputArray, MockGetMessageExtraInfo);

            sender.ReleaseModifiers();

            Assert.Equal(5u, _lastRefCall.nInputs); // 5 modifiers

            Assert.True(_lastRefCall.singleRef.HasValue);
            var input = _lastRefCall.singleRef.Value;

            Assert.Equal(NativeMethods.INPUT_KEYBOARD, (int)input.type);
            Assert.Equal(0x5B, input.U.ki.wVk); // First modifier is Left Win
            Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, input.U.ki.dwFlags);
        }

        [Fact]
        public void SendInputBatch_SendsCorrectArray()
        {
            var sender = new WindowsInputSender(MockSendInputRef, MockSendInputArray, MockGetMessageExtraInfo);

            sender.SendInputBatch("A");

            Assert.NotNull(_lastArrayCall.array);
            Assert.Equal(2u, _lastArrayCall.nInputs);

            var keyDown = _lastArrayCall.array[0];
            Assert.Equal(NativeMethods.INPUT_KEYBOARD, (int)keyDown.type);
            Assert.Equal('A', keyDown.U.ki.wScan);
            Assert.Equal(NativeMethods.KEYEVENTF_UNICODE, keyDown.U.ki.dwFlags);

            var keyUp = _lastArrayCall.array[1];
            Assert.Equal('A', keyUp.U.ki.wScan);
            Assert.Equal(NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP, keyUp.U.ki.dwFlags);
        }

        [Fact]
        public void SendInputBatch_HandlesCarriageReturnAndNewline()
        {
            var sender = new WindowsInputSender(MockSendInputRef, MockSendInputArray, MockGetMessageExtraInfo);

            sender.SendInputBatch("\r\n");

            Assert.NotNull(_lastArrayCall.array);
            Assert.Equal(2u, _lastArrayCall.nInputs); // \r is skipped, \n becomes 2 inputs (down/up)

            var enterDown = _lastArrayCall.array[0];
            Assert.Equal(NativeMethods.INPUT_KEYBOARD, (int)enterDown.type);
            Assert.Equal(0x0D, enterDown.U.ki.wVk); // VK_RETURN
            Assert.Equal(0u, enterDown.U.ki.dwFlags);

            var enterUp = _lastArrayCall.array[1];
            Assert.Equal(0x0D, enterUp.U.ki.wVk);
            Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, enterUp.U.ki.dwFlags);
        }

        [Fact]
        public async Task DelayAsync_DelaysExecution()
        {
            var sender = new WindowsInputSender(MockSendInputRef, MockSendInputArray, MockGetMessageExtraInfo);
            var task = sender.DelayAsync(10);
            await task;
            Assert.True(task.IsCompletedSuccessfully);
        }
    }
}
