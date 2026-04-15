using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TacticalMapUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas parentCanvas;
    public GameObject threatMarkerPrefab;     // 赤いアイコンなどのUIプレハブ
    public GameObject interceptorMarkerPrefab;// 青いアイコンなどのUIプレハブ

    private Camera mainCamera;
    
    // 追跡中のマーカー辞書
    private Dictionary<Transform, RectTransform> markers = new Dictionary<Transform, RectTransform>();

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        // 存在する全てのミサイルを検索（※本来はマネージャーで一括管理する方が軽量です）
        BallisticMissile[] threats = FindObjectsOfType<BallisticMissile>();
        InterceptorMissile[] interceptors = FindObjectsOfType<InterceptorMissile>();

        UpdateMarkers(threats, threatMarkerPrefab);
        UpdateMarkers(interceptors, interceptorMarkerPrefab);
    }

    private void UpdateMarkers<T>(T[] missiles, GameObject prefab) where T : MonoBehaviour
    {
        foreach (var missile in missiles)
        {
            Transform targetTransform = missile.transform;

            // マーカーが未生成なら作成
            if (!markers.ContainsKey(targetTransform))
            {
                GameObject markerObj = Instantiate(prefab, parentCanvas.transform);
                markers[targetTransform] = markerObj.GetComponent<RectTransform>();
            }

            // 3D座標から2Dスクリーン座標への変換
            Vector3 screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);

            // 画面の裏側にいる場合は非表示にする
            if (screenPos.z < 0)
            {
                markers[targetTransform].gameObject.SetActive(false);
            }
            else
            {
                markers[targetTransform].gameObject.SetActive(true);
                markers[targetTransform].position = screenPos;
            }
        }

        // 破壊されてシーンから消えたミサイルのマーカーを削除
        List<Transform> keysToRemove = new List<Transform>();
        foreach (var key in markers.Keys)
        {
            if (key == null)
            {
                Destroy(markers[key].gameObject);
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove) { markers.Remove(key); }
    }
}