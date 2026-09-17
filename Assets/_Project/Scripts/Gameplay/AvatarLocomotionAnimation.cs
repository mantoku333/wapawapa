using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Wapawapa.Gameplay
{
    /// <summary>Evaluates locomotion once, before tracked head and arms overwrite the animated pose.</summary>
    public sealed class AvatarLocomotionAnimation : IDisposable
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int PlaybackSpeed = Animator.StringToHash("PlaybackSpeed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private PlayableGraph graph;
        private AnimatorControllerPlayable controller;
        private Vector2 movement;

        public AvatarLocomotionAnimation(Animator animator, RuntimeAnimatorController asset)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            graph = PlayableGraph.Create("Avatar locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            controller = AnimatorControllerPlayable.Create(graph, asset);
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(controller);
            graph.Play();
        }

        public void Evaluate(Vector3 localVelocity, bool grounded, float referenceSpeed, float dt)
        {
            float speed = Mathf.Max(0.1f, referenceSpeed);
            Vector2 target = new Vector2(localVelocity.x, localVelocity.z) / speed;
            movement = Vector2.Lerp(movement, Vector2.ClampMagnitude(target, 1f), 1f - Mathf.Exp(-12f * dt));
            if (target.sqrMagnitude < 0.0001f && movement.sqrMagnitude < 0.0001f) movement = Vector2.zero;
            controller.SetFloat(MoveX, movement.x);
            controller.SetFloat(MoveZ, movement.y);
            controller.SetFloat(PlaybackSpeed, Mathf.Clamp(new Vector2(localVelocity.x, localVelocity.z).magnitude / speed, 0.65f, 1.5f));
            controller.SetFloat(VerticalSpeed, localVelocity.y);
            controller.SetBool(Grounded, grounded);
            graph.Evaluate(dt);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
