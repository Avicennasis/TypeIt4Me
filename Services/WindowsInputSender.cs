using System;
using System.Buffers;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class WindowsInputSender : IInputSender
    {
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
                // We don't check state; just spam KeyUp. It's safe and robust.
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
                            dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                        }
                    }
                };
            }
            NativeMethods.SendInput((uint)inputs.Length, ref inputs[0], NativeMethods.INPUT.Size);
        }

        public void SendVirtualKey(ushort vkCode)
        {
            Span<NativeMethods.INPUT> inputs = stackalloc NativeMethods.INPUT[2];

            // Key Down
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
                        dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                    }
                }
            };

            // Key Up
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
                        dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                    }
                }
            };

            NativeMethods.SendInput((uint)inputs.Length, ref inputs[0], NativeMethods.INPUT.Size);
        }

        public void SendInputBatch(System.ReadOnlySpan<char> text)
        {
            var pool = ArrayPool<NativeMethods.INPUT>.Shared;
            var inputs = pool.Rent(text.Length * 2);

            try
            {
                int count = 0;

                foreach (char c in text)
                {
                    // Handle newline characters specially - send as Enter key press
                    // Many apps don't interpret Unicode \r or \n as line breaks, but VK_RETURN works universally
                    if (c == '\r')
                    {
                        // Skip carriage return - we'll handle it with \n to avoid double line breaks
                        // Windows text typically uses \r\n, so we only act on \n
                        continue;
                    }

                    if (c == '\n')
                    {
                        // Send Enter key (VK_RETURN = 0x0D) as a virtual key press
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
                                    dwExtraInfo = NativeMethods.GetMessageExtraInfo()
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
                                    dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                                }
                            }
                        };
                        continue;
                    }

                    // Key Down (Unicode character)
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
                                dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                            }
                        }
                    };

                    // Key Up
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
                                dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                            }
                        }
                    };
                }

                if (count > 0)
                {
                    NativeMethods.SendInput((uint)count, inputs, NativeMethods.INPUT.Size);
                }
            }
            finally
            {
                pool.Return(inputs);
            }
        }
    }
}
