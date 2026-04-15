using UnityEngine;
using TMPro; // TextMeshProを使用

public class TelemetryHUD : MonoBehaviour
{
    [Header("UI Text Elements")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI machText;
    public TextMeshProUGUI gForceText;

    [Header("Target")]
    public BallisticMissile trackedMissile; // Inspectorから追跡したいミサイルをアサイン

    private Vector3 previousVelocity;
    private const float SoundSpeed = 340.29f; // 音速 (m/s) 簡易固定値
    private const float Gravity = 9.81f;      // 重力加速度 (m/s^2)

    void Update()
    {
        if (trackedMissile == null)
        {
            altitudeText.text = "Target Lost";
            return;
        }

        // 1. 高度
        float altitude = trackedMissile.CurrentPosition.y;
        altitudeText.text = $"ALT: {altitude:F0} m";

        // 2. 速度とマッハ数
        float speed = trackedMissile.Velocity.magnitude;
        float mach = speed / SoundSpeed;
        speedText.text = $"SPD: {speed:F0} m/s";
        machText.text = $"MACH: {mach:F2}";

        // 3. 加速度（G）の計算 (1フレーム前の速度との差分から算出)
        Vector3 accelerationVector = (trackedMissile.Velocity - previousVelocity) / Time.deltaTime;
        float gForce = accelerationVector.magnitude / Gravity;
        gForceText.text = $"G-FORCE: {gForce:F1} G";

        previousVelocity = trackedMissile.Velocity;
    }
}