using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// [살천스럽다] 찬바람 구역: 체험 카메라가 반경 안에 들어오면 매서운 돌풍
//  - 눈·땅 눈보라·카메라 눈보라가 몇 배로 빨라지고, 안개가 짙어지고, 화면 가장자리가 하얘짐
//  - 콜라이더 없이 카메라와의 가로 거리만 잼. Scene 뷰에 하늘색 원으로 범위가 보임.
//  - SalcheonSpace의 [④ 눈·눈보라·후처리·돌풍] 버튼이 자동으로 만들어 줌 (직접 붙여도 됨)
public class ColdGustZone : MonoBehaviour
{
    [Header("구역")]
    [Tooltip("이 거리(m) 안으로 들어오면 돌풍")] public float radius = 6f;
    [Tooltip("다시 터지기까지 (초)")] public float cooldown = 6f;

    [Header("연결")]
    public ParticleSystem snow;         // 눈
    public ParticleSystem groundDrift;  // 땅 눈보라
    public ParticleSystem blizzard;     // 카메라 주변 눈보라
    public Volume gustVolume;           // 돌풍 후처리 (Weight 0으로 둔 것)
    public AudioSource gustSound;       // 돌풍 소리 (없어도 됨)

    [Header("세기")]
    [Tooltip("바람 몇 배")] public float windMultiplier = 4f;
    [Tooltip("안개를 얼마나 더 짙게")] public float fogBoost = 0.025f;
    [Tooltip("돌풍 시간 (초)")] public float duration = 3f;

    bool inside;
    float lastTime = -99f;
    float baseFog;
    float snowX, snowZ, driftX, driftZ, driftRate, blizX, blizZ, blizRate;

    void Start()
    {
        baseFog = RenderSettings.fogDensity;
        if (snow) { var v = snow.velocityOverLifetime; snowX = v.xMultiplier; snowZ = v.zMultiplier; }
        if (groundDrift)
        {
            var v = groundDrift.velocityOverLifetime; driftX = v.xMultiplier; driftZ = v.zMultiplier;
            driftRate = groundDrift.emission.rateOverTimeMultiplier;
        }
        if (blizzard)
        {
            var v = blizzard.velocityOverLifetime; blizX = v.xMultiplier; blizZ = v.zMultiplier;
            blizRate = blizzard.emission.rateOverTimeMultiplier;
        }
    }

    void Update()
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 d = cam.transform.position - transform.position;
        d.y = 0;
        bool now = d.magnitude < radius;
        if (now && !inside && Time.time - lastTime > cooldown)
        {
            lastTime = Time.time;
            StartCoroutine(Gust());
        }
        inside = now;
    }

    [ContextMenu("지금 돌풍 (테스트)")]
    public void TriggerNow()
    {
        if (Application.isPlaying) { lastTime = Time.time; StartCoroutine(Gust()); }
    }

    IEnumerator Gust()
    {
        if (gustSound) gustSound.Play();
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / Mathf.Max(0.01f, duration - 0.3f); // 확 올랐다 천천히 잠잠
            Apply(Mathf.Clamp01(k));
            yield return null;
        }
        Apply(0);
    }

    void Apply(float k)
    {
        float m = 1f + (windMultiplier - 1f) * k;
        if (snow) { var v = snow.velocityOverLifetime; v.xMultiplier = snowX * m; v.zMultiplier = snowZ * m; }
        if (groundDrift)
        {
            var v = groundDrift.velocityOverLifetime; v.xMultiplier = driftX * m; v.zMultiplier = driftZ * m;
            var e = groundDrift.emission; e.rateOverTimeMultiplier = driftRate * (1f + 2f * k);
        }
        if (blizzard)
        {
            var v = blizzard.velocityOverLifetime; v.xMultiplier = blizX * m; v.zMultiplier = blizZ * m;
            var e = blizzard.emission; e.rateOverTimeMultiplier = blizRate * (1f + 3f * k);
        }
        RenderSettings.fogDensity = baseFog + fogBoost * k;
        if (gustVolume) gustVolume.weight = k;
    }

    void OnDisable()
    {
        if (Application.isPlaying && baseFog > 0) RenderSettings.fogDensity = baseFog;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
