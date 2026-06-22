using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ProcessDirector.Services
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly IntPtr _windowHandle;
        private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();
        private int _nextId = 1;

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public bool RegisterHotkey(int modifiers, Key key, Action action)
        {
            if (key == Key.None || modifiers == 0 || action == null)
            {
                return false;
            }

            uint mod = (uint)modifiers;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            int id = _nextId++;

            if (RegisterHotKey(_windowHandle, id, mod, vk))
            {
                _hotkeyActions[id] = action;
                return true;
            }

            return false;
        }

        public void UnregisterAll()
        {
            foreach (int id in _hotkeyActions.Keys)
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _hotkeyActions.Clear();
        }

        public void ProcessHotkey(int id)
        {
            if (_hotkeyActions.TryGetValue(id, out Action action))
            {
                action?.Invoke();
            }
        }

        public void Dispose()
        {
            UnregisterAll();
        }
    }
}