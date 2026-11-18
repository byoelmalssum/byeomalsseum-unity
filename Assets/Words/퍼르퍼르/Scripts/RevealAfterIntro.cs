using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// [퍼르퍼르] 글자가 날아가 자리 잡은 '뒤에' 천·파티클이 서서히 나타나게 하는 연출
//  - '공간' 아래 빈 오브젝트(예: 등장_연출)에 붙이고, Targets에 천줄/깃발/파티클 오브젝트들을 넣는다.
//  - 시작하면 Targets 안의 천은 투명하게 숨고, 파티클은 멈춘다.
//  - 글자 연출이 끝나면(자동 계산) 천이 줄 순서대로 차르르 나타나면서 돌풍이 한 번 분다.
//  - 글자 연출 시간은 [별말씀] 오브젝트의 Glyph Appear / Hold / Fly Seconds, Skip Intro 값을 읽어서 자동으로 맞춘다.
//    못 읽으면 Manual Delay 값을 쓴다.
public class RevealAfterIntro : MonoBehaviour
{
    [Tooltip("나중에 나타날 것들: 천줄_..., 깃발_..., 꽃잎_하늘, 먼지 등")]
    public GameObject[] targets;

    [Header("타이밍")]
    [Tooltip("글자 연출 시간을 [별말씀] 설정에서 자동으로 읽기")]
    public bool autoDelay = true;
    [Tooltip("자동으로 못 읽을 때 쓰는 대기 시간 (초). 기본 0.8 + 4 + 3")]
    public float manualDelay = 7.8f;
    [Tooltip("글자가 자리 잡고 나서 조금 더 기다리는 시간 (초)")]
    public float extraDelay = 0.3f;

    [Header("나타나는 모양")]
    [Tooltip("천 한 장이 투명 → 원래 색이 되는 시간 (초)")]
    public float fadeSeconds = 1.5f;
    [Tooltip("천과 천 사이 시차 (초). 줄을 따라 차르르 나타남")]
    public float stagger = 0.04f;
    [Tooltip("나타날 때 한 번 부는 돌풍 세기 (PeureuWind 필요). 0이면 없음")]
    public float revealGust = 12f;
    public float revealGustSeconds = 3f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    class Item
    {
        public Renderer r;
        public bool hasBase, hasEmission;
        public Color baseColor, emission;
    }

    readonly List<Item> items = new List<Item>();
    readonly List<ParticleSystem> particles = new List<ParticleSystem>();
    MaterialPropertyBlock block;

    void Start()
    {
        block = new MaterialPropertyBlock();
        if (targets == null) return;

        foreach (var t in targets)
        {
            if (t == null) continue;

            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Add(ps);
            }

            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var it = new Item { r = r };
                var m = r.sharedMaterial;
                if (m != null)
                {
                    it.hasBase = m.HasProperty(BaseColorId);
                    it.hasEmission = m.HasProperty(EmissionId);
                    if (it.hasBase) it.baseColor = m.GetColor(BaseColorId);
                    if (it.hasEmission) it.emission = m.GetColor(EmissionId);
                }
                Apply(it, 0f);
                r.enabled = false;
                items.Add(it);
            }
        }

        float delay = (autoDelay ? ReadIntroSeconds() : manualDelay) + extraDelay;
        StartCoroutine(Reveal(delay));
    }

    IEnumerator Reveal(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (revealGust > 0f && PeureuWind.Instance != null)
            PeureuWind.Instance.Boost(revealGust, revealGustSeconds);

        foreach (var ps in particles) if (ps != null) ps.Play(true);

        for (int i = 0; i < items.Count; i++)
            StartCoroutine(FadeIn(items[i], i * stagger));
    }

    IEnumerator FadeIn(Item it, float wait)
    {
        if (wait > 0f) yield return new WaitForSeconds(wait);
        if (it.r == null) yield break;

        it.r.enabled = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Apply(it, e);
            yield return null;
        }
        // 다 나타나면 원래 재질 그대로 (성능을 위해 덮어쓴 값 제거)
        it.r.SetPropertyBlock(null);
    }

    void Apply(Item it, float k)
    {
        if (it.r == null) return;
        it.r.GetPropertyBlock(block);
        if (it.hasBase)
        {
            Color c = it.baseColor; c.a *= k;
            block.SetColor(BaseColorId, c);
        }
        if (it.hasEmission) block.SetColor(EmissionId, it.emission * k);
        it.r.SetPropertyBlock(block);
    }

    // [별말씀] 글자 연출 시간 = Glyph Appear + Glyph Hold + Fly (Skip Intro면 0)
    float ReadIntroSeconds()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "WordSpace") continue;

                if (ReadBool(mb, "skipintro")) return 0f;
                float a = ReadFloat(mb, "glyphappearseconds");
                float h = ReadFloat(mb, "glyphholdseconds");
                float f = ReadFloat(mb, "flyseconds");
                if (a < 0f || h < 0f || f < 0f) break;
                return a + h + f;
            }
        }
        Debug.Log("[RevealAfterIntro] 글자 연출 시간을 못 읽어서 Manual Delay(" + manualDelay + "초)를 씀");
        return manualDelay;
    }

    static FieldInfo FindField(object o, string key)
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var fi in o.GetType().GetFields(F))
            if (fi.Name.Replace("_", "").ToLowerInvariant() == key) return fi;
        return null;
    }

    static float ReadFloat(object o, string key)
    {
        var fi = FindField(o, key);
        if (fi == null || fi.FieldType != typeof(float)) return -1f;
        return (float)fi.GetValue(o);
    }

    static bool ReadBool(object o, string key)
    {
        var fi = FindField(o, key);
        return fi != null && fi.FieldType == typeof(bool) && (bool)fi.GetValue(o);
    }
}
