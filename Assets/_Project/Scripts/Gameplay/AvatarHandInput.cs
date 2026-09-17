using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

namespace Wapawapa.Gameplay
{
    public static class AvatarHandInput
    {
        /// <returns>Left grip, left trigger, right grip, right trigger in [0,1].</returns>
        public static Vector4 Read(bool xrActive, bool keyboardTest)
        {
            if (xrActive)
            {
                var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                return new Vector4(Axis(left, CommonUsages.grip, CommonUsages.gripButton),
                    Axis(left, CommonUsages.trigger, CommonUsages.triggerButton),
                    Axis(right, CommonUsages.grip, CommonUsages.gripButton),
                    Axis(right, CommonUsages.trigger, CommonUsages.triggerButton));
            }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool l = keyboardTest ? keyboard != null && keyboard.qKey.isPressed : mouse != null && mouse.leftButton.isPressed;
            bool r = keyboardTest ? keyboard != null && (keyboard.eKey.isPressed || keyboard.digit5Key.isPressed) : mouse != null && mouse.rightButton.isPressed;
            return new Vector4(l ? 1f : 0f, l ? 1f : 0f, r ? 1f : 0f, r ? 1f : 0f);
        }
        private static float Axis(InputDevice device, InputFeatureUsage<float> axis, InputFeatureUsage<bool> button)
        {
            if (!device.isValid) return 0f;
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked) return 0f;
            if (device.TryGetFeatureValue(axis, out float amount)) return Mathf.Clamp01(amount);
            return device.TryGetFeatureValue(button, out bool down) && down ? 1f : 0f;
        }
    }
}
