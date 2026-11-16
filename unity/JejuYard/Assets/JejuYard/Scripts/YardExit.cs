using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Jeju.Yard
{
    public sealed class YardExit : MonoBehaviour
    {
        static YardExit landing;
        readonly List<GameObject> activeRoots = new List<GameObject>();
        PanelSettings settings;
        FontAsset font;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LeaveFlutterYard();
#endif

        public static void Leave()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            LeaveFlutterYard();
#elif UNITY_STANDALONE
            Application.Quit();
#else
            ShowLanding();
#endif
        }

        // The mobile demo runs separately from Flutter. Keep the yard in memory
        // so that re-entering restores the player and animals to the same places.
        static void ShowLanding()
        {
            if (landing != null) return;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var home = new GameObject("Courtyard landing");
            home.SetActive(false);
            landing = home.AddComponent<YardExit>();
            var camera = home.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(248, 245, 232, 255);
            camera.cullingMask = 0;

            var document = home.AddComponent<UIDocument>();
            landing.settings = Instantiate(Resources.Load<PanelSettings>("YardCollectionSettings"));
            landing.settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("YardCollectionTheme");
            landing.settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            landing.settings.referenceResolution = new Vector2Int(390, 844);
            landing.settings.match = .5f;
            document.panelSettings = landing.settings;
            home.SetActive(true);
            landing.BuildMenu(document.rootVisualElement);

            foreach (var root in roots)
            {
                if (!root.activeSelf) continue;
                landing.activeRoots.Add(root);
                root.SetActive(false);
            }
        }

        void BuildMenu(VisualElement root)
        {
            root.AddToClassList("yard-root");
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = (Color)new Color32(248, 245, 232, 255);

            var title = new Label("나의 탐험");
            title.style.fontSize = 31;
            title.style.color = (Color)new Color32(43, 65, 44, 255);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 20;
            root.Add(title);

            var enter = new Button(ReturnToYard) { text = "마당으로 돌아가기" };
            enter.AddToClassList("source-button");
            enter.style.minWidth = 178;
            enter.style.height = 52;
            enter.style.fontSize = 17;
            root.Add(enter);

            var families = Application.platform == RuntimePlatform.Android
                ? new[] { "Noto Sans CJK KR", "Noto Sans KR", "Noto Sans CJK JP" }
                : new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR" };
            foreach (string family in families)
            {
                font = FontAsset.CreateFontAsset(family, "Regular", 64);
                if (font == null) continue;
                var definition = FontDefinition.FromSDFFont(font);
                title.style.unityFontDefinition = definition;
                enter.style.unityFontDefinition = definition;
                break;
            }
            enter.Focus();
        }

        void ReturnToYard()
        {
            gameObject.SetActive(false);
            foreach (var root in activeRoots)
                if (root != null) root.SetActive(true);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (landing == this) landing = null;
            if (font != null) Destroy(font);
            if (settings != null) Destroy(settings);
        }
    }
}
