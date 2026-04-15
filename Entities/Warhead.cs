using UnityEngine;

[RequireComponent(typeof(InterceptorMissile))]
public class Warhead : MonoBehaviour
{
    [Header("Fuse Settings")]
    [Tooltip("近接信管の作動半径 (m)")]
    public float proximityRadius = 20.0f;
    [Tooltip("着発信管とみなす直撃半径 (m)")]
    public float contactRadius = 2.0f;
    [Tooltip("破壊有効半径 (m)")]
    public float lethalRadius = 30.0f;

    private InterceptorMissile missile;
    private Transform target;
    private float previousDistance = float.MaxValue;
    private bool isDetonated = false;

    void Start()
    {
        missile = GetComponent<InterceptorMissile>();
        target = missile.target;
    }

    void FixedUpdate()
    {
        if (isDetonated || target == null) return;

        float currentDistance = Vector3.Distance(transform.position, target.position);

        // 1. 着発信管（直撃判定）
        if (currentDistance <= contactRadius)
        {
            Detonate("直撃 (Contact)");
            return;
        }

        // 2. 近接信管（Proximity Fuse）の判定
        // 条件: 距離が作動半径以内で、かつ前回フレームより距離が開いた（最接近を過ぎた）瞬間
        if (currentDistance <= proximityRadius && currentDistance > previousDistance)
        {
            Detonate("近接起爆 (Proximity)");
            return;
        }

        previousDistance = currentDistance;
    }

    private void Detonate(string triggerType)
    {
        isDetonated = true;

        if (ExplosionManager.Instance != null)
        {
            ExplosionManager.Instance.SpawnExplosion(transform.position, lethalRadius * 0.1f);
        }

        // 破壊判定
        float finalDistance = Vector3.Distance(transform.position, target.position);
        bool targetDestroyed = finalDistance <= lethalRadius;

        Debug.Log($"【起爆】 トリガー: {triggerType} | 起爆距離: {finalDistance:F1}m");
        
        if (targetDestroyed)
        {
            Debug.Log("<color=green>迎撃成功！ 目標を破壊しました。</color>");
            Destroy(target.gameObject); // 目標の消滅

            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.RegisterIntercept();
            }

        }
        else
        {
            Debug.Log("<color=red>迎撃失敗。 目標は有効破壊範囲外です。</color>");
        }

        // Sceneビューに爆発範囲を描画（緑：成功、赤：失敗）
        Color gizmoColor = targetDestroyed ? Color.green : Color.red;
        Debug.DrawLine(transform.position + Vector3.up * lethalRadius, transform.position - Vector3.up * lethalRadius, gizmoColor, 3f);
        Debug.DrawLine(transform.position + Vector3.right * lethalRadius, transform.position - Vector3.right * lethalRadius, gizmoColor, 3f);
        Debug.DrawLine(transform.position + Vector3.forward * lethalRadius, transform.position - Vector3.forward * lethalRadius, gizmoColor, 3f);

        // 自身（迎撃ミサイル）を破棄
        Destroy(gameObject);
    }
}