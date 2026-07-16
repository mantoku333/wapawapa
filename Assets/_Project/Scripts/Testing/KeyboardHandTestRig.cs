using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Wapawapa.Boxing;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace Wapawapa.Testing
{
    public sealed class KeyboardHandTestRig : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float keyboardLookSpeed = 90f;
        [SerializeField] private float vrTurnSpeed = 75f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float punchDistance = 0.65f;
        [SerializeField] private float punchSpeed = 12f;
        [SerializeField] private float punchHitWindow = 0.35f;

        private Vector3 leftRestLocalPosition;
        private Vector3 rightRestLocalPosition;
        private PunchHitbox leftPunchHitbox;
        private PunchHitbox rightPunchHitbox;
        private float pitch;
        private float verticalVelocity;
        private float groundY;
        private Vector3 trackedHeadPosition;
        private Quaternion trackedHeadRotation = Quaternion.identity;
        private Vector3 trackedLeftHandPosition;
        private Quaternion trackedLeftHandRotation = Quaternion.identity;
        private Vector3 trackedRightHandPosition;
        private Quaternion trackedRightHandRotation = Quaternion.identity;
        private bool hasTrackedLeftHandPose;
        private bool hasTrackedRightHandPose;

        private void Awake()
        {
            groundY = transform.position.y;

            if (leftHand != null)
            {
                leftRestLocalPosition = leftHand.localPosition;
                leftPunchHitbox = leftHand.GetComponent<PunchHitbox>();
            }

            if (rightHand != null)
            {
                rightRestLocalPosition = rightHand.localPosition;
                rightPunchHitbox = rightHand.GetComponent<PunchHitbox>();
            }
        }

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyXrPoseBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyXrPoseBeforeRender;
        }

        private void Update()
        {
            if (TryReadXrRig())
            {
                ApplyTrackedRig();
                UpdateVrLocomotion();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            UpdateCursorLock();
            UpdateMouseLook();

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            UpdateKeyboardLook(keyboard);

            if (IsGrounded() && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            var movement = move.normalized * moveSpeed;
            movement.y = verticalVelocity;
            transform.position += movement * Time.deltaTime;

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                verticalVelocity = -0.5f;
            }

            var leftPunching = keyboard.qKey.isPressed;
            var rightPunching = keyboard.eKey.isPressed || keyboard.digit5Key.isPressed;
            if (keyboard.qKey.wasPressedThisFrame)
            {
                leftPunchHitbox?.StartManualPunch(transform.forward, punchHitWindow);
            }

            if (keyboard.eKey.wasPressedThisFrame || keyboard.digit5Key.wasPressedThisFrame)
            {
                rightPunchHitbox?.StartManualPunch(transform.forward, punchHitWindow);
            }

            ApplyPunchPose(leftHand, leftRestLocalPosition, leftPunching);
            ApplyPunchPose(rightHand, rightRestLocalPosition, rightPunching);
        }

        private void UpdateVrLocomotion()
        {
            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var moveInput = Vector2.zero;
            leftDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out moveInput);

            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out var turnInput))
            {
                transform.Rotate(0f, turnInput.x * vrTurnSpeed * Time.deltaTime, 0f);
            }

            var forward = Vector3.ProjectOnPlane(head != null ? head.forward : transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(head != null ? head.right : transform.right, Vector3.up).normalized;
            var movement = (forward * moveInput.y + right * moveInput.x) * moveSpeed;

            verticalVelocity += gravity * Time.deltaTime;
            movement.y = verticalVelocity;
            transform.position += movement * Time.deltaTime;

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                verticalVelocity = -0.5f;
            }
        }

        private bool TryReadXrRig()
        {
            if (!IsXrDisplayRunning())
            {
                return false;
            }

            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            var hasHeadPosition = headDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out trackedHeadPosition);
            var hasHeadRotation = headDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out trackedHeadRotation);
            hasTrackedLeftHandPose =
                leftDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out trackedLeftHandPosition) &&
                leftDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out trackedLeftHandRotation);
            hasTrackedRightHandPose =
                rightDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out trackedRightHandPosition) &&
                rightDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out trackedRightHandRotation);

            return hasHeadPosition && hasHeadRotation;
        }

        private void ApplyTrackedRig()
        {
            if (head != null)
            {
                head.localPosition = trackedHeadPosition;
                head.localRotation = trackedHeadRotation;
            }

            if (leftHand != null && hasTrackedLeftHandPose)
            {
                leftHand.localPosition = trackedLeftHandPosition;
                leftHand.localRotation = trackedLeftHandRotation;
            }

            if (rightHand != null && hasTrackedRightHandPose)
            {
                rightHand.localPosition = trackedRightHandPosition;
                rightHand.localRotation = trackedRightHandRotation;
            }
        }

        private void ApplyXrPoseBeforeRender()
        {
            if (TryReadXrRig())
            {
                ApplyTrackedRig();
            }
        }

        private static bool IsXrDisplayRunning()
        {
            var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (display.running)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateCursorLock()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateMouseLook()
        {
            var mouse = Mouse.current;
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var delta = mouse.delta.ReadValue();
            transform.Rotate(0f, delta.x * mouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -80f, 80f);

            if (head != null)
            {
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void UpdateKeyboardLook(Keyboard keyboard)
        {
            var look = Vector2.zero;
            if (keyboard.rightArrowKey.isPressed) look.x += 1f;
            if (keyboard.leftArrowKey.isPressed) look.x -= 1f;
            if (keyboard.upArrowKey.isPressed) look.y += 1f;
            if (keyboard.downArrowKey.isPressed) look.y -= 1f;

            if (look == Vector2.zero)
            {
                return;
            }

            var amount = keyboardLookSpeed * Time.deltaTime;
            transform.Rotate(0f, look.x * amount, 0f);
            pitch = Mathf.Clamp(pitch - look.y * amount, -80f, 80f);

            if (head != null)
            {
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private bool IsGrounded()
        {
            return transform.position.y <= groundY + 0.01f;
        }

        private void ApplyPunchPose(Transform hand, Vector3 restPosition, bool punching)
        {
            if (hand == null)
            {
                return;
            }

            var target = restPosition + Vector3.forward * (punching ? punchDistance : 0f);
            hand.localPosition = Vector3.Lerp(hand.localPosition, target, 1f - Mathf.Exp(-punchSpeed * Time.deltaTime));
        }
    }
}
