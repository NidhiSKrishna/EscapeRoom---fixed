using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor utility responsible for generating the visual and environment polish pass.
    /// Menu item: Tools > Escape Room > Build Visual Pass
    /// Safely builds architectural trim, furniture details, prop variations,
    /// cohesive URP materials, and atmospheric lighting under Environment/VisualDetails.
    /// Idempotent and preserves all gameplay objects intact.
    /// </summary>
    public static class VisualPassBuilder
    {
        private const string RootGameObjectName = "Environment";
        private const string VisualDetailsContainerName = "VisualDetails";
        private const string TexturesDir = "Assets/Art/Textures";
        private const string MaterialsDir = "Assets/Art/Materials";

        [MenuItem("Tools/Escape Room/Build Visual Pass", false, 15)]
        public static void BuildVisualPass()
        {
            Debug.Log("<b><color=#337ab7>[VisualPassBuilder]</color> Starting Environment & Lighting Visual Pass...</b>");

            // 1. Verify active scene is EscapeRoom_Main
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != SceneHousekeeper.MainScenePath)
            {
                if (File.Exists(SceneHousekeeper.MainScenePath))
                {
                    EditorSceneManager.OpenScene(SceneHousekeeper.MainScenePath);
                    activeScene = EditorSceneManager.GetActiveScene();
                }
            }

            GameObject rootEnv = GameObject.Find(RootGameObjectName);
            if (rootEnv == null)
            {
                Debug.LogError("<color=#d9534f>[FAIL]</color> 'Environment' root GameObject not found in active scene. Build prototype room first!");
                return;
            }

            // 2. Idempotent cleanup of existing VisualDetails container
            Transform existingVD = rootEnv.transform.Find(VisualDetailsContainerName);
            if (existingVD != null)
            {
                Undo.DestroyObjectImmediate(existingVD.gameObject);
                Debug.Log("[VisualPassBuilder] Removed previous 'VisualDetails' hierarchy for clean rebuild.");
            }

            // Create VisualDetails root under Environment
            GameObject vdRoot = new GameObject(VisualDetailsContainerName);
            vdRoot.transform.SetParent(rootEnv.transform, false);
            vdRoot.transform.localPosition = Vector3.zero;
            vdRoot.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(vdRoot, "Build Visual Pass");

            // Sub-containers for clean organization
            Transform archTrimContainer = CreateSubContainer("ArchitecturalTrim", vdRoot.transform);
            Transform fixtureContainer = CreateSubContainer("CeilingFixture", vdRoot.transform);
            Transform furnDetailsContainer = CreateSubContainer("FurnitureDetails", vdRoot.transform);
            Transform shelfClutterContainer = CreateSubContainer("ShelfDecorations", vdRoot.transform);
            Transform extraPropsContainer = CreateSubContainer("ExtraProps", vdRoot.transform);
            Transform lightingContainer = CreateSubContainer("Lighting", vdRoot.transform);

            // 3. Generate procedural textures & cohesive URP material palette
            EnsureDirectories();
            GenerateProceduralTextures();
            var palette = SetupMaterialPalette();

            // 4. Update existing Architecture, Furniture, and Crates to use cohesive palette
            UpdateExistingSceneMaterials(palette);

            // 5. Build Architectural Additions
            BuildArchitecturalTrim(archTrimContainer, palette);
            BuildCeilingFixtureAndConduits(fixtureContainer, palette);

            // 6. Build Furniture Enhancements
            BuildFurnitureDetails(furnDetailsContainer, palette);
            BuildShelfClutterSafe(shelfClutterContainer, palette);

            // 7. Build Prop Variations (Crates & Containers)
            BuildExtraProps(extraPropsContainer, palette);

            // 8. Build Atmospheric Lighting
            BuildAtmosphericLighting(lightingContainer);

            // 9. Tune URP Global Volume Post-Processing
            ConfigureGlobalVolumePostProcessing();

            // 10. Refresh ProBuilder, repaint views, and mark scene dirty
            ProBuilderEditor.Refresh(false);
            EditorSceneManager.MarkSceneDirty(activeScene);
            SceneView.RepaintAll();

            Debug.Log("<b><color=#5cb85c>[VisualPassBuilder]</color> Visual Pass completed successfully!</b> Environment transformed with architectural trim, furniture polish, prop variations, and atmospheric lighting.");
        }

        #region Directory & Texture Setup

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(TexturesDir))
            {
                Directory.CreateDirectory(TexturesDir);
                AssetDatabase.Refresh();
            }
            if (!Directory.Exists(MaterialsDir))
            {
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.Refresh();
            }
        }

        private static void GenerateProceduralTextures()
        {
            string floorTexPath = $"{TexturesDir}/T_Floor_Tiles.png";
            string wallTexPath = $"{TexturesDir}/T_Wall_Plaster.png";
            string woodTexPath = $"{TexturesDir}/T_Wood_Grain.png";
            string metalTexPath = $"{TexturesDir}/T_Metal_Brushed.png";
            string crateTexPath = $"{TexturesDir}/T_Crate_Slats.png";

            bool needRefresh = false;

            if (!File.Exists(floorTexPath))
            {
                Texture2D tex = CreateFloorTileTexture(512, 512, 8);
                SaveTexture(tex, floorTexPath);
                needRefresh = true;
            }

            if (!File.Exists(wallTexPath))
            {
                Texture2D tex = CreateWallPlasterTexture(512, 512);
                SaveTexture(tex, wallTexPath);
                needRefresh = true;
            }

            if (!File.Exists(woodTexPath))
            {
                Texture2D tex = CreateWoodGrainTexture(512, 512);
                SaveTexture(tex, woodTexPath);
                needRefresh = true;
            }

            if (!File.Exists(metalTexPath))
            {
                Texture2D tex = CreateBrushedMetalTexture(512, 512);
                SaveTexture(tex, metalTexPath);
                needRefresh = true;
            }

            if (!File.Exists(crateTexPath))
            {
                Texture2D tex = CreateCrateSlatsTexture(512, 512);
                SaveTexture(tex, crateTexPath);
                needRefresh = true;
            }

            if (needRefresh)
            {
                AssetDatabase.Refresh();
                ConfigureTextureImporter(floorTexPath, true);
                ConfigureTextureImporter(wallTexPath, true);
                ConfigureTextureImporter(woodTexPath, true);
                ConfigureTextureImporter(metalTexPath, true);
                ConfigureTextureImporter(crateTexPath, true);
            }
        }

        private static void SaveTexture(Texture2D tex, string path)
        {
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void ConfigureTextureImporter(string path, bool isTileable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = isTileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D CreateFloorTileTexture(int width, int height, int tilesAcross)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            int tileSize = width / tilesAcross;
            int groutWidth = 3;

            for (int y = 0; y < height; y++)
            {
                int localY = y % tileSize;
                bool isGroutY = localY < groutWidth || localY >= tileSize - groutWidth;

                for (int x = 0; x < width; x++)
                {
                    int localX = x % tileSize;
                    bool isGroutX = localX < groutWidth || localX >= tileSize - groutWidth;

                    int idx = y * width + x;
                    if (isGroutX || isGroutY)
                    {
                        // Grout: dark matte charcoal with slight noise
                        float noise = UnityEngine.Random.Range(-0.02f, 0.02f);
                        colors[idx] = new Color(0.18f + noise, 0.18f + noise, 0.19f + noise, 1f);
                    }
                    else
                    {
                        // Tile: subtle bevel towards edges and micro concrete texture
                        float distToEdgeX = Mathf.Min(localX - groutWidth, tileSize - groutWidth - localX);
                        float distToEdgeY = Mathf.Min(localY - groutWidth, tileSize - groutWidth - localY);
                        float bevel = Mathf.Clamp01(Mathf.Min(distToEdgeX, distToEdgeY) / 6f);

                        float tileNoise = (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.08f;
                        float baseTone = Mathf.Lerp(0.40f, 0.52f, bevel) + tileNoise;
                        colors[idx] = new Color(baseTone, baseTone * 0.98f, baseTone * 0.95f, 1f);
                    }
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateWallPlasterTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
                    float n2 = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                    float micro = UnityEngine.Random.Range(-0.02f, 0.02f);
                    float val = 0.82f + (n1 - 0.5f) * 0.08f + (n2 - 0.5f) * 0.04f + micro;
                    colors[y * width + x] = new Color(val, val * 0.98f, val * 0.95f, 1f);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateWoodGrainTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            int plankHeight = height / 4;

            for (int y = 0; y < height; y++)
            {
                int localY = y % plankHeight;
                bool isSeam = localY < 2;

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (isSeam)
                    {
                        colors[idx] = new Color(0.20f, 0.13f, 0.07f, 1f);
                    }
                    else
                    {
                        float grainWave = Mathf.Sin(x * 0.15f + Mathf.PerlinNoise(x * 0.02f, y * 0.05f) * 8f);
                        float grain = Mathf.Abs(grainWave);
                        float tone = Mathf.Lerp(0.52f, 0.70f, grain);
                        float noise = (UnityEngine.Random.value - 0.5f) * 0.05f;
                        float r = (tone + noise) * 0.85f;
                        float g = (tone + noise) * 0.58f;
                        float b = (tone + noise) * 0.35f;
                        colors[idx] = new Color(r, g, b, 1f);
                    }
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBrushedMetalTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float streak = UnityEngine.Random.Range(0.42f, 0.58f);
                for (int x = 0; x < width; x++)
                {
                    float micro = (UnityEngine.Random.value - 0.5f) * 0.04f;
                    float val = Mathf.Clamp01(streak + micro);
                    colors[y * width + x] = new Color(val * 0.95f, val, val * 1.05f, 1f);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateCrateSlatsTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            int slatH = height / 4;

            for (int y = 0; y < height; y++)
            {
                int localY = y % slatH;
                bool isGap = localY < 4;

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (isGap)
                    {
                        colors[idx] = new Color(0.18f, 0.12f, 0.08f, 1f);
                    }
                    else
                    {
                        float woodTone = 0.60f + (Mathf.PerlinNoise(x * 0.05f, y * 0.1f) - 0.5f) * 0.15f;
                        // Nail heads near slat ends
                        bool isNailArea = (x == 24 || x == width - 24) && (localY >= slatH / 2 - 4 && localY <= slatH / 2 + 4);
                        if (isNailArea)
                        {
                            colors[idx] = new Color(0.25f, 0.26f, 0.28f, 1f);
                        }
                        else
                        {
                            colors[idx] = new Color(woodTone, woodTone * 0.75f, woodTone * 0.50f, 1f);
                        }
                    }
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        #endregion

        #region Material Palette

        public struct MaterialPalette
        {
            public Material Wall;
            public Material Floor;
            public Material Ceiling;
            public Material Wood;
            public Material DarkWood;
            public Material Metal;
            public Material Crate;
            public Material Door;
            public Material Trim;
            public Material GlassOrLightEmissive;
        }

        public static MaterialPalette SetupMaterialPalette()
        {
            Texture2D floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/T_Floor_Tiles.png");
            Texture2D wallTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/T_Wall_Plaster.png");
            Texture2D woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/T_Wood_Grain.png");
            Texture2D metalTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/T_Metal_Brushed.png");
            Texture2D crateTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/T_Crate_Slats.png");

            MaterialPalette p = new MaterialPalette();

            p.Wall = ConfigureLitMaterial("M_Proto_Wall", new Color(0.84f, 0.83f, 0.80f), 0.15f, 0.0f, wallTex, new Vector2(3f, 2f));
            p.Floor = ConfigureLitMaterial("M_Proto_Floor", new Color(0.42f, 0.44f, 0.46f), 0.35f, 0.05f, floorTex, new Vector2(4f, 6f));
            p.Ceiling = ConfigureLitMaterial("M_Proto_Ceiling", new Color(0.18f, 0.19f, 0.22f), 0.10f, 0.0f, null, Vector2.one);
            p.Wood = ConfigureLitMaterial("M_Proto_Wood", new Color(0.52f, 0.36f, 0.22f), 0.35f, 0.0f, woodTex, new Vector2(2f, 2f));
            p.DarkWood = ConfigureLitMaterial("M_Proto_DarkWood", new Color(0.24f, 0.16f, 0.10f), 0.40f, 0.0f, woodTex, new Vector2(2f, 2f));
            p.Metal = ConfigureLitMaterial("M_Proto_Metal", new Color(0.42f, 0.45f, 0.48f), 0.65f, 0.85f, metalTex, new Vector2(2f, 2f));
            p.Crate = ConfigureLitMaterial("M_Proto_Crate", new Color(0.65f, 0.50f, 0.35f), 0.20f, 0.0f, crateTex, new Vector2(1f, 1f));
            p.Door = ConfigureLitMaterial("M_Proto_Door", new Color(0.25f, 0.28f, 0.30f), 0.45f, 0.70f, metalTex, new Vector2(1f, 2f));
            p.Trim = ConfigureLitMaterial("M_Proto_Trim", new Color(0.28f, 0.22f, 0.16f), 0.30f, 0.05f, woodTex, new Vector2(1f, 4f));

            // Emissive lamp bulb material
            p.GlassOrLightEmissive = ConfigureLitMaterial("M_Proto_LightEmissive", new Color(1.0f, 0.95f, 0.85f), 0.85f, 0.1f, null, Vector2.one, new Color(1.0f, 0.92f, 0.75f) * 2.5f);

            AssetDatabase.SaveAssets();
            return p;
        }

        private static Material ConfigureLitMaterial(
            string matName,
            Color baseColor,
            float smoothness,
            float metallic,
            Texture2D texture,
            Vector2 tiling,
            Color? emissionColor = null)
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

            if (texture != null && mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", texture);
                mat.SetTextureScale("_BaseMap", tiling);
            }

            if (emissionColor.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor.Value);
            }

            UnityEditor.EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void UpdateExistingSceneMaterials(MaterialPalette p)
        {
            // Floor
            AssignMaterialToGameObject("Floor", p.Floor);
            // Ceiling
            AssignMaterialToGameObject("Ceiling", p.Ceiling);
            // Walls
            AssignMaterialToGameObject("Wall_Left", p.Wall);
            AssignMaterialToGameObject("Wall_Right", p.Wall);
            AssignMaterialToGameObject("Wall_Back", p.Wall);
            AssignMaterialToGameObject("Wall_Front_Left", p.Wall);
            AssignMaterialToGameObject("Wall_Front_Right", p.Wall);
            AssignMaterialToGameObject("Wall_Front_Header", p.Wall);

            // Door Frame
            AssignMaterialToGameObject("Frame_LeftJamb", p.Trim);
            AssignMaterialToGameObject("Frame_RightJamb", p.Trim);
            AssignMaterialToGameObject("Frame_Lintel", p.Trim);
            AssignMaterialToGameObject("Frame_Threshold", p.Metal);
            AssignMaterialToGameObject("Door_Slab", p.Door);

            // Furniture
            AssignMaterialToGameObject("Shelf_BackPanel", p.DarkWood);
            AssignMaterialToGameObject("Shelf_Upright_Left", p.DarkWood);
            AssignMaterialToGameObject("Shelf_Upright_Right", p.DarkWood);
            AssignMaterialToGameObject("Shelf_Tier_1", p.Wood);
            AssignMaterialToGameObject("Shelf_Tier_2", p.Wood);
            AssignMaterialToGameObject("Shelf_Tier_3", p.Wood);
            AssignMaterialToGameObject("Shelf_Tier_4", p.Wood);

            AssignMaterialToGameObject("Table_Top", p.Wood);
            AssignMaterialToGameObject("Table_Leg_FL", p.DarkWood);
            AssignMaterialToGameObject("Table_Leg_FR", p.DarkWood);
            AssignMaterialToGameObject("Table_Leg_BL", p.DarkWood);
            AssignMaterialToGameObject("Table_Leg_BR", p.DarkWood);

            AssignMaterialToGameObject("Cabinet_Body", p.DarkWood);
            AssignMaterialToGameObject("Cabinet_Plinth", p.DarkWood);
            AssignMaterialToGameObject("Cabinet_Crown", p.DarkWood);

            // Crates
            AssignMaterialToGameObject("Crate_01_Large", p.Crate);
            AssignMaterialToGameObject("Crate_02_Stacked", p.Crate);
            AssignMaterialToGameObject("Crate_03_Medium", p.Crate);
            AssignMaterialToGameObject("Crate_04_Small", p.Crate);
            AssignMaterialToGameObject("Crate_05_Medium", p.Crate);
            AssignMaterialToGameObject("Crate_06_Small", p.Crate);
        }

        private static void AssignMaterialToGameObject(string goName, Material mat)
        {
            GameObject go = GameObject.Find(goName);
            if (go == null || mat == null) return;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
            }

            ProBuilderMesh pb = go.GetComponent<ProBuilderMesh>();
            if (pb != null)
            {
                var faces = pb.faces;
                foreach (var face in faces)
                {
                    face.submeshIndex = 0;
                }
                pb.ToMesh();
                pb.Refresh();
            }
        }

        #endregion

        #region Architectural Trim

        private static void BuildArchitecturalTrim(Transform parent, MaterialPalette p)
        {
            // Room bounds: X in [-4, +4], Z in [-6, +6], Y in [0, 3.5]
            float baseboardH = 0.14f;
            float baseboardD = 0.035f;

            // 1. Baseboards (floor perimeter, skipping doorway at X in [-0.55, +0.55] on Z = 6.0)
            // Left Wall Baseboard
            CreateVisualCube("Baseboard_Left", parent, new Vector3(-3.982f, baseboardH * 0.5f, 0f), new Vector3(baseboardD, baseboardH, 12.0f), Quaternion.identity, p.Trim);
            // Right Wall Baseboard
            CreateVisualCube("Baseboard_Right", parent, new Vector3(3.982f, baseboardH * 0.5f, 0f), new Vector3(baseboardD, baseboardH, 12.0f), Quaternion.identity, p.Trim);
            // Back Wall Baseboard
            CreateVisualCube("Baseboard_Back", parent, new Vector3(0f, baseboardH * 0.5f, -5.982f), new Vector3(8.0f, baseboardH, baseboardD), Quaternion.identity, p.Trim);
            // Front Wall Baseboard Left of Doorway (X: -4.0 to -0.55 -> length 3.45m, center -2.275m)
            CreateVisualCube("Baseboard_Front_Left", parent, new Vector3(-2.275f, baseboardH * 0.5f, 5.982f), new Vector3(3.45f, baseboardH, baseboardD), Quaternion.identity, p.Trim);
            // Front Wall Baseboard Right of Doorway (X: +0.55 to +4.0 -> length 3.45m, center +2.275m)
            CreateVisualCube("Baseboard_Front_Right", parent, new Vector3(2.275f, baseboardH * 0.5f, 5.982f), new Vector3(3.45f, baseboardH, baseboardD), Quaternion.identity, p.Trim);

            // 2. Crown Molding / Ceiling Border (perimeter at Y = 3.44m, H: 0.12m, D: 0.05m)
            float crownH = 0.12f;
            float crownD = 0.05f;
            float crownY = 3.50f - crownH * 0.5f;

            CreateVisualCube("CrownMolding_Left", parent, new Vector3(-3.975f, crownY, 0f), new Vector3(crownD, crownH, 12.0f), Quaternion.identity, p.Trim);
            CreateVisualCube("CrownMolding_Right", parent, new Vector3(3.975f, crownY, 0f), new Vector3(crownD, crownH, 12.0f), Quaternion.identity, p.Trim);
            CreateVisualCube("CrownMolding_Back", parent, new Vector3(0f, crownY, -5.975f), new Vector3(8.0f, crownH, crownD), Quaternion.identity, p.Trim);
            CreateVisualCube("CrownMolding_Front", parent, new Vector3(0f, crownY, 5.975f), new Vector3(8.0f, crownH, crownD), Quaternion.identity, p.Trim);

            // 3. Wall Pilasters / Structural Bays (Vertical ribs dividing long 12m walls into 3m bays)
            // Left Wall Pilasters at Z = -3.0m, Z = 0.0m, Z = +3.0m
            float pilasterH = 3.50f - baseboardH - crownH; // 3.24m
            float pilasterY = baseboardH + pilasterH * 0.5f;
            float pilasterW = 0.18f;
            float pilasterD = 0.04f;

            float[] pilasterZs = new float[] { -3.0f, 0.0f, 3.0f };
            foreach (float z in pilasterZs)
            {
                CreateVisualCube($"Pilaster_Left_Z{z:+0;-0;0}", parent, new Vector3(-3.98f, pilasterY, z), new Vector3(pilasterD, pilasterH, pilasterW), Quaternion.identity, p.Trim);
                CreateVisualCube($"Pilaster_Right_Z{z:+0;-0;0}", parent, new Vector3(3.98f, pilasterY, z), new Vector3(pilasterD, pilasterH, pilasterW), Quaternion.identity, p.Trim);
            }

            // Back wall pilasters at X = -2.0m, X = +2.0m
            CreateVisualCube("Pilaster_Back_Left", parent, new Vector3(-2.0f, pilasterY, -5.98f), new Vector3(pilasterW, pilasterH, pilasterD), Quaternion.identity, p.Trim);
            CreateVisualCube("Pilaster_Back_Right", parent, new Vector3(2.0f, pilasterY, -5.98f), new Vector3(pilasterW, pilasterH, pilasterD), Quaternion.identity, p.Trim);

            // 4. Horizontal Dado / Chair Rail (running at Y = 1.05m along walls)
            float railH = 0.06f;
            float railD = 0.025f;
            float railY = 1.05f;

            CreateVisualCube("DadoRail_Left", parent, new Vector3(-3.987f, railY, 0f), new Vector3(railD, railH, 12.0f), Quaternion.identity, p.Trim);
            CreateVisualCube("DadoRail_Right", parent, new Vector3(3.987f, railY, 0f), new Vector3(railD, railH, 12.0f), Quaternion.identity, p.Trim);
            CreateVisualCube("DadoRail_Back", parent, new Vector3(0f, railY, -5.987f), new Vector3(8.0f, railH, railD), Quaternion.identity, p.Trim);
            // Front wall dado rails (split for doorway)
            CreateVisualCube("DadoRail_Front_Left", parent, new Vector3(-2.275f, railY, 5.987f), new Vector3(3.45f, railH, railD), Quaternion.identity, p.Trim);
            // Note: Terminal_Keypad is at X = 0.90, Y = 1.45, Z = 5.96. Dado rail at Y = 1.05 sits well below keypad!
            CreateVisualCube("DadoRail_Front_Right", parent, new Vector3(2.275f, railY, 5.987f), new Vector3(3.45f, railH, railD), Quaternion.identity, p.Trim);

            // 5. Door Architrave / Casing (Front-facing doorway casing)
            float architraveW = 0.08f;
            float architraveD = 0.035f;
            float doorOpeningHalfW = 0.52f;
            float doorH = 2.26f;

            // Left architrave
            CreateVisualCube("Architrave_Left", parent, new Vector3(-doorOpeningHalfW - architraveW * 0.5f, doorH * 0.5f, 5.975f), new Vector3(architraveW, doorH, architraveD), Quaternion.identity, p.Trim);
            // Right architrave
            CreateVisualCube("Architrave_Right", parent, new Vector3(doorOpeningHalfW + architraveW * 0.5f, doorH * 0.5f, 5.975f), new Vector3(architraveW, doorH, architraveD), Quaternion.identity, p.Trim);
            // Top architrave header
            CreateVisualCube("Architrave_Header", parent, new Vector3(0f, doorH + architraveW * 0.5f, 5.975f), new Vector3(doorOpeningHalfW * 2f + architraveW * 2f, architraveW, architraveD), Quaternion.identity, p.Trim);
        }

        private static void BuildCeilingFixtureAndConduits(Transform parent, MaterialPalette p)
        {
            // Ceiling is at Y = 3.50m
            // 1. Industrial Ceiling Junction Box / Base Plate
            CreateVisualCube("Ceiling_MountPlate", parent, new Vector3(0f, 3.47f, 0f), new Vector3(0.70f, 0.06f, 0.70f), Quaternion.identity, p.Metal);

            // 2. Metal Conduit Pipes running along the ceiling
            // Conduit to Left Wall
            CreateVisualCube("Conduit_Left", parent, new Vector3(-2.0f, 3.48f, 0f), new Vector3(4.0f, 0.04f, 0.04f), Quaternion.identity, p.Metal);
            // Conduit to Back Wall
            CreateVisualCube("Conduit_Back", parent, new Vector3(0f, 3.48f, -3.0f), new Vector3(0.04f, 0.04f, 6.0f), Quaternion.identity, p.Metal);

            // 3. Suspension Struts hanging from mount plate down to lamp fixture
            float strutLen = 0.22f;
            float strutY = 3.44f - strutLen * 0.5f;
            CreateVisualCube("Lamp_Strut_FL", parent, new Vector3(-0.18f, strutY, -0.18f), new Vector3(0.025f, strutLen, 0.025f), Quaternion.identity, p.Metal);
            CreateVisualCube("Lamp_Strut_FR", parent, new Vector3(0.18f, strutY, -0.18f), new Vector3(0.025f, strutLen, 0.025f), Quaternion.identity, p.Metal);
            CreateVisualCube("Lamp_Strut_BL", parent, new Vector3(-0.18f, strutY, 0.18f), new Vector3(0.025f, strutLen, 0.025f), Quaternion.identity, p.Metal);
            CreateVisualCube("Lamp_Strut_BR", parent, new Vector3(0.18f, strutY, 0.18f), new Vector3(0.025f, strutLen, 0.025f), Quaternion.identity, p.Metal);

            // 4. Industrial Pendant Hood / Reflector Cage
            CreateVisualCube("Lamp_ReflectorHood", parent, new Vector3(0f, 3.28f, 0f), new Vector3(0.48f, 0.10f, 0.48f), Quaternion.identity, p.Metal);

            // 5. Glowing Emissive Light Diffuser/Bulb Mesh
            CreateVisualCube("Lamp_DiffuserBulb", parent, new Vector3(0f, 3.22f, 0f), new Vector3(0.24f, 0.04f, 0.24f), Quaternion.identity, p.GlassOrLightEmissive);
        }

        #endregion

        #region Furniture Polish

        private static void BuildFurnitureDetails(Transform parent, MaterialPalette p)
        {
            // Cabinet doors and handles are now unified under Cabinet hierarchy in EscapeRoomBuilder.
            // Style them if present:
            AssignMaterialToGameObject("Cabinet_Door_Left", p.DarkWood);
            AssignMaterialToGameObject("Cabinet_Door_Right", p.DarkWood);
            AssignMaterialToGameObject("Cabinet_Handle_Left", p.Metal);
            AssignMaterialToGameObject("Cabinet_Handle_Right", p.Metal);

            // --- TABLE POLISH (X = 1.50m, Z = -1.00m) ---
            // Table top sits at Y = 0.85m. Under-table apron beams sit at Y = 0.76m.
            // Preserves Container_Lockbox at (1.50, 0.850, -0.40) completely!
            float apronH = 0.08f;
            float apronThick = 0.04f;
            float apronY = 0.77f;

            // Front & Back apron beams
            CreateVisualCube("Table_Apron_Front", parent, new Vector3(1.50f, apronY, -0.38f), new Vector3(1.60f, apronH, apronThick), Quaternion.identity, p.Wood);
            CreateVisualCube("Table_Apron_Back", parent, new Vector3(1.50f, apronY, -1.62f), new Vector3(1.60f, apronH, apronThick), Quaternion.identity, p.Wood);
            // Left & Right apron beams
            CreateVisualCube("Table_Apron_Left", parent, new Vector3(0.68f, apronY, -1.00f), new Vector3(apronThick, apronH, 1.20f), Quaternion.identity, p.Wood);
            CreateVisualCube("Table_Apron_Right", parent, new Vector3(2.32f, apronY, -1.00f), new Vector3(apronThick, apronH, 1.20f), Quaternion.identity, p.Wood);

            // Lower Leg Cross-Stretchers (foot rails connecting legs at Y = 0.12m)
            float stretcherH = 0.04f;
            float stretcherY = 0.12f;
            CreateVisualCube("Table_Stretcher_Left", parent, new Vector3(0.68f, stretcherY, -1.00f), new Vector3(apronThick, stretcherH, 1.20f), Quaternion.identity, p.DarkWood);
            CreateVisualCube("Table_Stretcher_Right", parent, new Vector3(2.32f, stretcherY, -1.00f), new Vector3(apronThick, stretcherH, 1.20f), Quaternion.identity, p.DarkWood);
            CreateVisualCube("Table_Stretcher_Cross", parent, new Vector3(1.50f, stretcherY, -1.00f), new Vector3(1.60f, stretcherH, apronThick), Quaternion.identity, p.DarkWood);

            // Tabletop decorative clipboard/ledger placed on far end (Z = -1.45m), 1.05m away from lockbox
            CreateVisualCube("Table_DeskLedger", parent, new Vector3(1.05f, 0.855f, -1.45f), new Vector3(0.24f, 0.015f, 0.32f), Quaternion.Euler(0f, 18f, 0f), p.DarkWood);
        }

        private static void BuildShelfClutterSafe(Transform parent, MaterialPalette p)
        {
            // SHELF AREA: Center (-3.70, 0, -2.00). Width along Z: 2.0m (Z from -3.00 to -1.00).
            // Shelf Tiers:
            // Tier 1 (bottom): Y = 0.25m
            // Tier 2 (KEY TIER): Y = 0.85m -> Key_Room is at (-3.55, 0.872, -2.00). MUST STAY COMPLETELY UNOBSTRUCTED!
            // Tier 3: Y = 1.45m
            // Tier 4: Y = 2.05m

            // --- Tier 1 (Heavy Storage & Bin props) ---
            CreateVisualCube("Shelf_Bin_01", parent, new Vector3(-3.60f, 0.40f, -2.60f), new Vector3(0.28f, 0.24f, 0.38f), Quaternion.identity, p.Metal);
            CreateVisualCube("Shelf_Bin_02", parent, new Vector3(-3.60f, 0.38f, -1.40f), new Vector3(0.26f, 0.20f, 0.34f), Quaternion.Euler(0f, -6f, 0f), p.Wood);

            // --- Tier 2 (Key Tier): ONLY place one tiny bookend at extreme boundary (Z = -2.85m), 0.85m away from Key ---
            CreateVisualCube("Shelf_Bookend_Edge", parent, new Vector3(-3.65f, 0.94f, -2.85f), new Vector3(0.18f, 0.16f, 0.04f), Quaternion.identity, p.Metal);

            // --- Tier 3 (Books, Binders & Folders) ---
            // Set of 4 upright books
            Color[] bookColors = new Color[]
            {
                new Color(0.55f, 0.20f, 0.20f),
                new Color(0.20f, 0.35f, 0.50f),
                new Color(0.25f, 0.45f, 0.25f),
                new Color(0.45f, 0.40f, 0.20f)
            };

            float bookStartZ = -2.65f;
            for (int i = 0; i < 4; i++)
            {
                Material bMat = ConfigureLitMaterial($"M_Proto_Book_{i + 1}", bookColors[i], 0.25f, 0.0f, null, Vector2.one);
                float h = 0.18f + (i % 2) * 0.04f;
                CreateVisualCube($"Shelf_Book_{i + 1}", parent, new Vector3(-3.62f, 1.47f + h * 0.5f, bookStartZ + i * 0.05f), new Vector3(0.18f, h, 0.04f), Quaternion.identity, bMat);
            }

            // Set of 3 thick archive binders on other side
            for (int i = 0; i < 3; i++)
            {
                float bZ = -1.55f + i * 0.07f;
                CreateVisualCube($"Shelf_Binder_{i + 1}", parent, new Vector3(-3.60f, 1.60f, bZ), new Vector3(0.22f, 0.26f, 0.06f), Quaternion.identity, p.DarkWood);
            }

            // Horizontal stack of 2 reference manuals
            CreateVisualCube("Shelf_ManualStack_Bottom", parent, new Vector3(-3.60f, 1.485f, -1.85f), new Vector3(0.22f, 0.035f, 0.16f), Quaternion.Euler(0f, 5f, 0f), p.Trim);
            CreateVisualCube("Shelf_ManualStack_Top", parent, new Vector3(-3.60f, 1.520f, -1.85f), new Vector3(0.21f, 0.030f, 0.15f), Quaternion.Euler(0f, 8f, 0f), p.Wood);

            // --- Tier 4 (Archive Storage Boxes) ---
            CreateVisualCube("Shelf_ArchiveBox_01", parent, new Vector3(-3.60f, 2.18f, -2.50f), new Vector3(0.28f, 0.22f, 0.40f), Quaternion.identity, p.Crate);
            CreateVisualCube("Shelf_ArchiveBox_02", parent, new Vector3(-3.60f, 2.18f, -1.45f), new Vector3(0.28f, 0.22f, 0.40f), Quaternion.Euler(0f, -4f, 0f), p.Crate);
        }

        #endregion

        #region Prop Variations

        private static void BuildExtraProps(Transform parent, MaterialPalette p)
        {
            // CRATE 07: Heavy Equipment Trunk / Footlocker in Back-Right corner
            // Size: 1.20m x 0.45m x 0.60m. Positioned at (3.10, 0.225, -4.80), rotated 14°
            Vector3 c7Pos = new Vector3(3.10f, 0.225f, -4.80f);
            Vector3 c7Size = new Vector3(0.65f, 0.45f, 1.20f);
            Quaternion c7Rot = Quaternion.Euler(0f, 14f, 0f);
            var crate07 = CreateProBuilderCubeWithCollider("Crate_07_Trunk", parent, c7Pos, c7Size, c7Rot, p.Crate);

            // Decorative metal corner brackets on trunk
            CreateVisualCube("Trunk_Bracket_L", crate07.transform, new Vector3(-c7Size.x * 0.48f, 0f, 0f), new Vector3(0.02f, c7Size.y * 0.95f, c7Size.z * 0.98f), Quaternion.identity, p.Metal);

            // CRATE 08: Tall Narrow Supply Crate in Back-Left corner near crate stack
            Vector3 c8Pos = new Vector3(-2.85f, 0.55f, -4.60f);
            Vector3 c8Size = new Vector3(0.60f, 1.10f, 0.60f);
            Quaternion c8Rot = Quaternion.Euler(0f, -12f, 0f);
            CreateProBuilderCubeWithCollider("Crate_08_TallSupply", parent, c8Pos, c8Size, c8Rot, p.Crate);

            // CRATE 09: Slanted Medium Crate stacked on Crate 07 trunk
            Vector3 c9Pos = new Vector3(3.00f, 0.70f, -4.70f);
            Vector3 c9Size = new Vector3(0.60f, 0.50f, 0.70f);
            Quaternion c9Rot = Quaternion.Euler(0f, 32f, 0f);
            CreateProBuilderCubeWithCollider("Crate_09_SlantedStack", parent, c9Pos, c9Size, c9Rot, p.Crate);
        }

        #endregion

        #region Atmospheric Lighting

        private static void BuildAtmosphericLighting(Transform parent)
        {
            // 1. Tame or disable directional sunlight to prevent indoor blowout
            GameObject sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                Light sunLight = sunGo.GetComponent<Light>();
                if (sunLight != null)
                {
                    // For an indoor escape room, directional sunlight must be near zero or disabled
                    sunLight.intensity = 0.0f;
                    sunLight.enabled = false;
                    Debug.Log("[VisualPassBuilder] Disabled Directional Light to achieve atmospheric indoor contrast.");
                }
            }

            // Disable legacy prototype CeilingLight under Architecture to prevent duplicate ceiling lights
            GameObject oldCeilingLight = GameObject.Find("CeilingLight");
            if (oldCeilingLight != null && oldCeilingLight.transform.parent != null && oldCeilingLight.transform.parent.name == "Architecture")
            {
                Light oldLight = oldCeilingLight.GetComponent<Light>();
                if (oldLight != null)
                {
                    oldLight.enabled = false;
                    Debug.Log("[VisualPassBuilder] Disabled legacy prototype CeilingLight under Architecture.");
                }
            }

            // 2. Configure dark cool slate ambient lighting
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f, 1.0f);

            // 3. Primary Practical Ceiling Fixture Light
            // Positioned right under the ceiling fixture lamp (0.0, 3.15, 0.0)
            GameObject ceilingLightGo = new GameObject("Light_CeilingPractical");
            ceilingLightGo.transform.SetParent(parent, false);
            ceilingLightGo.transform.localPosition = new Vector3(0f, 3.15f, 0f);

            Light cLight = ceilingLightGo.AddComponent<Light>();
            cLight.type = LightType.Point;
            cLight.color = new Color(1.0f, 0.93f, 0.82f); // Warm incandescent
            cLight.intensity = 2.4f;
            cLight.range = 13.0f;
            cLight.shadows = LightShadows.Soft;

            ceilingLightGo.AddComponent<UniversalAdditionalLightData>();

            // 4. Accent Light: Keypad & Doorway Area
            // Positioned near front wall above keypad (0.90, 2.30, 5.40m)
            GameObject keypadLightGo = new GameObject("Light_KeypadAccent");
            keypadLightGo.transform.SetParent(parent, false);
            keypadLightGo.transform.localPosition = new Vector3(0.90f, 2.30f, 5.40f);

            Light kLight = keypadLightGo.AddComponent<Light>();
            kLight.type = LightType.Point;
            kLight.color = new Color(0.85f, 0.94f, 1.0f); // Cool technical neutral
            kLight.intensity = 0.95f;
            kLight.range = 3.8f;
            kLight.shadows = LightShadows.None;

            // 5. Accent Light: Table Workspace
            // Positioned above the table (1.50, 2.20, -1.00m)
            GameObject tableLightGo = new GameObject("Light_TableAccent");
            tableLightGo.transform.SetParent(parent, false);
            tableLightGo.transform.localPosition = new Vector3(1.50f, 2.20f, -1.00f);

            Light tLight = tableLightGo.AddComponent<Light>();
            tLight.type = LightType.Point;
            tLight.color = new Color(1.0f, 0.88f, 0.74f); // Warm reading pool
            tLight.intensity = 0.85f;
            tLight.range = 3.6f;
            tLight.shadows = LightShadows.None;

            // 6. Accent Light: Shelf & Key Area
            // Positioned in front of shelf (-3.00, 2.00, -2.00m)
            GameObject shelfLightGo = new GameObject("Light_ShelfAccent");
            shelfLightGo.transform.SetParent(parent, false);
            shelfLightGo.transform.localPosition = new Vector3(-3.00f, 2.00f, -2.00f);

            Light sLight = shelfLightGo.AddComponent<Light>();
            sLight.type = LightType.Point;
            sLight.color = new Color(1.0f, 0.92f, 0.80f); // Soft amber fill
            sLight.intensity = 0.65f;
            sLight.range = 3.6f;
            sLight.shadows = LightShadows.None;
        }

        #endregion

        #region Post-Processing Configuration

        private static void ConfigureGlobalVolumePostProcessing()
        {
            Volume globalVolume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            if (globalVolume == null)
            {
                Debug.LogWarning("[VisualPassBuilder] Global Volume not found in scene.");
                return;
            }

            VolumeProfile profile = globalVolume.sharedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                string profilePath = "Assets/Settings/EscapeRoom_Main_Profile.asset";
                AssetDatabase.CreateAsset(profile, profilePath);
                globalVolume.sharedProfile = profile;
            }

            // 1. Tonemapping
            if (!profile.TryGet<Tonemapping>(out var tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            // 2. Vignette (Cinematic border darkening)
            if (!profile.TryGet<Vignette>(out var vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.28f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.35f;

            // 3. Bloom (Subtle glow around practical lights, no blinding blur)
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.95f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.20f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.5f;

            // 4. Color Adjustments (Subtle contrast and depth)
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0.22f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 14f;
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 5f;

            UnityEditor.EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualPassBuilder] Tuned URP Global Volume profile (Tonemapping, Vignette, Bloom, Color Adjustments).");
        }

        #endregion

        #region ProBuilder Helpers

        /// <summary>
        /// Creates a visual-only ProBuilder mesh (NO collider) to prevent snagging or raycast blocking.
        /// </summary>
        private static ProBuilderMesh CreateVisualCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size,
            Quaternion localRotation,
            Material material)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPosition;
            pb.transform.localRotation = localRotation;

            MeshRenderer mr = pb.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = material != null ? material : BuiltinMaterials.defaultMaterial;
            }

            pb.ToMesh();
            pb.Refresh();
            EditorMeshUtility.Optimize(pb);

            // Strip any collider so it's strictly visual
            BoxCollider col = pb.GetComponent<BoxCollider>();
            if (col != null)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            GameObjectUtility.SetStaticEditorFlags(pb.gameObject,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);

            Undo.RegisterCreatedObjectUndo(pb.gameObject, $"Create {name}");
            return pb;
        }

        /// <summary>
        /// Creates a ProBuilder cube with BoxCollider (for large walk-blocking crates).
        /// </summary>
        private static ProBuilderMesh CreateProBuilderCubeWithCollider(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size,
            Quaternion localRotation,
            Material material)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPosition;
            pb.transform.localRotation = localRotation;

            MeshRenderer mr = pb.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = material != null ? material : BuiltinMaterials.defaultMaterial;
            }

            pb.ToMesh();
            pb.Refresh();
            EditorMeshUtility.Optimize(pb);

            BoxCollider col = pb.gameObject.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = pb.gameObject.AddComponent<BoxCollider>();
            }
            col.size = size;
            col.center = Vector3.zero;

            GameObjectUtility.SetStaticEditorFlags(pb.gameObject,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);

            Undo.RegisterCreatedObjectUndo(pb.gameObject, $"Create {name}");
            return pb;
        }

        private static Transform CreateSubContainer(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name} Container");
            return go.transform;
        }

        #endregion
    }
}
