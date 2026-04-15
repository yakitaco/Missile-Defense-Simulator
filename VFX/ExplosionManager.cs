using System.Collections.Generic;
using UnityEngine;

public class ExplosionManager : MonoBehaviour
{
    public static ExplosionManager Instance { get; private set; }

    [Header("VFX Prefabs")]
    public GameObject explosionPrefab; // パーティクルシステムとAudioSourceを含んだプレハブ
    public int poolSize = 10;

    // オブジェクトプール（Queueを使って古いものから使い回す）
    private Queue<GameObject> explosionPool = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }

        // プールの初期化
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(explosionPrefab, transform);
            obj.SetActive(false);
            explosionPool.Enqueue(obj);
        }
    }

    /// <summary>
    /// 指定された位置で爆発エフェクトを再生する
    /// </summary>
    public void SpawnExplosion(Vector3 position, float scale = 1f)
    {
        if (explosionPool.Count == 0) return;

        // プールから1つ取り出す
        GameObject explosion = explosionPool.Dequeue();
        
        // 位置とスケールを設定して有効化
        explosion.transform.position = position;
        explosion.transform.localScale = Vector3.one * scale;
        explosion.SetActive(true);

        // パーティクルと音を再生
        ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();
        
        AudioSource audio = explosion.GetComponent<AudioSource>();
        if (audio != null) audio.Play();

        // 使い終わったら再びプールの最後尾に戻す（非アクティブ化はパーティクルの寿命後に自動で行う想定）
        explosionPool.Enqueue(explosion);
        
        // 再生終了後に非アクティブにするためのコルーチンなどを仕込むとなお良しです
        Invoke(nameof(DeactivateOldest), 3f); 
    }

    private void DeactivateOldest()
    {
        // 簡易的な非アクティブ化処理（キューの先頭＝一番古いものをオフにする）
        GameObject oldest = explosionPool.Peek();
        if (oldest != null && !oldest.GetComponent<ParticleSystem>().isPlaying)
        {
            oldest.SetActive(false);
        }
    }
}