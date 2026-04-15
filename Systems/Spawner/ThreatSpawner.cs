using System.Collections;
using UnityEngine;

public class ThreatSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject ballisticMissilePrefab;
    public float spawnInterval = 5f;
    public bool autoSpawn = true;

    [Header("Targeting Settings")]
    [Tooltip("攻撃目標の座標 (デフォルトは防衛陣地)")]
    public Vector3 targetCoordinate = Vector3.zero;
    [Tooltip("弾道ミサイルの打ち上げ仰角 (固定値: 通常は45〜60度)")]
    public float standardElevation = 50f;

    [Header("Randomization Parameters")]
    public float spawnRadiusMin = 30000f;
    public float spawnRadiusMax = 50000f;

    void Start()
    {
        if (autoSpawn)
        {
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnTargetedThreat();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void SpawnTargetedThreat()
    {
        // 1. 発射地点の決定（円周上のランダムな位置）
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(spawnRadiusMin, spawnRadiusMax);
        Vector3 spawnPosition = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

        // プレハブからミサイルの物理特性（質量や空気抵抗係数）を取得
        BallisticMissile prefabParams = ballisticMissilePrefab.GetComponent<BallisticMissile>();

        // 2. 方位角 (Azimuth) の計算
        Vector3 directionToTarget = targetCoordinate - spawnPosition;
        float azimuth = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;

        // 3. 初速 (Speed) の算出（弾道計算アルゴリズムの実行）
        float requiredSpeed = CalculateRequiredSpeed(spawnPosition, targetCoordinate, standardElevation, prefabParams);

        // 4. ミサイルの生成とパラメータの流し込み
        GameObject missileObj = Instantiate(ballisticMissilePrefab, spawnPosition, Quaternion.identity);
        BallisticMissile missile = missileObj.GetComponent<BallisticMissile>();

        missile.launchElevation = standardElevation;
        missile.launchAzimuth = azimuth;
        missile.launchSpeed = requiredSpeed; // 計算された正確な速度を代入

        if (SimulationManager.Instance != null) SimulationManager.Instance.RegisterThreatSpawn();

        missile.Launch();
        Debug.Log($"[FCS] 攻撃諸元算出完了。目標座標 {targetCoordinate} | 距離 {directionToTarget.magnitude:F0}m | 仰角 {standardElevation}° | 算出初速 {requiredSpeed:F1} m/s");
    }

    /// <summary>
    /// 数値計算（二分探索）によって、目標に到達するための正確な初速を算出する
    /// </summary>
    private float CalculateRequiredSpeed(Vector3 start, Vector3 target, float elevation, BallisticMissile p)
    {
        float targetDistance = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(target.x, target.z));
        float g = CustomPhysicsEnvironment.Instance.gravity.magnitude;
        
        // 真空状態での必要速度を計算（これより遅くなることはあり得ないため、これを最小値とする）
        float vacuumSpeed = Mathf.Sqrt((targetDistance * g) / Mathf.Sin(2f * elevation * Mathf.Deg2Rad));

        float minSpeed = vacuumSpeed;
        float maxSpeed = vacuumSpeed * 3f; // 空気抵抗を考慮し、最大で真空の3倍の初速を上限とする
        float bestSpeed = vacuumSpeed;

        // 二分探索法（Binary Search）による反復計算(20回繰り返して精度向上)
        for (int i = 0; i < 20; i++)
        {
            bestSpeed = (minSpeed + maxSpeed) / 2f;
            
            // 仮の速度で弾道をシミュレーションし、飛距離を測る
            float simDistance = SimulateFlightDistance(bestSpeed, elevation, p);

            if (simDistance < targetDistance)
            {
                minSpeed = bestSpeed; // 届かなかったので、下限速度を引き上げる
            }
            else
            {
                maxSpeed = bestSpeed; // 飛びすぎたので、上限速度を引き下げる
            }
        }

        return bestSpeed;
    }

    /// <summary>
    /// 指定された初速と仰角で飛ばした場合の、空気抵抗込みの仮想飛距離を計算する
    /// （GameObjectを生成せず、計算式だけで高速処理する）
    /// </summary>
    private float SimulateFlightDistance(float speed, float elevation, BallisticMissile p)
    {
        float dt = 0.5f; // シミュレーションのタイムステップ（大きいほど計算は早いが精度は落ちる。0.5秒で十分）
        Vector3 pos = Vector3.zero; // 原点からZ軸に向かって飛ばすモデルに簡略化
        
        float radElev = elevation * Mathf.Deg2Rad;
        Vector3 vel = new Vector3(0, speed * Mathf.Sin(radElev), speed * Mathf.Cos(radElev));

        CustomPhysicsEnvironment env = CustomPhysicsEnvironment.Instance;

        // 着弾する（Y座標が0以下になる）までループ計算
        while (pos.y >= 0 && pos.z < 200000f) // 安全装置として距離上限を設定
        {
            float airDensity = env.GetAirDensity(pos.y);
            float currentSpeed = vel.magnitude;
            
            Vector3 dragForce = Vector3.zero;
            if (currentSpeed > 0)
            {
                float dragMag = 0.5f * airDensity * (currentSpeed * currentSpeed) * p.dragCoefficient * p.crossSectionalArea;
                dragForce = -vel.normalized * dragMag;
            }
            
            Vector3 gravityForce = p.mass * env.gravity;
            Vector3 acceleration = (dragForce + gravityForce) / p.mass;

            vel += acceleration * dt;
            pos += vel * dt;
        }

        return pos.z; // Z軸方向への移動距離＝飛距離
    }
}