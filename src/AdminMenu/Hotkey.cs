using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdminMenu
{
    /// <summary>
    /// Reads a <see cref="KeyboardShortcut"/> through the legacy Input manager when the build has it,
    /// otherwise through the Input System package. Mouse buttons are only supported on the legacy path.
    /// </summary>
    internal static class Hotkey
    {
        private static bool? _legacyAvailable;

        private static readonly Dictionary<KeyCode, Key> Renamed = new Dictionary<KeyCode, Key>
        {
            [KeyCode.Return] = Key.Enter,
            [KeyCode.KeypadEnter] = Key.NumpadEnter,
            [KeyCode.LeftControl] = Key.LeftCtrl,
            [KeyCode.RightControl] = Key.RightCtrl,
            [KeyCode.LeftCommand] = Key.LeftMeta,
            [KeyCode.RightCommand] = Key.RightMeta,
            [KeyCode.BackQuote] = Key.Backquote,
            [KeyCode.KeypadPlus] = Key.NumpadPlus,
            [KeyCode.KeypadMinus] = Key.NumpadMinus,
            [KeyCode.KeypadMultiply] = Key.NumpadMultiply,
            [KeyCode.KeypadDivide] = Key.NumpadDivide,
            [KeyCode.KeypadPeriod] = Key.NumpadPeriod,
        };

        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return false;

            if (LegacyAvailable)
                return shortcut.IsDown();

            var keyboard = Keyboard.current;
            if (keyboard == null || !TryGetKey(shortcut.MainKey, out var main) || !keyboard[main].wasPressedThisFrame)
                return false;

            foreach (var modifier in shortcut.Modifiers)
            {
                if (!TryGetKey(modifier, out var key) || !keyboard[key].isPressed)
                    return false;
            }
            return true;
        }

        private static bool LegacyAvailable
        {
            get
            {
                if (_legacyAvailable == null)
                {
                    try
                    {
                        Input.GetKey(KeyCode.None);
                        _legacyAvailable = true;
                    }
                    catch (InvalidOperationException)
                    {
                        _legacyAvailable = false;
                    }
                    Plugin.Log.LogInfo(_legacyAvailable.Value
                        ? "Reading hotkeys through the legacy Input manager"
                        : "Legacy Input manager is disabled; reading hotkeys through the Input System package");
                }
                return _legacyAvailable.Value;
            }
        }

        private static bool TryGetKey(KeyCode code, out Key key)
        {
            if (Renamed.TryGetValue(code, out key))
                return true;

            var name = code.ToString();
            if (name.StartsWith("Alpha"))
                name = "Digit" + name.Substring("Alpha".Length);
            else if (name.StartsWith("Keypad"))
                name = "Numpad" + name.Substring("Keypad".Length);

            return Enum.TryParse(name, true, out key) && key != Key.None;
        }
    }
}
