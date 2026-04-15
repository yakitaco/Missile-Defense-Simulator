using UnityEngine;

/// <summary>
/// シミュレーション環境の物理定数と環境変数を管理するシングルトン
/// </summary>
public class CustomPhysicsEnvironment : MonoBehaviour
{
    // どこからでもアクセスできるようにシングルトン化
    public static CustomPhysicsEnvironment Instance { get; private set; }

    [Header("Planetary Environment")]
    [Tooltip("重力加速度 (m/s^2)")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Header("Atmosphere Model")]
    [Tooltip("海抜0mでの空気密度 (kg/m^3)")]
    public float seaLevelDensity = 1.225f; 
    [Tooltip("スケールハイト (m) - 地球の場合は約8500m")]
    public float scaleHeight = 8500f; 

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    /// <summary>
    /// 高度に応じた空気密度を計算する（簡易的な指数大気モデル）
    /// </summary>
    public float GetAirDensity(float altitude)
    {
        if (altitude < 0) return seaLevelDensity;
        // 空気密度 = 海面密度 * exp(-高度 / スケールハイト)
        return seaLevelDensity * Mathf.Exp(-altitude / scaleHeight);
    }
}