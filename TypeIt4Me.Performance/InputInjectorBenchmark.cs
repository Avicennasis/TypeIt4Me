using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Buffers;

namespace TypeIt4Me.Performance
{
    // --- Mock NativeMethods & Structs ---
    public static class NativeMethods
    {
        // Mock SendInput: just returns 0, does nothing, effectively cost-free for benchmark
        public static uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize)
        {
            // Return success
            return nInputs;
        }

        public static IntPtr GetMessageExtraInfo() => IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public const int INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;
    }

    [MemoryDiagnoser]
    public class InputInjectorBenchmark
    {
        private string _shortText = string.Empty;
        private string _mediumText = string.Empty;
        private string _longText = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            _shortText = "Hello"; // 5 chars
            _mediumText = new string('a', 100); // 100 chars
            _longText = new string('a', 10000); // 10kb
        }

        // --- Baseline Implementation ---

        [Benchmark(Baseline = true)]
        public void Baseline_SendInputBatch()
        {
            SendInputBatch_Baseline(_mediumText);
        }

        [Benchmark]
        public void Baseline_ReleaseModifiers()
        {
            ReleaseModifiers_Baseline();
        }

        private void ReleaseModifiers_Baseline()
        {
            var keys = new[]
            {
                (ushort)0x5B, // Left Win
                (ushort)0x5C, // Right Win
                (ushort)0x10, // Shift
                (ushort)0x11, // Ctrl
                (ushort)0x12  // Alt
            };

            var inputs = new List<NativeMethods.INPUT>();
            foreach(var k in keys)
            {
               inputs.Add(new NativeMethods.INPUT
               {
                   type = NativeMethods.INPUT_KEYBOARD,
                   U = new NativeMethods.InputUnion
                   {
                       ki = new NativeMethods.KEYBDINPUT
                       {
                           wVk = k,
                           dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                           time = 0,
                           dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                       }
                   }
               });
            }
            NativeMethods.INPUT[] inputArr = inputs.ToArray();
            NativeMethods.SendInput((uint)inputArr.Length, inputArr, NativeMethods.INPUT.Size);
        }

        private void SendInputBatch_Baseline(string text)
        {
            var inputs = new NativeMethods.INPUT[text.Length * 2];
            int count = 0;

            foreach (char c in text)
            {
                if (c == '\r') continue;

                if (c == '\n')
                {
                    inputs[count++] = new NativeMethods.INPUT
                    {
                        type = NativeMethods.INPUT_KEYBOARD,
                        U = new NativeMethods.InputUnion
                        {
                            ki = new NativeMethods.KEYBDINPUT
                            {
                                wVk = 0x0D,
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
                                wVk = 0x0D,
                                dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = NativeMethods.GetMessageExtraInfo()
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

        // --- Optimized Implementation ---

        [Benchmark]
        public void Optimized_SendInputBatch()
        {
            SendInputBatch_Optimized(_mediumText);
        }

        [Benchmark]
        public void Optimized_ReleaseModifiers()
        {
            ReleaseModifiers_Optimized();
        }

        private void ReleaseModifiers_Optimized()
        {
             // Optimization: Use fixed size array, avoid List and ToArray
            var inputs = new NativeMethods.INPUT[5];

            // Unroll or just loop over a local span of keys to fill
            // Hardcoding keys here for direct comparison of logic
            ushort[] keys = { 0x5B, 0x5C, 0x10, 0x11, 0x12 };

            for(int i = 0; i < keys.Length; i++)
            {
               inputs[i] = new NativeMethods.INPUT
               {
                   type = NativeMethods.INPUT_KEYBOARD,
                   U = new NativeMethods.InputUnion
                   {
                       ki = new NativeMethods.KEYBDINPUT
                       {
                           wVk = keys[i],
                           dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                           time = 0,
                           dwExtraInfo = NativeMethods.GetMessageExtraInfo()
                       }
                   }
               };
            }
            // Pass the array directly
            NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        }

        private void SendInputBatch_Optimized(string text)
        {
            // Optimization: Use ArrayPool
            var pool = ArrayPool<NativeMethods.INPUT>.Shared;
            var inputs = pool.Rent(text.Length * 2);

            try
            {
                int count = 0;

                foreach (char c in text)
                {
                    if (c == '\r') continue;

                    if (c == '\n')
                    {
                        inputs[count++] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.InputUnion
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = 0x0D,
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
                                    wVk = 0x0D,
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                                    time = 0,
                                    dwExtraInfo = NativeMethods.GetMessageExtraInfo()
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
