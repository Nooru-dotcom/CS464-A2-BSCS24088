using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Assignment02Tests
{
    [MenuItem("CS464/Run Assignment 02 Verifications")]
    public static void RunAllVerifications()
    {
        Debug.Log("==================================================");
        Debug.Log("RUNNING AUTOMATED CS464 ASSIGNMENT 02 VERIFICATIONS");
        Debug.Log("==================================================");

        VerifyPrefabConfigurations();
        VerifyLevelIntegrity();

        Debug.Log(">>> ALL ASSIGNMENT 02 CHECKS PASSED SUCCESSFULLY!");
    }

    private static void VerifyPrefabConfigurations()
    {
        Debug.Log("[Test 1] Verifying Player.prefab...");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab == null) throw new System.Exception("Player.prefab is missing!");

        Rigidbody rb = playerPrefab.GetComponent<Rigidbody>();
        if (rb == null) throw new System.Exception("Player is missing Rigidbody!");
        if (!rb.freezeRotation) throw new System.Exception("Player Rigidbody.freezeRotation must be true!");
        if (rb.interpolation != RigidbodyInterpolation.Interpolate) throw new System.Exception("Player Rigidbody.interpolation must be Interpolate!");

        CapsuleCollider col = playerPrefab.GetComponent<CapsuleCollider>();
        if (col == null) throw new System.Exception("Player is missing CapsuleCollider!");
        if (Mathf.Abs(col.radius - 0.5f) > 0.01f || Mathf.Abs(col.height - 2f) > 0.01f)
            throw new System.Exception($"Player CapsuleCollider dimensions invalid: radius={col.radius}, height={col.height}");

        Transform body = playerPrefab.transform.Find("Body");
        if (body == null) throw new System.Exception("Player is missing child 'Body'!");
        if (body.GetComponent<Collider>() != null) throw new System.Exception("Child 'Body' MUST NOT have a Collider!");
        if (body.GetComponent<MeshFilter>() == null || body.GetComponent<Renderer>() == null)
            throw new System.Exception("Child 'Body' is missing MeshFilter or Renderer!");

        Transform feet = playerPrefab.transform.Find("Feet");
        if (feet == null) throw new System.Exception("Player is missing child 'Feet'!");

        PlayerController pc = playerPrefab.GetComponent<PlayerController>();
        if (pc == null) throw new System.Exception("Player is missing PlayerController script!");
        if (pc.Speed < 4.9f || pc.Speed > 5.1f) throw new System.Exception($"Unexpected Speed: {pc.Speed}");
        if (pc.JumpHeight < 1.79f || pc.JumpHeight > 1.81f) throw new System.Exception($"Unexpected JumpHeight: {pc.JumpHeight}");
        if (pc.FallMultiplier < 2.19f || pc.FallMultiplier > 2.21f) throw new System.Exception($"Unexpected FallMultiplier: {pc.FallMultiplier}");
        if (pc.CoyoteTime < 0.09f || pc.CoyoteTime > 0.11f) throw new System.Exception($"Unexpected CoyoteTime: {pc.CoyoteTime}");
        if (pc.JumpBuffer < 0.11f || pc.JumpBuffer > 0.13f) throw new System.Exception($"Unexpected JumpBuffer: {pc.JumpBuffer}");

        Debug.Log("[Test 1 PASS] Player.prefab configuration is verified!");

        Debug.Log("[Test 2] Verifying MainCamera.prefab...");
        GameObject camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCamera.prefab");
        if (camPrefab == null) throw new System.Exception("MainCamera.prefab is missing!");

        CameraFollow cf = camPrefab.GetComponent<CameraFollow>();
        if (cf == null) throw new System.Exception("MainCamera is missing CameraFollow script!");
        if (cf.Sharpness < 4.9f || cf.Sharpness > 5.1f) throw new System.Exception($"Unexpected Sharpness: {cf.Sharpness}");
        if (cf.LookAhead < 0.34f || cf.LookAhead > 0.36f) throw new System.Exception($"Unexpected LookAhead: {cf.LookAhead}");

        Debug.Log("[Test 2 PASS] MainCamera.prefab configuration is verified!");

        Debug.Log("[Test 3] Verifying Ground Layer...");
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer == -1) throw new System.Exception("Layer 'Ground' is not defined in TagManager!");
        Debug.Log($"[Test 3 PASS] Ground layer is defined at layer index {groundLayer}.");
    }

    private static void VerifyLevelIntegrity()
    {
        for (int i = 1; i <= 5; i++)
        {
            string sceneName = $"Level0{i}";
            string scenePath = $"Assets/Scenes/{sceneName}.unity";
            Debug.Log($"[Test Level] Checking {sceneName}...");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) throw new System.Exception($"Player not found in {sceneName}!");

            GameObject cam = GameObject.FindWithTag("MainCamera");
            if (cam == null) cam = GameObject.Find("Main Camera");
            if (cam == null) throw new System.Exception($"Main Camera not found in {sceneName}!");
            if (cam.GetComponent<CameraFollow>() == null) throw new System.Exception($"Main Camera in {sceneName} missing CameraFollow!");

            GameObject goal = GameObject.Find("Goal");
            if (goal == null) throw new System.Exception($"Goal not found in {sceneName}!");

            GameObject spawn = GameObject.Find("SpawnPad");
            if (spawn == null) throw new System.Exception($"SpawnPad not found in {sceneName}!");

            Debug.Log($"[Test Level PASS] {sceneName} verified (Player, Cam, Spawn, Goal present).");
        }
    }
}
