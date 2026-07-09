using System;
using System.Buffers;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class WindowsInputSender : IInputSender
    {
        internal delegate uint SendInputRefDelegate(uint nInputs, ref NativeMethods.INPUT pInputs, int cbSize);
        internal delegate uint SendInputArrayDelegate(uint nInputs, NativeMethods.INPUT[] pInputs, int cbSize);

        private readonly SendInputRefDelegate _sendInputRef;
        private readonly SendInputArrayDelegate _sendInputArray;
        private readonly Func<IntPtr> _getMessageExtraInfo;

        public WindowsInputSender()
            : this(NativeMethods.SendInput, NativeMethods.SendInput, NativeMethods.GetMessageExtraInfo)
        {
        }

        internal WindowsInputSender(
            SendInputRefDelegate sendInputRef,
            SendInputArrayDelegate sendInputArray,
            Func<IntPtr> getMessageExtraInfo)
        {
            _sendInputRef = sendInputRef;
            _sendInputArray = sendInputArray;
            _getMessageExtraInfo = getMessageExtraInfo;
        }

        private static readonly ushort[] ModifierKeys =
        {
            0x5B, // Left Win
            0x5C, // Right Win
            0x10, // Shift
            0x11, // Ctrl
            0x12  // Alt
        };

        public Task DelayAsync(int milliseconds)
        {
            return Task.Delay(milliseconds);
        }

        public void ReleaseModifiers()
        {
            Span<NativeMethods.INPUT> inputs = stackalloc NativeMethods.INPUT[ModifierKeys.Length];
            for (int i = 0; i < ModifierKeys.Length; i++)
            {
                inputs[i] = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wVk = ModifierKeys[i],
                            dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = _getMessageExtraInfo()
                        }
                    }
                };
            }
            _sendInputRef((uint)inputs.Length, ref inputs[0], NativeMethods.INPUT.Size);
        }

        public void SendVirtualKey(ushort vkCode)
        {
            Span<NativeMethods.INPUT> inputs = stackalloc NativeMethods.INPUT[2];

            inputs[0] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vkCode,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = _getMessageExtraInfo()
                    }
                }
            };

            inputs[1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vkCode,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = _getMessageExtraInfo()
                    }
                }
            };

            _sendInputRef((uint)inputs.Length, ref inputs[0], NativeMethods.INPUT.Size);
        }

        public void SendInputBatch(ReadOnlySpan<char> text)
        {
            var pool = ArrayPool<NativeMethods.INPUT>.Shared;
            var inputs = pool.Rent(text.Length * 2);

            try
            {
                int count = 0;

                foreach (char c in text)
                {
                    if (c == '\r')
                    {
                        continue;
                    }

                    if (c == '\n')
                    {
                        inputs[count++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = 0x0D, // VK_RETURN
                                    dwFlags = 0,
                                    time = 0,
                                    dwExtraInfo = _getMessageExtraInfo()
                                }
                            }
                        };

                        inputs[count++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = 0x0D, // VK_RETURN
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                                    time = 0,
                                    dwExtraInfo = _getMessageExtraInfo()
                                }
                            }
                        };
                        continue;
                    }

                    inputs[count++] = new NativeMethods.INPUT
                    {
                        type = NativeMethods.INPUT_KEYBOARD,
                        U = new NativeMethods.InputUnion
                        {
                            ki = new NativeMethods.KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                                time = 0,
                                dwExtraInfo = _getMessageExtraInfo()
                            }
                        }
                    };

                    inputs[count++] = new NativeMethods.INPUT
                    {
                        type = NativeMethods.INPUT_KEYBOARD,
                        U = new NativeMethods.InputUnion
                        {
                            ki = new NativeMethods.KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = _getMessageExtraInfo()
                            }
                        }
                    };
                }

                if (count > 0)
                {
                    _sendInputArray((uint)count, inputs, NativeMethods.INPUT.Size);
                }
            }
            finally
            {
                pool.Return(inputs);
            }
        }
    }
}
