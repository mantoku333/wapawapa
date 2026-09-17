using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Wapawapa.Editor
{
    public static class AvatarLocomotionAssets
    {
        public const string Folder = "Assets/_Project/Animations/Player";
        public const string ControllerPath = Folder + "/PlayerLocomotion.controller";

        [MenuItem("Tools/Wapawapa/Create Player Locomotion Assets")]
        public static void Create()
        {
            // Preserve subsequent artist edits to the controller and imports.
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null) return;
            var idle = Import("Idle", true);
            var forward = Import("Running", true);
            var backward = Import("Running Backward", true);
            var left = Import("Left Strafe", true);
            var right = Import("Right Strafe", true);
            var jump = Import("Jump", false);
            var falling = Import("Falling Idle", true);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("PlaybackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            var parameters = controller.parameters;
            foreach (var parameter in parameters)
            {
                if (parameter.name == "Grounded") parameter.defaultBool = true;
                if (parameter.name == "PlaybackSpeed") parameter.defaultFloat = 1f;
            }
            controller.parameters = parameters;
            var mask = new AvatarMask { name = "Lower Body" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                    i == (int)AvatarMaskBodyPart.Root || i == (int)AvatarMaskBodyPart.Body
                    || i == (int)AvatarMaskBodyPart.LeftLeg || i == (int)AvatarMaskBodyPart.RightLeg);
            AssetDatabase.CreateAsset(mask, Folder + "/LowerBody.mask");
            var layers = controller.layers;
            layers[0].avatarMask = mask;
            controller.layers = layers;
            var tree = new BlendTree
            {
                name = "Directional locomotion", blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX", blendParameterY = "MoveZ", useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, Vector2.zero);
            tree.AddChild(forward, Vector2.up);
            tree.AddChild(backward, Vector2.down);
            tree.AddChild(left, Vector2.left);
            tree.AddChild(right, Vector2.right);
            var machine = controller.layers[0].stateMachine;
            var ground = machine.AddState("Locomotion");
            ground.motion = tree;
            ground.speedParameter = "PlaybackSpeed";
            ground.speedParameterActive = true;
            var ascent = machine.AddState("Jump");
            ascent.motion = jump;
            var descent = machine.AddState("Fall");
            descent.motion = falling;
            machine.defaultState = ground;
            Transition(ground, ascent, false).AddCondition(AnimatorConditionMode.Greater, 0.2f, "VerticalSpeed");
            Transition(ground, descent, false).AddCondition(AnimatorConditionMode.Less, 0.2f, "VerticalSpeed");
            Transition(ascent, ground, true);
            Transition(descent, ground, true);
            var apex = ascent.AddTransition(descent);
            ConfigureTransition(apex);
            apex.AddCondition(AnimatorConditionMode.Less, -0.1f, "VerticalSpeed");
            AssetDatabase.SaveAssets();
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool grounded)
        {
            var transition = from.AddTransition(to);
            ConfigureTransition(transition);
            transition.AddCondition(grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Grounded");
            return transition;
        }

        private static void ConfigureTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.12f;
        }

        private static AnimationClip Import(string name, bool loop)
        {
            string path = Folder + "/Motions/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing motion: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = name;
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Invalid Humanoid motion: " + path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        }
    }
}
