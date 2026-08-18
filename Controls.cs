using UnityEngine;
using UnityEngine.InputSystem;

namespace PengooinLabs.ReplayMod
{
    public class Controls
    {
  
        public static Vector2 getMouseWheel()
        {
            return Mouse.current.scroll.ReadValue();
        }

        public static bool wasToggleCameraTargetButtonPressed()
        {
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
            if (Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.cKey.wasPressedThisFrame) return true;
            return false;
        }

        public static bool wasPrevActorButtonPressed()
        {
            if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame) return true;
            if (Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.aKey.wasPressedThisFrame) return true;
            return false;
        }

        public static bool wasNextActorButtonPressed()
        {
            if (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame) return true;
            if (Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.dKey.wasPressedThisFrame) return true;
            return false;
        }

        public static bool wasPauseButtonPressed()
        {
            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) return true;
            if (Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            return false;
        }

        public static bool wasSpeedIncreaseKeyPressed()
        {
            return Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.wKey.wasPressedThisFrame;
        }

        public static bool wasSpeedDecreaseKeyPressed()
        {
            return Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.sKey.wasPressedThisFrame;
        }

        public static bool isSpeedControlButtonDown()
        {
            return isLeftTriggerPressed();
        }

        public static bool isFasterSpeedButtonDown()
        {
            return isRightTriggerPressed();
        }

        public static bool isRightTriggerPressed()
        {
            if (Gamepad.current == null) return false;
            if (Gamepad.current.rightTrigger.isPressed) return true;
            return false;
        }

        public static bool isLeftTriggerPressed()
        {
            if (Gamepad.current == null) return false;
            if (Gamepad.current.leftTrigger.isPressed) return true;
            return false;
        }
    }
}
