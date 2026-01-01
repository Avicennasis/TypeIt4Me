using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class InputInjector
    {
        public async Task TypeTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 1. Release modifiers potentially held down by the user (Win, Alt, Ctrl, Shift)
            // This prevents "stuck key" behavior when firing hotkeys.
            // We do this by sending 'KeyUp' events for common modifiers before typing.
            ReleaseModifiers();
            
            // Small delay to ensure target window is ready/focused and modifiers registered as up
            await Task.Delay(150);

            // 2. Split input to avoid overwhelming buffer
            // 50 chars per batch is usually safe.
            const int BatchSize = 50; 
            for (int i = 0; i < text.Length; i += BatchSize)
            {
                string batch = text.Substring(i, Math.Min(BatchSize, text.Length - i));
                SendInputBatch(batch);
                // Tiny delay between batches to let the event loop catch up
                await Task.Delay(10);
            }
        }

        private void ReleaseModifiers()
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
               // We don't check state; just spam KeyUp. It's safe and robust.
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

        private void SendInputBatch(string text)
        {
            var inputs = new List<NativeMethods.INPUT>();

            foreach (char c in text)
            {
                // Key Down
                var down = new NativeMethods.INPUT
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
                inputs.Add(down);

                // Key Up
                var up = new NativeMethods.INPUT
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
                inputs.Add(up);
            }

            NativeMethods.INPUT[] inputArr = inputs.ToArray();
            NativeMethods.SendInput((uint)inputArr.Length, inputArr, NativeMethods.INPUT.Size);
        }
    }
}
