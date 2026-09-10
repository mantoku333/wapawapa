using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wapawapa.Gameplay
{
    /// <summary>Visual-only three-point avatar. Gameplay tracking targets remain independent of bones.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class TrackedAvatar : MonoBehaviour
    {
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private Vector3 modelEyePosition = new Vector3(0f, 1.8396751f, 0.13871512f);
        [SerializeField] private float standingEyeHeight = 1.65f;
        [SerializeField] private Vector3 wristOffset = new Vector3(0f, 0f, -0.04f);
        [SerializeField] private Vector3 leftWristEuler;
        [SerializeField] private Vector3 rightWristEuler;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float stepDistance = 0.22f;
        [SerializeField] private float stepDuration = 0.18f;
        [SerializeField] private float stepHeight = 0.10f;

        private Animator animator;
        private Transform hips, skull;
        private Transform[] bones;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Vector3 eyeInHead;
        private Quaternion skullOffset;
        private Limb leftArm, rightArm, leftLeg, rightLeg;
        private readonly List<Finger> fingers = new List<Finger>();
        private readonly List<HeadMesh> headMeshes = new List<HeadMesh>();
        private AvatarBoneFollower[] followers;
        private Vector4 requestedHands, hands;
        private bool ready, localView;
        private float bodyYaw;
        private Vector3 previousBody;
        private Vector3 velocity;
        private readonly RaycastHit[] floorHits = new RaycastHit[32];
        private Foot leftFoot, rightFoot;

        private sealed class Limb
        {
            public Transform upper, lower, tip;
            public Quaternion rotationOffset;
        }
        private sealed class Finger
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public bool left, index;
            public float angle;
        }
        private sealed class Foot
        {
            public Vector3 position, start, goal, home;
            public Quaternion rotation;
            public float phase = 1f;
        }
        private sealed class HeadMesh
        {
            public SkinnedMeshRenderer renderer;
            public Mesh full, firstPerson;
        }

        public void SetHandInput(Vector4 input)
        {
            requestedHands = new Vector4(Mathf.Clamp01(input.x), Mathf.Clamp01(input.y), Mathf.Clamp01(input.z), Mathf.Clamp01(input.w));
        }

        public void SetLocalView(bool value) => localView = value;

        public Vector3 ClampDesktopHandPosition(Vector3 localPosition, bool left)
        {
            if (!ready) return localPosition;
            Limb arm = left ? leftArm : rightArm;
            Transform target = left ? leftHandTarget : rightHandTarget;
            float reach = Vector3.Distance(arm.upper.position, arm.lower.position) + Vector3.Distance(arm.lower.position, arm.tip.position) - 0.01f;
            Vector3 offset = target.rotation * wristOffset;
            Vector3 desired = transform.TransformPoint(localPosition) + offset;
            Vector3 wrist = arm.upper.position + Vector3.ClampMagnitude(desired - arm.upper.position, reach);
            return transform.InverseTransformPoint(wrist - offset);
        }

        private void Awake() => Initialize();
        private void OnEnable()
        {
            Application.onBeforeRender += BeforeRender;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            Application.onBeforeRender -= BeforeRender;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreMeshes();
        }
        private void OnDestroy()
        {
            foreach (var entry in headMeshes)
            {
                if (entry.firstPerson == null) continue;
                if (Application.isPlaying) Destroy(entry.firstPerson);
                else DestroyImmediate(entry.firstPerson);
            }
        }

        public bool Initialize()
        {
            if (ready) return true;
            if (avatarRoot == null || headTarget == null || leftHandTarget == null || rightHandTarget == null) return false;
            animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
            {
                Debug.LogError("TrackedAvatar requires a valid Humanoid avatar.", this);
                return false;
            }
            // This solver owns the pose; an Animator Controller must not overwrite it afterwards.
            animator.applyRootMotion = false;
            animator.enabled = false;
            avatarRoot.localScale = Vector3.one * (standingEyeHeight / modelEyePosition.y);
            avatarRoot.localPosition = Vector3.zero;
            avatarRoot.localRotation = Quaternion.identity;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            skull = animator.GetBoneTransform(HumanBodyBones.Head);
            leftArm = MakeLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            rightArm = MakeLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            leftLeg = MakeLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            rightLeg = MakeLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            if (hips == null || skull == null || leftArm == null || rightArm == null || leftLeg == null || rightLeg == null) return false;
            eyeInHead = skull.InverseTransformPoint(avatarRoot.TransformPoint(modelEyePosition));
            skullOffset = Quaternion.Inverse(avatarRoot.rotation) * skull.rotation;
            SetupHand(leftArm, true);
            SetupHand(rightArm, false);
            bones = avatarRoot.GetComponentsInChildren<Transform>(true);
            restPositions = new Vector3[bones.Length];
            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restPositions[i] = bones[i].localPosition;
                restRotations[i] = bones[i].localRotation;
            }
            leftFoot = MakeFoot(leftLeg);
            rightFoot = MakeFoot(rightLeg);
            followers = avatarRoot.GetComponentsInChildren<AvatarBoneFollower>(true);
            foreach (var follower in followers) follower.Initialize();
            bodyYaw = transform.eulerAngles.y;
            previousBody = headTarget.position;
            BuildFirstPersonMeshes();
            if (ownerCamera != null) ownerCamera.nearClipPlane = 0.025f;
            ready = true;
            return true;
        }

        private Limb MakeLimb(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
        {
            var limb = new Limb { upper = animator.GetBoneTransform(a), lower = animator.GetBoneTransform(b), tip = animator.GetBoneTransform(c) };
            if (limb.upper == null || limb.lower == null || limb.tip == null) return null;
            limb.rotationOffset = Quaternion.Inverse(avatarRoot.rotation) * limb.tip.rotation;
            return limb;
        }

        private void SetupHand(Limb arm, bool left)
        {
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (index == null || little == null || middle == null) return;
            var forward = (middle.position - arm.tip.position).normalized;
            var dorsal = Vector3.Cross(index.position - little.position, forward).normalized * (left ? 1f : -1f);
            if (Vector3.Dot(dorsal, avatarRoot.up) < 0f) dorsal = -dorsal;
            arm.rotationOffset = Quaternion.Inverse(Quaternion.LookRotation(forward, dorsal)) * arm.tip.rotation;
            // Unity's Humanoid finger enum is ordered as 5 groups of 3 joints per hand.
            int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int digit = 0; digit < 5; digit++)
                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = animator.GetBoneTransform((HumanBodyBones)(first + digit * 3 + joint));
                    if (bone == null) continue;
                    Vector3 direction = bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
                    Vector3 inward = digit == 0 ? (middle.position - bone.position).normalized - dorsal * 0.6f : -dorsal;
                    var axis = Vector3.Cross(direction.normalized, inward.normalized).normalized;
                    fingers.Add(new Finger { bone = bone, rest = bone.localRotation, axis = bone.InverseTransformDirection(axis),
                        left = left, index = digit == 1, angle = digit == 0 ? (joint == 0 ? 35f : 45f) : (joint == 0 ? 70f : 85f) });
                }
        }

        private Foot MakeFoot(Limb leg)
        {
            Vector3 home = avatarRoot.InverseTransformPoint(leg.tip.position) * avatarRoot.localScale.x;
            return new Foot { position = leg.tip.position, goal = leg.tip.position, home = home, rotation = leg.rotationOffset };
        }

        private void LateUpdate() => Simulate(Time.deltaTime);

        /// <summary>Called once per frame; also used by the isolated avatar integration checks.</summary>
        public void Simulate(float deltaTime)
        {
            if (!ready && !Initialize()) return;
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            hands = Vector4.Lerp(hands, requestedHands, 1f - Mathf.Exp(-24f * dt));
            Vector3 heading = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
            if (heading.sqrMagnitude > 0.01f)
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, Quaternion.LookRotation(heading).eulerAngles.y, 180f * dt);
            Vector3 displacement = Vector3.ProjectOnPlane(headTarget.position - previousBody, Vector3.up);
            velocity = dt > 0f ? Vector3.ClampMagnitude(displacement / dt, 5f) : Vector3.zero;
            previousBody = headTarget.position;
            UpdateFeet(dt);
            ApplyPose();
        }

        [BeforeRenderOrder(100)]
        private void BeforeRender()
        {
            // Tracking rigs run first. Do not advance gait twice in the same frame.
            if (ready && localView) ApplyPose();
        }

        private void ApplyPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == avatarRoot) continue;
                bones[i].localPosition = restPositions[i];
                bones[i].localRotation = restRotations[i];
            }
            avatarRoot.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            avatarRoot.position = new Vector3(headTarget.position.x, transform.position.y, headTarget.position.z);
            skull.rotation = headTarget.rotation * skullOffset;
            // Match the avatar's eyes, rather than its head-bone pivot, to the HMD.
            hips.position += headTarget.position - skull.TransformPoint(eyeInHead);
            SolveLimb(leftLeg.upper, leftLeg.lower, leftLeg.tip, leftFoot.position,
                avatarRoot.position + avatarRoot.forward * 2f, Quaternion.Euler(0f, bodyYaw, 0f) * leftFoot.rotation);
            SolveLimb(rightLeg.upper, rightLeg.lower, rightLeg.tip, rightFoot.position,
                avatarRoot.position + avatarRoot.forward * 2f, Quaternion.Euler(0f, bodyYaw, 0f) * rightFoot.rotation);
            ApplyArm(leftArm, leftHandTarget, -1f, leftWristEuler);
            ApplyArm(rightArm, rightHandTarget, 1f, rightWristEuler);
            foreach (var finger in fingers)
            {
                float amount = finger.left ? (finger.index ? hands.y : hands.x) : (finger.index ? hands.w : hands.z);
                finger.bone.localRotation = finger.rest * Quaternion.AngleAxis(finger.angle * amount, finger.axis);
            }
            foreach (var follower in followers) follower.Apply();
        }

        private void ApplyArm(Limb arm, Transform target, float side, Vector3 correction)
        {
            Vector3 pole = arm.upper.position + avatarRoot.right * side * 0.5f - avatarRoot.up * 0.7f - avatarRoot.forward * 0.25f;
            SolveLimb(arm.upper, arm.lower, arm.tip, target.position + target.rotation * wristOffset,
                pole, target.rotation * Quaternion.Euler(correction) * arm.rotationOffset);
        }

        public static void SolveLimb(Transform upper, Transform lower, Transform tip, Vector3 target, Vector3 pole, Quaternion rotation)
        {
            Vector3 a = upper.position, b = lower.position, c = tip.position;
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            if (first < 0.00001f || second < 0.00001f) return;
            Vector3 delta = target - a;
            Vector3 axis = delta.sqrMagnitude > 0.0000001f ? delta.normalized : (c - a).normalized;
            if (axis.sqrMagnitude < 0.5f) axis = Vector3.forward;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + 0.00001f, first + second - 0.00001f);
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, axis).normalized;
            if (bend.sqrMagnitude < 0.5f) bend = Vector3.ProjectOnPlane(b - a, axis).normalized;
            if (bend.sqrMagnitude < 0.5f) bend = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float along = (first * first - second * second + distance * distance) / (2f * distance);
            Vector3 elbow = a + axis * along + bend * Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(tip.position - lower.position, a + axis * distance - lower.position) * lower.rotation;
            tip.rotation = rotation;
        }

        private void UpdateFeet(float dt)
        {
            var yaw = Quaternion.Euler(0f, bodyYaw, 0f);
            Vector3 center = new Vector3(headTarget.position.x, transform.position.y, headTarget.position.z);
            Vector3 Desired(Foot foot)
            {
                Vector3 desired = center + yaw * new Vector3(foot.home.x, 0f, foot.home.z) + velocity * 0.07f;
                float floor = transform.position.y;
                if (TryFindFloor(desired + Vector3.up * 0.8f, 2f, out var hit)) floor = hit.point.y;
                // In the air the feet follow the player; don't stretch the legs down to the floor.
                if (transform.position.y - floor > 0.3f) floor = transform.position.y;
                desired.y = floor + foot.home.y;
                return desired;
            }
            Vector3 leftGoal = Desired(leftFoot), rightGoal = Desired(rightFoot);
            if (Vector3.Distance(leftFoot.position, leftGoal) > 1.2f || Vector3.Distance(rightFoot.position, rightGoal) > 1.2f)
            {
                leftFoot.position = leftFoot.goal = leftGoal;
                rightFoot.position = rightFoot.goal = rightGoal;
                leftFoot.phase = rightFoot.phase = 1f;
            }
            if (leftFoot.phase >= 1f && rightFoot.phase >= 1f)
            {
                float l = Vector3.Distance(leftFoot.position, leftGoal), r = Vector3.Distance(rightFoot.position, rightGoal);
                if (Mathf.Max(l, r) > stepDistance)
                {
                    Foot moving = l >= r ? leftFoot : rightFoot;
                    moving.start = moving.position;
                    moving.goal = l >= r ? leftGoal : rightGoal;
                    moving.phase = 0f;
                }
            }
            AdvanceFoot(leftFoot, dt);
            AdvanceFoot(rightFoot, dt);
        }

        private void AdvanceFoot(Foot foot, float dt)
        {
            if (foot.phase < 1f)
            {
                foot.phase = Mathf.Min(1f, foot.phase + dt / Mathf.Max(0.06f, stepDuration));
                foot.position = Vector3.Lerp(foot.start, foot.goal, Mathf.SmoothStep(0f, 1f, foot.phase)) + Vector3.up * (Mathf.Sin(foot.phase * Mathf.PI) * stepHeight);
            }
        }

        public bool TryFindFloor(Vector3 origin, float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, floorHits, distance, groundMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = floorHits[i];
                if (hit.transform.IsChildOf(transform) || hit.normal.y < 0.5f || hit.distance >= best) continue;
                closest = hit;
                best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }

        private void BuildFirstPersonMeshes()
        {
            foreach (var renderer in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
                Mesh original = renderer.sharedMesh;
                if (original == null || !original.isReadable) continue;
                BoneWeight[] weights = original.boneWeights;
                if (weights.Length != original.vertexCount) continue;
                Transform[] rig = renderer.bones;
                bool HeadBone(int i) => i >= 0 && i < rig.Length && rig[i] != null && (rig[i] == skull || rig[i].IsChildOf(skull));
                var hidden = new bool[weights.Length];
                bool any = false;
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    float influence = (HeadBone(w.boneIndex0) ? w.weight0 : 0f) + (HeadBone(w.boneIndex1) ? w.weight1 : 0f)
                        + (HeadBone(w.boneIndex2) ? w.weight2 : 0f) + (HeadBone(w.boneIndex3) ? w.weight3 : 0f);
                    hidden[i] = influence > 0.35f;
                    any |= hidden[i];
                }
                if (!any) continue;
                Mesh copy = Instantiate(original);
                copy.name = original.name + " (owner view)";
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    int[] indices = original.GetTriangles(sub);
                    var kept = new List<int>(indices.Length);
                    for (int i = 0; i < indices.Length; i += 3)
                        if (!hidden[indices[i]] && !hidden[indices[i + 1]] && !hidden[indices[i + 2]])
                        { kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]); }
                    copy.SetTriangles(kept, sub, false);
                }
                headMeshes.Add(new HeadMesh { renderer = renderer, full = original, firstPerson = copy });
            }
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            bool hideHead = localView && camera == ownerCamera;
            foreach (var entry in headMeshes)
                if (entry.renderer != null) entry.renderer.sharedMesh = hideHead ? entry.firstPerson : entry.full;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => RestoreMeshes();
        private void RestoreMeshes()
        {
            foreach (var entry in headMeshes)
                if (entry.renderer != null) entry.renderer.sharedMesh = entry.full;
        }
    }
}
