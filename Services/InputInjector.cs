using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class InputInjector : IInputInjector
    {
        private readonly IInputSender _inputSender;

        // Security: Maximum snippet content length to prevent DoS (100 KB)
        private const int MaxSnippetLength = 100 * 1024;

        // Timing constants for better maintainability
        private const int ModifierReleaseDelayMs = 150;
        private const int KeyPressDelayMs = 10;
        private const int BatchDelayMs = 10;

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

        public InputInjector() : this(new WindowsInputSender()) { }

        public InputInjector(IInputSender inputSender)
        {
            _inputSender = inputSender;
        }

        public async Task TypeTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Security: Validate input length to prevent DoS attacks
            if (text.Length > MaxSnippetLength)
            {
                throw new ArgumentException($"Snippet content exceeds maximum allowed length of {MaxSnippetLength} characters.");
            }

            // 1. Release modifiers potentially held down by the user (Win, Alt, Ctrl, Shift)
            // This prevents "stuck key" behavior when firing hotkeys.
            _inputSender.ReleaseModifiers();

            // Small delay to ensure target window is ready/focused and modifiers registered as up
            await _inputSender.DelayAsync(ModifierReleaseDelayMs);

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
                    var beforeText = text.AsMemory(lastIndex, match.Index - lastIndex);
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
                var remainingText = text.AsMemory(lastIndex);
                await TypePlainTextAsync(remainingText);
            }
        }

        /// <summary>
        /// Processes a single command like TAB, ENTER, SLEEP 1500, etc.
        /// </summary>
        private bool TryParseSleepDuration(string command, out int milliseconds)
        {
            var span = command.AsSpan("SLEEP".Length).Trim();
            return int.TryParse(span, out milliseconds);
        }

        private async Task ProcessCommand(string command)
        {
            // Check for SLEEP command with duration
            if (command.StartsWith("SLEEP", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseSleepDuration(command, out int milliseconds))
                {
                    // Clamp to reasonable range (1ms to 60 seconds)
                    milliseconds = Math.Max(1, Math.Min(milliseconds, 60000));
                    await _inputSender.DelayAsync(milliseconds);
                }
                return;
            }

            // Check if it's a known special key
            if (SpecialKeys.TryGetValue(command, out ushort vkCode))
            {
                _inputSender.SendVirtualKey(vkCode);
                await _inputSender.DelayAsync(KeyPressDelayMs); // Small delay after special key
            }
            // If not recognized, type it literally including the braces
            else
            {
                await TypePlainTextAsync(("{" + command + "}").AsMemory());
            }
        }

        /// <summary>
        /// Types plain text in batches, handling newlines specially
        /// </summary>
        private async Task TypePlainTextAsync(ReadOnlyMemory<char> text)
        {
            const int BatchSize = 50;
            for (int i = 0; i < text.Length; i += BatchSize)
            {
                ReadOnlyMemory<char> batchMemory = text.Slice(i, Math.Min(BatchSize, text.Length - i));
                _inputSender.SendInputBatch(batchMemory.Span);
                await _inputSender.DelayAsync(BatchDelayMs);
            }
        }
    }
}
