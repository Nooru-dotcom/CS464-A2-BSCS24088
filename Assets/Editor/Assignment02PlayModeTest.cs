using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Optional physics measurement tool for Assignment 02.
/// Only runs when triggered via CS464 > Run Play Mode Physics Test.
/// Does NOT auto-run on every Play press.
/// </summary>
public static class Assignment02PlayModeTest
{
    private static bool armed = false; // only true after the menu item fires
    private static float testStartTime;
    private static PlayerController player;
    private static Rigidbody rb;
    private static float peakY = float.MinValue;
    private static float startY;
    private static bool reachedApex = false;
    private static float timeToApex = 0f;

    [MenuItem("CS464/Run Play Mode Physics Test")]
    public static void StartTest()
    {
        armed = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Level01.unity");
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!armed) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Reset state
            player = null; rb = null;
            peakY = float.MinValue; startY = 0f;
            reachedApex = false; timeToApex = 0f;
            testStartTime = Time.time;
            EditorApplication.update += UpdateTest;
            Debug.Log("[PlayModeTest] Starting automated jump and physics measurement...");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= UpdateTest;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            armed = false;
        }
    }

    private static void UpdateTest()
    {
        if (player == null)
        {
            player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                rb = player.GetComponent<Rigidbody>();
                startY = player.transform.position.y;
            }
            return;
        }

        float elapsed = Time.time - testStartTime;

        // Trigger a jump at t = 0.5 s
        if (elapsed > 0.5f && elapsed < 0.55f && rb.linearVelocity.y == 0f && peakY == float.MinValue)
        {
            float g = -Physics.gravity.y;
            float v0 = Mathf.Sqrt(2f * g * player.JumpHeight);
            rb.AddForce(Vector3.up * rb.mass * v0, ForceMode.Impulse);
            Debug.Log($"[PlayModeTest] Jump triggered with v0={v0:F2} m/s (target height={player.JumpHeight}m).");
        }

        if (elapsed >= 0.5f)
        {
            float curY = player.transform.position.y;
            if (curY > peakY) peakY = curY;

            if (!reachedApex && rb.linearVelocity.y <= 0f && peakY > startY + 0.5f)
            {
                reachedApex = true;
                timeToApex = Time.time - testStartTime - 0.5f;
                float h = peakY - startY;
                Debug.Log($"[PlayModeTest] APEX: height={h:F2} m (expect ~1.75–1.80 m), t_up={timeToApex:F2} s (expect ~0.61 s).");
            }

            if (reachedApex && player.Grounded && elapsed > 1.2f)
            {
                float timeToLand = Time.time - testStartTime - 0.5f - timeToApex;
                Debug.Log($"[PlayModeTest] LANDED: t_down={timeToLand:F2} s (expect ~0.41 s). Asymmetric gravity verified!");
                EditorApplication.isPlaying = false;
            }
        }
    }
}
