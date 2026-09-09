using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ironfield.Drone
{
    /// <summary>
    /// One frame of pilot intent, read from gamepad first and keyboard/mouse as
    /// fallback. Kept as a plain poll (no .inputactions asset) so the prototype
    /// has zero binding setup.
    ///
    /// Gamepad: left stick = yaw + throttle, right stick = pitch + roll,
    ///          RT = boost, RB / A = fire, Y = recall.
    /// KB/M:    W/S throttle, A/D yaw, mouse = pitch/roll (hold RMB), Q/E roll,
    ///          Shift = boost, Space / LMB = fire, R = recall.
    /// </summary>
    public struct DroneInput
    {
        public float Throttle;   // -1..1  (down..up)
        public float Yaw;        // -1..1  (left..right)
        public float Pitch;      // -1..1  (nose down..up)
        public float Roll;       // -1..1  (left..right)
        public bool Boost;
        public bool FirePressed;
        public bool RecallPressed;

        public static DroneInput Read(float mouseSensitivity = 1f)
        {
            var i = new DroneInput();
#if ENABLE_INPUT_SYSTEM
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 l = gp.leftStick.ReadValue();
                Vector2 r = gp.rightStick.ReadValue();
                i.Yaw = l.x;
                i.Throttle = l.y;
                i.Pitch = r.y;
                i.Roll = r.x;
                i.Boost = gp.rightTrigger.ReadValue() > 0.5f;
                i.FirePressed = gp.rightShoulder.wasPressedThisFrame ||
                                gp.buttonSouth.wasPressedThisFrame;
                i.RecallPressed = gp.buttonNorth.wasPressedThisFrame;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) i.Throttle += 1f;
                if (kb.sKey.isPressed) i.Throttle -= 1f;
                if (kb.dKey.isPressed) i.Yaw += 1f;
                if (kb.aKey.isPressed) i.Yaw -= 1f;
                if (kb.eKey.isPressed) i.Roll += 1f;
                if (kb.qKey.isPressed) i.Roll -= 1f;
                if (kb.upArrowKey.isPressed) i.Pitch += 1f;
                if (kb.downArrowKey.isPressed) i.Pitch -= 1f;
                if (kb.leftArrowKey.isPressed) i.Roll -= 1f;
                if (kb.rightArrowKey.isPressed) i.Roll += 1f;
                i.Boost |= kb.leftShiftKey.isPressed;
                i.FirePressed |= kb.spaceKey.wasPressedThisFrame;
                i.RecallPressed |= kb.rKey.wasPressedThisFrame;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * (0.06f * mouseSensitivity);
                i.Pitch += Mathf.Clamp(-d.y, -1f, 1f);
                i.Roll += Mathf.Clamp(d.x, -1f, 1f);
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                i.FirePressed = true;
#endif
            i.Throttle = Mathf.Clamp(i.Throttle, -1f, 1f);
            i.Yaw = Mathf.Clamp(i.Yaw, -1f, 1f);
            i.Pitch = Mathf.Clamp(i.Pitch, -1f, 1f);
            i.Roll = Mathf.Clamp(i.Roll, -1f, 1f);
            return i;
        }
    }
}
