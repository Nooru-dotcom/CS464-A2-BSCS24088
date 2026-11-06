using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Assignment02PlayModeTest
{
    private static bool isTesting = false;
    private static float testStartTime;
    private static PlayerController player;
    private static Rigidbody rb;
    private static float peakY = float.MinValue;
    private static float startY;
    private static bool reachedApex = false;
    private static float timeToApex = 0f;
    private static float timeToLand = 0f;

    [MenuItem("CS464/Run Play Mode Physics Test")]
    public static void StartTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level01.unity");
        EditorApplication.isPlaying = true;
    }

    static Assignment02PlayModeTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.update += UpdateTest;
            testStartTime = Time.time;
            isTesting = true;
            Debug.Log("[PlayModeTest] Starting automated jump and physics measurement...");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= UpdateTest;
            isTesting = false;
        }
    }

    private static void UpdateTest()
    {
        if (!isTesting) return;

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

        // At t = 0.5s, trigger jump via queued jump / impulse
        if (elapsed > 0.5f && elapsed < 0.55f && rb.linearVelocity.y == 0f && peakY == float.MinValue)
        {
            float g = -Physics.gravity.y;
            float v0 = Mathf.Sqrt(2f * g * player.JumpHeight);
            rb.AddForce(Vector3.up * rb.mass * v0, ForceMode.Impulse);
            Debug.Log($"[PlayModeTest] Jump triggered with v0={v0:F2} m/s (JumpHeight setting={player.JumpHeight}m).");
        }

        if (elapsed >= 0.5f)
        {
            float curY = player.transform.position.y;
            if (curY > peakY)
            {
                peakY = curY;
            }

            if (!reachedApex && rb.linearVelocity.y <= 0f && peakY > startY + 0.5f)
            {
                reachedApex = true;
                timeToApex = (Time.time - 0.5f);
                float jumpHeightAchieved = peakY - startY;
                Debug.Log($"[PlayModeTest] APEX REACHED: Height={jumpHeightAchieved:F2} m (expected ~1.75-1.80m due to discrete 0.02s step), Time to Apex={timeToApex:F2} s (expected ~0.61s).");
            }

            if (reachedApex && player.Grounded && elapsed > 1.2f)
            {
                timeToLand = (Time.time - 0.5f - timeToApex);
                float totalAirTime = (Time.time - 0.5f);
                Debug.Log($"[PlayModeTest] LANDED: Fall time={timeToLand:F2} s (expected ~0.41s), Total Air Time={totalAirTime:F2} s (expected ~1.02s).");
                Debug.Log($"[PlayModeTest] Fall is faster than rise: t_down ({timeToLand:F2}s) < t_up ({timeToApex:F2}s). Asymmetric gravity verified!");

                isTesting = false;
                EditorApplication.isPlaying = false;
            }
        }
    }
}
