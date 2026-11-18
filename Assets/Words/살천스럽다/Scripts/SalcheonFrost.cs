using System.Collections;
using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;

// [살천스럽다] 자모가 날아와 자리 잡은 '뒤에'
//  ① 고드름·얼음 결정·얼음 기둥이 하나씩 자라나고 (처음엔 숨겨 둠 → 글자 연출 중에 공중에 떠 보이지 않음)
//  ② 자모에 서리가 끼듯 색이 차갑게 바뀌고
//  ③ 체험자가 가까이 가면 자모가 푸르게 은은히 빛남 (재질에 Emission이 켜져 있어야 보임)
//  ④ 자모 테두리에 차가운 네온 (가까이 가면 밝아지고, 가끔 고장 난 간판처럼 지직거림)
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

    [Header("네온 테두리")]
    public bool neonOutline = true;
    [ColorUsage(false, true)] public Color neonColor = new Color(0.45f, 0.82f, 1f, 1f);
    [Tooltip("평소 밝기")] public float neonBase = 1.6f;
    [Tooltip("가까이 갔을 때 밝기")] public float neonNear = 4.5f;
    [Tooltip("테두리 두께 (획 대비 배율, 클수록 두꺼움)")] [Range(1.05f, 1.8f)] public float neonWidth = 1.22f;
    [Tooltip("켜지는 데 걸리는 시간 (초) — 글자 도착 후 하나씩 탁탁 켜짐")] public float neonOnSeconds = 2.5f;
    [Tooltip("가끔 지직거림")] public bool neonFlicker = true;
    [Tooltip("지직거리는 빈도 (1이면 자모마다 평균 10초에 한 번쯤)")] [Range(0f, 3f)] public float flickerRate = 1f;

    class Neon
    {
        public Renderer r, letter;
        public float onAt, glow, nextFlicker, flickerUntil;
        public bool on;
    }
    readonly List<Neon> neons = new List<Neon>();
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

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

        if (neonOutline) BuildNeon();

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

    // 길차다와 같은 방식: 획을 조금 두껍게 만든 껍질의 뒷면만 그림 → 가장자리만 빛나는 윤곽
    void BuildNeon()
    {
        if (ws == null) return;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) return;
        var mat = new Material(sh) { name = "네온 테두리 (플레이용)" };
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 1f); // 앞면 숨김
        float th = ws.strokeThickness, dp = ws.strokeDepth;
        var order = new List<JamoPiece>(ws.Pieces);
        // 시작 지점에서 가까운 자모부터 켜짐
        var cam = Camera.main;
        if (cam) order.Sort((a, b) => (a.transform.position - cam.transform.position).sqrMagnitude.CompareTo((b.transform.position - cam.transform.position).sqrMagnitude));
        for (int i = 0; i < order.Count; i++)
        {
            var p = order[i];
            if (!p || !p.visual || p.points == null) continue;
            var go = new GameObject("네온 테두리", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p.visual, false);
            var mesh = StrokeMeshBuilder.Build(p.Strokes, p.pivot, th * neonWidth, dp + th * (neonWidth - 1f));
            mesh.hideFlags = HideFlags.DontSave;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            neons.Add(new Neon
            {
                r = r, letter = p.visual.GetComponent<Renderer>(),
                onAt = Time.time + (order.Count > 1 ? neonOnSeconds * i / (order.Count - 1) : 0f) + Random.Range(0f, 0.3f),
                nextFlicker = Time.time + Random.Range(4f, 14f)
            });
            SetNeon(r, 0f);
        }
    }

    void SetNeon(Renderer r, float k)
    {
        r.GetPropertyBlock(block);
        block.SetColor(ColorId, neonColor * k);
        r.SetPropertyBlock(block);
    }

    void UpdateNeon(Camera cam, float dt)
    {
        float t = Time.time;
        foreach (var n in neons)
        {
            if (!n.r) continue;
            if (!n.on)
            {
                if (t < n.onAt) continue;
                n.on = true;
                n.flickerUntil = t + 0.35f; // 켜질 때 탁, 탁, 지직
            }
            float target = neonBase;
            if (cam && n.letter)
            {
                float d = Vector3.Distance(cam.transform.position, n.letter.bounds.ClosestPoint(cam.transform.position));
                target = Mathf.Lerp(neonNear, neonBase, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(glowNear, glowFar, d)));
            }
            n.glow = Mathf.Lerp(n.glow, target, 1f - Mathf.Exp(-3f * dt));
            float k = n.glow * (1f + 0.04f * Mathf.Sin(t * 50f)); // 형광등 같은 미세한 떨림

            if (neonFlicker && flickerRate > 0f && t > n.nextFlicker)
            {
                n.flickerUntil = t + Random.Range(0.15f, 0.6f);
                n.nextFlicker = t + Random.Range(6f, 14f) / flickerRate;
            }
            if (t < n.flickerUntil)
                k *= Mathf.PerlinNoise(t * 35f, n.onAt) > 0.5f ? 1f : Random.Range(0f, 0.15f);
            SetNeon(n.r, k);
        }
    }

    void Update()
    {
        if (started && neons.Count > 0) UpdateNeon(Camera.main, Time.deltaTime);
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
