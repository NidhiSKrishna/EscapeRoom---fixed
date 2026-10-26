using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoom.UI
{
    /// <summary>
    /// GlobalFontColorEnforcer ensures that the default font color used throughout the entire game
    /// (all IMGUI labels, buttons, boxes, text fields, windows, and in-world TextMeshes) is pure white.
    /// It auto-initializes before scene load and persists across all scenes.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class GlobalFontColorEnforcer : MonoBehaviour
    {
        private static GlobalFontColorEnforcer instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnStartup()
        {
            if (instance != null) return;

            GameObject go = new GameObject("GlobalFontColorEnforcer");
            instance = go.AddComponent<GlobalFontColorEnforcer>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToSkin();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToSkin();
            EnforceSceneTextMeshes();
        }

        private void OnGUI()
        {
            // Execute early so any standard GUI.Label() or GUIStyle inheriting from skin defaults to white
            GUI.depth = 1000;
            GUI.contentColor = Color.white;
            ApplyToSkin();
        }

        private static void ApplyToSkin()
        {
            if (GUI.skin == null) return;

            EnforceStyleTextColor(GUI.skin.label);
            EnforceStyleTextColor(GUI.skin.box);
            EnforceStyleTextColor(GUI.skin.button);
            EnforceStyleTextColor(GUI.skin.textField);
            EnforceStyleTextColor(GUI.skin.textArea);
            EnforceStyleTextColor(GUI.skin.window);
            EnforceStyleTextColor(GUI.skin.toggle);
        }

        private static void EnforceStyleTextColor(GUIStyle style)
        {
            if (style == null) return;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
            style.onFocused.textColor = Color.white;
        }

        private static void EnforceSceneTextMeshes()
        {
            var textMeshes = FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (textMeshes[i] != null)
                {
                    textMeshes[i].color = Color.white;
                }
            }
        }
    }
}
