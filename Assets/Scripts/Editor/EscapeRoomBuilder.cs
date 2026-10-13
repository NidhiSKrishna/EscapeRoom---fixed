using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor.ProBuilder;

namespace EscapeRoom.Editor
{
    /// <summary>
    /// Editor tool responsible for generating parameterized escape room environments using ProBuilder.
    /// Hierarchy output:
    /// Environment
    ///     Architecture
    ///     Furniture
    ///     Props
    /// </summary>
    public static class EscapeRoomBuilder
    {
        private const string RootGameObjectName = "Environment";
        private const string ArchitectureContainerName = "Architecture";
        private const string FurnitureContainerName = "Furniture";
        private const string PropsContainerName = "Props";

        [MenuItem("Tools/Escape Room/Build Prototype Room", false, 10)]
        public static void BuildPrototypeRoomMenu()
        {
            var config = RoomConfiguration.CreatePrototypeDefault();
            BuildRoom(config);
        }

        [MenuItem("Tools/Escape Room/Clear Environment", false, 30)]
        public static void ClearEnvironmentMenu()
        {
            GameObject existing = GameObject.Find(RootGameObjectName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log($"[EscapeRoomBuilder] Removed '{RootGameObjectName}' from active scene.");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            else
            {
                Debug.Log($"[EscapeRoomBuilder] No '{RootGameObjectName}' found to clear.");
            }
        }

        [MenuItem("Tools/Escape Room/Rebuild Cabinet (Almirah)", false, 23)]
        public static void RebuildCabinetMenu()
        {
            GameObject env = GameObject.Find("Environment");
            if (env == null)
            {
                Debug.LogWarning("[EscapeRoomBuilder] 'Environment' root not found.");
                return;
            }
            Transform furn = env.transform.Find("Furniture");
            if (furn == null)
            {
                furn = CreateSubContainer("Furniture", env.transform).transform;
            }
            Transform oldCab = furn.Find("Cabinet");
            if (oldCab != null)
            {
                Undo.DestroyObjectImmediate(oldCab.gameObject);
            }
            Material woodMat = GetOrCreateMaterial("M_Proto_Wood", new Color(0.45f, 0.30f, 0.18f), 0.30f);
            BuildCabinet(RoomConfiguration.CreatePrototypeDefault(), furn, woodMat);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[EscapeRoomBuilder] Rebuilt Cabinet (Almirah) with unified hierarchy.");
        }

        [MenuItem("Tools/Escape Room/Room Builder Window", false, 20)]
        public static void OpenBuilderWindow()
        {
            EscapeRoomBuilderWindow.ShowWindow();
        }

        /// <summary>
        /// Generates the escape room environment based on the specified parameterized configuration.
        /// </summary>
        /// <param name="config">The room parameters.</param>
        /// <returns>The root Environment GameObject.</returns>
        public static GameObject BuildRoom(RoomConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "RoomConfiguration cannot be null.");
            }

            // Remove existing Environment root if present
            GameObject existing = GameObject.Find(RootGameObjectName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log($"[EscapeRoomBuilder] Replacing existing '{RootGameObjectName}' hierarchy.");
            }

            // Create root container
            GameObject root = new GameObject(RootGameObjectName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(root, "Build Escape Room");

            // Create structural containers
            GameObject archContainer = CreateSubContainer(ArchitectureContainerName, root.transform);
            GameObject furnContainer = CreateSubContainer(FurnitureContainerName, root.transform);
            GameObject propsContainer = CreateSubContainer(PropsContainerName, root.transform);

            // Fetch or create prototype materials
            Material floorMat = GetOrCreateMaterial("M_Proto_Floor", new Color(0.42f, 0.44f, 0.46f), 0.25f);
            Material wallMat = GetOrCreateMaterial("M_Proto_Wall", new Color(0.85f, 0.84f, 0.81f), 0.15f);
            Material ceilingMat = GetOrCreateMaterial("M_Proto_Ceiling", new Color(0.24f, 0.25f, 0.27f), 0.10f);
            Material frameMat = GetOrCreateMaterial("M_Proto_DoorFrame", new Color(0.18f, 0.19f, 0.20f), 0.35f, 0.20f);
            Material woodMat = GetOrCreateMaterial("M_Proto_Wood", new Color(0.45f, 0.30f, 0.18f), 0.30f);
            Material crateMat = GetOrCreateMaterial("M_Proto_Crate", new Color(0.64f, 0.50f, 0.34f), 0.20f);

            // 1. Build Architecture (Floor, Ceiling, Walls, Doorway/Frame)
            BuildArchitecture(config, archContainer.transform, floorMat, wallMat, ceilingMat, frameMat);

            // 2. Build Furniture (Shelf, Table, Cabinet)
            BuildFurniture(config, furnContainer.transform, woodMat);

            // 3. Build Props (Crates)
            BuildProps(config, propsContainer.transform, crateMat);

            // 4. Optional prototype interior lighting
            if (config.includeCeilingLight)
            {
                BuildInteriorLighting(config, archContainer.transform);
            }

            // Repaint Scene View and update selection
            if (!Application.isBatchMode)
            {
                Selection.activeGameObject = root;
                EditorGUIUtility.PingObject(root);
                ProBuilderEditor.Refresh(false);
                SceneView.RepaintAll();
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"<color=#5cb85c><b>[EscapeRoomBuilder]</b></color> Prototype room successfully generated! " +
                      $"Dimensions: {config.length:F1}m L x {config.width:F1}m W x {config.height:F1}m H | Wall Thickness: {config.wallThickness:F2}m.");

            return root;
        }

        #region Architecture Generation

        private static void BuildArchitecture(
            RoomConfiguration config,
            Transform parent,
            Material floorMat,
            Material wallMat,
            Material ceilingMat,
            Material frameMat)
        {
            float len = config.length;
            float wid = config.width;
            float hgt = config.height;
            float thk = config.wallThickness;

            // --- Floor ---
            // Top plane sits at Y = 0
            Vector3 floorSize = new Vector3(wid + 2f * thk, thk, len + 2f * thk);
            Vector3 floorPos = new Vector3(0f, -thk * 0.5f, 0f);
            CreateProBuilderCube("Floor", parent, floorPos, floorSize, Quaternion.identity, floorMat);

            // --- Ceiling ---
            // Bottom plane sits at Y = hgt (3.5m)
            Vector3 ceilSize = new Vector3(wid + 2f * thk, thk, len + 2f * thk);
            Vector3 ceilPos = new Vector3(0f, hgt + thk * 0.5f, 0f);
            CreateProBuilderCube("Ceiling", parent, ceilPos, ceilSize, Quaternion.identity, ceilingMat);

            // --- Left Wall (-X) ---
            Vector3 leftWallSize = new Vector3(thk, hgt, len + 2f * thk);
            Vector3 leftWallPos = new Vector3(-(wid * 0.5f + thk * 0.5f), hgt * 0.5f, 0f);
            CreateProBuilderCube("Wall_Left", parent, leftWallPos, leftWallSize, Quaternion.identity, wallMat);

            // --- Right Wall (+X) ---
            Vector3 rightWallSize = new Vector3(thk, hgt, len + 2f * thk);
            Vector3 rightWallPos = new Vector3(wid * 0.5f + thk * 0.5f, hgt * 0.5f, 0f);
            CreateProBuilderCube("Wall_Right", parent, rightWallPos, rightWallSize, Quaternion.identity, wallMat);

            // --- Back Wall (-Z) ---
            Vector3 backWallSize = new Vector3(wid, hgt, thk);
            Vector3 backWallPos = new Vector3(0f, hgt * 0.5f, -(len * 0.5f + thk * 0.5f));
            CreateProBuilderCube("Wall_Back", parent, backWallPos, backWallSize, Quaternion.identity, wallMat);

            // --- Front Wall (+Z) with Exit Doorway ---
            float frontWallZ = len * 0.5f + thk * 0.5f;

            if (!config.includeExitDoorway)
            {
                // Solid front wall
                Vector3 solidFrontSize = new Vector3(wid, hgt, thk);
                Vector3 solidFrontPos = new Vector3(0f, hgt * 0.5f, frontWallZ);
                CreateProBuilderCube("Wall_Front", parent, solidFrontPos, solidFrontSize, Quaternion.identity, wallMat);
            }
            else
            {
                // Front wall with doorway
                GameObject frontWallContainer = CreateSubContainer("Wall_Front", parent);
                frontWallContainer.transform.localPosition = new Vector3(0f, 0f, frontWallZ);

                float doorW = Mathf.Clamp(config.doorwayWidth, 0.5f, wid - 0.5f);
                float doorH = Mathf.Clamp(config.doorwayHeight, 1.0f, hgt - 0.2f);
                float sideWallWidth = (wid - doorW) * 0.5f;

                // Left segment of front wall
                Vector3 leftSegSize = new Vector3(sideWallWidth, hgt, thk);
                Vector3 leftSegPos = new Vector3(-(doorW * 0.5f + sideWallWidth * 0.5f), hgt * 0.5f, 0f);
                CreateProBuilderCube("Wall_Front_Left", frontWallContainer.transform, leftSegPos, leftSegSize, Quaternion.identity, wallMat);

                // Right segment of front wall
                Vector3 rightSegSize = new Vector3(sideWallWidth, hgt, thk);
                Vector3 rightSegPos = new Vector3(doorW * 0.5f + sideWallWidth * 0.5f, hgt * 0.5f, 0f);
                CreateProBuilderCube("Wall_Front_Right", frontWallContainer.transform, rightSegPos, rightSegSize, Quaternion.identity, wallMat);

                // Header segment above doorway opening
                float headerHeight = hgt - doorH;
                Vector3 headerSize = new Vector3(doorW, headerHeight, thk);
                Vector3 headerPos = new Vector3(0f, doorH + headerHeight * 0.5f, 0f);
                CreateProBuilderCube("Wall_Front_Header", frontWallContainer.transform, headerPos, headerSize, Quaternion.identity, wallMat);

                // Basic exit doorway / frame
                BuildExitDoorFrame(config, frontWallContainer.transform, doorW, doorH, frameMat);
            }
        }

        private static void BuildExitDoorFrame(
            RoomConfiguration config,
            Transform parent,
            float doorWidth,
            float doorHeight,
            Material frameMat)
        {
            GameObject frameContainer = CreateSubContainer("ExitDoorFrame", parent);
            frameContainer.transform.localPosition = Vector3.zero;

            float trimW = config.doorFrameTrimWidth;
            float trimD = config.doorFrameTrimDepth;

            // Left jamb
            Vector3 leftJambSize = new Vector3(trimW, doorHeight, trimD);
            Vector3 leftJambPos = new Vector3(-doorWidth * 0.5f + trimW * 0.5f, doorHeight * 0.5f, 0f);
            CreateProBuilderCube("Frame_LeftJamb", frameContainer.transform, leftJambPos, leftJambSize, Quaternion.identity, frameMat);

            // Right jamb
            Vector3 rightJambSize = new Vector3(trimW, doorHeight, trimD);
            Vector3 rightJambPos = new Vector3(doorWidth * 0.5f - trimW * 0.5f, doorHeight * 0.5f, 0f);
            CreateProBuilderCube("Frame_RightJamb", frameContainer.transform, rightJambPos, rightJambSize, Quaternion.identity, frameMat);

            // Top lintel / header trim
            Vector3 lintelSize = new Vector3(doorWidth, trimW, trimD);
            Vector3 lintelPos = new Vector3(0f, doorHeight + trimW * 0.5f, 0f);
            CreateProBuilderCube("Frame_Lintel", frameContainer.transform, lintelPos, lintelSize, Quaternion.identity, frameMat);

            // Threshold plate (subtle floor plate marking exit threshold)
            Vector3 thresholdSize = new Vector3(doorWidth - 2f * trimW, 0.02f, trimD);
            Vector3 thresholdPos = new Vector3(0f, 0.01f, 0f);
            CreateProBuilderCube("Frame_Threshold", frameContainer.transform, thresholdPos, thresholdSize, Quaternion.identity, frameMat);
        }

        #endregion

        #region Furniture Generation

        private static void BuildFurniture(RoomConfiguration config, Transform parent, Material woodMat)
        {
            if (config.includeShelfArea)
            {
                BuildShelfArea(config, parent, woodMat);
            }

            if (config.includeTable)
            {
                BuildTable(config, parent, woodMat);
            }

            if (config.includeCabinet)
            {
                BuildCabinet(config, parent, woodMat);
            }
        }

        private static void BuildShelfArea(RoomConfiguration config, Transform parent, Material mat)
        {
            GameObject shelfRoot = CreateSubContainer("ShelfArea", parent);
            shelfRoot.transform.localPosition = config.shelfPosition;

            Vector3 size = config.shelfSize; // X: depth (0.5m), Y: height (2.4m), Z: width (2.2m)
            float depth = size.x;
            float height = size.y;
            float width = size.z;
            float sideThk = 0.05f;
            float shelfThk = 0.04f;
            float backThk = 0.03f;

            // Upright Side - Left (-Z)
            Vector3 sideSize = new Vector3(depth, height, sideThk);
            Vector3 leftSidePos = new Vector3(0f, height * 0.5f, -(width * 0.5f - sideThk * 0.5f));
            CreateProBuilderCube("Shelf_Upright_Left", shelfRoot.transform, leftSidePos, sideSize, Quaternion.identity, mat);

            // Upright Side - Right (+Z)
            Vector3 rightSidePos = new Vector3(0f, height * 0.5f, width * 0.5f - sideThk * 0.5f);
            CreateProBuilderCube("Shelf_Upright_Right", shelfRoot.transform, rightSidePos, sideSize, Quaternion.identity, mat);

            // Back Panel (against wall at -X)
            Vector3 backSize = new Vector3(backThk, height, width);
            Vector3 backPos = new Vector3(-depth * 0.5f + backThk * 0.5f, height * 0.5f, 0f);
            CreateProBuilderCube("Shelf_BackPanel", shelfRoot.transform, backPos, backSize, Quaternion.identity, mat);

            // Horizontal Tiers
            int tiers = Mathf.Max(2, config.shelfTiers);
            float innerWidth = width - 2f * sideThk;
            float innerDepth = depth - backThk;
            float spacing = (height - 0.15f) / (tiers - 1);

            for (int i = 0; i < tiers; i++)
            {
                float tierY = 0.08f + i * spacing;
                Vector3 tierSize = new Vector3(innerDepth, shelfThk, innerWidth);
                Vector3 tierPos = new Vector3(backThk * 0.5f, tierY, 0f);
                CreateProBuilderCube($"Shelf_Tier_{i + 1}", shelfRoot.transform, tierPos, tierSize, Quaternion.identity, mat);
            }
        }

        private static void BuildTable(RoomConfiguration config, Transform parent, Material mat)
        {
            GameObject tableRoot = CreateSubContainer("Table", parent);
            tableRoot.transform.localPosition = config.tablePosition;

            Vector3 size = config.tableSize; // X: width (1.0m), Y: height (0.85m), Z: length (2.0m)
            float tableW = size.x;
            float tableH = size.y;
            float tableL = size.z;
            float topThk = config.tableTopThickness;
            float legThk = config.tableLegThickness;

            // Tabletop
            Vector3 topSize = new Vector3(tableW, topThk, tableL);
            Vector3 topPos = new Vector3(0f, tableH - topThk * 0.5f, 0f);
            CreateProBuilderCube("Table_Top", tableRoot.transform, topPos, topSize, Quaternion.identity, mat);

            // 4 Table Legs
            float legHeight = tableH - topThk;
            Vector3 legSize = new Vector3(legThk, legHeight, legThk);
            float legY = legHeight * 0.5f;
            float legOffsetX = tableW * 0.5f - legThk * 0.6f;
            float legOffsetZ = tableL * 0.5f - legThk * 0.6f;

            CreateProBuilderCube("Table_Leg_FL", tableRoot.transform, new Vector3(-legOffsetX, legY, legOffsetZ), legSize, Quaternion.identity, mat);
            CreateProBuilderCube("Table_Leg_FR", tableRoot.transform, new Vector3(legOffsetX, legY, legOffsetZ), legSize, Quaternion.identity, mat);
            CreateProBuilderCube("Table_Leg_BL", tableRoot.transform, new Vector3(-legOffsetX, legY, -legOffsetZ), legSize, Quaternion.identity, mat);
            CreateProBuilderCube("Table_Leg_BR", tableRoot.transform, new Vector3(legOffsetX, legY, -legOffsetZ), legSize, Quaternion.identity, mat);
        }

        private static void BuildCabinet(RoomConfiguration config, Transform parent, Material mat)
        {
            GameObject cabinetRoot = CreateSubContainer("Cabinet", parent);
            cabinetRoot.transform.localPosition = config.cabinetPosition;

            Vector3 size = config.cabinetSize; // X: depth (0.6m), Y: height (2.0m), Z: width (1.1m)
            float cabD = size.x;
            float cabH = size.y;
            float cabW = size.z;

            // Base plinth
            float plinthH = 0.08f;
            Vector3 plinthSize = new Vector3(cabD + 0.02f, plinthH, cabW + 0.02f);
            Vector3 plinthPos = new Vector3(0f, plinthH * 0.5f, 0f);
            CreateProBuilderCube("Cabinet_Plinth", cabinetRoot.transform, plinthPos, plinthSize, Quaternion.identity, mat);

            // Main body
            float bodyH = cabH - plinthH - 0.06f; // 1.86m
            Vector3 bodySize = new Vector3(cabD, bodyH, cabW);
            Vector3 bodyPos = new Vector3(0f, plinthH + bodyH * 0.5f, 0f); // Y = 1.01m
            CreateProBuilderCube("Cabinet_Body", cabinetRoot.transform, bodyPos, bodySize, Quaternion.identity, mat);

            // Top crown / rim
            float crownH = 0.06f;
            Vector3 crownSize = new Vector3(cabD + 0.04f, crownH, cabW + 0.04f);
            Vector3 crownPos = new Vector3(0f, cabH - crownH * 0.5f, 0f); // Y = 1.97m
            CreateProBuilderCube("Cabinet_Crown", cabinetRoot.transform, crownPos, crownSize, Quaternion.identity, mat);

            // Metal material for door handles
            Material metalMat = GetOrCreateMaterial("M_Proto_Metal", new Color(0.70f, 0.70f, 0.75f), 0.85f, 0.35f);

            // Doors and hinges (assembled cleanly under cabinetRoot in local coordinates)
            float doorThk = 0.025f;
            float doorH = 1.80f;
            float doorW = 0.50f;
            float hingeX = -cabD * 0.5f - 0.005f; // -0.305m (facing -X into room)
            float hingeZ = cabW * 0.5f - 0.04f;  // 0.51m

            // Left Hinge & Door
            GameObject leftHinge = new GameObject("Door_Left_Hinge");
            leftHinge.transform.SetParent(cabinetRoot.transform, false);
            leftHinge.transform.localPosition = new Vector3(hingeX, bodyPos.y, -hingeZ);
            Undo.RegisterCreatedObjectUndo(leftHinge, "Create Door Left Hinge");

            Vector3 leftDoorLocalPos = new Vector3(0f, 0f, doorW * 0.5f);
            var leftDoor = CreateProBuilderCube("Cabinet_Door_Left", leftHinge.transform, leftDoorLocalPos, new Vector3(doorThk, doorH, doorW), Quaternion.identity, mat);

            Vector3 handleSize = new Vector3(0.02f, 0.14f, 0.02f);
            Vector3 leftHandleLocalPos = new Vector3(-doorThk * 0.5f - handleSize.x * 0.5f, 0f, doorW * 0.5f - 0.04f);
            CreateProBuilderCube("Cabinet_Handle_Left", leftDoor.transform, leftHandleLocalPos, handleSize, Quaternion.identity, metalMat);

            // Right Hinge & Door
            GameObject rightHinge = new GameObject("Door_Right_Hinge");
            rightHinge.transform.SetParent(cabinetRoot.transform, false);
            rightHinge.transform.localPosition = new Vector3(hingeX, bodyPos.y, hingeZ);
            Undo.RegisterCreatedObjectUndo(rightHinge, "Create Door Right Hinge");

            Vector3 rightDoorLocalPos = new Vector3(0f, 0f, -doorW * 0.5f);
            var rightDoor = CreateProBuilderCube("Cabinet_Door_Right", rightHinge.transform, rightDoorLocalPos, new Vector3(doorThk, doorH, doorW), Quaternion.identity, mat);

            Vector3 rightHandleLocalPos = new Vector3(-doorThk * 0.5f - handleSize.x * 0.5f, 0f, -doorW * 0.5f + 0.04f);
            CreateProBuilderCube("Cabinet_Handle_Right", rightDoor.transform, rightHandleLocalPos, handleSize, Quaternion.identity, metalMat);
        }

        #endregion

        #region Props Generation

        private static void BuildProps(RoomConfiguration config, Transform parent, Material crateMat)
        {
            if (!config.includeCrates || config.crates == null) return;

            GameObject cratesContainer = CreateSubContainer("Crates", parent);

            foreach (var crateData in config.crates)
            {
                if (crateData == null) continue;
                Quaternion rot = Quaternion.Euler(0f, crateData.rotationY, 0f);
                CreateProBuilderCube(
                    crateData.name,
                    cratesContainer.transform,
                    crateData.position,
                    crateData.size,
                    rot,
                    crateMat);
            }
        }

        #endregion

        #region Prototype Lighting

        private static void BuildInteriorLighting(RoomConfiguration config, Transform parent)
        {
            GameObject lightGo = new GameObject("CeilingLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, config.height - 0.25f, 0f);

            Light lightComp = lightGo.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = config.ceilingLightColor;
            lightComp.intensity = config.ceilingLightIntensity;
            lightComp.range = config.ceilingLightRange;
            lightComp.shadows = LightShadows.Soft;

            Undo.RegisterCreatedObjectUndo(lightGo, "Create Ceiling Light");
        }

        #endregion

        #region ProBuilder & Utility Helpers

        /// <summary>
        /// Instantiates and configures a ProBuilder cube mesh with BoxCollider, materials, and static flags.
        /// </summary>
        public static ProBuilderMesh CreateProBuilderCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 size,
            Quaternion localRotation,
            Material material)
        {
            // Generate cube using ProBuilder's ShapeGenerator
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPosition;
            pb.transform.localRotation = localRotation;

            // Assign material
            MeshRenderer mr = pb.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = material != null ? material : BuiltinMaterials.defaultMaterial;
            }

            // Refresh geometry and optimize
            pb.ToMesh();
            pb.Refresh();
            EditorMeshUtility.Optimize(pb);

            // Ensure BoxCollider is present and configured
            BoxCollider col = pb.gameObject.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = pb.gameObject.AddComponent<BoxCollider>();
            }
            col.size = size;
            col.center = Vector3.zero;

            // Set static editor flags
            GameObjectUtility.SetStaticEditorFlags(pb.gameObject,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);

            Undo.RegisterCreatedObjectUndo(pb.gameObject, $"Create {name}");
            return pb;
        }

        private static GameObject CreateSubContainer(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name} Container");
            return go;
        }

        /// <summary>
        /// Loads existing material from Assets/Art/Materials/ or creates a new one using URP Lit shader.
        /// </summary>
        public static Material GetOrCreateMaterial(string matName, Color color, float smoothness = 0.2f, float metallic = 0.0f)
        {
            string path = $"Assets/Art/Materials/{matName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null && BuiltinMaterials.defaultMaterial != null)
            {
                shader = BuiltinMaterials.defaultMaterial.shader;
            }

            Material mat = new Material(shader);

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        #endregion
    }

    /// <summary>
    /// Interactive Editor Window providing customizable controls for building the prototype room.
    /// </summary>
    public class EscapeRoomBuilderWindow : EditorWindow
    {
        private RoomConfiguration config;
        private Vector2 scrollPos;
        private bool showDimensions = true;
        private bool showDoorway = true;
        private bool showFurniture = true;
        private bool showProps = true;

        public static void ShowWindow()
        {
            var window = GetWindow<EscapeRoomBuilderWindow>("Escape Room Builder");
            window.minSize = new Vector2(380f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            if (config == null)
            {
                config = RoomConfiguration.CreatePrototypeDefault();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Escape Room Prototype Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds the prototype escape room environment using ProBuilder meshes, standard 1m = 1 unit scaling, " +
                "and hierarchical organization under Environment > Architecture, Furniture, Props.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Room Dimensions
            showDimensions = EditorGUILayout.Foldout(showDimensions, "Room Dimensions (Meters)", true);
            if (showDimensions)
            {
                EditorGUI.indentLevel++;
                config.length = EditorGUILayout.FloatField("Length (Z)", config.length);
                config.width = EditorGUILayout.FloatField("Width (X)", config.width);
                config.height = EditorGUILayout.FloatField("Height (Y)", config.height);
                config.wallThickness = EditorGUILayout.FloatField("Wall Thickness", config.wallThickness);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Doorway
            showDoorway = EditorGUILayout.Foldout(showDoorway, "Exit Doorway", true);
            if (showDoorway)
            {
                EditorGUI.indentLevel++;
                config.includeExitDoorway = EditorGUILayout.Toggle("Include Exit Doorway", config.includeExitDoorway);
                if (config.includeExitDoorway)
                {
                    config.doorwayWidth = EditorGUILayout.FloatField("Doorway Width", config.doorwayWidth);
                    config.doorwayHeight = EditorGUILayout.FloatField("Doorway Height", config.doorwayHeight);
                    config.doorFrameTrimWidth = EditorGUILayout.FloatField("Frame Trim Width", config.doorFrameTrimWidth);
                    config.doorFrameTrimDepth = EditorGUILayout.FloatField("Frame Trim Depth", config.doorFrameTrimDepth);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Furniture
            showFurniture = EditorGUILayout.Foldout(showFurniture, "Furniture", true);
            if (showFurniture)
            {
                EditorGUI.indentLevel++;
                config.includeShelfArea = EditorGUILayout.Toggle("Include Shelf Area", config.includeShelfArea);
                if (config.includeShelfArea)
                {
                    config.shelfPosition = EditorGUILayout.Vector3Field("Shelf Position", config.shelfPosition);
                    config.shelfSize = EditorGUILayout.Vector3Field("Shelf Size", config.shelfSize);
                    config.shelfTiers = EditorGUILayout.IntSlider("Shelf Tiers", config.shelfTiers, 2, 8);
                }

                EditorGUILayout.Space(3);
                config.includeTable = EditorGUILayout.Toggle("Include Table", config.includeTable);
                if (config.includeTable)
                {
                    config.tablePosition = EditorGUILayout.Vector3Field("Table Position", config.tablePosition);
                    config.tableSize = EditorGUILayout.Vector3Field("Table Size", config.tableSize);
                }

                EditorGUILayout.Space(3);
                config.includeCabinet = EditorGUILayout.Toggle("Include Cabinet", config.includeCabinet);
                if (config.includeCabinet)
                {
                    config.cabinetPosition = EditorGUILayout.Vector3Field("Cabinet Position", config.cabinetPosition);
                    config.cabinetSize = EditorGUILayout.Vector3Field("Cabinet Size", config.cabinetSize);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Props
            showProps = EditorGUILayout.Foldout(showProps, "Props", true);
            if (showProps)
            {
                EditorGUI.indentLevel++;
                config.includeCrates = EditorGUILayout.Toggle("Include Crates", config.includeCrates);
                if (config.includeCrates && config.crates != null)
                {
                    EditorGUILayout.LabelField($"Configured Crates ({config.crates.Count})", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < config.crates.Count; i++)
                    {
                        var c = config.crates[i];
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        c.name = EditorGUILayout.TextField("Name", c.name);
                        c.position = EditorGUILayout.Vector3Field("Position", c.position);
                        c.size = EditorGUILayout.Vector3Field("Size", c.size);
                        c.rotationY = EditorGUILayout.FloatField("Rotation Y (deg)", c.rotationY);
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            config.includeCeilingLight = EditorGUILayout.Toggle("Ceiling Light", config.includeCeilingLight);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Action Buttons
            if (GUILayout.Button("Reset to Prototype Defaults (12m x 8m x 3.5m)", GUILayout.Height(26)))
            {
                config = RoomConfiguration.CreatePrototypeDefault();
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
            if (GUILayout.Button("Build Prototype Room", GUILayout.Height(36)))
            {
                EscapeRoomBuilder.BuildRoom(config);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Build Prototype Player", GUILayout.Height(30)))
            {
                PlayerBuilder.BuildPrototypePlayerMenu();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Clear Existing Environment", GUILayout.Height(24)))
            {
                EscapeRoomBuilder.ClearEnvironmentMenu();
            }

            EditorGUILayout.Space(10);
        }
    }
}
