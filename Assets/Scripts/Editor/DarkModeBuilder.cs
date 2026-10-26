using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor utility that transforms Level 1 (EscapeRoom_Main) into a rich, atmospheric Dark Mode:
    /// - Mood lighting: Disables blinding directional sunlight, sets deep midnight slate ambient lighting,
    ///   dims the central ceiling light to a warm amber pool (intensity 55), and adds focused accent lights
    ///   on key interactables (Shelf & Key, Table & Lockbox, Terminal Keypad).
    /// - Material palette: Transforms all Level 1 materials (walls, floor, ceiling, wood, crates, door, lockbox, keypad)
    ///   into sleek dark slate, graphite, and ebony finishes with luminous interactive items.
    /// - Post-processing: Tunes URP Global Volume with cinematic vignette, bloom, and contrast adjustments.
    /// Menu item: Tools > Escape Room > Apply Dark Mode to Level 1
    /// </summary>
    [InitializeOnLoad]
    public static class DarkModeBuilder
    {
        private const string MaterialsDir = "Assets/Art/Materials";
        private const string ScenePath = "Assets/Scenes/EscapeRoom_Main.unity";
        private const string SessionKey = "DarkModeLevel1_Applied_v1";

        static DarkModeBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                ApplyDarkModeToLevel1();
            }
        }

        [MenuItem("Tools/Escape Room/Apply Dark Mode to Level 1", false, 18)]
        public static void ApplyDarkModeToLevel1()
        {
            Debug.Log("<b><color=#337ab7>[DarkModeBuilder]</color> Applying Dark Mode transformation to Level 1...</b>");

            // 1. Verify and open EscapeRoom_Main scene if needed
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (File.Exists(ScenePath))
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    activeScene = EditorSceneManager.GetActiveScene();
                }
            }

            // 2. Configure Dark Mode Materials
            UpdateDarkModeMaterials();

            // 3. Configure Atmospheric Lighting in Level 1
            ConfigureAtmosphericLighting();

            // 4. Configure Post-Processing Global Volume
            ConfigureGlobalVolume();

            // 5. Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("<b><color=#5cb85c>[DarkModeBuilder]</color> Level 1 Dark Mode applied successfully!</b> Walls, floor, props, lighting, and post-processing updated.");
        }

        private static void UpdateDarkModeMaterials()
        {
            if (!Directory.Exists(MaterialsDir))
            {
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.Refresh();
            }

            // Architecture: Dark charcoal slate walls, graphite tiles, deep dark ceiling
            SetMaterialProperties("M_Proto_Wall", new Color(0.12f, 0.13f, 0.16f, 1f), 0.15f, 0.0f);
            SetMaterialProperties("M_Proto_Floor", new Color(0.10f, 0.11f, 0.13f, 1f), 0.30f, 0.05f);
            SetMaterialProperties("M_Proto_Ceiling", new Color(0.05f, 0.06f, 0.07f, 1f), 0.05f, 0.0f);

            // Timber & Trim: Dark walnut, ebony, dark weathered crates
            SetMaterialProperties("M_Proto_Wood", new Color(0.18f, 0.13f, 0.09f, 1f), 0.30f, 0.0f);
            SetMaterialProperties("M_Proto_DarkWood", new Color(0.10f, 0.07f, 0.05f, 1f), 0.35f, 0.0f);
            SetMaterialProperties("M_Proto_Crate", new Color(0.20f, 0.16f, 0.12f, 1f), 0.15f, 0.0f);
            SetMaterialProperties("M_Proto_Trim", new Color(0.12f, 0.10f, 0.08f, 1f), 0.25f, 0.05f);

            // Door & Metals: Gunmetal door, iron frame, brushed dark alloy
            SetMaterialProperties("M_Proto_Door", new Color(0.12f, 0.13f, 0.16f, 1f), 0.45f, 0.70f);
            SetMaterialProperties("M_Proto_DoorFrame", new Color(0.08f, 0.09f, 0.11f, 1f), 0.35f, 0.30f);
            SetMaterialProperties("M_Proto_Metal", new Color(0.22f, 0.24f, 0.28f, 1f), 0.65f, 0.85f);

            // Puzzles & Interactables: Dark tactile containers with luminous clues
            SetMaterialProperties("M_Proto_Lockbox", new Color(0.10f, 0.11f, 0.13f, 1f), 0.50f, 0.40f);
            SetMaterialProperties("M_Proto_Keypad", new Color(0.08f, 0.09f, 0.11f, 1f), 0.40f, 0.20f);
            SetMaterialProperties("M_Proto_Key", new Color(1.0f, 0.82f, 0.25f, 1f), 0.75f, 0.85f, new Color(0.40f, 0.28f, 0.06f, 1f));
            SetMaterialProperties("M_Proto_Clue", new Color(0.85f, 0.82f, 0.72f, 1f), 0.20f, 0.0f, new Color(0.18f, 0.16f, 0.12f, 1f));
            SetMaterialProperties("M_Proto_LightEmissive", new Color(1.0f, 0.85f, 0.60f, 1f), 0.85f, 0.1f, new Color(2.2f, 1.6f, 0.8f, 1f));

            // Books (Shelf clutter)
            SetMaterialProperties("M_Proto_Book_1", new Color(0.25f, 0.08f, 0.08f, 1f), 0.20f, 0.0f);
            SetMaterialProperties("M_Proto_Book_2", new Color(0.08f, 0.14f, 0.24f, 1f), 0.20f, 0.0f);
            SetMaterialProperties("M_Proto_Book_3", new Color(0.08f, 0.18f, 0.10f, 1f), 0.20f, 0.0f);
            SetMaterialProperties("M_Proto_Book_4", new Color(0.20f, 0.15f, 0.08f, 1f), 0.20f, 0.0f);

            AssetDatabase.SaveAssets();
            Debug.Log("[DarkModeBuilder] All Level 1 materials updated to Dark Mode palette.");
        }

        private static void SetMaterialProperties(string matName, Color baseColor, float smoothness, float metallic, Color? emissionColor = null)
        {
            string path = $"{MaterialsDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", baseColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", baseColor);

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);

            if (emissionColor.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor.Value);
            }

            EditorUtility.SetDirty(mat);
        }

        private static void ConfigureAtmosphericLighting()
        {
            // 1. Tame / disable outdoor directional sunlight
            GameObject sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                Light sunLight = sunGo.GetComponent<Light>();
                if (sunLight != null)
                {
                    sunLight.intensity = 0.0f;
                    sunLight.enabled = false;
                }
                sunGo.SetActive(false);
                Debug.Log("[DarkModeBuilder] Disabled Directional Light to eliminate daylight washout.");
            }

            // 2. Midnight slate ambient lighting (prevents pitch-black clipping while keeping moody contrast)
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.030f, 0.045f, 1.0f);

            // 3. Central Ceiling Light: warm amber pool under the attic lamp
            GameObject ceilingLightGo = GameObject.Find("CeilingLight");
            if (ceilingLightGo != null)
            {
                Light cLight = ceilingLightGo.GetComponent<Light>();
                if (cLight != null)
                {
                    cLight.type = LightType.Point;
                    cLight.color = new Color(1.0f, 0.82f, 0.60f, 1.0f); // Warm tungsten incandescent
                    cLight.intensity = 55.0f; // Atmospheric pool in URP lumens
                    cLight.range = 14.0f;
                    cLight.shadows = LightShadows.Soft;
                    cLight.enabled = true;
                }
                var addLight = ceilingLightGo.GetComponent<UniversalAdditionalLightData>();
                if (addLight == null) ceilingLightGo.AddComponent<UniversalAdditionalLightData>();
            }

            // 4. Accent Lights for interactive gameplay stations
            GameObject env = GameObject.Find("Environment");
            if (env != null)
            {
                Transform lightingRoot = env.transform.Find("Lighting");
                if (lightingRoot == null)
                {
                    GameObject lightingGo = new GameObject("Lighting");
                    lightingGo.transform.SetParent(env.transform, false);
                    lightingGo.transform.localPosition = Vector3.zero;
                    lightingGo.transform.localRotation = Quaternion.identity;
                    lightingRoot = lightingGo.transform;
                }

                // Table & Lockbox Accent Light: Warm reading pool directly over the lockbox table
                EnsureAccentPointLight(
                    "Light_TableAccent",
                    lightingRoot,
                    new Vector3(1.50f, 2.20f, -1.00f),
                    new Color(1.0f, 0.85f, 0.70f, 1.0f),
                    18.0f,
                    3.8f);

                // Shelf & Key Accent Light: Soft amber pool highlighting the key shelf
                EnsureAccentPointLight(
                    "Light_ShelfAccent",
                    lightingRoot,
                    new Vector3(-3.30f, 2.20f, -2.00f),
                    new Color(1.0f, 0.90f, 0.75f, 1.0f),
                    15.0f,
                    3.8f);

                // Keypad & Doorway Accent Light: Cool technical pool highlighting the keypad terminal
                EnsureAccentPointLight(
                    "Light_KeypadAccent",
                    lightingRoot,
                    new Vector3(0.90f, 2.20f, 5.20f),
                    new Color(0.85f, 0.94f, 1.0f, 1.0f),
                    20.0f,
                    3.6f);
            }

            Debug.Log("[DarkModeBuilder] Atmospheric lighting configured (Directional Light off, Ceiling Light dimmed warm, station accent lights active).");
        }

        private static void EnsureAccentPointLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            Transform existing = parent.Find(name);
            GameObject lightGo;
            if (existing != null)
            {
                lightGo = existing.gameObject;
            }
            else
            {
                lightGo = new GameObject(name);
                lightGo.transform.SetParent(parent, false);
            }

            lightGo.transform.localPosition = localPos;
            lightGo.transform.localRotation = Quaternion.identity;

            Light light = lightGo.GetComponent<Light>();
            if (light == null) light = lightGo.AddComponent<Light>();

            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = true;

            var addLight = lightGo.GetComponent<UniversalAdditionalLightData>();
            if (addLight == null) lightGo.AddComponent<UniversalAdditionalLightData>();
        }

        private static void ConfigureGlobalVolume()
        {
            Volume globalVolume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            if (globalVolume == null)
            {
                GameObject volumeGo = new GameObject("Global Volume");
                globalVolume = volumeGo.AddComponent<Volume>();
                globalVolume.isGlobal = true;
            }

            VolumeProfile profile = globalVolume.sharedProfile;
            if (profile == null)
            {
                string profilePath = "Assets/Settings/SampleSceneProfile.asset";
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
                globalVolume.sharedProfile = profile;
            }

            // 1. Tonemapping: Neutral
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            // 2. Vignette: Cinematic border falloff for dark mode
            if (!profile.TryGet<Vignette>(out var vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.38f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.40f;

            // 3. Bloom: Glow on light fixtures, key glint, and emissives
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.90f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.50f;

            // 4. Color Adjustments: Rich shadows and crisp contrast
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = -0.10f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 18.0f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 6.0f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[DarkModeBuilder] Global Volume post-processing configured with Vignette, Bloom, and Color Adjustments.");
        }
    }
}
