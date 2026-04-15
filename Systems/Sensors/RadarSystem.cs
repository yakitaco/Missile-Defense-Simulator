using System.Collections;
using UnityEngine;

public class RadarSystem : MonoBehaviour
{
    [Header("System Integration")]
    public TrackingAndTargeting trackingSystem;
    public float scanInterval = 1.0f; // スキャン周期（秒）

    [Header("Radar RF Parameters (Realistic)")]
    [Tooltip("送信電力 (Watts) - 例: 1,000,000W (早期警戒レーダー級)")]
    public float transmitPowerW = 1000000f;
    [Tooltip("アンテナ利得 (dBi) - 例: 40dBi")]
    public float antennaGainDb = 40f;
    [Tooltip("動作周波数 (Hz) - 例: 3,000,000,000Hz (3GHz / Sバンド)")]
    public float frequencyHz = 3000000000f;
    [Tooltip("システム損失 (dB) - 大気減衰やケーブル損失など")]
    public float systemLossDb = 5f;

    [Header("Receiver & Noise Parameters")]
    [Tooltip("受信帯域幅 (Hz) - 例: 1,000,000Hz (1MHz)")]
    public float receiverBandwidthHz = 1000000f;
    [Tooltip("システム雑音温度 (K) - 通常 290K")]
    public float systemTemperatureK = 290f;
    [Tooltip("雑音指数 (dB) - 受信機自身のノイズ")]
    public float noiseFigureDb = 3f;
    [Tooltip("探知に必要な最低SNR閾値 (dB) - 例: 13dB")]
    public float snrThresholdDb = 13.0f;

    [Header("Scan Volume (Field of View)")]
    [Tooltip("正面を基準とした左右の探知限界角 (度)")]
    [Range(0, 180)] public float azimuthLimit = 60f;
    [Tooltip("水平を基準とした上下の探知限界角 (度)")]
    [Range(0, 90)] public float elevationLimit = 80f;

    // 物理定数
    private const float LightSpeed = 299792458f; // 光速 (m/s)
    private const float BoltzmannConst = 1.38e-23f; // ボルツマン定数 (J/K)

    private float wavelength;
    private float noiseFloorDbw; // 熱雑音フロア (dBW)

    private Transform cuedTarget;

    void Start()
    {
        InitializeRadarPhysics();
        StartCoroutine(RadarScanRoutine());
    }

    /// <summary>
    /// レーダーの固定物理パラメータを事前計算する
    /// </summary>
    private void InitializeRadarPhysics()
    {
        // 波長の計算: λ = c / f
        wavelength = LightSpeed / frequencyHz;

        // 熱雑音の計算: N = k * T * B
        float noisePowerW = BoltzmannConst * systemTemperatureK * receiverBandwidthHz;
        
        // WattsからdBW(デシベルワット)への変換
        // 最終的なノイズフロア = 熱雑音(dBW) + 雑音指数(dB)
        noiseFloorDbw = (10f * Mathf.Log10(noisePowerW)) + noiseFigureDb;
        
        Debug.Log($"[Radar Init] 波長: {wavelength:F3}m, ノイズフロア: {noiseFloorDbw:F1} dBW");
    }

    private IEnumerator RadarScanRoutine()
    {
        while (true)
        {
            ScanEnvironment();
            yield return new WaitForSeconds(scanInterval);
        }
    }

    private void ScanEnvironment()
    {
        BallisticMissile[] activeMissiles = FindObjectsOfType<BallisticMissile>();

        foreach (var missile in activeMissiles)
        {
            Vector3 directionToTarget = missile.transform.position - transform.position;
            float distance = directionToTarget.magnitude;

            // --- 1. スキャンボリューム（視野）判定 ---
            Vector3 localDir = transform.InverseTransformDirection(directionToTarget);
            float targetAzimuth = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float targetElevation = Mathf.Atan2(localDir.y, new Vector2(localDir.x, localDir.z).magnitude) * Mathf.Rad2Deg;

            if (Mathf.Abs(targetAzimuth) > azimuthLimit || targetElevation > elevationLimit || targetElevation < 0)
            {
                continue; // レーダーの死角
            }

            // --- 2. 視線 (Line of Sight) 判定 ---
            if (Physics.Raycast(transform.position, directionToTarget.normalized, out RaycastHit hit, distance))
            {
                if (hit.collider.gameObject != missile.gameObject) continue; // 地形などに遮蔽されている
            }

            // --- 3. レーダー方程式による SNR計算 (dBベース) ---
            float rcs = missile.crossSectionalArea; // 目標のRCS (m^2)

            // 送信電力をdBWに変換
            float ptDbw = 10f * Mathf.Log10(transmitPowerW);
            
            // RCSをdBsmに変換
            float rcsDbsm = 10f * Mathf.Log10(rcs > 0 ? rcs : 0.001f);
            
            // 距離減衰項の計算 (R^4 を dBに変換すると 40*log10(R))
            float rangeLossDb = 40f * Mathf.Log10(distance);
            
            // 定数項 ( (4π)^3 ) のdB計算
            float constantLossDb = 30f * Mathf.Log10(4f * Mathf.PI);

            // 受信電力 (dBW) の算出
            // Pr = Pt + 2G + 20log(λ) + σ - 30log(4π) - 40log(R) - L
            float receivedPowerDbw = ptDbw 
                                   + (2f * antennaGainDb) 
                                   + (20f * Mathf.Log10(wavelength)) 
                                   + rcsDbsm 
                                   - constantLossDb 
                                   - rangeLossDb 
                                   - systemLossDb;

            // SNR (信号対雑音比) の計算: 受信電力 - ノイズフロア
            float snrDb = receivedPowerDbw - noiseFloorDbw;

            // スワーリング変動（Swerling Fluctuation）の簡易再現
            // 目標の姿勢変化による反射波のフェージング（揺らぎ）をランダムノイズとして付与
            float fluctuation = Random.Range(-3f, 3f); 
            float finalSnr = snrDb + fluctuation;

            // --- 4. 探知閾値の判定 ---
            if (finalSnr >= snrThresholdDb)
            {
                trackingSystem.RegisterBlip(missile, missile.transform.position, Time.time);
                Debug.Log($"<color=#00FF00>[Radar]</color> 探知成功! 距離: {distance:F0}m | RCS: {rcs}m² | SNR: {finalSnr:F1}dB (閾値: {snrThresholdDb}dB)");
            }
            else
            {
                // SNRが足りずにノイズに埋もれた場合
                Debug.Log($"<color=#FF8800>[Radar]</color> 信号微弱(ノイズ埋没). 距離: {distance:F0}m | SNR: {finalSnr:F1}dB");
            }
        }
    }

    /// <summary>
    /// エディタ上でレーダーの探知範囲を視覚化する
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.1f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, elevationLimit * 2f, 10000f, 0f, azimuthLimit / elevationLimit);
    }

    public void SetCueing(Transform target)
    {
        cuedTarget = target;
        // ターゲットの方向へレーダーの正面を向ける（自動旋回）
        StartCoroutine(RotateTowardsTarget(target));
    }

    private IEnumerator RotateTowardsTarget(Transform target)
    {
        while (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            yield return null;
        }
    }
}