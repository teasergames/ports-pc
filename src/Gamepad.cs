using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Nexus {
  public enum PadButton { Up, Down, Left, Right, A, B, Y, Start }

  // Polls the first XInput controller (Xbox and compatible pads) while the window is active and
  // raises one event per press. Directions repeat while held, like keyboard arrows.
  public sealed class Gamepad : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct PadState { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LeftX, LeftY, RightX, RightY; }
    [StructLayout(LayoutKind.Sequential)] struct InputState { public uint Packet; public PadState Pad; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern int GetState(int index, out InputState state);

    const int StickThreshold = 16000;
    const ushort DpadUp = 0x1, DpadDown = 0x2, DpadLeft = 0x4, DpadRight = 0x8, StartMask = 0x10, AMask = 0x1000, BMask = 0x2000, YMask = 0x8000;

    public event Action<PadButton> Pressed;
    readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
    readonly Func<bool> active;
    int slot = -1;
    DateTime nextScan = DateTime.MinValue;
    ushort previous;
    PadButton? held;
    DateTime nextRepeat;
    bool available = true;

    public Gamepad(Func<bool> isActive) {
      active = isActive;
      timer.Tick += delegate { Poll(); };
      timer.Start();
    }

    void Poll() {
      if (!available || !active()) { previous = 0; held = null; return; }
      InputState state;
      try {
        if (slot < 0) {
          // Querying empty slots is slow, so look for a newly connected pad only every two seconds.
          if (DateTime.UtcNow < nextScan) return;
          nextScan = DateTime.UtcNow.AddSeconds(2);
          for (int i = 0; i < 4 && slot < 0; i++) if (GetState(i, out state) == 0) slot = i;
          if (slot < 0) return;
        }
        if (GetState(slot, out state) != 0) { slot = -1; previous = 0; held = null; return; }
      } catch (DllNotFoundException) { available = false; return; }
      catch (EntryPointNotFoundException) { available = false; return; }

      ushort buttons = state.Pad.Buttons;
      if (state.Pad.LeftY > StickThreshold) buttons |= DpadUp;
      if (state.Pad.LeftY < -StickThreshold) buttons |= DpadDown;
      if (state.Pad.LeftX < -StickThreshold) buttons |= DpadLeft;
      if (state.Pad.LeftX > StickThreshold) buttons |= DpadRight;
      ushort pressed = (ushort)(buttons & ~previous);
      previous = buttons;

      if ((pressed & AMask) != 0) Raise(PadButton.A);
      if ((pressed & BMask) != 0) Raise(PadButton.B);
      if ((pressed & YMask) != 0) Raise(PadButton.Y);
      if ((pressed & StartMask) != 0) Raise(PadButton.Start);

      PadButton? direction = (buttons & DpadUp) != 0 ? PadButton.Up : (buttons & DpadDown) != 0 ? PadButton.Down
        : (buttons & DpadLeft) != 0 ? PadButton.Left : (buttons & DpadRight) != 0 ? (PadButton?)PadButton.Right : null;
      if (direction == null) { held = null; return; }
      if (direction != held) { held = direction; nextRepeat = DateTime.UtcNow.AddMilliseconds(400); Raise(direction.Value); }
      else if (DateTime.UtcNow >= nextRepeat) { nextRepeat = DateTime.UtcNow.AddMilliseconds(120); Raise(direction.Value); }
    }

    void Raise(PadButton button) { var handler = Pressed; if (handler != null) handler(button); }

    public void Dispose() { timer.Stop(); }
  }
}
