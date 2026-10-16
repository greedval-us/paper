using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace PaperDefense.Tests
{
    /// <summary>Keep live hardware from taking Mouse.current or Keyboard.current during a synthetic-input test.</summary>
    internal sealed class NativeInputIsolation : IDisposable
    {
        readonly List<InputDevice> disabled = new List<InputDevice>();
        readonly Mouse previousMouse = Mouse.current;
        readonly Keyboard previousKeyboard = Keyboard.current;
        bool disposed;

        internal NativeInputIsolation()
        {
            try
            {
                foreach (var device in InputSystem.devices)
                    if (device.native && device.enabled && (device is Mouse || device is Keyboard))
                    {
                        disabled.Add(device);
                        InputSystem.DisableDevice(device, keepSendingEvents: false);
                    }
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var device in disabled)
                if (device.added) InputSystem.EnableDevice(device);
            disabled.Clear();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        }
    }
}
