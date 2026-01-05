using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class InputInjector
    {
        // Virtual key code mappings for special keys
        private static readonly Dictionary<string, ushort> SpecialKeys = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            // Navigation keys
            { "TAB", 0x09 },
            { "ENTER", 0x0D },
            { "RETURN", 0x0D },
            { "ESC", 0x1B },
            { "ESCAPE", 0x1B },
            { "BACKSPACE", 0x08 },
            { "DELETE", 0x2E },
            { "DEL", 0x2E },
            { "INSERT", 0x2D },
            { "INS", 0x2D },
            { "HOME", 0x24 },
            { "END", 0x23 },
            { "PAGEUP", 0x21 },
            { "PGUP", 0x21 },
            { "PAGEDOWN", 0x22 },
            { "PGDN", 0x22 },
            
            // Arrow keys
            { "ARROWUP", 0x26 },
            { "UP", 0x26 },
            { "ARROWDOWN", 0x28 },
            { "DOWN", 0x28 },
            { "ARROWLEFT", 0x25 },
            { "LEFT", 0x25 },
            { "ARROWRIGHT", 0x27 },
            { "RIGHT", 0x27 },
            
            // Modifier keys
            { "SHIFT", 0x10 },
            { "CTRL", 0x11 },
            { "CONTROL", 0x11 },
            { "ALT", 0x12 },
            { "WINKEY", 0x5B },
            { "WIN", 0x5B },
            { "LWIN", 0x5B },
            { "RWIN", 0x5C },
            
            // Toggle keys
            { "CAPSLOCK", 0x14 },
            { "CAPS", 0x14 },
            { "NUMLOCK", 0x90 },
            { "SCROLLLOCK", 0x91 },
            
            // Special keys
            { "PRINTSCREEN", 0x2C },
            { "PRTSC", 0x2C },
            { "SPACE", 0x20 },
            
            // Function keys
            { "F1", 0x70 },
            { "F2", 0x71 },
            { "F3", 0x72 },
            { "F4", 0x73 },
            { "F5", 0x74 },
            { "F6", 0x75 },
            { "F7", 0x76 },
            { "F8", 0x77 },
            { "F9", 0x78 },
            { "F10", 0x79 },
            { "F11", 0x7A },
            { "F12", 0x7B }
        };

        // Regex to match special commands like {TAB}, {ENTER}, {SLEEP 1500}, etc.
        private static readonly Regex CommandPattern = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        public async Task TypeTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 1. Release modifiers potentially held down by the user (Win, Alt, Ctrl, Shift)
            // This prevents "stuck key" behavior when firing hotkeys.
            ReleaseModifiers();
            
            // Small delay to ensure target window is ready/focused and modifiers registered as up
            await Task.Delay(150);

            // 2. Parse and process the text, handling special commands
            await ProcessTextWithCommands(text);
        }

        /// <summary>
        /// Processes text that may contain special commands like {TAB}, {ENTER}, {SLEEP 1500}
        /// </summary>
        private async Task ProcessTextWithCommands(string text)
        {
            int lastIndex = 0;
            var matches = CommandPattern.Matches(text);

            foreach (Match match in matches)
            {
                // Type any text before this command
                if (match.Index > lastIndex)
                {
                    string beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    await TypePlainTextAsync(beforeText);
                }

                // Process the command
                string command = match.Groups[1].Value.Trim();
                await ProcessCommand(command);

                lastIndex = match.Index + match.Length;
            }

            // Type any remaining text after the last command
            if (lastIndex < text.Length)
            {
                string remainingText = text.Substring(lastIndex);
                await TypePlainTextAsync(remainingText);
            }
        }

        /// <summary>
        /// Processes a single command like TAB, ENTER, SLEEP 1500, etc.
        /// </summary>
        private async Task ProcessCommand(string command)
        {
            // Check for SLEEP command with duration
            if (command.StartsWith("SLEEP", StringComparison.OrdinalIgnoreCase))
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int milliseconds))
                {
                    // Clamp to reasonable range (1ms to 60 seconds)
                    milliseconds = Math.Max(1, Math.Min(milliseconds, 60000));
                    await Task.Delay(milliseconds);
                }
                return;
            }

            // Check if it's a known special key
            if (SpecialKeys.TryGetValue(command, out ushort vkCode))
            {
                SendVirtualKey(vkCode);
                await Task.Delay(10); // Small delay after special key
            }
            // If not recognized, type it literally including the braces
            else
            {
                await TypePlainTextAsync("{" + command + "}");
            }
        }

        /// <summary>
        /// Types plain text in batches, handling newlines specially
        /// </summary>
        private async Task TypePlainTextAsync(string text)
        {
            const int BatchSize = 50;
            for (int i = 0; i < text.Length; i += BatchSize)
            {
                string batch = text.Substring(i, Math.Min(BatchSize, text.Length - i));
                SendInputBatch(batch);
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

        /// <summary>
        /// Sends a single virtual key press (down + up)
        /// </summary>
        private void SendVirtualKey(ushort vkCode)
        {
            var inputs = new NativeMethods.INPUT[2];

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

            NativeMethods.SendInput(2, inputs, NativeMethods.INPUT.Size);
        }

        private void SendInputBatch(string text)
        {
            var inputs = new List<NativeMethods.INPUT>();

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
                    var enterDown = new NativeMethods.INPUT
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
                    inputs.Add(enterDown);

                    var enterUp = new NativeMethods.INPUT
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
                    inputs.Add(enterUp);
                    continue;
                }

                // Key Down (Unicode character)
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

            if (inputs.Count > 0)
            {
                NativeMethods.INPUT[] inputArr = inputs.ToArray();
                NativeMethods.SendInput((uint)inputArr.Length, inputArr, NativeMethods.INPUT.Size);
            }
        }
    }
}
