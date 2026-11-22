using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace StrikeZone.EditorTools
{
    /// <summary>
    /// Menu: Strike Zone > Build Title Screen Scene.
    /// Creates Assets/Scenes/TitleScreen.unity with the whole bowling title screen,
    /// generates the small UI sprites it needs, and puts the scene first in the build list.
    /// Safe to run again: it rebuilds the scene from scratch.
    /// </summary>
    public static class StrikeZoneSetup
    {
        const string Root = "Assets/StrikeZone";
        const string SpriteDir = Root + "/Sprites";
        const string MaterialDir = Root + "/Materials";
        const string BackgroundPath = Root + "/Resources/StrikeZoneBackground.png";
        const string ScenePath = "Assets/Scenes/TitleScreen.unity";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        static readonly Vector2 RefResolution = new Vector2(1920, 1080);

        [MenuItem("Strike Zone/Build Title Screen Scene")]
        public static void BuildMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Strike Zone",
                    "TitleScreen.unity already exists. Rebuild it from scratch?\n\n" +
                    "Every change made in that scene is lost, and the whole file changes in git.",
                    "Rebuild", "Cancel"))
                return;

            if (!File.Exists(TmpSettingsPath))
            {
                if (!EditorUtility.DisplayDialog("Strike Zone",
                        "The title screen uses TextMesh Pro for its text. Import the TMP Essential Resources now? " +
                        "(This is Unity's standard one-time import.)", "Import", "Cancel"))
                    return;
                AssetDatabase.importPackageCompleted += OnTmpImported;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }
            Build();
        }

        static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }

        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                EditorUtility.DisplayDialog("Strike Zone",
                    "TextMesh Pro has no default font yet. Run Window > TextMesh Pro > Import TMP Essential Resources, then try again.", "OK");
                return;
            }

            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(MaterialDir);

            var background = ConfigureBackground();
            var pill = SaveSprite("Pill.png", PillTexture(256, 128, 64), new Vector4(64, 64, 64, 64));
            var glow = SaveSprite("RadialGlow.png", RadialTexture(256), Vector4.zero);
            var ballSprite = SaveSprite("BowlingBall.png", BallTexture(256), Vector4.zero);
            var titleMat = TitleMaterial(font);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (the UI draws on top of it; it just clears the screen)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(11, 7, 16, 255);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            // Event system for mouse / keyboard / gamepad UI input
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            // Canvas
            var canvasGo = new GameObject("Title Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Background: fills the screen, cropping the edges to keep 16:9
            var bgRt = UI("Background", root);
            Stretch(bgRt);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = background;
            bgImg.raycastTarget = false;
            var fitter = bgRt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = RefResolution.x / RefResolution.y;

            // Rolling ball (reflection, contact shadow, ball)
            var reflection = Img("Ball Reflection", bgRt, ballSprite, Color.white);
            reflection.rectTransform.localScale = new Vector3(1, -1, 1);
            var shadow = Img("Ball Shadow", bgRt, glow, Color.black);
            var ball = Img("Ball", bgRt, ballSprite, Color.white);
            var roller = bgRt.gameObject.AddComponent<RollingBall>();
            roller.background = bgRt;
            roller.ball = ball;
            roller.reflection = reflection;
            roller.shadow = shadow;

            // Dark glow behind the title so it reads over the colorful lanes
            var backdrop = Img("Title Backdrop", root, glow, new Color32(10, 4, 18, 170));
            Place(backdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1700, 1050));

            // Kicker + title
            var kicker = Text("Kicker", root, "TEN-PIN BOWLING", 28, new Color32(255, 210, 59, 255), font);
            kicker.characterSpacing = 40;
            kicker.fontStyle = FontStyles.Bold;
            Place(kicker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(900, 50));

            var titleRt = UI("Title", root);
            Place(titleRt, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1200, 330));
            titleRt.gameObject.AddComponent<TitleBob>();

            var strike = Text("Strike", titleRt, "STRIKE", 190, Color.white, font);
            strike.fontStyle = FontStyles.Bold;
            strike.fontSharedMaterial = titleMat;
            strike.extraPadding = true;
            strike.enableVertexGradient = true;
            strike.colorGradient = new VertexGradient(Hex("#FFF0A0"), Hex("#FFE066"), Hex("#FF7A1A"), Hex("#FF3D8B"));
            Place(strike.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1200, 200));

            var zone = Text("Zone", titleRt, "ZONE", 150, Color.white, font);
            zone.fontStyle = FontStyles.Bold;
            zone.characterSpacing = 12;
            zone.fontSharedMaterial = titleMat;
            zone.extraPadding = true;
            zone.enableVertexGradient = true;
            zone.colorGradient = new VertexGradient(Hex("#F0E6FF"), Hex("#E8D9FF"), Hex("#8A4DFF"), Hex("#6A2BE0"));
            Place(zone.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -85), new Vector2(1200, 160));

            var press = Text("Press Enter", root, "PRESS ENTER TO START", 34, new Color32(255, 246, 230, 255), font);
            press.fontStyle = FontStyles.Bold;
            press.characterSpacing = 10;
            press.fontSharedMaterial = titleMat;
            press.extraPadding = true;
            Place(press.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(900, 50));
            press.gameObject.AddComponent<Blink>();

            // Menu
            var menuRt = UI("Menu", root);
            Place(menuRt, new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(480, 352));
            var layout = menuRt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // sceneToLoad lives here too, so rebuilding the title screen keeps PLAY pointed at the alley.
            var items = new (string label, string message, string sceneToLoad)[]
            {
                ("PLAY", "Lanes are being oiled... gameplay coming soon!", BowlingAlleySetup.SceneName),
                ("HOW TO PLAY", "Aim, spin, and knock 'em all down. (Coming soon)", ""),
                ("SETTINGS", "Settings coming soon.", ""),
                ("HIGH SCORES", "No high scores yet. Be the first!", ""),
            };
            var buttons = items.Select(i => MakeButton(menuRt, i.label, i.message, i.sceneToLoad, pill, ballSprite, font)).ToArray();

            // Footer hint
            var footer = Img("Controls Hint", root, pill, new Color32(12, 6, 20, 180));
            footer.type = Image.Type.Sliced;
            footer.pixelsPerUnitMultiplier = 64f / 22f;
            footer.raycastTarget = false;
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(700, 44));
            var footerText = Text("Label", footer.transform, "ARROW KEYS TO CHOOSE  ·  ENTER TO SELECT", 20, new Color32(255, 246, 230, 210), font);
            footerText.characterSpacing = 8;
            Stretch(footerText.rectTransform);

            // Toast
            var toastImg = Img("Toast", root, pill, new Color32(20, 8, 32, 235));
            toastImg.type = Image.Type.Sliced;
            toastImg.pixelsPerUnitMultiplier = 2f;
            toastImg.raycastTarget = false;
            Place(toastImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(860, 64));
            var toastGroup = toastImg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            var toastText = Text("Message", toastImg.transform, "", 26, new Color32(255, 246, 230, 255), font);
            toastText.fontStyle = FontStyles.Bold;
            Stretch(toastText.rectTransform);

            // Controller
            var controller = canvasGo.AddComponent<TitleScreenController>();
            controller.buttons = buttons;
            controller.toast = toastGroup;
            controller.toastText = toastText;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Selection.activeGameObject = canvasGo;
            Debug.Log("Strike Zone: title screen built at " + ScenePath + ". Press Play to try it.");
        }

        // ---------- UI helpers ----------

        static MenuButton MakeButton(Transform parent, string label, string message, string sceneToLoad, Sprite pill, Sprite ballSprite, TMP_FontAsset font)
        {
            var rt = UI(label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = pill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 64f / 38f;
            var depth = rt.gameObject.AddComponent<Shadow>();
            depth.effectColor = new Color32(18, 6, 28, 255);
            depth.effectDistance = new Vector2(0, -5);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color32(60, 22, 90, 235);
            colors.highlightedColor = new Color32(255, 106, 74, 255);
            colors.selectedColor = new Color32(255, 92, 110, 255);
            colors.pressedColor = new Color32(216, 58, 106, 255);
            colors.disabledColor = new Color32(80, 80, 90, 200);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.Vertical;
            nav.wrapAround = true;
            button.navigation = nav;

            var text = Text("Label", rt, label, 32, Color.white, font);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6;
            Stretch(text.rectTransform);

            var marker = Img("Ball Marker", rt, ballSprite, Color.white);
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(40, 0), new Vector2(28, 28));
            marker.gameObject.SetActive(false);

            var mb = rt.gameObject.AddComponent<MenuButton>();
            mb.message = message;
            mb.sceneToLoad = sceneToLoad;
            mb.marker = marker.gameObject;
            return mb;
        }

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = UI(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            var t = UI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ---------- assets ----------

        static Sprite ConfigureBackground()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            if (importer == null)
                throw new FileNotFoundException("Missing background image", BackgroundPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        }

        static Material TitleMaterial(TMP_FontAsset font)
        {
            string path = MaterialDir + "/StrikeZoneTitle.mat";
            var mat = new Material(font.material) { name = "StrikeZoneTitle" };
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.2f);
            mat.SetColor("_OutlineColor", new Color32(58, 15, 79, 255));
            mat.SetFloat("_FaceDilate", 0.15f);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color32(20, 4, 30, 230));
            mat.SetFloat("_UnderlayOffsetX", 0.15f);
            mat.SetFloat("_UnderlayOffsetY", -0.9f);
            mat.SetFloat("_UnderlaySoftness", 0.2f);
            mat.SetFloat("_UnderlayDilate", 0.2f);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Sprite SaveSprite(string fileName, Texture2D tex, Vector4 border)
        {
            string path = SpriteDir + "/" + fileName;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        // White rounded rectangle (pill), used 9-sliced for buttons and panels.
        static Texture2D PillTexture(int w, int h, int radius)
        {
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * w + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // White circle that fades out toward the edge.
        static Texture2D RadialTexture(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float a = Mathf.SmoothStep(0f, 1f, 1f - d);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // Glossy dark bowling ball with a highlight, lit from the upper left.
        static Texture2D BallTexture(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Color c0 = Hex("#9AA1B2"), c1 = Hex("#3A3F4C"), c2 = Hex("#0B0C10");
            var focus = new Vector2(-0.35f, 0.4f);
            var spec = new Vector2(-0.35f, 0.45f);
            float cosA = Mathf.Cos(0.6f), sinA = Mathf.Sin(0.6f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float d = p.magnitude;
                float edge = Mathf.Clamp01((1f - d) * size * 0.5f);
                float g = Mathf.Clamp01(Vector2.Distance(p, focus) / 1.35f);
                Color col = g < 0.25f ? Color.Lerp(c0, c1, g / 0.25f) : Color.Lerp(c1, c2, (g - 0.25f) / 0.75f);

                // soft elliptical specular highlight
                var q = p - spec;
                float qx = q.x * cosA + q.y * sinA, qy = -q.x * sinA + q.y * cosA;
                float e = (qx * qx) / (0.22f * 0.22f) + (qy * qy) / (0.12f * 0.12f);
                float s = Mathf.Clamp01((1f - e) * 3f) * 0.55f;
                col = Color.Lerp(col, Color.white, s);

                col.a = edge;
                px[y * size + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
