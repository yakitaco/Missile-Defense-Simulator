using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class TrailController : MonoBehaviour
{
    public enum MissileType { Threat, Interceptor }
    public MissileType type;

    [Header("Trail Settings")]
    public Color threatColor = Color.red;
    public Color interceptorColor = Color.blue;
    public float trailTime = 10f; // 軌跡が残る時間（秒）

    private TrailRenderer trail;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        
        // 軌跡の長さを設定
        trail.time = trailTime;
        
        // 先端から末尾にかけて透明になるグラデーションを作成
        Gradient gradient = new Gradient();
        Color targetColor = type == MissileType.Threat ? threatColor : interceptorColor;
        
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(targetColor, 0.0f), new GradientColorKey(targetColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        
        trail.colorGradient = gradient;

        // マテリアルが設定されていない場合はデフォルトのパーティクルマテリアルを割り当て
        if (trail.material == null)
        {
            trail.material = new Material(Shader.Find("Sprites/Default"));
        }
    }
}