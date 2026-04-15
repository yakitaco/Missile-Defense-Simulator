using System.Collections.Generic;
using UnityEngine;

public class TrackingAndTargeting : MonoBehaviour
{
    public LauncherController launcher;
    
    [Tooltip("迎撃ミサイルの想定平均速度 (m/s)")]
    public float interceptorAverageSpeed = 1200f;
    [Tooltip("未来軌道の予測時間上限 (秒)")]
    public float predictionTimeLimit = 60f;

    [Header("Early Warning UI")]
    public GameObject satelliteAlertUI; // 「Launch Detected」などの警告UI

    // 軌道変化とみなす誤差の閾値 (m)
    public float maneuverTolerance = 1000f; 

    // 追跡中の目標データ
    private class TrackData
    {
        public BallisticMissile missile;
        public Vector3 lastPosition;
        public float lastTime;
        public Vector3 estimatedVelocity;
        
        // 管理変数の追加
        public bool isEngaged = false;
        public Vector3 lastPredictedInterceptPoint;
        public float lastEngageTime;
    }

    private Dictionary<BallisticMissile, TrackData> tracks = new Dictionary<BallisticMissile, TrackData>();

    public void RegisterBlip(BallisticMissile missile, Vector3 position, float time)
    {
        if (!tracks.ContainsKey(missile))
        {
            // 新規目標
            tracks[missile] = new TrackData { missile = missile, lastPosition = position, lastTime = time };
        }
        else
        {
            // 既存目標の速度ベクトルを更新
            TrackData data = tracks[missile];
            float deltaTime = time - data.lastTime;
            
            if (deltaTime > 0)
            {
                data.estimatedVelocity = (position - data.lastPosition) / deltaTime;
            }
            
            data.lastPosition = position;
            data.lastTime = time;

            // 軌道変化の監視と再迎撃ロジック
            if (data.estimatedVelocity.magnitude > 0)
            {
                if (!data.isEngaged)
                {
                    // 初回迎撃
                    CalculateIntercept(data);
                }
                else if (Time.time - data.lastEngageTime > 3.0f) // 連射を防ぐためのクールダウン
                {
                    // 現在の速度ベクトルから簡易的に数秒後の未来位置を出し、以前の予測点と比較
                    Vector3 currentExpectedPath = data.lastPosition + data.estimatedVelocity * 5f;
                    Vector3 oldExpectedPath = data.lastPredictedInterceptPoint; // 厳密には時間同期が必要ですが、ズレの検知として簡易化
                    
                    // 目標が予測軌道から大きく逸脱した（マニューバした）場合
                    if (Vector3.Distance(currentExpectedPath, oldExpectedPath) > maneuverTolerance)
                    {
                        Debug.LogWarning("[FCS] 脅威の重大な軌道変更(MaRV)を検知。会敵予測を再計算し、追撃を開始します！");
                        CalculateIntercept(data);
                    }
                }
            }
        }
    }

    private void CalculateIntercept(TrackData data)
    {
        Vector3 predictedPos = data.lastPosition;
        Vector3 predictedVel = data.estimatedVelocity;
        float timeStep = 0.5f; // 0.5秒刻みで未来をシミュレーション
        CustomPhysicsEnvironment env = CustomPhysicsEnvironment.Instance;

        // 目標の未来軌道をシミュレーションして、迎撃可能な点を探す
        for (float t = 0; t <= predictionTimeLimit; t += timeStep)
        {
            // 予測シミュレーション (重力と空気抵抗)
            float airDensity = env.GetAirDensity(predictedPos.y);
            float dragMag = 0.5f * airDensity * predictedVel.sqrMagnitude * data.missile.dragCoefficient * data.missile.crossSectionalArea;
            Vector3 acceleration = (env.gravity * data.missile.mass - predictedVel.normalized * dragMag) / data.missile.mass;

            predictedVel += acceleration * timeStep;
            predictedPos += predictedVel * timeStep;

            // 着弾してしまったら計算打ち切り
            if (predictedPos.y <= 0) break;

            // ランチャーから予測位置までの距離
            float distanceToIntercept = Vector3.Distance(launcher.transform.position, predictedPos);

            // 迎撃ミサイルが t 秒後にその位置に到達できるか？（時間 = 距離 / 速度）
            float timeForInterceptor = distanceToIntercept / interceptorAverageSpeed;

            if (Mathf.Abs(timeForInterceptor - t) < timeStep)
            {
                Debug.Log($"[FCS] 迎撃ポイント算出完了。予想会敵時間: {t:F1}秒後");
                
                // データの更新
                data.isEngaged = true;
                data.lastPredictedInterceptPoint = predictedPos;
                data.lastEngageTime = Time.time;

                // ランチャーに迎撃ポイントと目標情報を送る
                launcher.AssignTarget(predictedPos, data.missile.transform);

                // Sceneに予測ポイントを描画（黄色）
                Debug.DrawLine(launcher.transform.position, predictedPos, Color.yellow, 10f);
                break;
            }
        }
    }

    // 衛星からのアラート受信
    public void ReceiveSatelliteAlert(BallisticMissile missile, Vector3 launchPoint, Vector3 velocity)
    {
        if (!tracks.ContainsKey(missile))
        {
            Debug.Log($"<color=yellow>[Satellite Alert]</color> {missile.name} の発射を探知！ 推定発射地点: {launchPoint}");

            // トラックデータの初期作成
            RegisterBlip(missile, missile.transform.position, Time.time);
            
            // 早期警戒フラグを立てる
            tracks[missile].isEngaged = false; // まだレーダー確定ではない
            
            // UIでの警告表示（もしあれば）
            if (satelliteAlertUI != null) satelliteAlertUI.SetActive(true);

            // レーダーに対して「その方向を重点的に探せ」というキューイングを出すロジックもここに入れられます
        }
    }
}