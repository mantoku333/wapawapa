using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float punchDistance = 0.65f;
        [SerializeField] private float punchSpeed = 12f;

        private Vector3 leftRestLocalPosition;
        private Vector3 rightRestLocalPosition;
        private float pitch;
        private float verticalVelocity;
        private float groundY;

        private void Awake()
        {
            groundY = transform.position.y;

            if (leftHand != null)
            {
                leftRestLocalPosition = leftHand.localPosition;
            }

            if (rightHand != null)
            {
                rightRestLocalPosition = rightHand.localPosition;
            }
        }

        private void Update()
        {
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

            var mouse = Mouse.current;
            var leftPunching = mouse != null && mouse.leftButton.isPressed;
            var rightPunching = mouse != null && mouse.rightButton.isPressed;
            ApplyPunchPose(leftHand, leftRestLocalPosition, leftPunching);
            ApplyPunchPose(rightHand, rightRestLocalPosition, rightPunching);
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
