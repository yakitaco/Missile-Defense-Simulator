using System.Collections;
using UnityEngine;

public class EarlyWarningSatellite : MonoBehaviour
{
    [Header("System Link")]
    public TrackingAndTargeting trackingSystem;

    [Header("Satellite Parameters")]
    [Tooltip("衛星の名称 (例: SBIRS-1, DSP-23)")]
    public string satelliteName = "EWS-Alpha";
    [Tooltip("スキャン間隔 (秒)")]
    public float scanInterval = 2.0f;
    [Tooltip("探知可能な最小高度 (m) - ブーストの炎が雲を抜ける高度")]
    public float detectionMinAltitude = 1000f;

    [Header("Detection Capabilities")]
    [Tooltip("IR(赤外線)センサーの感度 0-1 (低いほどステルスに強い)")]
    public float irSensitivity = 0.1f;

    void Start()
    {
        StartCoroutine(ContinuousSurveillance());
    }

    private IEnumerator ContinuousSurveillance()
    {
        while (true)
        {
            ScanForPlumes();
            yield return new WaitForSeconds(scanInterval);
        }
    }

    private void ScanForPlumes()
    {
        // 全ての弾道ミサイルを検索
        BallisticMissile[] missiles = FindObjectsOfType<BallisticMissile>();

        foreach (var missile in missiles)
        {
            // 1. 物理的な視認判定 (宇宙から地表方向へのレイキャスト)
            // 簡易的に高度と進行状態で判定
            if (missile.CurrentPosition.y > detectionMinAltitude)
            {
                // 2. 赤外線シグネチャ（RCSとブースト出力から推測）の判定
                // ミサイルの断面積(RCS)が大きく、上昇中であれば探知しやすい
                float irSignature = missile.crossSectionalArea * (missile.Velocity.magnitude / 100f);

                if (irSignature > irSensitivity)
                {
                    ReportLaunch(missile);
                }
            }
        }
    }

    private void ReportLaunch(BallisticMissile missile)
    {
        // 地上システムへ「早期警戒通知」を送信
        // まだ正確な軌道は不明だが、発射点と方位を伝える
        Vector3 launchPoint = missile.transform.position;
        Vector3 currentVelocity = missile.Velocity;

        if (trackingSystem != null)
        {
            trackingSystem.ReceiveSatelliteAlert(missile, launchPoint, currentVelocity);
        }
    }
}