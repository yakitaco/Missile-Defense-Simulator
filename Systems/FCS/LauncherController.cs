using System.Collections;
using UnityEngine;

public class LauncherController : MonoBehaviour
{
    [Header("Launcher Setup")]
    public GameObject interceptorPrefab;
    public Transform launchPoint; // 発射口のトランスフォーム

    [Header("Turret Mechanics")]
    public float rotationSpeed = 45f; // 毎秒の旋回角度
    public bool requiresAiming = true; // サイロ型（真上発射）ならfalse

    private bool isReadyToFire = true;

    /// <summary>
    /// FCSから目標データを受け取り、迎撃シークエンスを開始する
    /// </summary>
    public void AssignTarget(Vector3 interceptPoint, Transform targetTransform)
    {
        if (isReadyToFire)
        {
            StartCoroutine(EngageRoutine(interceptPoint, targetTransform));
        }
    }

    private IEnumerator EngageRoutine(Vector3 interceptPoint, Transform targetTransform)
    {
        isReadyToFire = false;
        
        // 1. 迎撃ポイントへの方向ベクトルを計算
        Vector3 aimDirection = (interceptPoint - transform.position).normalized;

        // 2. 指向（旋回）プロセス
        if (requiresAiming)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);
            
            // 向きがほぼ一致するまで旋回する
            while (Quaternion.Angle(transform.rotation, targetRotation) > 1.0f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                yield return null;
            }
        }

        // 3. 発射準備完了、発射！
        Debug.Log("[Launcher] 迎撃ミサイル発射！");
        GameObject missileObj = Instantiate(interceptorPrefab, launchPoint.position, transform.rotation);
        
        InterceptorMissile interceptor = missileObj.GetComponent<InterceptorMissile>();
        
        // Targetの設定（前回作成したInterceptorMissile.csの仕様に合わせる）
        interceptor.target = targetTransform;
        
        // ランチャーが向いている方向へ初速を与えて発射
        float initialSpeed = 50f; // 射出システムの初速
        interceptor.Launch(aimDirection * initialSpeed);

        // 次の発射までのクールダウン
        yield return new WaitForSeconds(3.0f);
        isReadyToFire = true;
    }
}