using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Wapawapa.Boxing;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace Wapawapa.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopVrNetworkPlayer : NetworkBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Camera localCamera;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float keyboardLookSpeed = 90f;
        [SerializeField] private float vrTurnSpeed = 75f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float desktopPunchDistance = 0.95f;
        [SerializeField] private float desktopPunchSpeed = 12f;
        [SerializeField] private float desktopPunchHitWindow = 0.45f;

        private CharacterController characterController;
        private PunchHitbox leftPunchHitbox;
        private PunchHitbox rightPunchHitbox;
        private Vector2 accumulatedMouseDelta;
        private float desktopPitch;
        private bool jumpRequested;
        private bool leftPunchRequested;
        private bool rightPunchRequested;
        private float verticalVelocity;
        private bool xrTrackingAvailable;

        private Vector3 trackedHeadPosition;
        private Quaternion trackedHeadRotation = Quaternion.identity;
        private Vector3 trackedLeftHandPosition;
        private Quaternion trackedLeftHandRotation = Quaternion.identity;
        private Vector3 trackedRightHandPosition;
        private Quaternion trackedRightHandRotation = Quaternion.identity;
        private bool hasTrackedLeftHandPose;
        private bool hasTrackedRightHandPose;

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyLocalXrPoseBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyLocalXrPoseBeforeRender;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            leftPunchHitbox = leftHand != null ? leftHand.GetComponent<PunchHitbox>() : null;
            rightPunchHitbox = rightHand != null ? rightHand.GetComponent<PunchHitbox>() : null;
        }

        public override void Spawned()
        {
            var isLocal = HasStateAuthority;
            if (localCamera != null)
            {
                localCamera.enabled = isLocal;
                var listener = localCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = isLocal;
                }
            }

            SetLocalHeadVisible(!isLocal);

            if (isLocal && !IsXrDisplayRunning())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ApplyPlayerColor();
        }

        private void Update()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            xrTrackingAvailable = TryReadXrRig();
            if (xrTrackingAvailable)
            {
                // Keep the local camera responsive at the render frame rate. The same
                // pose is applied again in FixedUpdateNetwork for network replication.
                ApplyTrackedRig();
            }
            else
            {
                CaptureDesktopLook();
                CaptureKeyboardLook();
                CaptureDesktopJump();
                CaptureDesktopPunch();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            var moveInput = ReadMovement();
            if (xrTrackingAvailable)
            {
                var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rightDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out var turnInput))
                {
                    transform.Rotate(0f, turnInput.x * vrTurnSpeed * Runner.DeltaTime, 0f);
                }

                ApplyTrackedRig();
            }
            else
            {
                transform.Rotate(0f, accumulatedMouseDelta.x * mouseSensitivity, 0f);
                desktopPitch = Mathf.Clamp(desktopPitch - accumulatedMouseDelta.y * mouseSensitivity, -80f, 80f);
                head.localPosition = new Vector3(0f, 1.65f, 0f);
                head.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
                var mouse = Mouse.current;
                var leftPunching = mouse != null && mouse.leftButton.isPressed;
                var rightPunching = mouse != null && mouse.rightButton.isPressed;
                if (leftPunchRequested)
                {
                    leftPunchHitbox?.StartManualPunch(transform.forward, desktopPunchHitWindow);
                }

                if (rightPunchRequested)
                {
                    rightPunchHitbox?.StartManualPunch(transform.forward, desktopPunchHitWindow);
                }

                leftPunchRequested = false;
                rightPunchRequested = false;
                var leftHandTarget = new Vector3(-0.32f, 1.25f, 0.38f + (leftPunching ? desktopPunchDistance : 0f));
                var rightHandTarget = new Vector3(0.32f, 1.25f, 0.38f + (rightPunching ? desktopPunchDistance : 0f));
                leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, leftHandTarget, 1f - Mathf.Exp(-desktopPunchSpeed * Runner.DeltaTime));
                leftHand.localRotation = Quaternion.identity;
                rightHand.localPosition = Vector3.Lerp(rightHand.localPosition, rightHandTarget, 1f - Mathf.Exp(-desktopPunchSpeed * Runner.DeltaTime));
                rightHand.localRotation = Quaternion.identity;
                accumulatedMouseDelta = Vector2.zero;
            }

            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            var movement = (forward * moveInput.y + right * moveInput.x) * moveSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (characterController.isGrounded && jumpRequested)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            jumpRequested = false;
            verticalVelocity += gravity * Runner.DeltaTime;
            movement.y = verticalVelocity;
            characterController.Move(movement * Runner.DeltaTime);
        }

        private Vector2 ReadMovement()
        {
            var movement = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) movement.y += 1f;
                if (keyboard.sKey.isPressed) movement.y -= 1f;
                if (keyboard.dKey.isPressed) movement.x += 1f;
                if (keyboard.aKey.isPressed) movement.x -= 1f;
            }

            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftDevice.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out var stick))
            {
                movement += stick;
            }

            return Vector2.ClampMagnitude(movement, 1f);
        }

        private void CaptureDesktopLook()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.middleButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                accumulatedMouseDelta += mouse.delta.ReadValue();
            }
        }

        private void CaptureDesktopJump()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        private void CaptureDesktopPunch()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            leftPunchRequested |= mouse.leftButton.wasPressedThisFrame;
            rightPunchRequested |= mouse.rightButton.wasPressedThisFrame;
        }

        private void CaptureKeyboardLook()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var look = Vector2.zero;
            if (keyboard.rightArrowKey.isPressed) look.x += 1f;
            if (keyboard.leftArrowKey.isPressed) look.x -= 1f;
            if (keyboard.upArrowKey.isPressed) look.y += 1f;
            if (keyboard.downArrowKey.isPressed) look.y -= 1f;

            if (look == Vector2.zero)
            {
                return;
            }

            accumulatedMouseDelta += look * (keyboardLookSpeed / mouseSensitivity * Time.deltaTime);
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
            head.localPosition = trackedHeadPosition;
            head.localRotation = trackedHeadRotation;
            if (hasTrackedLeftHandPose)
            {
                leftHand.localPosition = trackedLeftHandPosition;
                leftHand.localRotation = trackedLeftHandRotation;
            }

            if (hasTrackedRightHandPose)
            {
                rightHand.localPosition = trackedRightHandPosition;
                rightHand.localRotation = trackedRightHandRotation;
            }
        }

        private void ApplyLocalXrPoseBeforeRender()
        {
            if (Object == null || !Object.HasStateAuthority || !TryReadXrRig())
            {
                return;
            }

            xrTrackingAvailable = true;
            ApplyTrackedRig();
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

        private void SetLocalHeadVisible(bool visible)
        {
            if (head == null)
            {
                return;
            }

            var renderer = head.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private void ApplyPlayerColor()
        {
            var bodyRenderer = GetComponent<Renderer>();
            if (bodyRenderer == null)
            {
                return;
            }

            var color = Object.StateAuthority.PlayerId % 2 == 0
                ? new Color(0.2f, 0.65f, 1f)
                : new Color(1f, 0.4f, 0.25f);
            bodyRenderer.material.color = color;
        }
    }
}
