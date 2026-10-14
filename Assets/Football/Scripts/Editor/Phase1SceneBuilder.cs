using Football.Ball;
using Football.CameraRig;
using Football.Control;
using Football.Core;
using Football.Debugging;
using Football.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Football.EditorTools
{
    /// <summary>
    /// Builds the phase 1 test scene from scratch: pitch, ball, one user-controlled player, camera and debug tools.
    /// Re-running it replaces the scene file, so do not hand-edit Phase1_Pitch.unity; change this builder instead.
    /// Menu: Football > Build Phase 1 Scene.
    /// </summary>
    public static class Phase1SceneBuilder
    {
        public const string ScenePath = FootballAssetPaths.Scenes + "/Phase1_Pitch.unity";
        const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        const string CharacterMaterialPath = "Assets/Football/Art/Characters/Prototype/FootballPlayer_v1_Lit.mat";

        [MenuItem("Football/Build Phase 1 Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        public static string Build()
        {
            FootballAssetPaths.EnsureFolder(FootballAssetPaths.Scenes);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ballSettings = FootballAssetPaths.GetOrCreateAsset<BallPhysicsSettings>(FootballAssetPaths.Data + "/BallPhysicsSettings.asset");
            var movementProfile = FootballAssetPaths.GetOrCreateAsset<MovementProfile>(FootballAssetPaths.Data + "/MovementProfile_Default.asset");

            CreateLighting();
            PitchBuilder.Build(PitchBuilder.CreateMaterials());

            BallController ball = CreateBall(ballSettings);

            var systems = new GameObject("MatchSystems");
            var input = systems.AddComponent<FootballInput>();
            var rules = systems.AddComponent<BallEventsMonitor>();
            SetReference(rules, "ball", ball);

            PlayerMovementController player = CreatePlayer(movementProfile, input, ball);

            Camera camera = CreateCamera(ball, player.transform);
            SetReference(input, "viewCamera", camera.transform);
            SetReference(input, "ball", ball);

            var debugTools = new GameObject("DebugTools");
            var kicker = debugTools.AddComponent<BallTestKicker>();
            SetReference(kicker, "ball", ball);
            SetReference(kicker, "player", player);
            var hud = debugTools.AddComponent<FootballDebugHud>();
            SetReference(hud, "player", player);
            SetReference(hud, "playerBall", player.GetComponent<PlayerBallInteraction>());
            SetReference(hud, "ball", ball);
            SetReference(hud, "rules", rules);
            SetReference(hud, "kicker", kicker);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Football] Built {ScenePath}");
            return ScenePath;
        }

        static void CreateLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
            {
                var volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.25f, 0.15f);
        }

        static BallController CreateBall(BallPhysicsSettings settings)
        {
            var root = new GameObject("Ball");
            var ball = root.AddComponent<BallController>();
            SetReference(ball, "settings", settings);

            float diameter = settings.parameters.radius * 2f;
            var shell = Visual(PrimitiveType.Sphere, "Shell", root.transform,
                FootballAssetPaths.GetOrCreateMaterial("Ball_White", new Color(0.97f, 0.97f, 0.97f), 0.55f));
            shell.transform.localScale = Vector3.one * diameter;

            // A dark band around the equator makes spin visible.
            var band = Visual(PrimitiveType.Cylinder, "SpinBand", root.transform,
                FootballAssetPaths.GetOrCreateMaterial("Ball_Band", new Color(0.1f, 0.1f, 0.12f), 0.4f));
            band.transform.localScale = new Vector3(diameter * 1.02f, 0.012f, diameter * 1.02f);

            root.transform.position = new Vector3(0f, settings.parameters.radius, 0f);
            return ball;
        }

        static PlayerMovementController CreatePlayer(MovementProfile profile, FootballInput input, BallController ball)
        {
            var root = new GameObject("Player_Home_10");
            root.transform.SetPositionAndRotation(new Vector3(-2f, 0f, 0f), Quaternion.LookRotation(Vector3.right));

            var movement = root.AddComponent<PlayerMovementController>();
            SetReference(movement, "profile", profile);
            SetReference(movement, "commandSource", input);
            SetReference(movement, "ball", ball);

            var interaction = root.AddComponent<PlayerBallInteraction>();
            SetReference(interaction, "ball", ball);

            if (!TryAddCharacterModel(root, movement))
                AddCapsuleStandIn(root);
            return movement;
        }

        /// <summary>Rigged Mixamo character driven by the locomotion blend tree. The model faces +Z like the player root.</summary>
        static bool TryAddCharacterModel(GameObject root, PlayerMovementController movement)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAnimatorBuilder.CharacterPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CharacterMaterialPath);
            if (model == null || material == null)
            {
                Debug.LogWarning($"[Football] Character model or material missing; using a capsule stand-in");
                return false;
            }

            PlayerAnimatorBuilder.Result animator = PlayerAnimatorBuilder.Build();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            var characterAnimator = instance.GetComponent<Animator>();
            characterAnimator.runtimeAnimatorController = animator.Controller;
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var animation = root.AddComponent<PlayerAnimationController>();
            animation.Configure(movement, characterAnimator, animator.Limits);
            EditorUtility.SetDirty(animation);
            Debug.Log($"[Football] Player animator: {animator.Report}");
            return true;
        }

        static void AddCapsuleStandIn(GameObject root)
        {
            var body = Visual(PrimitiveType.Capsule, "Body", root.transform,
                FootballAssetPaths.GetOrCreateMaterial("Kit_Home", new Color(0.12f, 0.32f, 0.85f), 0.3f));
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.56f, 0.9f, 0.56f);

            var front = Visual(PrimitiveType.Cube, "FacingMarker", root.transform,
                FootballAssetPaths.GetOrCreateMaterial("Kit_Detail", new Color(0.95f, 0.95f, 0.95f), 0.3f));
            front.transform.localPosition = new Vector3(0f, 1.45f, 0.26f);
            front.transform.localScale = new Vector3(0.22f, 0.12f, 0.08f);
        }

        static Camera CreateCamera(BallController ball, Transform player)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            var broadcast = go.AddComponent<BroadcastCamera>();
            SetReference(broadcast, "ball", ball);
            SetReference(broadcast, "player", player);
            go.transform.SetPositionAndRotation(new Vector3(0f, 17f, -52f), Quaternion.LookRotation(new Vector3(0f, -17f, 52f)));
            return camera;
        }

        static GameObject Visual(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
