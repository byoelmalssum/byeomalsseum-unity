using UnityEngine;

// [퍼르퍼르] 공간 전체에 부는 바람
//  - '공간' 오브젝트에 하나만 붙인다. 그 아래에 있는 모든 천(Cloth)이 바람에 날린다.
//  - 바람은 Perlin Noise로 세졌다 약해졌다 한다 (진짜 바람처럼 '훅' 불었다 잦아듦).
//  - BreezeZone이 Boost()를 부르면 잠깐 돌풍이 분다.
//  - 자모(글자 조각)에는 아무 영향 없음.
public class PeureuWind : MonoBehaviour
{
    public static PeureuWind Instance { get; private set; }

    [Header("기본 바람")]
    [Tooltip("바람이 부는 방향 (월드 기준)")]
    public Vector3 direction = new Vector3(1f, 0.15f, 0.4f);
    [Tooltip("항상 부는 바람 세기")]
    public float strength = 4f;

    [Header("자연스러운 강약")]
    [Tooltip("세졌다 약해졌다 하는 폭")]
    public float gustStrength = 6f;
    [Tooltip("강약이 바뀌는 빠르기 (작을수록 느긋하게)")]
    public float gustSpeed = 0.25f;
    [Tooltip("천이 제각각 파르르 떨리는 정도 (0~1)")]
    [Range(0f, 1f)] public float flutter = 0.6f;

    /// <summary>지금 부는 바람 세기 (기본 + 강약 + 돌풍). 풍경(리본·바람개비·풀)이 읽어 감</summary>
    public float CurrentStrength { get; private set; }
    /// <summary>바람 방향 (길이 1)</summary>
    public Vector3 Direction => direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;
    /// <summary>바람 세기를 0~1쯤으로 (보통 0.5~1.4, 돌풍 때 2 이상)</summary>
    public float Amount => CurrentStrength / Mathf.Max(0.01f, strength + gustStrength * 0.5f);

    Cloth[] cloths;
    float[] gains;       // 천마다 바람 배수 (WindGain이 붙은 부모가 있으면 그 값)
    float boost;          // 지금 더해지는 돌풍 세기
    float boostEndTime;
    float boostPeak;
    float boostDuration;

    void Awake()
    {
        Instance = this;
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 플레이 중에 천을 새로 만들었으면 호출
    public void Refresh()
    {
        cloths = GetComponentsInChildren<Cloth>(true);
        gains = new float[cloths.Length];
        for (int i = 0; i < cloths.Length; i++)
        {
            var g = cloths[i].GetComponentInParent<WindGain>();
            gains[i] = g != null ? g.gain : 1f;
        }
    }

    // BreezeZone에서 부름: amount만큼 세게, seconds 동안 서서히 잦아듦
    public void Boost(float amount, float seconds)
    {
        boostPeak = Mathf.Max(boost, amount);
        boostDuration = Mathf.Max(0.01f, seconds);
        boostEndTime = Time.time + boostDuration;
    }

    void Update()
    {
        // 돌풍: 처음에 확 세졌다가 서서히 줄어듦
        float left = boostEndTime - Time.time;
        boost = left > 0f ? boostPeak * (left / boostDuration) : 0f;

        float gust = Mathf.PerlinNoise(Time.time * gustSpeed, 0.37f) * gustStrength;
        float s = strength + gust + boost;
        CurrentStrength = s;
        Vector3 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;

        if (cloths == null) return;
        for (int i = 0; i < cloths.Length; i++)
        {
            var c = cloths[i];
            if (c == null) continue;
            float g = (gains != null && i < gains.Length) ? gains[i] : 1f;
            c.externalAcceleration = dir * (s * g);
            c.randomAcceleration = Vector3.one * (s * Mathf.Sqrt(g) * flutter);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f);
        Vector3 d = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;
        Gizmos.DrawLine(transform.position, transform.position + d * 4f);
        Gizmos.DrawWireSphere(transform.position + d * 4f, 0.2f);
    }
}
