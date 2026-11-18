using System.Collections;
using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;

// [살천스럽다] 자모가 날아와 자리 잡은 '뒤에'
//  ① 고드름·얼음 결정·얼음 기둥이 하나씩 자라나고 (처음엔 숨겨 둠 → 글자 연출 중에 공중에 떠 보이지 않음)
//  ② 자모에 서리가 끼듯 색이 차갑게 바뀌고
//  ③ 체험자가 가까이 가면 자모가 푸르게 은은히 빛남 (재질에 Emission이 켜져 있어야 보임)
//  - SalcheonSpace 버튼이 자동으로 '살천 생성물' 그룹에 붙여 줌
public class SalcheonFrost : MonoBehaviour
{
    [Header("자라날 것들 (각 그룹의 자식들이 하나씩 자라남)")]
    public Transform[] growGroups;
    [Tooltip("하나가 자라는 시간 (초)")] public float growSeconds = 1.2f;
    [Tooltip("전부 다 자라기까지 걸리는 시간 (초). 개수가 많으면 촘촘하게")] public float spreadSeconds = 3f;

    [Header("자모 서리")]
    public bool frostJamo = true;
    public Color frostTint = new Color(0.82f, 0.9f, 0.97f, 1f);
    [Range(0f, 1f)] public float frostAmount = 0.35f;
    [Tooltip("서리가 끼는 시간 (초)")] public float frostSeconds = 2.5f;

    [Header("가까이 가면 빛남")]
    public bool proximityGlow = true;
    public Color glowColor = new Color(0.62f, 0.85f, 1f, 1f);
    [Tooltip("이 거리 안이면 가장 밝음 (m)")] public float glowNear = 8f;
    [Tooltip("이 거리 밖이면 꺼짐 (m)")] public float glowFar = 30f;
    public float glowStrength = 1.2f;

    WordSpace ws;
    readonly List<(Transform t, Vector3 scale)> growing = new List<(Transform, Vector3)>();

    class JamoLook
    {
        public Renderer r;
        public Color baseColor, baseEmission;
        public bool hasBase, hasEmission;
        public float frost, glow;
    }
    readonly List<JamoLook> jamos = new List<JamoLook>();
    MaterialPropertyBlock block;
    bool started;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        ws = GetComponentInParent<WordSpace>();
        if (ws == null) ws = FindAnyObjectByType<WordSpace>();
        block = new MaterialPropertyBlock();

        // ① 자라날 것들 숨겨 두기
        if (growGroups != null)
            foreach (var g in growGroups)
            {
                if (!g) continue;
                foreach (Transform c in g)
                {
                    growing.Add((c, c.localScale));
                    c.localScale = Vector3.zero;
                }
            }

        // ②③ 자모 렌더러 모으기
        if (ws != null)
            foreach (var p in ws.Pieces)
            {
                if (!p || !p.visual) continue;
                var r = p.visual.GetComponent<Renderer>();
                if (!r || !r.sharedMaterial) continue;
                var m = r.sharedMaterial;
                var j = new JamoLook { r = r };
                j.hasBase = m.HasProperty(BaseColorId);
                if (j.hasBase) j.baseColor = m.GetColor(BaseColorId);
                j.hasEmission = m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION");
                if (j.hasEmission) j.baseEmission = m.GetColor(EmissionId);
                jamos.Add(j);
            }

        StartCoroutine(WaitForLanding());
    }

    IEnumerator WaitForLanding()
    {
        // 글자 연출이 끝나 '체험' 단계가 되면 시작 (WordSpace가 없으면 바로)
        while (ws != null && ws.CurrentPhase != WordSpace.Phase.체험 && ws.CurrentPhase != WordSpace.Phase.종료)
            yield return null;
        yield return new WaitForSeconds(0.2f);
        started = true;

        int n = growing.Count;
        float step = n > 1 ? spreadSeconds / n : 0f;
        // 가까운 것부터 자라도록 카메라 기준 정렬
        var cam = Camera.main;
        if (cam) growing.Sort((a, b) =>
            (a.t ? (a.t.position - cam.transform.position).sqrMagnitude : 0f)
            .CompareTo(b.t ? (b.t.position - cam.transform.position).sqrMagnitude : 0f));
        for (int i = 0; i < n; i++)
        {
            StartCoroutine(Grow(growing[i].t, growing[i].scale, i * step));
        }
    }

    IEnumerator Grow(Transform t, Vector3 target, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        for (float e = 0; e < growSeconds; e += Time.deltaTime)
        {
            if (!t) yield break;
            float x = Mathf.Clamp01(e / growSeconds);
            float k = 1f + 2.2f * Mathf.Pow(x - 1f, 3) + 1.2f * Mathf.Pow(x - 1f, 2); // 살짝 튀어나왔다 자리 잡기
            t.localScale = target * k;
            yield return null;
        }
        if (t) t.localScale = target;
    }

    void Update()
    {
        if (!started || jamos.Count == 0) return;
        var cam = Camera.main;
        float dt = Time.deltaTime;
        foreach (var j in jamos)
        {
            if (!j.r) continue;
            if (frostJamo) j.frost = Mathf.MoveTowards(j.frost, 1f, dt / Mathf.Max(0.01f, frostSeconds));

            float targetGlow = 0f;
            if (proximityGlow && cam)
            {
                float d = Vector3.Distance(cam.transform.position, j.r.bounds.ClosestPoint(cam.transform.position));
                targetGlow = 1f - Mathf.InverseLerp(glowNear, glowFar, d);
            }
            j.glow = Mathf.Lerp(j.glow, targetGlow, 1f - Mathf.Exp(-2f * dt));

            j.r.GetPropertyBlock(block);
            if (j.hasBase && frostJamo)
            {
                Color c = Color.Lerp(j.baseColor, frostTint, frostAmount * j.frost);
                c.a = j.baseColor.a;
                block.SetColor(BaseColorId, c);
            }
            if (j.hasEmission && proximityGlow)
                block.SetColor(EmissionId, j.baseEmission + glowColor * (glowStrength * j.glow));
            j.r.SetPropertyBlock(block);
        }
    }
}
