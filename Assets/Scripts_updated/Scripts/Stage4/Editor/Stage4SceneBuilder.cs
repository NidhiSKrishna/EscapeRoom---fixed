#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to construct the complete Stage 4 Final Control Room scene
/// according to PERSON_4_STAGE4.md specifications.
/// Accessible via menu item: "Stage 4 -> Build Final Control Room Scene"
/// </summary>
public static class Stage4SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/FinalControlRoom.unity";
    private const string MaterialFolder = "Assets/Materials/Stage4";

    [MenuItem("Stage 4/Build Final Control Room Scene")]
    public static void BuildScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Stage4SceneBuilder] Cannot build scene during Play Mode. Please exit Play Mode first.");
            return;
        }

        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Stage4");
        }

        // Open or create scene
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Clear existing scene contents
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = roots.Length - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(roots[i]);
        }

        // Create Materials
        Material wallMat = CreateOrGetMaterial("M_Wall_DarkMetal", new Color(0.12f, 0.14f, 0.18f), 0.7f, 0.4f);
        Material floorMat = CreateOrGetMaterial("M_Floor_Industrial", new Color(0.16f, 0.18f, 0.22f), 0.4f, 0.2f);
        Material ceilingMat = CreateOrGetMaterial("M_Ceiling_Dark", new Color(0.08f, 0.09f, 0.12f), 0.2f, 0.1f);
        Material serverMat = CreateOrGetMaterial("M_ServerRack", new Color(0.06f, 0.07f, 0.09f), 0.8f, 0.5f);
        Material consoleMat = CreateOrGetMaterial("M_Console", new Color(0.18f, 0.20f, 0.26f), 0.5f, 0.3f);
        Material screenMat = CreateOrGetEmissiveMaterial("M_Screen_Cyan", new Color(0.1f, 0.6f, 0.9f), 1.8f);
        Material doorMat = CreateOrGetMaterial("M_BlastDoor", new Color(0.24f, 0.28f, 0.36f), 0.8f, 0.6f);
        Material stripeMat = CreateOrGetMaterial("M_HazardStripe", new Color(0.85f, 0.7f, 0.1f), 0.3f, 0.2f);

        // 1. Root Container: FinalControlRoom
        GameObject root = new GameObject("FinalControlRoom");

        // 2. Environment
        GameObject env = new GameObject("Environment");
        env.transform.SetParent(root.transform, false);

        // Floor: 18m wide x 26m long
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(env.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        floor.transform.localScale = new Vector3(18f, 0.5f, 26f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Ceiling: at height 6m
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(env.transform, false);
        ceiling.transform.localPosition = new Vector3(0f, 6.25f, 0f);
        ceiling.transform.localScale = new Vector3(18f, 0.5f, 26f);
        ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

        // Walls: North (Z = +13, Far wall with exit door), South (Z = -13, Entrance), West (X = -9), East (X = +9)
        GameObject wallN_Left = CreateWall("WallN_Left", env, new Vector3(-5.5f, 3f, 13f), new Vector3(7f, 6f, 0.6f), wallMat);
        GameObject wallN_Right = CreateWall("WallN_Right", env, new Vector3(5.5f, 3f, 13f), new Vector3(7f, 6f, 0.6f), wallMat);
        GameObject wallN_Top = CreateWall("WallN_Top", env, new Vector3(0f, 4.8f, 13f), new Vector3(4f, 2.4f, 0.6f), wallMat);

        CreateWall("WallS", env, new Vector3(0f, 3f, -13f), new Vector3(18f, 6f, 0.6f), wallMat);
        CreateWall("WallW", env, new Vector3(-9f, 3f, 0f), new Vector3(0.6f, 6f, 26f), wallMat);
        CreateWall("WallE", env, new Vector3(9f, 3f, 0f), new Vector3(0.6f, 6f, 26f), wallMat);

        // Server Racks along West & East walls
        GameObject serverRacks = new GameObject("ServerRacks");
        serverRacks.transform.SetParent(env.transform, false);
        for (int i = 0; i < 5; i++)
        {
            float z = -6f + i * 2.8f;
            CreateServerCabinet("ServerRack_W_" + i, serverRacks, new Vector3(-7.8f, 1.8f, z), new Vector3(1.2f, 3.6f, 1.8f), serverMat, screenMat);
            CreateServerCabinet("ServerRack_E_" + i, serverRacks, new Vector3(7.8f, 1.8f, z), new Vector3(1.2f, 3.6f, 1.8f), serverMat, screenMat);
        }

        // Consoles
        GameObject consoles = new GameObject("Consoles");
        consoles.transform.SetParent(env.transform, false);

        // 3. Player Spawn
        GameObject playerSpawn = new GameObject("PlayerSpawn");
        playerSpawn.transform.SetParent(root.transform, false);
        playerSpawn.transform.localPosition = new Vector3(0f, 0f, -10f);

        // Stand-in Player
        GameObject playerObj = new GameObject("Stage4TestPlayer");
        playerObj.transform.SetParent(root.transform, false);
        playerObj.transform.localPosition = new Vector3(0f, 1f, -10f);
        playerObj.tag = "Player";
        CharacterController cc = playerObj.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0f, 0f);

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(playerObj.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();

        Stage4TestPlayer testPlayer = playerObj.AddComponent<Stage4TestPlayer>();

        // 4. Puzzle Area
        GameObject puzzleArea = new GameObject("PuzzleArea");
        puzzleArea.transform.SetParent(root.transform, false);

        // Puzzle 1: Pattern Panel (Stationed at X = -5.5, Z = -3.5)
        GameObject patternStation = CreatePuzzleStation("PatternPanel", puzzleArea, new Vector3(-5.5f, 0f, -3.5f), consoleMat, screenMat);
        Stage4PatternPuzzle patternPuzzle = patternStation.AddComponent<Stage4PatternPuzzle>();

        // Puzzle 2: Memory Panel (Stationed at X = -5.5, Z = 3.5)
        GameObject memoryStation = new GameObject("MemoryPanel");
        memoryStation.transform.SetParent(puzzleArea.transform, false);
        memoryStation.transform.localPosition = new Vector3(-5.5f, 1.6f, 3.5f);
        Stage4MemoryPuzzle memoryPuzzle = memoryStation.AddComponent<Stage4MemoryPuzzle>();

        // Puzzle 3: Control Panel (Consoles A, B, C, D stationed at X = 5.5, Z = -3.5)
        GameObject controlStation = new GameObject("ControlPanelArea");
        controlStation.transform.SetParent(puzzleArea.transform, false);
        controlStation.transform.localPosition = new Vector3(5.5f, 0f, -3.5f);
        Stage4ControlPanel controlPanel = controlStation.AddComponent<Stage4ControlPanel>();

        // Puzzle 4: Wire Hacking junction box (centered at floor X = 0, Z = 0)
        GameObject wireStation = new GameObject("WireHackPanel");
        wireStation.transform.SetParent(puzzleArea.transform, false);
        wireStation.transform.localPosition = new Vector3(0f, 0f, 0f);
        Stage4WirePuzzle wirePuzzle = wireStation.AddComponent<Stage4WirePuzzle>();

        // Puzzle 5: Code Panel (Stationed at X = 5.5, Z = 3.5)
        GameObject codeStation = CreatePuzzleStation("CodePanel", puzzleArea, new Vector3(5.5f, 0f, 3.5f), consoleMat, screenMat);
        Stage4CodePuzzle codePuzzle = codeStation.AddComponent<Stage4CodePuzzle>();

        // 5. Central Control Terminal (Positioned at X = 0, Z = 6.5)
        GameObject centralTerminalObj = CreatePuzzleStation("CentralControlTerminal", root, new Vector3(0f, 0f, 6.5f), consoleMat, screenMat);
        Stage4CentralTerminal centralTerminal = centralTerminalObj.AddComponent<Stage4CentralTerminal>();

        // 6. Emergency Control Panel (Positioned at X = -4.5, Z = 11.5)
        GameObject emergencyObj = new GameObject("EmergencyControlPanel");
        emergencyObj.transform.SetParent(root.transform, false);
        emergencyObj.transform.localPosition = new Vector3(-4.5f, 0f, 11.5f);
        Stage4SecurityLockdown lockdown = emergencyObj.AddComponent<Stage4SecurityLockdown>();

        // 7. Final Exit Door (Positioned at X = 0, Z = 12.8)
        GameObject doorObj = new GameObject("FinalExitDoor");
        doorObj.transform.SetParent(root.transform, false);
        doorObj.transform.localPosition = new Vector3(0f, 0f, 12.8f);
        Stage4ExitDoor exitDoor = doorObj.AddComponent<Stage4ExitDoor>();

        // 8. Lighting
        GameObject lightingObj = new GameObject("Lighting");
        lightingObj.transform.SetParent(root.transform, false);

        // Facility Key Directional Light
        GameObject dirLightObj = new GameObject("FacilityDirectionalLight");
        dirLightObj.transform.SetParent(lightingObj.transform, false);
        dirLightObj.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
        Light dirLight = dirLightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.85f, 0.92f, 1.0f);
        dirLight.intensity = 0.85f;

        // Cyan Ambient Facility Point Light
        GameObject ambientLightObj = new GameObject("CyanAmbientLight");
        ambientLightObj.transform.SetParent(lightingObj.transform, false);
        ambientLightObj.transform.localPosition = new Vector3(0f, 4.5f, 0f);
        Light ambientLight = ambientLightObj.AddComponent<Light>();
        ambientLight.type = LightType.Point;
        ambientLight.range = 25f;
        ambientLight.color = new Color(0.4f, 0.7f, 1f);
        ambientLight.intensity = 1.2f;

        // Emergency Red Hazard Lights (2 lights)
        GameObject redLight1 = new GameObject("EmergencyLight_1");
        redLight1.transform.SetParent(lightingObj.transform, false);
        redLight1.transform.localPosition = new Vector3(-4f, 4.5f, 10f);
        Light el1 = redLight1.AddComponent<Light>();
        el1.type = LightType.Point;
        el1.color = new Color(1f, 0.1f, 0.1f);
        el1.range = 15f;
        el1.intensity = 2f;
        el1.enabled = false;

        GameObject redLight2 = new GameObject("EmergencyLight_2");
        redLight2.transform.SetParent(lightingObj.transform, false);
        redLight2.transform.localPosition = new Vector3(4f, 4.5f, 10f);
        Light el2 = redLight2.AddComponent<Light>();
        el2.type = LightType.Point;
        el2.color = new Color(1f, 0.1f, 0.1f);
        el2.range = 15f;
        el2.intensity = 2f;
        el2.enabled = false;

        // 9. Audio
        GameObject audioObj = new GameObject("Audio");
        audioObj.transform.SetParent(root.transform, false);
        AudioSource audioSource = audioObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // 10. Stage4Manager
        GameObject managerObj = new GameObject("Stage4Manager");
        managerObj.transform.SetParent(root.transform, false);
        Stage4PuzzleGenerator generator = managerObj.AddComponent<Stage4PuzzleGenerator>();
        Stage4Manager manager = managerObj.AddComponent<Stage4Manager>();
        Stage4TimerAdapter timerAdapter = managerObj.AddComponent<Stage4TimerAdapter>();
        Stage4IntegrationAdapter integrationAdapter = managerObj.AddComponent<Stage4IntegrationAdapter>();

        // 11. Stage4Canvas / UI
        GameObject canvasObj = new GameObject("Stage4Canvas");
        canvasObj.transform.SetParent(root.transform, false);
        Stage4UI ui = canvasObj.AddComponent<Stage4UI>();

        // Wire references across components
        SerializedObject soLockdown = new SerializedObject(lockdown);
        soLockdown.FindProperty("manager").objectReferenceValue = manager;
        soLockdown.FindProperty("exitDoor").objectReferenceValue = exitDoor;
        SerializedProperty normLightsProp = soLockdown.FindProperty("normalLights");
        normLightsProp.arraySize = 2;
        normLightsProp.GetArrayElementAtIndex(0).objectReferenceValue = dirLight;
        normLightsProp.GetArrayElementAtIndex(1).objectReferenceValue = ambientLight;
        SerializedProperty emergLightsProp = soLockdown.FindProperty("emergencyLights");
        emergLightsProp.arraySize = 2;
        emergLightsProp.GetArrayElementAtIndex(0).objectReferenceValue = el1;
        emergLightsProp.GetArrayElementAtIndex(1).objectReferenceValue = el2;
        soLockdown.ApplyModifiedProperties();

        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Stage4SceneBuilder] FinalControlRoom.unity successfully built and saved!");
    }

    private static GameObject CreateWall(string name, GameObject parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
        return wall;
    }

    private static void CreateServerCabinet(string name, GameObject parent, Vector3 pos, Vector3 scale, Material cabMat, Material screenMat)
    {
        GameObject cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cab.name = name;
        cab.transform.SetParent(parent.transform, false);
        cab.transform.localPosition = pos;
        cab.transform.localScale = scale;
        cab.GetComponent<Renderer>().sharedMaterial = cabMat;

        GameObject leds = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leds.name = "LEDs";
        leds.transform.SetParent(cab.transform, false);
        leds.transform.localPosition = new Vector3(0f, 0f, -0.52f);
        leds.transform.localScale = new Vector3(0.7f, 0.8f, 0.05f);
        leds.GetComponent<Renderer>().sharedMaterial = screenMat;
    }

    private static GameObject CreatePuzzleStation(string name, GameObject parent, Vector3 pos, Material consoleMat, Material screenMat)
    {
        GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
        station.name = name;
        station.transform.SetParent(parent.transform, false);
        station.transform.localPosition = new Vector3(pos.x, 0.65f, pos.z);
        station.transform.localScale = new Vector3(1.4f, 1.3f, 0.9f);
        station.GetComponent<Renderer>().sharedMaterial = consoleMat;

        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "Screen";
        screen.transform.SetParent(station.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.35f, -0.4f);
        screen.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        screen.transform.localScale = new Vector3(0.9f, 0.6f, 0.08f);
        screen.GetComponent<Renderer>().sharedMaterial = screenMat;

        return station;
    }

    private static Material CreateOrGetMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static Material CreateOrGetEmissiveMaterial(string name, Color emitColor, float intensity)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            mat.color = emitColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", emitColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emitColor * intensity);
            }
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}
#endif
