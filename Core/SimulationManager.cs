using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    [Header("Time Controls")]
    [Tooltip("シミュレーション速度 (1=等倍, 0.5=スロー, 2=倍速)")]
    [Range(0f, 10f)]
    public float timeScale = 1.0f;
    private float previousTimeScale = 1.0f;
    private bool isPaused = false;

    [Header("Simulation Stats (ReadOnly)")]
    public int totalThreatsSpawned = 0;
    public int interceptedThreats = 0;
    public int impactedThreats = 0;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    private void Update()
    {
        // Spaceキーで一時停止 / 再開
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }

        // 一時停止中でなければ、InspectorのTimeScaleの値をUnityのシステムに適用する
        if (!isPaused)
        {
            Time.timeScale = timeScale;
            // 物理演算の精度を保つため、TimeScaleに合わせてFixedDeltaTimeも調整（弾道計算の精度低下を防ぐ）
            Time.fixedDeltaTime = 0.02f * Time.timeScale; 
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            previousTimeScale = timeScale;
            Time.timeScale = 0f;
            Debug.Log("【シミュレーション一時停止】");
        }
        else
        {
            timeScale = previousTimeScale;
            Time.timeScale = timeScale;
            Debug.Log("【シミュレーション再開】");
        }
    }

    // スコアリング用メソッド群
    public void RegisterThreatSpawn() => totalThreatsSpawned++;

    public void RegisterIntercept()
    {
        interceptedThreats++;
        LogStats();
    }

    public void RegisterImpact()
    {
        impactedThreats++;
        LogStats();
    }

    private void LogStats()
    {
        float successRate = totalThreatsSpawned == 0 ? 0 : ((float)interceptedThreats / (interceptedThreats + impactedThreats)) * 100f;
        Debug.Log($"<color=cyan>--- 戦況報告 --- 飛来数: {totalThreatsSpawned} | 迎撃成功: {interceptedThreats} | 被弾: {impactedThreats} | 防衛率: {successRate:F1}%</color>");
    }
}