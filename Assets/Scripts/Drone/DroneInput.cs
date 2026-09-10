using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ironfield.Drone
{
    /// <summary>
    /// Mouse-aim flight input. The mouse drives a virtual aim reticle inside a
    /// unit circle (relative mouse delta, springs back toward centre); the drone
    /// continuously turns its nose onto that reticle. The keyboard only handles
    /// speed, altitude trim, boost and roll.
    ///
    /// Mouse : move = aim / steer   ·   LMB = detonate   ·   RMB (hold) = precision
    /// Keys  : W / S  throttle-brake   ·   SPACE / CTRL  climb / descend trim
    ///         Q / E  roll   ·   SHIFT  boost   ·   R  recall
    /// </summary>
    public struct DroneInput
    {
        public Vector2 AimDelta;   // raw mouse delta this frame (px), pre-sensitivity
        public float Throttle;     // -1..1  brake .. full
        public float ClimbTrim;    // -1..1  descend .. climb (gentle collective)
        public float Roll;         // -1..1
        public bool Boost;
        public bool Precision;     // RMB held
        public bool FirePressed;
        public bool RecallPressed;

        public static DroneInput Read()
        {
            var i = new DroneInput();
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                i.AimDelta = mouse.delta.ReadValue();
                i.Precision = mouse.rightButton.isPressed;
                i.FirePressed = mouse.leftButton.wasPressedThisFrame;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) i.Throttle += 1f;
                if (kb.sKey.isPressed) i.Throttle -= 1f;
                if (kb.spaceKey.isPressed) i.ClimbTrim += 1f;
                if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) i.ClimbTrim -= 1f;
                if (kb.eKey.isPressed) i.Roll += 1f;
                if (kb.qKey.isPressed) i.Roll -= 1f;
                i.Boost |= kb.leftShiftKey.isPressed;
                i.RecallPressed |= kb.rKey.wasPressedThisFrame;
                i.FirePressed |= kb.enterKey.wasPressedThisFrame;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                // right stick also drives the aim reticle (as a rate)
                Vector2 r = gp.rightStick.ReadValue();
                i.AimDelta += r * 18f;
                Vector2 l = gp.leftStick.ReadValue();
                i.Throttle += l.y;
                i.Roll += l.x * 0.5f;
                i.ClimbTrim += (gp.rightShoulder.ReadValue() > 0.5f ? 1f : 0f)
                             - (gp.leftShoulder.ReadValue() > 0.5f ? 1f : 0f);
                i.Boost |= gp.rightTrigger.ReadValue() > 0.5f;
                i.Precision |= gp.leftTrigger.ReadValue() > 0.5f;
                i.FirePressed |= gp.buttonSouth.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame;
                i.RecallPressed |= gp.buttonNorth.wasPressedThisFrame;
            }
#endif
            i.Throttle = Mathf.Clamp(i.Throttle, -1f, 1f);
            i.ClimbTrim = Mathf.Clamp(i.ClimbTrim, -1f, 1f);
            i.Roll = Mathf.Clamp(i.Roll, -1f, 1f);
            return i;
        }
    }
}
