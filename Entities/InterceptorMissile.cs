using UnityEngine;

public class InterceptorMissile : MonoBehaviour
{
    [Header("Targeting")]
    public Transform target;
    [Tooltip("比例航法定数 (通常3〜5)")]
    public float navigationConstant = 4.0f;

    [Header("Missile Performance")]
    public float mass = 300f;
    public float thrust = 15000f; // 推力 (N)
    public float maxThrustTime = 8.0f; // 燃焼時間
    public float dragCoefficient = 0.25f;
    public float crossSectionalArea = 0.2f;

    private Vector3 currentVelocity;
    private Vector3 currentPosition;
    private bool isFlying = false;
    private float flightTime = 0f;

    // 目標の速度計算用
    private Vector3 lastTargetPosition;
    // 目標の加速度計算用
    private Vector3 lastTargetVelocity;

    public void Launch(Vector3 initialVelocity)
    {
        currentPosition = transform.position;
        currentVelocity = initialVelocity;

        if (target != null)
        {
            lastTargetPosition = target.position;
            lastTargetVelocity = Vector3.zero;
        }

        isFlying = true;
    }

    void FixedUpdate()
    {
        if (!isFlying || target == null) return;

        float dt = Time.fixedDeltaTime;
        flightTime += dt;
        CustomPhysicsEnvironment env = CustomPhysicsEnvironment.Instance;

        // 1. 目標の速度ベクトルを推定
        Vector3 targetVelocity = (target.position - lastTargetPosition) / dt;
        Vector3 targetAcceleration = (targetVelocity - lastTargetVelocity) / dt;
        
        lastTargetPosition = target.position;
        lastTargetVelocity = targetVelocity;

        // 2. 比例航法 (Proportional Navigation) による誘導加速度の計算
        Vector3 relativePos = target.position - currentPosition;
        Vector3 relativeVel = targetVelocity - currentVelocity;

        // 視線角速度ベクトル (LOS Rate Vector)
        Vector3 omega = Vector3.Cross(relativePos, relativeVel) / relativePos.sqrMagnitude;

        // 誘導コマンド加速度 (コマンドは現在の進行方向に対する直交ベクトル)
        Vector3 guidanceAcceleration = Vector3.zero;
        if (currentVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 missileDir = currentVelocity.normalized;
            
            // 従来のPN項
            Vector3 pnCommand = navigationConstant * relativeVel.magnitude * Vector3.Cross(omega, missileDir);
            
            // 目標の加速度を考慮したAPN拡張項 (目標加速度の視線に直交する成分)
            Vector3 targetAccProjected = Vector3.ProjectOnPlane(targetAcceleration, relativePos.normalized);
            Vector3 apnCommand = (navigationConstant / 2.0f) * targetAccProjected;

            guidanceAcceleration = pnCommand + apnCommand;

            // ミサイル自身の最大G制限（例: 35G限界）をかけるとよりリアルになります
            float maxGAcceleration = 35f * env.gravity.magnitude;
            guidanceAcceleration = Vector3.ClampMagnitude(guidanceAcceleration, maxGAcceleration);
        }

        // 3. 環境からの力（空気抵抗と重力）
        float altitude = currentPosition.y;
        float speed = currentVelocity.magnitude;
        Vector3 dragForce = Vector3.zero;

        if (speed > 0)
        {
            float airDensity = env.GetAirDensity(altitude);
            float dragMag = 0.5f * airDensity * (speed * speed) * dragCoefficient * crossSectionalArea;
            dragForce = -currentVelocity.normalized * dragMag;
        }
        Vector3 gravityForce = mass * env.gravity;

        // 4. 推力 (Thrust)
        Vector3 thrustForce = Vector3.zero;
        if (flightTime < maxThrustTime && speed > 0)
        {
            // 推力は現在の進行方向に向かって発生
            thrustForce = currentVelocity.normalized * thrust;
        }

        // 5. 運動方程式による更新
        Vector3 physicalForce = dragForce + gravityForce + thrustForce;
        Vector3 physicalAcceleration = physicalForce / mass;

        // 物理的な加速度に、誘導コンピュータからのコマンド加速度を合成
        Vector3 totalAcceleration = physicalAcceleration + guidanceAcceleration;
        currentVelocity += totalAcceleration * dt;
        currentPosition += currentVelocity * dt;

        // 姿勢の更新
        if (currentVelocity != Vector3.zero)
        {
            transform.forward = currentVelocity.normalized;
        }
        transform.position = currentPosition;

        // 軌跡の描画（青線）
        Debug.DrawLine(currentPosition - currentVelocity * dt, currentPosition, Color.blue, 5f);
    }
}