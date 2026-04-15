using UnityEngine;

public class BallisticMissile : MonoBehaviour
{
    [Header("Launch Parameters")]
    [Tooltip("初速 (m/s)")]
    public float launchSpeed = 1500f;
    [Tooltip("打ち上げ仰角 (度)")]
    public float launchElevation = 45f;
    [Tooltip("打ち上げ方位角 (度)")]
    public float launchAzimuth = 0f;

    [Header("Physical Properties")]
    [Tooltip("質量 (kg)")]
    public float mass = 1000f;
    [Tooltip("抗力係数 (Cd値)")]
    public float dragCoefficient = 0.3f;
    [Tooltip("正面投影面積 (m^2)")]
    public float crossSectionalArea = 0.5f;

    [Header("Maneuver Capabilities")]
    [Tooltip("軌道変更能力を有効にするか")]
    public bool enableManeuver = true;
    [Tooltip("機動を開始する高度 (m) - 再突入フェーズ")]
    public float maneuverStartAltitude = 30000f;
    [Tooltip("回避機動の強さ (G)")]
    public float maneuverGForce = 5f;
    [Tooltip("機動方向を変える間隔 (秒)")]
    public float maneuverInterval = 2f;

    private Vector3 currentVelocity;
    private Vector3 currentPosition;
    private bool isFlying = false;
    private Vector3 lastPosition;

    // 追加: マニューバ用変数
    private Vector3 evasiveAcceleration = Vector3.zero;
    private float maneuverTimer = 0f;

    public Vector3 Velocity => currentVelocity;
    public Vector3 CurrentPosition => currentPosition;

    // ... (StartとLaunchメソッドは既存のまま) ...
    void Start() { /* ThreatSpawnerから呼ばれる */ }

    public void Launch()
    {
        currentPosition = transform.position;
        lastPosition = currentPosition;

        // 角度（度）をラジアンに変換
        float radElev = launchElevation * Mathf.Deg2Rad;
        float radAzim = launchAzimuth * Mathf.Deg2Rad;

        // 球面座標系から初期速度ベクトル(x, y, z)を算出
        float vx = launchSpeed * Mathf.Cos(radElev) * Mathf.Sin(radAzim);
        float vy = launchSpeed * Mathf.Sin(radElev);
        float vz = launchSpeed * Mathf.Cos(radElev) * Mathf.Cos(radAzim);

        currentVelocity = new Vector3(vx, vy, vz);
        isFlying = true;
        
        Debug.Log("ミサイル発射！");
    }

    void FixedUpdate()
    {
        if (!isFlying) return;

        float dt = Time.fixedDeltaTime;
        CustomPhysicsEnvironment env = CustomPhysicsEnvironment.Instance;

        if (env == null)
        {
            Debug.LogError("CustomPhysicsEnvironmentが存在しません。");
            return;
        }

        // 環境情報の取得
        float altitude = currentPosition.y;
        float airDensity = env.GetAirDensity(altitude);
        float speed = currentVelocity.magnitude;

        // 回避機動 (Evasive Maneuver) の計算
        evasiveAcceleration = Vector3.zero;
        // 頂点を越えて下降中 かつ 指定高度以下の場合にマニューバ開始
        if (enableManeuver && currentVelocity.y < 0 && altitude < maneuverStartAltitude)
        {
            maneuverTimer -= dt;
            if (maneuverTimer <= 0)
            {
                // 進行方向に対して垂直な平面上のランダムなベクトルを生成
                Vector3 randomDir = Random.onUnitSphere;
                Vector3 orthogonalDir = Vector3.ProjectOnPlane(randomDir, currentVelocity.normalized).normalized;
                
                // 設定されたGフォース分の加速度を計算 (1G = env.gravity.magnitude)
                float gForceMagnitude = maneuverGForce * env.gravity.magnitude;
                evasiveAcceleration = orthogonalDir * gForceMagnitude;
                
                maneuverTimer = maneuverInterval; // タイマーリセット
                Debug.Log($"[Threat] 軌道変更検知！ 高度 {altitude:F0}m で {maneuverGForce}G の回避機動を実施。");
            }
        }

        // 2. 空気抵抗の計算
        Vector3 dragForce = Vector3.zero;
        if (speed > 0)
        {
            Vector3 velocityDir = currentVelocity.normalized;
            float dragMagnitude = 0.5f * airDensity * (speed * speed) * dragCoefficient * crossSectionalArea;
            dragForce = -velocityDir * dragMagnitude;
        }

        // 3. 重力の計算
        Vector3 gravityForce = mass * env.gravity;

        // 4. 加速度の計算 (空力 + 重力 + 【回避機動】)
        Vector3 totalForce = dragForce + gravityForce;
        Vector3 physicalAcceleration = totalForce / mass;
        Vector3 finalAcceleration = physicalAcceleration + evasiveAcceleration;

        // 5. 速度と位置の更新
        currentVelocity += finalAcceleration * dt;
        currentPosition += currentVelocity * dt;

        // 6. オブジェクトの向き更新と描画
        if (currentVelocity != Vector3.zero) transform.forward = currentVelocity.normalized;
        
        // 座標の適用
        transform.position = currentPosition;
        
        // Sceneビューに軌跡を描画 (赤線で10秒間残る)
        Debug.DrawLine(lastPosition, currentPosition, Color.red, 10f);
        lastPosition = currentPosition;

        // 7. 着弾判定 (y = 0 を地面とする)
        if (currentPosition.y <= 0)
        {
            currentPosition.y = 0;
            transform.position = currentPosition;
            isFlying = false;
            if (SimulationManager.Instance != null) SimulationManager.Instance.RegisterImpact();

            // 原点(0,0,0)からの飛距離をログ出力
            float distance = Vector2.Distance(Vector2.zero, new Vector2(currentPosition.x, currentPosition.z));
            Debug.Log($"着弾！ 飛距離: {distance:F2} m");

            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterImpact();
            }
        }
    }
}