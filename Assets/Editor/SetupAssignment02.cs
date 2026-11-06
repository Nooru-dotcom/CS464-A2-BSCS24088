using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupAssignment02
{
    private const string GroundLayerName = "Ground";
    private const int GroundLayerIndex = 6;

    [MenuItem("CS464/Setup Assignment 02 Project")]
    public static void RunFullSetup()
    {
        Debug.Log(">>> Starting CS464 Assignment 02 Setup...");

        EnsureLayers();
        EnsureFolders();
        PhysicsMaterial frictionlessMat = EnsurePhysicsMaterial();
        GameObject playerPrefab = SetupPlayerPrefab(frictionlessMat);
        GameObject cameraPrefab = SetupCameraPrefab();
        UpdateLevelPrefabs();

        BuildOrUpdateAllScenes(playerPrefab, cameraPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(">>> CS464 Assignment 02 Setup Complete! No errors.");
    }

    private static void EnsureFolders()
    {
        string[] folders = { "Prefabs", "Materials", "Scenes", "Scripts", "Editor" };
        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder("Assets/" + folder))
            {
                AssetDatabase.CreateFolder("Assets", folder);
            }
        }
    }

    private static void EnsureLayers()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        SerializedProperty groundProp = layers.GetArrayElementAtIndex(GroundLayerIndex);
        if (groundProp.stringValue != GroundLayerName)
        {
            groundProp.stringValue = GroundLayerName;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"Assigned Layer {GroundLayerIndex} to '{GroundLayerName}'.");
        }
    }

    private static PhysicsMaterial EnsurePhysicsMaterial()
    {
        string path = "Assets/Materials/PlayerFrictionless.physicMaterial";
        PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            mat.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static Material GetPlayerMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat");
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { color = new Color(0.90f, 0.15f, 0.15f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.90f, 0.15f, 0.15f));
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Player.mat");
        }
        return mat;
    }

    private static GameObject SetupPlayerPrefab(PhysicsMaterial frictionlessMat)
    {
        string prefabPath = "Assets/Prefabs/Player.prefab";

        // Root GameObject
        GameObject root = new GameObject("Player");
        root.tag = "Player";
        root.layer = LayerMask.NameToLayer("Default");

        // Rigidbody
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.freezeRotation = true;

        // CapsuleCollider: height 2, radius 0.5, center (0, 1, 0)
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.radius = 0.5f;
        col.height = 2f;
        col.center = new Vector3(0f, 1f, 0f);
        col.sharedMaterial = frictionlessMat;

        // Child Body (visual mesh only, no collider)
        GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObj.name = "Body";
        bodyObj.transform.SetParent(root.transform, false);
        bodyObj.transform.localPosition = new Vector3(0f, 1f, 0f);
        bodyObj.transform.localRotation = Quaternion.identity;
        bodyObj.transform.localScale = Vector3.one;

        // Remove collider from child Body
        Collider bodyCol = bodyObj.GetComponent<Collider>();
        if (bodyCol != null) Object.DestroyImmediate(bodyCol);

        // Assign player material
        Renderer bodyRenderer = bodyObj.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.sharedMaterial = GetPlayerMaterial();
        }

        // Child Feet (transform only for ground check sphere)
        GameObject feetObj = new GameObject("Feet");
        feetObj.transform.SetParent(root.transform, false);
        feetObj.transform.localPosition = new Vector3(0f, 0f, 0f);

        // PlayerController script
        PlayerController pc = root.AddComponent<PlayerController>();

        // Set serialized fields via SerializedObject to ensure exact persistence
        SerializedObject so = new SerializedObject(pc);
        so.FindProperty("speed").floatValue = 5f;
        so.FindProperty("jumpHeight").floatValue = 1.8f;
        so.FindProperty("fallMultiplier").floatValue = 2.2f;
        so.FindProperty("coyoteTime").floatValue = 0.1f;
        so.FindProperty("jumpBuffer").floatValue = 0.12f;
        so.FindProperty("feet").objectReferenceValue = feetObj.transform;
        so.FindProperty("checkRadius").floatValue = 0.2f;
        so.FindProperty("groundMask").intValue = 1 << GroundLayerIndex;
        so.FindProperty("body").objectReferenceValue = bodyObj.transform;
        so.FindProperty("squashDuration").floatValue = 0.25f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"Saved updated Player prefab to {prefabPath}");
        return savedPrefab;
    }

    private static GameObject SetupCameraPrefab()
    {
        string prefabPath = "Assets/Prefabs/MainCamera.prefab";

        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";

        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;

        camObj.AddComponent<AudioListener>();

        CameraFollow follow = camObj.AddComponent<CameraFollow>();
        SerializedObject so = new SerializedObject(follow);
        so.FindProperty("offset").vector3Value = new Vector3(0f, 5f, -8f);
        so.FindProperty("sharpness").floatValue = 5f;
        so.FindProperty("lookAhead").floatValue = 0.35f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(camObj, prefabPath);
        Object.DestroyImmediate(camObj);

        Debug.Log($"Saved updated MainCamera prefab to {prefabPath}");
        return saved;
    }

    private static void UpdateLevelPrefabs()
    {
        string[] prefabNames = { "Floor", "Wall", "Cover", "Ramp", "SpawnPad", "Goal", "Landmark", "Marker", "Reward" };
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer == -1) groundLayer = GroundLayerIndex;

        foreach (var pName in prefabNames)
        {
            string path = $"Assets/Prefabs/{pName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
                contents.layer = groundLayer;
                foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = groundLayer;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.Log($"Set prefab {pName} to layer '{GroundLayerName}' ({groundLayer}).");
            }
        }
    }

    private static void BuildOrUpdateAllScenes(GameObject playerPrefab, GameObject cameraPrefab)
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer == -1) groundLayer = GroundLayerIndex;

        for (int i = 1; i <= 5; i++)
        {
            string sceneName = $"Level0{i}";
            string scenePath = $"Assets/Scenes/{sceneName}.unity";

            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"Updating scene {sceneName}...");

            // 1. Set all environment geometry in scene to Ground layer
            GameObject[] rootObjects = scene.GetRootGameObjects();
            Vector3 spawnPadPos = Vector3.zero;
            bool foundSpawn = false;

            foreach (var go in rootObjects)
            {
                if (go.name == "SpawnPad")
                {
                    spawnPadPos = go.transform.position;
                    foundSpawn = true;
                }

                // If not Player and not Camera, ensure it's on Ground layer
                if (!go.CompareTag("Player") && !go.CompareTag("MainCamera") && !go.name.Contains("Player") && !go.name.Contains("Camera"))
                {
                    go.layer = groundLayer;
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = groundLayer;
                    }
                }
            }

            // 2. Remove any old Main Camera in the scene and instantiate cameraPrefab
            GameObject[] cameras = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var c in cameras)
            {
                Object.DestroyImmediate(c);
            }
            // Also check for any untagged camera named "Main Camera"
            Camera existingCam = Object.FindAnyObjectByType<Camera>();
            if (existingCam != null)
            {
                Object.DestroyImmediate(existingCam.gameObject);
            }

            GameObject camInstance = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
            camInstance.name = "Main Camera";

            // 3. Remove old Player instances and instantiate fresh Player prefab
            GameObject[] oldPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in oldPlayers)
            {
                Object.DestroyImmediate(p);
            }
            // Also search for objects named "Player"
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == "Player")
                {
                    Object.DestroyImmediate(rootGo);
                }
            }

            GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.name = "Player";

            // Position Player on the spawn pad
            if (foundSpawn)
            {
                // SpawnPad is at y = top + 0.05f. Player root origin is at the feet.
                // Placing player at y = spawnPadPos.y + 0.05f sets feet directly on top of the pad!
                playerInstance.transform.position = new Vector3(spawnPadPos.x, spawnPadPos.y + 0.05f, spawnPadPos.z);
            }
            else
            {
                playerInstance.transform.position = new Vector3(0f, 0.05f, 3f);
            }

            // Initial camera placement behind player
            CameraFollow camFollow = camInstance.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camInstance.transform.position = playerInstance.transform.position + camFollow.Offset;
                camInstance.transform.LookAt(playerInstance.transform.position);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Scene {sceneName} updated and saved successfully.");
        }
    }
}
