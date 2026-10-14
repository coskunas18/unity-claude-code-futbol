using System.Collections.Generic;
using Football.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Football.EditorTools
{
    /// <summary>
    /// Builds the player's Animator Controller from the Mixamo clips: a 2D freeform-directional locomotion blend tree
    /// whose clip positions are each clip's measured natural velocity, plus one-shot Pass and Kick states.
    /// Natural velocity = how fast the planted foot slides back under the body, sampled from the clip itself.
    /// Re-run after adding or replacing clips (Football > Build Player Animator).
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        public const string CharacterPath = "Assets/Football/Art/Characters/Mixamo/FootballPlayer_v1.fbx";
        public const string ClipFolder = "Assets/Football/Art/Characters/Mixamo/Animations";
        public const string ControllerPath = "Assets/Football/Art/Characters/Mixamo/FootballPlayer.controller";

        public static readonly int PassTrigger = Animator.StringToHash("Pass");
        public static readonly int KickTrigger = Animator.StringToHash("Kick");

        static readonly string[] LocomotionClips = { "Walk", "JogForward", "JogBackward", "StrafeLeft", "StrafeRight", "Run" };

        public readonly struct Result
        {
            public readonly AnimatorController Controller;
            public readonly LocomotionSpeedLimits Limits;
            public readonly string Report;

            public Result(AnimatorController controller, LocomotionSpeedLimits limits, string report)
            {
                Controller = controller;
                Limits = limits;
                Report = report;
            }
        }

        [MenuItem("Football/Build Player Animator")]
        static void BuildFromMenu() => Debug.Log($"[Football] {Build().Report}");

        public static Result Build()
        {
            var character = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            if (character == null)
                throw new System.IO.FileNotFoundException($"Character not found at {CharacterPath}");

            AnimationClip idle = LoadClip("Idle");
            var velocities = new Dictionary<string, Vector2>();
            var report = new System.Text.StringBuilder("Natural velocities: ");
            foreach (string name in LocomotionClips)
            {
                Vector2 velocity = MeasureNaturalVelocity(character, LoadClip(name));
                velocities[name] = velocity;
                report.Append($"{name}=({velocity.x:0.00},{velocity.y:0.00}) ");
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("VelX", AnimatorControllerParameterType.Float);
            controller.AddParameter("VelZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Pass", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Kick", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "VelX";
            tree.blendParameterY = "VelZ";
            tree.AddChild(idle, Vector2.zero);
            foreach (string name in LocomotionClips)
                tree.AddChild(LoadClip(name), velocities[name]);
            machine.defaultState = locomotion;

            AddOneShot(machine, locomotion, LoadClip("Pass"), "Pass");
            AddOneShot(machine, locomotion, LoadClip("PenaltyKick"), "Kick");

            var limits = new LocomotionSpeedLimits(
                Mathf.Max(velocities["Run"].y, velocities["JogForward"].y),
                Mathf.Abs(velocities["JogBackward"].y),
                Mathf.Abs(velocities["StrafeLeft"].x),
                Mathf.Abs(velocities["StrafeRight"].x));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            report.Append($"| limits fwd={limits.forward:0.00} back={limits.backward:0.00} left={limits.left:0.00} right={limits.right:0.00}");
            return new Result(controller, limits, report.ToString());
        }

        static void AddOneShot(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip, string trigger)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;

            AnimatorStateTransition enter = locomotion.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.1f;

            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.2f;
        }

        static AnimationClip LoadClip(string name)
        {
            string path = $"{ClipFolder}/{name}.fbx";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    return clip;
            }
            throw new System.IO.FileNotFoundException($"No animation clip in {path}");
        }

        /// <summary>
        /// Average horizontal velocity of the planted foot relative to the body, negated: the speed the body would
        /// travel at if the planted foot did not slide. A foot counts as planted within 3 cm of its lowest point.
        /// </summary>
        public static Vector2 MeasureNaturalVelocity(GameObject characterPrefab, AnimationClip clip, int samples = 90)
        {
            var instance = (GameObject)Object.Instantiate(characterPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = instance.GetComponent<Animator>();
                Transform[] feet =
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                    animator.GetBoneTransform(HumanBodyBones.RightFoot)
                };
                var positions = new Vector3[2, samples + 1];
                for (int k = 0; k <= samples; k++)
                {
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clip.SampleAnimation(instance, clip.length * k / samples);
                    for (int f = 0; f < 2; f++)
                        positions[f, k] = feet[f].position;
                }

                Vector3 sum = Vector3.zero;
                int count = 0;
                float step = clip.length / samples;
                for (int f = 0; f < 2; f++)
                {
                    float lowest = float.MaxValue;
                    for (int k = 0; k <= samples; k++)
                        lowest = Mathf.Min(lowest, positions[f, k].y);
                    for (int k = 0; k < samples; k++)
                    {
                        if (positions[f, k].y > lowest + 0.03f || positions[f, k + 1].y > lowest + 0.03f)
                            continue;
                        Vector3 delta = positions[f, k + 1] - positions[f, k];
                        sum += new Vector3(delta.x, 0f, delta.z) / step;
                        count++;
                    }
                }
                Vector3 velocity = count > 0 ? -sum / count : Vector3.zero;
                return new Vector2(velocity.x, velocity.z);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
