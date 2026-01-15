using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [SerializeField] int targetFPS = 60;

    void Awake()
    {
        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0; // Disable VSync to let targetFrameRate work
    }
}