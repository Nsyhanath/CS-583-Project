using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace StrikeZone.EditorTools
{
    /// <summary>
    /// Menu: Strike Zone > Build Bowling Alley Scene.
    /// Builds Assets/Scenes/BowlingAlley.unity: a playable alley at real-world scale (spec.md, sections 4-9).
    ///
    /// Art-safe: materials, textures, meshes, prefabs, physics materials and config assets are only
    /// created when they are MISSING. Edit them freely; re-running the menu keeps your changes.
    /// The scene file itself IS rebuilt from scratch, so put art changes in those assets, not in the scene.
    /// </summary>
    public static class BowlingAlleySetup
    {
        public const string SceneName = "BowlingAlley";
        const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
        const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        const string LaneDir = "Assets/StrikeZone/Lane";
        const string MatDir = LaneDir + "/Materials";
        const string TexDir = LaneDir + "/Textures";
        const string PhysDir = LaneDir + "/Physics";
        const string MeshDir = LaneDir + "/Meshes";
        const string PrefabDir = LaneDir + "/Prefabs";
        const string BallDir = LaneDir + "/Balls";
        const string ConfigPath = LaneDir + "/LaneConfig.asset";
        const string LaneTexPath = TexDir + "/LanePlaceholder.png";
        const string PinPrefabPath = PrefabDir + "/Pin.prefab";
        const string BallPrefabPath = PrefabDir + "/Ball.prefab";

        // Geometry in meters (spec.md, section 4). Origin = center of the foul line, lane runs along +Z.
        const float LaneWidth = 1.0668f;
        const float HalfLane = LaneWidth / 2f;
        const float HeadPinZ = 18.288f;
        const float PitEdgeZ = 19.156f;
        const float GutterWidth = 0.235f;
        const float ApproachLength = 4.572f;
        const float BallRadius = 0.109f;
        const float PinSpacing = 0.3048f;
        const float RowSpacing = 0.26397f;
        const int BoardCount = 39;
        const float NeighborOffset = 1.72f;

        // Pin outline (radius, height) from base to top, used to lathe the placeholder pin and its collider.
        static readonly Vector2[] PinProfile =
        {
            new(0f, 0f), new(0.026f, 0f), new(0.045f, 0.04f), new(0.06f, 0.11f), new(0.055f, 0.17f),
            new(0.034f, 0.23f), new(0.022f, 0.26f), new(0.024f, 0.29f), new(0.031f, 0.33f),
            new(0.028f, 0.36f), new(0.016f, 0.378f), new(0f, 0.381f),
        };

        [MenuItem("Strike Zone/Build Bowling Alley Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Strike Zone",
                    "Rebuild BowlingAlley.unity from scratch?\n\nChanges made directly in that scene are lost. " +
                    "Materials, textures, prefabs and other assets in Assets/StrikeZone/Lane are kept.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PointPlayButtonAtAlley();
            ApplyPhysicsSettings();

            foreach (var dir in new[] { MatDir, TexDir, PhysDir, MeshDir, PrefabDir, BallDir }) EnsureFolder(dir);
            var a = new Assets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            var cam = BuildCamera();
            BuildLights();

            var alley = new GameObject("Alley").transform;
            BuildLane(Group("Lane", alley), 0f, a, true);
            BuildLane(Group("Neighbor Lane L", alley), -NeighborOffset, a, false);
            BuildLane(Group("Neighbor Lane R", alley), NeighborOffset, a, false);
            BuildSurroundings(alley, a);
            BuildSign(alley);

            var lane = BuildGameplay(a);
            cam.AddComponent<LaneCamera>().lane = lane;

            new GameObject("Scene Flow").AddComponent<ReturnToTitle>();
            BuildHud();

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Strike Zone: bowling alley built at " + ScenePath + ". Press Play here to test, or start from TitleScreen.");
        }

        // ---------- Gameplay objects ----------

        static BowlingLane BuildGameplay(Assets a)
        {
            var root = new GameObject("Bowling Lane");
            var lane = root.AddComponent<BowlingLane>();

            var setterGo = new GameObject("Pin Setter");
            setterGo.transform.SetParent(root.transform, false);
            var setter = setterGo.AddComponent<PinSetter>();
            setter.config = a.config;
            setter.pinPrefab = a.pinPrefab.GetComponent<Pin>();
            for (int i = 0; i < 10; i++)
            {
                var pinGo = (GameObject)PrefabUtility.InstantiatePrefab(a.pinPrefab, setterGo.transform);
                pinGo.name = "Pin " + (i + 1);
                pinGo.transform.position = a.config.PinHome(i);
                pinGo.GetComponent<Pin>().number = i + 1;
            }

            var ballGo = (GameObject)PrefabUtility.InstantiatePrefab(a.ballPrefab, root.transform);
            ballGo.transform.position = new Vector3(a.config.BoardToX(a.config.startBoard), BallRadius + 0.001f, a.config.ballStartZ);
            var ball = ballGo.GetComponent<BowlingBall>();

            var pit = new GameObject("Pit Trigger");
            pit.transform.SetParent(root.transform, false);
            pit.transform.position = new Vector3(0f, -0.26f, 19.70f);
            var pitBox = pit.AddComponent<BoxCollider>();
            pitBox.isTrigger = true;
            pitBox.size = new Vector3(5.2f, 0.48f, 1.10f);
            pit.AddComponent<PitTrigger>();

            lane.config = a.config;
            lane.ball = ball;
            lane.pinSetter = setter;
            lane.startingBall = a.balls[1];

            var debugGo = new GameObject("Debug Thrower");
            debugGo.transform.SetParent(root.transform, false);
            var debug = debugGo.AddComponent<DebugThrower>();
            debug.lane = lane;
            debug.balls = a.balls;

            return lane;
        }

        // ---------- Title screen, build list, project settings ----------

        static void PointPlayButtonAtAlley()
        {
            if (!File.Exists(TitleScenePath)) return;
            var title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            var play = title.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MenuButton>(true))
                .FirstOrDefault(b => b.name == "PLAY");
            if (play == null || play.sceneToLoad == SceneName) return;

            play.sceneToLoad = SceneName;
            EditorUtility.SetDirty(play);
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);
        }

        static void UpdateBuildScenes()
        {
            var keep = EditorBuildSettings.scenes
                .Where(s => s.path != TitleScenePath && s.path != ScenePath)
                .Select(s => s.path == SampleScenePath ? new EditorBuildSettingsScene(s.path, false) : s)
                .ToList();
            int i = 0;
            if (File.Exists(TitleScenePath)) keep.Insert(i++, new EditorBuildSettingsScene(TitleScenePath, true));
            keep.Insert(i, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = keep.ToArray();
        }

        /// <summary>spec.md, section 5.1.</summary>
        static void ApplyPhysicsSettings()
        {
            var time = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            SetFloat(time, "Fixed Timestep", 0.01f);
            time.ApplyModifiedPropertiesWithoutUndo();
            Time.fixedDeltaTime = 0.01f;

            var physics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            SetInt(physics, "m_DefaultSolverIterations", 12);
            SetInt(physics, "m_DefaultSolverVelocityIterations", 4);
            SetFloat(physics, "m_BounceThreshold", 1f);
            SetFloat(physics, "m_DefaultMaxAngularSpeed", 100f);
            physics.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning("Strike Zone: couldn't find project setting " + name + ". Set it by hand (spec.md 5.1).");
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
            else Debug.LogWarning("Strike Zone: couldn't find project setting " + name + ". Set it by hand (spec.md 5.1).");
        }

        // ---------- Environment, camera, lights, HUD ----------

        static void SetupEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.18f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#15121F");
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 48f;
        }

        static GameObject BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#15121F");
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 80f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0.37f, 0.62f, -2.0f);
            go.transform.LookAt(new Vector3(0.05f, 0.15f, HeadPinZ));
            return go;
        }

        static void BuildLights()
        {
            var lights = new GameObject("Lights").transform;

            var fill = NewLight("Fill (Directional)", lights, LightType.Directional, Hex("#FFF1DD"), 0.35f);
            fill.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            fill.shadows = LightShadows.None;

            var deck = NewLight("Pin Deck Spot", lights, LightType.Spot, Hex("#FFE6B0"), 6f);
            deck.transform.position = new Vector3(0f, 2.6f, 17.4f);
            deck.transform.LookAt(new Vector3(0f, 0f, 18.7f));
            deck.range = 7f;
            deck.spotAngle = 70f;
            deck.innerSpotAngle = 40f;
            deck.shadows = LightShadows.Soft;

            var approach = NewLight("Approach Spot", lights, LightType.Spot, Color.white, 3f);
            approach.transform.position = new Vector3(0f, 3.5f, -1.5f);
            approach.transform.LookAt(new Vector3(0f, 0f, 0.5f));
            approach.range = 8f;
            approach.spotAngle = 75f;
            approach.shadows = LightShadows.Soft;

            foreach (var z in new[] { 4f, 9f, 14f })
            {
                var p = NewLight("Lane Light " + z, lights, LightType.Point, Hex("#FFF0D8"), 1.2f);
                p.transform.position = new Vector3(0f, 2.2f, z);
                p.range = 6f;
            }

            var neon = NewLight("Neon Pink", lights, LightType.Point, Hex("#FF6FA8"), 2.5f);
            neon.transform.position = new Vector3(0f, 1.0f, 19.0f);
            neon.range = 4f;

            var neonTeal = NewLight("Neon Teal", lights, LightType.Point, Hex("#5DCAA5"), 1.5f);
            neonTeal.transform.position = new Vector3(-1.5f, 1.1f, 19.0f);
            neonTeal.range = 4f;
        }

        static Light NewLight(string name, Transform parent, LightType type, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            return l;
        }

        static void BuildHud()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null) return;

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            HudText(canvasGo.transform, "Title", "STRIKE ZONE  ·  BOWLING ALLEY (WORK IN PROGRESS)",
                new Vector2(0f, 1f), new Vector2(40f, -40f), TextAlignmentOptions.TopLeft, font);
            HudText(canvasGo.transform, "Back Hint", "ESC  BACK TO TITLE",
                new Vector2(0f, 0f), new Vector2(40f, 40f), TextAlignmentOptions.BottomLeft, font);
        }

        static void HudText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = 26;
            t.characterSpacing = 4;
            t.color = new Color32(255, 246, 230, 220);
            t.alignment = align;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1200f, 40f);
        }

        // ---------- Alley geometry (spec.md, section 4.5) ----------

        static void BuildLane(Transform parent, float x, Assets a, bool main)
        {
            Box("Lane Bed", parent, new Vector3(x, -0.025f, PitEdgeZ / 2f), new Vector3(LaneWidth, 0.05f, PitEdgeZ), a.wood, null);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Lane Surface";
            surface.transform.SetParent(parent, false);
            surface.transform.position = new Vector3(x, 0.0005f, PitEdgeZ / 2f);
            surface.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            surface.transform.localScale = new Vector3(LaneWidth, PitEdgeZ, 1f);
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.GetComponent<MeshRenderer>().sharedMaterial = main ? a.laneSurface : a.laneSurfaceDim;

            float gx = HalfLane + GutterWidth / 2f;
            for (int s = -1; s <= 1; s += 2)
                Box(s < 0 ? "Gutter L" : "Gutter R", parent, new Vector3(x + s * gx, -0.073f, 9.8f),
                    new Vector3(GutterWidth, 0.05f, 19.6f), a.gutter, main ? a.physLane : null);

            if (!main) return;

            // One collider for the whole ball path, starting under the approach, so the ball never hits a seam.
            var col = new GameObject("Lane Collider");
            col.transform.SetParent(parent, false);
            var box = col.AddComponent<BoxCollider>();
            box.center = new Vector3(x, -0.025f, (PitEdgeZ - 0.6f) / 2f);
            box.size = new Vector3(LaneWidth, 0.05f, PitEdgeZ + 0.6f);
            box.sharedMaterial = a.physLane;

            for (int s = -1; s <= 1; s += 2)
            {
                Box(s < 0 ? "Divider L" : "Divider R", parent, new Vector3(s * 0.86f, 0f, 9.0f),
                    new Vector3(0.183f, 0.06f, 23.0f), a.divider, a.physKickback);
                Box(s < 0 ? "Kickback L" : "Kickback R", parent, new Vector3(s * 0.82f, 0.25f, 18.75f),
                    new Vector3(0.05f, 0.6f, 1.7f), a.kickback, a.physKickback);
            }
        }

        static void BuildSurroundings(Transform parent, Assets a)
        {
            Box("Approach", parent, new Vector3(0f, -0.025f, -ApproachLength / 2f), new Vector3(5.2f, 0.05f, ApproachLength), a.approach, a.physLane);
            Box("Floor", parent, new Vector3(0f, -0.16f, 4.575f), new Vector3(30f, 0.1f, 29.15f), a.floor, null);
            Box("Pit Floor", parent, new Vector3(0f, -0.5f, 19.70f), new Vector3(5.2f, 0.05f, 1.10f), a.pit, a.physCushion);
            Box("Back Cushion", parent, new Vector3(0f, 0.10f, 20.25f), new Vector3(5.2f, 1.2f, 0.10f), a.cushion, a.physCushion);
            Box("Masking Unit", parent, new Vector3(0f, 1.0f, 19.40f), new Vector3(5.2f, 0.9f, 0.15f), a.masking, null);
            Box("Back Wall", parent, new Vector3(0f, 1.5f, 20.6f), new Vector3(12f, 4f, 0.2f), a.wall, null);
        }

        static void BuildSign(Transform parent)
        {
            if (TMP_Settings.defaultFontAsset == null) return;
            var go = new GameObject("Neon Sign", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 1.05f, 19.32f);
            var t = go.AddComponent<TextMeshPro>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = "STRIKE ZONE";
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.5f;
            t.fontSizeMax = 6f;
            t.color = Hex("#FF7DB0");
            t.rectTransform.sizeDelta = new Vector2(1.6f, 0.35f);
        }

        /// <summary>A cube. Pass a physics material to give it a collider, or null for a visual-only box.</summary>
        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, PhysicsMaterial phys)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (phys == null) Object.DestroyImmediate(col);
            else col.sharedMaterial = phys;
            return go;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------- Assets (created only when missing) ----------

        class Assets
        {
            public readonly LaneConfig config;
            public readonly Material wood, laneSurface, laneSurfaceDim, approach, gutter, divider, kickback,
                floor, pit, cushion, masking, wall, pinWhite, pinStripe, ballHole;
            public readonly PhysicsMaterial physLane, physBall, physPin, physKickback, physCushion;
            public readonly BallSpec[] balls;
            public readonly GameObject pinPrefab, ballPrefab;

            public Assets()
            {
                config = LoadOrCreate<LaneConfig>(ConfigPath, _ => { });

                var laneTex = LaneTexture();
                wood = Mat("Wood", Hex("#C48A50"), 0.4f);
                laneSurface = Mat("LaneSurface", Color.white, 0.75f, 0f, laneTex);
                laneSurfaceDim = Mat("LaneSurfaceDim", new Color(0.55f, 0.55f, 0.55f), 0.6f, 0f, laneTex);
                approach = Mat("Approach", Hex("#E2B07A"), 0.55f);
                gutter = Mat("Gutter", Hex("#6B6A66"), 0.65f, 0.5f);
                divider = Mat("Divider", Hex("#2A2540"), 0.3f);
                kickback = Mat("Kickback", Hex("#3C3489"), 0.3f);
                floor = Mat("Floor", Hex("#1A1728"), 0.2f);
                pit = Mat("Pit", Hex("#050408"), 0.1f);
                cushion = Mat("Cushion", Hex("#0E0C16"), 0.1f);
                masking = Mat("MaskingUnit", Hex("#2A2540"), 0.3f);
                wall = Mat("BackWall", Hex("#1E1A30"), 0.2f);
                pinWhite = Mat("Pin", Hex("#FBFAF6"), 0.7f);
                pinStripe = Mat("PinStripe", Hex("#D8343A"), 0.6f);
                ballHole = Mat("BallHole", Hex("#0A1830"), 0.1f);

                physLane = Phys("LaneSurface", 0.04f, 0.04f, 0f);
                physBall = Phys("Ball", 0.10f, 0.10f, 0.05f);
                physPin = Phys("Pin", 0.35f, 0.40f, 0.40f);
                physKickback = Phys("Kickback", 0.20f, 0.20f, 0.50f);
                physCushion = Phys("Cushion", 0.60f, 0.60f, 0.05f);

                balls = new[]
                {
                    Ball("Ball_08lb", "8 lb", 3.6f, new Vector2(7f, 9f), 1.25f, Mat("Ball08", Hex("#E8605A"), 0.85f, 0.15f)),
                    Ball("Ball_12lb", "12 lb", 5.4f, new Vector2(6f, 8.5f), 1.0f, Mat("Ball12", Hex("#2F7FD6"), 0.85f, 0.15f)),
                    Ball("Ball_16lb", "16 lb", 7.3f, new Vector2(5f, 8f), 0.75f, Mat("Ball16", Hex("#3C3489"), 0.85f, 0.15f)),
                };

                pinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PinPrefabPath);
                if (pinPrefab == null) pinPrefab = MakePinPrefab(this);
                ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
                if (ballPrefab == null) ballPrefab = MakeBallPrefab(this);
            }
        }

        static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static BallSpec Ball(string file, string label, float mass, Vector2 speed, float control, Material mat) =>
            LoadOrCreate<BallSpec>(BallDir + "/" + file + ".asset", b =>
            {
                b.displayName = label;
                b.massKg = mass;
                b.speedRange = speed;
                b.control = control;
                b.material = mat;
            });

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Texture2D map = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (map != null)
            {
                mat.mainTexture = map;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", map);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static PhysicsMaterial Phys(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            string path = PhysDir + "/" + name + ".physicsMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null) return existing;

            var m = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh LatheMesh(string name, int segments)
        {
            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            int rings = PinProfile.Length, cols = segments + 1;
            var verts = new Vector3[rings * cols];
            var uvs = new Vector2[rings * cols];
            for (int j = 0; j < rings; j++)
            for (int s = 0; s < cols; s++)
            {
                float t = s / (float)segments, ang = t * Mathf.PI * 2f;
                var p = PinProfile[j];
                verts[j * cols + s] = new Vector3(p.x * Mathf.Cos(ang), p.y, p.x * Mathf.Sin(ang));
                uvs[j * cols + s] = new Vector2(t, p.y / PinProfile[rings - 1].y);
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int j = 0; j < rings - 1; j++)
            for (int s = 0; s < segments; s++)
            {
                int a = j * cols + s, b = a + 1, c = a + cols, d = c + 1;
                tris.AddRange(new[] { a, c, d, a, d, b });
            }
            var mesh = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Pin prefab: physics on the root, art in "Visual", collision in "Collider" (spec.md 4.4).</summary>
        static GameObject MakePinPrefab(Assets a)
        {
            var root = new GameObject("Pin");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.58f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<Pin>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = LatheMesh("PinPlaceholder", 32);
            visual.AddComponent<MeshRenderer>().sharedMaterial = a.pinWhite;
            foreach (var (y, diameter) in new[] { (0.247f, 0.058f), (0.268f, 0.049f) })
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stripe.name = "Stripe";
                stripe.transform.SetParent(visual.transform, false);
                stripe.transform.localPosition = new Vector3(0f, y, 0f);
                stripe.transform.localScale = new Vector3(diameter, 0.006f, diameter);
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                stripe.GetComponent<MeshRenderer>().sharedMaterial = a.pinStripe;
            }

            var colGo = new GameObject("Collider");
            colGo.transform.SetParent(root.transform, false);
            var mc = colGo.AddComponent<MeshCollider>();
            mc.sharedMesh = LatheMesh("PinCollider", 12);
            mc.convex = true;
            mc.sharedMaterial = a.physPin;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PinPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Ball prefab: physics on the root, art in "Visual" (swap the Body mesh freely).</summary>
        static GameObject MakeBallPrefab(Assets a)
        {
            var root = new GameObject("Ball");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 5.4f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = BallRadius;
            sc.sharedMaterial = a.physBall;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(visual.transform, false);
            body.transform.localScale = Vector3.one * BallRadius * 2f;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = a.balls[1].material;

            foreach (var dir in new[] { new Vector3(-0.02f, 0.9f, 0.3f), new Vector3(0.02f, 0.9f, 0.3f), new Vector3(0f, 0.75f, 0.62f) })
            {
                var hole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hole.name = "Finger Hole";
                hole.transform.SetParent(visual.transform, false);
                hole.transform.localPosition = dir.normalized * BallRadius * 0.93f;
                hole.transform.localScale = Vector3.one * 0.026f;
                Object.DestroyImmediate(hole.GetComponent<Collider>());
                hole.GetComponent<MeshRenderer>().sharedMaterial = a.ballHole;
            }

            var ball = root.AddComponent<BowlingBall>();
            ball.config = a.config;
            ball.visualRenderer = bodyRenderer;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Placeholder lane art: 39 boards, aiming arrows, guide dots, foul line and pin spots.
        /// Row 0 of the image is the foul line; the top row is the pit edge. Only generated if the file is missing.
        /// </summary>
        static Texture2D LaneTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(LaneTexPath);
            if (existing != null) return existing;

            const int px = 8;
            int w = BoardCount * px, h = 4096;
            float ppm = h / PitEdgeZ;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color32[w * h];
            var light = new Color32(217, 160, 102, 255);
            var dark = new Color32(207, 149, 90, 255);
            var seam = new Color32(185, 132, 78, 255);
            var mark = new Color32(90, 50, 24, 255);
            var rng = new System.Random(583);
            var boardTint = Enumerable.Range(0, BoardCount).Select(_ => rng.Next(-6, 7)).ToArray();

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int b = x / px;
                var c = b % 2 == 0 ? light : dark;
                int t = boardTint[b];
                c = new Color32((byte)Mathf.Clamp(c.r + t, 0, 255), (byte)Mathf.Clamp(c.g + t, 0, 255), (byte)Mathf.Clamp(c.b + t, 0, 255), 255);
                if (x % px == 0) c = seam;
                pixels[y * w + x] = c;
            }

            float BoardX(int board) => w - (board - 0.5f) * px;
            int Row(float meters) => Mathf.RoundToInt(meters * ppm);

            void Disc(float cx, int cy, float r)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if (x >= 0 && x < w && y >= 0 && y < h && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        pixels[y * w + x] = mark;
            }

            for (int y = 0; y < Row(0.02f); y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = new Color32(40, 38, 36, 255);

            foreach (var b in new[] { 3, 5, 8, 11, 14, 26, 29, 32, 35, 37 })
                Disc(BoardX(b), Row(2.13f), px * 0.35f);

            foreach (var b in new[] { 5, 10, 15, 20, 25, 30, 35 })
            {
                float d = 4.88f - Mathf.Abs(b - 20) / 5f * 0.305f;
                int baseRow = Row(d), tipRow = Row(d + 0.30f);
                float cx = BoardX(b), half = px * 0.45f;
                for (int y = baseRow; y <= tipRow; y++)
                {
                    float k = 1f - (y - baseRow) / (float)(tipRow - baseRow);
                    for (int x = Mathf.FloorToInt(cx - half * k); x <= Mathf.CeilToInt(cx + half * k); x++)
                        if (x >= 0 && x < w) pixels[y * w + x] = mark;
                }
            }

            float boardsPerMeter = BoardCount / LaneWidth;
            for (int r = 0; r < 4; r++)
            for (int i = 0; i <= r; i++)
                Disc(w / 2f + (i - r / 2f) * PinSpacing * boardsPerMeter * px, Row(HeadPinZ + r * RowSpacing), px * 0.6f);

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(LaneTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(LaneTexPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LaneTexPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 16;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LaneTexPath);
        }

        // ---------- Helpers ----------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
