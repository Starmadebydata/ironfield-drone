using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ironfield.Drone
{
    /// <summary>
    /// One frame of pilot intent, read from gamepad first and keyboard/mouse as
    /// fallback. Plain poll (no .inputactions asset) so the prototype needs zero
    /// binding setup.
    ///
    /// Gamepad: left stick = throttle (fwd/back) + yaw, right stick Y = climb,
    ///          right stick X = roll, RT = boost, RB / A = detonate.
    /// KB/M:    W/S forward-back, A/D yaw, SPACE / CTRL climb-descend,
    ///          Q/E roll, hold RMB + mouse = fine pitch/roll aim,
    ///          Shift = boost, LMB or ENTER = detonate.
    /// </summary>
    public struct DroneInput
    {
        public float Throttle;   // -1..1  forward / back
        public float Yaw;        // -1..1  left / right
        public float Climb;      // -1..1  down / up   (dedicated altitude control)
        public float Pitch;      // -1..1  nose down / up  (fine mouse aim only)
        public float Roll;       // -1..1  left / right
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
                i.Climb = r.y;                       // right stick up = climb
                i.Roll = r.x;
                i.Climb += gp.rightShoulder.ReadValue() > 0.5f ? 1f : 0f;
                i.Climb -= gp.leftShoulder.ReadValue() > 0.5f ? 1f : 0f;
                i.Boost = gp.rightTrigger.ReadValue() > 0.5f;
                i.FirePressed = gp.buttonSouth.wasPressedThisFrame ||
                                gp.buttonEast.wasPressedThisFrame;
                i.RecallPressed = gp.buttonNorth.wasPressedThisFrame;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) i.Throttle += 1f;
                if (kb.sKey.isPressed) i.Throttle -= 1f;
                if (kb.dKey.isPressed) i.Yaw += 1f;
                if (kb.aKey.isPressed) i.Yaw -= 1f;

                // dedicated altitude
                if (kb.spaceKey.isPressed) i.Climb += 1f;
                if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) i.Climb -= 1f;
                if (kb.rKey.isPressed) i.Climb += 1f;   // alt: R / F
                if (kb.fKey.isPressed) i.Climb -= 1f;

                if (kb.eKey.isPressed) i.Roll += 1f;
                if (kb.qKey.isPressed) i.Roll -= 1f;
                if (kb.leftArrowKey.isPressed) i.Roll -= 1f;
                if (kb.rightArrowKey.isPressed) i.Roll += 1f;
                if (kb.upArrowKey.isPressed) i.Climb += 1f;
                if (kb.downArrowKey.isPressed) i.Climb -= 1f;

                i.Boost |= kb.leftShiftKey.isPressed;
                i.FirePressed |= kb.enterKey.wasPressedThisFrame;
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
            i.Climb = Mathf.Clamp(i.Climb, -1f, 1f);
            i.Pitch = Mathf.Clamp(i.Pitch, -1f, 1f);
            i.Roll = Mathf.Clamp(i.Roll, -1f, 1f);
            return i;
        }
    }
}
