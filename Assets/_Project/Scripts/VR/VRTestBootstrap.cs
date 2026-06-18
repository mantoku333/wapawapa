using UnityEngine;
using UnityEngine.InputSystem;

public sealed class VRTestBootstrap : MonoBehaviour
{
    private const string RigName = "VR Test Rig";

    private void Awake()
    {
        if (GameObject.Find(RigName) != null)
        {
            return;
        }

        CreateEnvironment();
        CreateRig();
    }

    private static void CreateEnvironment()
    {
        RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.58f);

        if (Object.FindFirstObjectByType<Light>() == null)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "VR Test Floor";
        floor.transform.position = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(12f, 0.1f, 12f);
        floor.GetComponent<Renderer>().material = CreateMaterial("Floor Material", new Color(0.26f, 0.30f, 0.30f));

        CreateTarget(new Vector3(-1.6f, 0.75f, 5.5f), new Color(0.9f, 0.35f, 0.22f));
        CreateTarget(new Vector3(0f, 1.1f, 6.25f), new Color(0.16f, 0.62f, 0.86f));
        CreateTarget(new Vector3(1.6f, 0.75f, 5.5f), new Color(0.95f, 0.82f, 0.25f));
    }

    private static void CreateTarget(Vector3 position, Color color)
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = "Projectile Test Target";
        target.transform.position = position;
        target.transform.localScale = new Vector3(0.75f, 0.75f, 0.18f);
        target.GetComponent<Renderer>().material = CreateMaterial("Target Material", color);

        var body = target.AddComponent<Rigidbody>();
        body.isKinematic = true;
    }

    private static void CreateRig()
    {
        var rig = new GameObject(RigName);
        rig.transform.position = Vector3.zero;

        var controller = rig.AddComponent<CharacterController>();
        controller.radius = 0.28f;
        controller.height = 1.65f;
        controller.center = new Vector3(0f, 0.825f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;

        var cameraObject = new GameObject("XR Head Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 150f;
        cameraObject.AddComponent<AudioListener>();

        var leftHand = CreateHand(rig.transform, "Left Hand", new Vector3(-0.25f, 1.25f, 0.45f), new Color(0.22f, 0.65f, 1f));
        var rightHand = CreateHand(rig.transform, "Right Hand", new Vector3(0.25f, 1.25f, 0.45f), new Color(1f, 0.47f, 0.22f));

        var movement = rig.AddComponent<VRTestRig>();
        movement.Configure(camera.transform, leftHand, rightHand);

        var leftLauncher = leftHand.gameObject.AddComponent<VRProjectileLauncher>();
        leftLauncher.Configure(VRInputHand.Left, leftHand, camera.transform, false);

        var rightLauncher = rightHand.gameObject.AddComponent<VRProjectileLauncher>();
        rightLauncher.Configure(VRInputHand.Right, rightHand, camera.transform, true);
    }

    private static Transform CreateHand(Transform parent, string name, Vector3 fallbackLocalPosition, Color color)
    {
        var hand = new GameObject(name);
        hand.transform.SetParent(parent, false);
        hand.transform.localPosition = fallbackLocalPosition;
        hand.transform.localRotation = Quaternion.identity;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Controller Visual";
        visual.transform.SetParent(hand.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        visual.transform.localScale = new Vector3(0.08f, 0.08f, 0.22f);
        visual.GetComponent<Renderer>().material = CreateMaterial(name + " Material", color);

        var collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        return hand.transform;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        return material;
    }
}

internal sealed class VRTestRig : MonoBehaviour
{
    private const float MinControllerHeight = 1f;

    private Transform head;
    private Transform leftHand;
    private Transform rightHand;
    private CharacterController characterController;
    private float verticalVelocity;
    private float fallbackPitch;
    private float snapTurnTimer;
    private InputAction headPositionAction;
    private InputAction headRotationAction;
    private InputAction leftHandPositionAction;
    private InputAction leftHandRotationAction;
    private InputAction rightHandPositionAction;
    private InputAction rightHandRotationAction;
    private InputAction moveAction;
    private InputAction turnAction;
    private InputAction mouseDeltaAction;

    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float snapTurnDegrees = 30f;
    [SerializeField] private float snapTurnCooldown = 0.28f;

    public void Configure(Transform headTransform, Transform leftHandTransform, Transform rightHandTransform)
    {
        head = headTransform;
        leftHand = leftHandTransform;
        rightHand = rightHandTransform;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        CreateActions();
    }

    private void OnEnable()
    {
        SetActionsEnabled(true);
    }

    private void OnDisable()
    {
        SetActionsEnabled(false);
    }

    private void OnDestroy()
    {
        headPositionAction?.Dispose();
        headRotationAction?.Dispose();
        leftHandPositionAction?.Dispose();
        leftHandRotationAction?.Dispose();
        rightHandPositionAction?.Dispose();
        rightHandRotationAction?.Dispose();
        moveAction?.Dispose();
        turnAction?.Dispose();
        mouseDeltaAction?.Dispose();
    }

    private void Update()
    {
        UpdateTrackedPose(headPositionAction, headRotationAction, head, head.localPosition, head.localRotation);
        UpdateTrackedPose(leftHandPositionAction, leftHandRotationAction, leftHand, leftHand.localPosition, leftHand.localRotation);
        UpdateTrackedPose(rightHandPositionAction, rightHandRotationAction, rightHand, rightHand.localPosition, rightHand.localRotation);

        UpdateFallbackLook();
        UpdateCharacterController();
        Move();
        SnapTurn();
    }

    private void UpdateFallbackLook()
    {
        if (HasAnyControl(headPositionAction) || HasAnyControl(headRotationAction))
        {
            return;
        }

        var mouseDelta = mouseDeltaAction.ReadValue<Vector2>();
        var yaw = mouseDelta.x * 0.08f;
        var pitch = mouseDelta.y * 0.08f;

        transform.Rotate(Vector3.up, yaw, Space.World);
        fallbackPitch = Mathf.Clamp(fallbackPitch - pitch, -75f, 75f);
        head.localRotation = Quaternion.Euler(fallbackPitch, 0f, 0f);
    }

    private void UpdateCharacterController()
    {
        var cameraLocalPosition = head.localPosition;
        var height = Mathf.Max(MinControllerHeight, cameraLocalPosition.y);

        characterController.height = height;
        characterController.center = new Vector3(cameraLocalPosition.x, height * 0.5f, cameraLocalPosition.z);
    }

    private void Move()
    {
        var axis = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        var planarMovement = (forward * axis.y + right * axis.x);

        if (planarMovement.sqrMagnitude > 1f)
        {
            planarMovement.Normalize();
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        var velocity = planarMovement * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void SnapTurn()
    {
        snapTurnTimer -= Time.deltaTime;
        var axis = turnAction.ReadValue<Vector2>();

        if (snapTurnTimer > 0f || Mathf.Abs(axis.x) < 0.75f)
        {
            return;
        }

        transform.Rotate(Vector3.up, Mathf.Sign(axis.x) * snapTurnDegrees, Space.World);
        snapTurnTimer = snapTurnCooldown;
    }

    private void CreateActions()
    {
        headPositionAction = CreateVector3Action("Head Position", "<XRHMD>/centerEyePosition", "<XRHMD>/devicePosition");
        headRotationAction = CreateQuaternionAction("Head Rotation", "<XRHMD>/centerEyeRotation", "<XRHMD>/deviceRotation");
        leftHandPositionAction = CreateVector3Action("Left Hand Position", "<XRController>{LeftHand}/devicePosition");
        leftHandRotationAction = CreateQuaternionAction("Left Hand Rotation", "<XRController>{LeftHand}/deviceRotation");
        rightHandPositionAction = CreateVector3Action("Right Hand Position", "<XRController>{RightHand}/devicePosition");
        rightHandRotationAction = CreateQuaternionAction("Right Hand Rotation", "<XRController>{RightHand}/deviceRotation");

        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        turnAction = new InputAction("Snap Turn", InputActionType.Value, expectedControlType: "Vector2");
        turnAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
        turnAction.AddBinding("<Gamepad>/rightStick");

        mouseDeltaAction = new InputAction("Fallback Mouse Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetEnabled(headPositionAction, enabled);
        SetEnabled(headRotationAction, enabled);
        SetEnabled(leftHandPositionAction, enabled);
        SetEnabled(leftHandRotationAction, enabled);
        SetEnabled(rightHandPositionAction, enabled);
        SetEnabled(rightHandRotationAction, enabled);
        SetEnabled(moveAction, enabled);
        SetEnabled(turnAction, enabled);
        SetEnabled(mouseDeltaAction, enabled);
    }

    private static void SetEnabled(InputAction action, bool enabled)
    {
        if (action == null)
        {
            return;
        }

        if (enabled)
        {
            action.Enable();
        }
        else
        {
            action.Disable();
        }
    }

    private static InputAction CreateVector3Action(string name, params string[] bindings)
    {
        var action = new InputAction(name, InputActionType.Value, expectedControlType: "Vector3");

        foreach (var binding in bindings)
        {
            action.AddBinding(binding);
        }

        return action;
    }

    private static InputAction CreateQuaternionAction(string name, params string[] bindings)
    {
        var action = new InputAction(name, InputActionType.Value, expectedControlType: "Quaternion");

        foreach (var binding in bindings)
        {
            action.AddBinding(binding);
        }

        return action;
    }

    private static void UpdateTrackedPose(InputAction positionAction, InputAction rotationAction, Transform target, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (target == null)
        {
            return;
        }

        if (TryReadPose(positionAction, rotationAction, out var position, out var rotation))
        {
            target.localPosition = position;
            target.localRotation = rotation;
            return;
        }

        target.localPosition = fallbackPosition;
        target.localRotation = fallbackRotation;
    }

    private static bool TryReadPose(InputAction positionAction, InputAction rotationAction, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        var hasPosition = HasAnyControl(positionAction);
        if (hasPosition)
        {
            position = positionAction.ReadValue<Vector3>();
        }

        var hasRotation = HasAnyControl(rotationAction);
        if (hasRotation)
        {
            rotation = rotationAction.ReadValue<Quaternion>();
            hasRotation = rotation.x != 0f || rotation.y != 0f || rotation.z != 0f || rotation.w != 0f;
        }

        return hasPosition || hasRotation;
    }

    private static bool HasAnyControl(InputAction action)
    {
        return action != null && action.enabled && action.controls.Count > 0;
    }
}

internal enum VRInputHand
{
    Left,
    Right
}

internal sealed class VRProjectileLauncher : MonoBehaviour
{
    private Transform muzzle;
    private Transform fallbackCamera;
    private InputAction fireAction;
    private InputAction fallbackFireAction;
    private bool wasPressed;
    private float cooldownTimer;
    private Material projectileMaterial;
    private bool allowFallbackFire;

    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileRadius = 0.08f;
    [SerializeField] private float projectileLifetime = 6f;
    [SerializeField] private float fireCooldown = 0.18f;

    public void Configure(VRInputHand hand, Transform muzzleTransform, Transform fallbackCameraTransform, bool enableFallbackFire)
    {
        muzzle = muzzleTransform;
        fallbackCamera = fallbackCameraTransform;
        allowFallbackFire = enableFallbackFire;

        fireAction?.Dispose();
        fireAction = CreateFireAction(hand);

        if (isActiveAndEnabled)
        {
            fireAction.Enable();
        }
    }

    private void Awake()
    {
        projectileMaterial = new Material(Shader.Find("Standard"));
        projectileMaterial.name = "VR Projectile Material";
        projectileMaterial.color = new Color(0.95f, 0.95f, 1f);

        fallbackFireAction = new InputAction("Fallback Fire", InputActionType.Button);
        fallbackFireAction.AddBinding("<Mouse>/leftButton");
        fallbackFireAction.AddBinding("<Keyboard>/space");
    }

    private void OnEnable()
    {
        fireAction?.Enable();
        fallbackFireAction?.Enable();
    }

    private void OnDisable()
    {
        fireAction?.Disable();
        fallbackFireAction?.Disable();
    }

    private void OnDestroy()
    {
        fireAction?.Dispose();
        fallbackFireAction?.Dispose();
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        var pressed = fireAction != null && fireAction.IsPressed();
        if (allowFallbackFire && fallbackFireAction.IsPressed())
        {
            pressed = true;
        }

        if (pressed && !wasPressed && cooldownTimer <= 0f)
        {
            Fire();
            cooldownTimer = fireCooldown;
        }

        wasPressed = pressed;
    }

    private void Fire()
    {
        var source = muzzle != null ? muzzle : fallbackCamera;
        var direction = source.forward;
        var spawnPosition = source.position + direction * 0.18f;

        if (muzzle == null && fallbackCamera != null)
        {
            direction = fallbackCamera.forward;
            spawnPosition = fallbackCamera.position + direction * 0.35f;
        }

        var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "VR Projectile";
        projectile.transform.position = spawnPosition;
        projectile.transform.localScale = Vector3.one * (projectileRadius * 2f);
        projectile.GetComponent<Renderer>().material = projectileMaterial;

        var body = projectile.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction.normalized * projectileSpeed;

        Object.Destroy(projectile, projectileLifetime);
    }

    private static InputAction CreateFireAction(VRInputHand hand)
    {
        var usage = hand == VRInputHand.Left ? "LeftHand" : "RightHand";
        var action = new InputAction(hand + " Trigger Fire", InputActionType.Button);
        action.AddBinding("<XRController>{" + usage + "}/triggerPressed");
        action.AddBinding("<XRController>{" + usage + "}/triggerButton");
        action.AddBinding("<XRController>{" + usage + "}/trigger");
        return action;
    }
}
