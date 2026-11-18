using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 길차다 씬 전용: 자모가 자리에 내려앉으면
//  ① 글자 색이 이끼 낀 나무색으로 바뀌고, 테두리가 은은하게 빛남
//  ② 이끼 · 버섯 · 담쟁이 덩굴이 글자를 타고 자라남
//  ③ 가까이 지나가면 버섯 · 담쟁이 열매 · 이끼 사이 빛이 청록으로 깨어남
// 쓰는 법: '길차다 숲' 오브젝트에 붙이고 플레이.
public class GilchadaJamoGlow : MonoBehaviour
{
    [Header("글자 (내려앉은 뒤)")]
    public Color letterColor = new Color32(0x5e, 0x70, 0x60, 255);
    [Tooltip("원래 색 → 숲 색으로 바뀌는 시간 (초)")] public float darkenSeconds = 2.5f;
    [Tooltip("테두리 빛 색")] public Color outlineColor = new Color32(0x3c, 0xe6, 0xc8, 255);
    [Tooltip("평소 테두리 빛 세기")] public float outlineBase = .12f;
    [Tooltip("가까이 갔을 때 테두리 빛 세기")] public float outlineNear = .45f;
    [Tooltip("테두리 두께 (글자 획 대비 배율)")] public float outlineWidth = 1.18f;

    [Header("이끼 · 버섯 · 담쟁이")]
    public Color mossColor = new Color32(0x3a, 0x5e, 0x3c, 255);
    public Color mushroomColor = new Color32(0x4a, 0x6a, 0x60, 255);
    public Color ivyColor = new Color32(0x3b, 0x66, 0x42, 255);
    [Range(0, 3)] public float mossAmount = 1f;
    [Range(0, 3)] public float mushroomAmount = .5f;
    [Range(0, 3)] public float ivyAmount = 1f;
    [Tooltip("자라나는 시간 (초)")] public float growSeconds = 3.5f;

    [Header("청록빛")]
    public Color glowColor = new Color32(0x2e, 0xd8, 0xc8, 255);
    [Tooltip("이 거리 안이면 가장 밝음 (m)")] public float glowNear = 6f;
    [Tooltip("이 거리 밖이면 꺼짐 (m)")] public float glowFar = 18f;
    [Tooltip("버섯 · 담쟁이 열매 발광 세기")] public float mushroomGlow = 1.3f;
    [Tooltip("이끼 · 담쟁이 잎 발광 세기")] public float mossGlow = .25f;
    [Tooltip("이끼 사이 번지는 빛 세기")] public float poolGlow = .7f;
    [Range(0, 3)] public float poolAmount = .7f;
    [Tooltip("주변을 비추는 빛 세기")] public float lightIntensity = 1.4f;
    [Tooltip("주변을 비추는 빛 범위 (m)")] public float lightRange = 9f;
    [Tooltip("켜지고 꺼지는 빠르기")] public float fadeSpeed = 1.2f;
    [Tooltip("빛나는 포자 떠오르기")] public bool spores = true;

    // 스크립트를 고쳤을 때 예전 값이 남지 않도록 (버전이 바뀌면 기본값으로 한 번 맞춤)
    [SerializeField, HideInInspector] int settingsVersion;
    const int Version = 5;

    void OnValidate()
    {
        if (settingsVersion == Version) return;
        SetDefaults();
        settingsVersion = Version;
    }

    void SetDefaults()
    {
        letterColor = new Color32(0x5e, 0x70, 0x60, 255); darkenSeconds = 2.5f;
        outlineColor = new Color32(0x3c, 0xe6, 0xc8, 255); outlineBase = .12f; outlineNear = .45f; outlineWidth = 1.18f;
        mossColor = new Color32(0x3a, 0x5e, 0x3c, 255); mushroomColor = new Color32(0x4a, 0x6a, 0x60, 255); ivyColor = new Color32(0x3b, 0x66, 0x42, 255);
        mossAmount = 1f; mushroomAmount = .5f; ivyAmount = 1f; growSeconds = 3.5f;
        glowColor = new Color32(0x2e, 0xd8, 0xc8, 255); glowNear = 6f; glowFar = 18f;
        mushroomGlow = 1.3f; mossGlow = .25f; poolGlow = .7f; poolAmount = .7f;
        lightIntensity = 1.4f; lightRange = 9f; fadeSpeed = 1.2f; spores = true;
    }

    class Jamo
    {
        public Transform visual;
        public Renderer letter;
        public Material letterMat, outlineMat, mush, moss, ivyLeaf, poolMat;
        public Color fromColor, fromEmission;
        public readonly List<(Transform t, Vector3 scale)> grown = new List<(Transform, Vector3)>();
        public Light light;
        public ParticleSystem ps;
        public float v, phase;
    }

    readonly List<Jamo> jamos = new List<Jamo>();
    WordSpace ws;
    ExperienceCamera ec;
    bool built;
    float builtTime;
    static Material softMat;
    static Texture2D leafTex;

    void Start()
    {
        ws = FindAnyObjectByType<WordSpace>();
        ec = FindAnyObjectByType<ExperienceCamera>();
        var cams = new List<Camera>(Camera.allCameras);
        if (ec) cams.Add(ec.Cam);
        if (ws && ws.workCamera) cams.Add(ws.workCamera);
        foreach (var cam in cams)
        {
            if (cam == null) continue;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data) data.renderPostProcessing = true;
        }
    }

    void Update()
    {
        if (ws == null) return;
        if (!built)
        {
            if (!Landed()) return;
            Build();
            built = true; builtTime = Time.time;
        }

        float age = Time.time - builtTime;
        float dark = Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, darkenSeconds));
        float grow = Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, growSeconds));
        Transform cam = ec ? ec.transform : (Camera.main ? Camera.main.transform : null);

        foreach (var j in jamos)
        {
            // ① 글자 색
            SetColor(j.letterMat, Color.Lerp(j.fromColor, letterColor, dark));
            SetEmission(j.letterMat, Color.Lerp(j.fromEmission, Color.black, dark));

            // ② 자라나기 (담쟁이 잎은 하나씩 돋아나듯)
            if (grow < 1.001f)
            {
                foreach (var g in j.grown) g.t.localScale = g.scale * grow;
                if (j.ivyLeaf && j.ivyLeaf.HasProperty("_Cutoff")) j.ivyLeaf.SetFloat("_Cutoff", Mathf.Lerp(1f, .45f, grow));
            }

            // ③ 가까이 오면 청록빛
            float target = 0;
            if (cam && j.letter)
            {
                float d = Vector3.Distance(cam.position, j.letter.bounds.ClosestPoint(cam.position));
                target = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(glowNear, glowFar, d));
            }
            target *= grow;
            j.v = Mathf.MoveTowards(j.v, target, fadeSpeed * Time.deltaTime);
            float v = j.v * j.v * (3 - 2 * j.v);
            float pulse = 1 + .12f * Mathf.Sin(Time.time * 1.6f + j.phase) + .05f * Mathf.Sin(Time.time * 3.7f + j.phase * 2);

            SetEmission(j.outlineMat, outlineColor * Mathf.Lerp(outlineBase, outlineNear, v) * pulse * dark);
            SetEmission(j.mush, glowColor * (.03f + v * mushroomGlow * pulse));
            SetColor(j.mush, Color.Lerp(mushroomColor, glowColor, v * .4f));
            SetEmission(j.moss, glowColor * (v * mossGlow * pulse));
            SetEmission(j.ivyLeaf, glowColor * (v * mossGlow * .6f * pulse));
            if (j.light) j.light.intensity = v * lightIntensity * pulse;
            if (j.ps) { var em = j.ps.emission; em.rateOverTime = v * 4f; }
            if (j.poolMat)
            {
                Color pc = glowColor * (poolGlow * v * pulse); pc.a = Mathf.Clamp01(v);
                SetColor(j.poolMat, pc);
            }
        }
    }

    bool Landed()
    {
        if (ec && !ec.MovementEnabled) return false;
        foreach (var p in ws.Pieces)
            if (p.visual && p.visual.parent != p.transform) return false;
        return ws.Pieces.Length > 0;
    }

    // ───────────────────────────── 만들기
    void Build()
    {
        foreach (var p in ws.Pieces)
        {
            if (p.visual == null) continue;
            var j = new Jamo { visual = p.visual, letter = p.visual.GetComponent<Renderer>(), phase = Random.value * 6.28f };
            if (j.letter == null) continue;

            j.letterMat = new Material(j.letter.sharedMaterial);
            j.fromColor = GetColor(j.letterMat);
            j.fromEmission = j.letterMat.HasProperty("_EmissionColor") ? j.letterMat.GetColor("_EmissionColor") : Color.black;
            if (j.letterMat.HasProperty("_Smoothness")) j.letterMat.SetFloat("_Smoothness", .2f);
            j.letter.sharedMaterial = j.letterMat;

            j.moss = MakeMat(j.letterMat, mossColor, .1f);
            j.mush = MakeMat(j.letterMat, mushroomColor, .55f);

            MakeOutline(p, j);
            Decorate(p, j);
            MakeIvy(p, j);
            MakePools(p, j);

            var lgo = new GameObject("청록빛");
            lgo.transform.SetParent(j.visual, false);
            lgo.transform.position = j.letter.bounds.center;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.color = glowColor; l.range = lightRange; l.intensity = 0; l.shadows = LightShadows.None;
            j.light = l;

            if (spores) j.ps = MakeSpores(j);
            jamos.Add(j);
        }
    }

    // 테두리 빛: 획을 조금 두껍게 만든 껍질의 안쪽 면만 그려서 윤곽처럼 보이게
    void MakeOutline(JamoPiece p, Jamo j)
    {
        var go = new GameObject("테두리 빛", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(j.visual, false);
        var mesh = StrokeMeshBuilder.Build(p.Strokes, p.pivot, ws.strokeThickness * outlineWidth, ws.strokeDepth + ws.strokeThickness * (outlineWidth - 1));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        j.outlineMat = MakeMat(j.letterMat, outlineColor * .25f, 0);
        if (j.outlineMat.HasProperty("_Cull")) j.outlineMat.SetFloat("_Cull", 1); // 앞면 숨김 → 윤곽만
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = j.outlineMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Decorate(JamoPiece p, Jamo j)
    {
        float th = ws.strokeThickness, dp = ws.strokeDepth;
        float s = Mathf.Max(.0001f, j.visual.lossyScale.x);
        foreach (var stroke in p.Strokes)
        {
            for (int i = 0; i < stroke.Length - 1; i++)
            {
                Vector2 a = stroke[i] - p.pivot, b = stroke[i + 1] - p.pivot, seg = b - a;
                float L = seg.magnitude; if (L < 1e-4f) continue;
                Vector2 perp = new Vector2(-seg.y, seg.x) / L;
                float worldLen = L * s;

                // 이끼: 납작하게 표면을 덮음
                int nMoss = Mathf.RoundToInt(worldLen / 1.1f * mossAmount);
                for (int k = 0; k < nMoss; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, true, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    float sz = Random.Range(.35f, .8f);
                    var m = Prim(PrimitiveType.Sphere, j.visual, j.moss, wp + wn * .02f,
                                 Quaternion.LookRotation(wn) * Quaternion.Euler(0, 0, Random.Range(0, 360f)),
                                 new Vector3(sz, sz * Random.Range(.6f, 1f), sz * .25f), s);
                    j.grown.Add((m, m.localScale));
                }

                // 선반버섯: 옆면에 작게 겹쳐서
                int nShelf = Mathf.RoundToInt(worldLen / 2.2f * mushroomAmount);
                for (int k = 0; k < nShelf; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, false, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    Vector3 flat = Vector3.ProjectOnPlane(wn, Vector3.up);
                    if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
                    int cluster = Random.Range(1, 4);
                    for (int c = 0; c < cluster; c++)
                    {
                        float w = Random.Range(.1f, .22f) * (1 - c * .25f);
                        var sh = Prim(PrimitiveType.Sphere, j.visual, j.mush, wp + wn * w * .3f + Vector3.up * c * w * .45f,
                                      Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(Random.Range(-8, 8f), 0, Random.Range(-8, 8f)),
                                      new Vector3(w, w * .22f, w * .75f), s);
                        j.grown.Add((sh, sh.localScale));
                    }
                }

                // 작은 버섯 무리: 위를 향한 면에
                int nCap = Mathf.RoundToInt(worldLen / 2.6f * mushroomAmount);
                for (int k = 0; k < nCap; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, true, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    if (wn.y < .4f) continue;
                    int cnt = Random.Range(2, 5);
                    for (int c = 0; c < cnt; c++)
                    {
                        Vector3 off = new Vector3(Random.Range(-.18f, .18f), 0, Random.Range(-.18f, .18f));
                        float h = Random.Range(.08f, .18f), r = Random.Range(.04f, .09f);
                        var tilt = Quaternion.Euler(Random.Range(-15, 15f), Random.Range(0, 360f), Random.Range(-15, 15f));
                        var stem = Prim(PrimitiveType.Cylinder, j.visual, j.mush, wp + off + tilt * Vector3.up * h * .5f, tilt, new Vector3(r * .35f, h * .5f, r * .35f), s);
                        var cap = Prim(PrimitiveType.Sphere, j.visual, j.mush, wp + off + tilt * Vector3.up * h, tilt, new Vector3(r * 2, r * .9f, r * 2), s);
                        j.grown.Add((stem, stem.localScale)); j.grown.Add((cap, cap.localScale));
                    }
                }
            }
        }
        foreach (var g in j.grown) g.t.localScale = Vector3.zero;
    }

    // 담쟁이 덩굴: 획을 나선으로 감고 올라가는 줄기 + 잎 + 빛나는 열매
    void MakeIvy(JamoPiece p, Jamo j)
    {
        float th = ws.strokeThickness, dp = ws.strokeDepth;
        float s = Mathf.Max(.0001f, j.visual.lossyScale.x);
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var vineMat = MakeMat(j.letterMat, new Color(.17f, .27f, .17f), .1f);
        j.ivyLeaf = MakeMat(j.letterMat, ivyColor, .25f);
        var tex = LeafTexture();
        if (j.ivyLeaf.HasProperty("_BaseMap")) j.ivyLeaf.SetTexture("_BaseMap", tex);
        if (j.ivyLeaf.HasProperty("_AlphaClip")) j.ivyLeaf.SetFloat("_AlphaClip", 1);
        if (j.ivyLeaf.HasProperty("_Cutoff")) j.ivyLeaf.SetFloat("_Cutoff", 1f);
        j.ivyLeaf.EnableKeyword("_ALPHATEST_ON");
        if (j.ivyLeaf.HasProperty("_Cull")) j.ivyLeaf.SetFloat("_Cull", 0);
        j.ivyLeaf.renderQueue = 2450;

        foreach (var stroke in p.Strokes)
            for (int i = 0; i < stroke.Length - 1; i++)
            {
                if (Random.value > .65f * ivyAmount) continue;
                Vector2 a = stroke[i] - p.pivot, b = stroke[i + 1] - p.pivot, seg = b - a;
                float L = seg.magnitude; if (L < 1e-4f) continue;
                Vector2 dir = seg / L, perp = new Vector2(-dir.y, dir.x);
                float worldLen = L * s;
                float turns = Mathf.Max(.6f, worldLen / Random.Range(2.2f, 3.4f));
                float ph = Random.value * 6.28f;
                float t0 = Random.Range(0, .25f), t1 = Random.Range(.7f, 1f);
                int steps = Mathf.Max(6, Mathf.RoundToInt(worldLen * (t1 - t0) / .12f));
                float rx = th * .5f * 1.08f, rz = dp * .5f * 1.08f;
                var pts = new List<Vector3>();
                for (int k = 0; k <= steps; k++)
                {
                    float t = Mathf.Lerp(t0, t1, k / (float)steps);
                    float ang = t * turns * Mathf.PI * 2 + ph;
                    Vector2 c = a + seg * t;
                    Vector3 off = new Vector3(perp.x, perp.y, 0) * Mathf.Cos(ang) * rx + Vector3.forward * Mathf.Sin(ang) * rz;
                    Vector3 lp = new Vector3(c.x, c.y, 0) + off;
                    Vector3 wp = j.visual.TransformPoint(lp);
                    pts.Add(wp);
                    Vector3 outW = j.visual.TransformDirection(off).normalized;

                    // 잎: 줄기에서 바깥으로 1~2장
                    if (k % 2 == 0)
                    {
                        int nl = Random.value < .5f ? 1 : 2;
                        for (int q = 0; q < nl; q++)
                        {
                            float size = Random.Range(.16f, .3f);
                            Vector3 nrm = (outW + Random.insideUnitSphere * .5f).normalized;
                            Vector3 up = Vector3.Cross(nrm, Random.onUnitSphere).normalized;
                            Vector3 right = Vector3.Cross(up, nrm);
                            Vector3 center = wp + outW * .04f + up * size * .45f;
                            AddQuad(verts, norms, uvs, tris, j.visual.InverseTransformPoint(center),
                                    j.visual.InverseTransformVector(right * size * .5f), j.visual.InverseTransformVector(up * size * .5f),
                                    j.visual.InverseTransformDirection(nrm));
                        }
                    }
                    // 빛나는 열매: 가끔 2~3알
                    if (k % 7 == 3 && Random.value < .7f)
                    {
                        int nb = Random.Range(2, 4);
                        for (int q = 0; q < nb; q++)
                        {
                            float br = Random.Range(.03f, .05f);
                            var berry = Prim(PrimitiveType.Sphere, j.visual, j.mush, wp + outW * .07f + Random.insideUnitSphere * .06f, Quaternion.identity, Vector3.one * br * 2, s);
                            j.grown.Add((berry, berry.localScale));
                            berry.localScale = Vector3.zero;
                        }
                    }
                }
                // 줄기
                var lgo = new GameObject("담쟁이 줄기");
                lgo.transform.SetParent(j.visual, false);
                var lr = lgo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = pts.Count; lr.SetPositions(pts.ToArray());
                lr.widthMultiplier = .045f;
                lr.widthCurve = new AnimationCurve(new Keyframe(0, .6f), new Keyframe(.5f, 1), new Keyframe(1, .4f));
                lr.sharedMaterial = vineMat;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

        if (verts.Count == 0) return;
        var go = new GameObject("담쟁이 잎", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(j.visual, false);
        var mesh = new Mesh { name = "담쟁이 잎" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = j.ivyLeaf;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void AddQuad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 ax, Vector3 ay, Vector3 nrm)
    {
        int b = v.Count;
        v.Add(c - ax - ay); v.Add(c + ax - ay); v.Add(c + ax + ay); v.Add(c - ax + ay);
        for (int k = 0; k < 4; k++) n.Add(nrm);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
        t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
    }

    // 담쟁이 잎 한 장 (하트에 가까운 잎 모양)
    static Texture2D LeafTexture()
    {
        if (leafTex) return leafTex;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S;         // u: -1..1, v: 0(잎자루)..1(끝)
                float w = Mathf.Sin(Mathf.PI * Mathf.Pow(v, .8f)) * (1 - .25f * v); // 잎 폭
                float lobe = .12f * Mathf.Cos(v * Mathf.PI * 3);              // 살짝 갈라진 가장자리
                bool inside = Mathf.Abs(u) < w + lobe && v > .04f;
                float vein = Mathf.Abs(u) < .05f ? .8f : 1f;
                float shade = (.75f + .25f * (1 - Mathf.Abs(u))) * vein;
                tex.SetPixel(x, y, inside ? new Color(shade, shade, shade, 1) : new Color(0, 0, 0, 0));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        leafTex = tex;
        return tex;
    }

    // 획 표면 위의 한 점과 바깥 방향
    void Surface(Vector2 c, Vector2 perp, float th, float dp, out Vector3 local, out Vector3 n, bool preferTop, Jamo j)
    {
        float r = Random.value;
        Vector3 perpW = j.visual.TransformDirection(new Vector3(perp.x, perp.y, 0));
        float side = perpW.y >= 0 ? 1 : -1;
        if (preferTop ? r < .5f : r < .3f)
        {
            Vector2 q = c + perp * side * th * .5f;
            local = new Vector3(q.x, q.y, Random.Range(-.4f, .4f) * dp);
            n = new Vector3(perp.x * side, perp.y * side, 0);
        }
        else
        {
            float face = Random.value < .5f ? -1 : 1;
            Vector2 q = c + perp * Random.Range(-.4f, .4f) * th;
            local = new Vector3(q.x, q.y, face * dp * .5f);
            n = new Vector3(0, 0, face);
        }
    }

    Transform Prim(PrimitiveType type, Transform parent, Material mat, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, float parentScale)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(worldPos, worldRot);
        go.transform.localScale = worldScale / parentScale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // 이끼 사이로 번지는 작은 청록 빛 (레퍼런스: 이끼 틈의 푸른 빛 고임)
    void MakePools(JamoPiece p, Jamo j)
    {
        var go = new GameObject("이끼 사이 청록빛");
        go.transform.SetParent(j.visual, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 100000f; main.startSpeed = 0; main.maxParticles = 300;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        j.poolMat = new Material(SoftMaterial());
        r.sharedMaterial = j.poolMat;

        float th = ws.strokeThickness, dp = ws.strokeDepth;
        float s = Mathf.Max(.0001f, j.visual.lossyScale.x);
        ps.Play();
        foreach (var stroke in p.Strokes)
            for (int i = 0; i < stroke.Length - 1; i++)
            {
                Vector2 a = stroke[i] - p.pivot, b = stroke[i + 1] - p.pivot, seg = b - a;
                float L = seg.magnitude; if (L < 1e-4f) continue;
                Vector2 perp = new Vector2(-seg.y, seg.x) / L;
                int n = Mathf.RoundToInt(L * s / 1.3f * poolAmount);
                for (int k = 0; k < n; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 nrm, true, j);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = j.visual.TransformPoint(local) + j.visual.TransformDirection(nrm).normalized * .08f,
                        startSize = Random.Range(.25f, .65f),
                        startColor = new Color(1, 1, 1, Random.Range(.3f, .7f)),
                        startLifetime = 100000f,
                        velocity = Vector3.zero,
                        applyShapeToPosition = false,
                    };
                    ps.Emit(ep, 1);
                }
            }
    }

    ParticleSystem MakeSpores(Jamo j)
    {
        var go = new GameObject("빛나는 포자");
        go.transform.SetParent(j.visual, false);
        go.transform.position = j.letter.bounds.center;
        go.transform.rotation = Quaternion.identity;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3, 5.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .05f);
        main.startColor = glowColor;
        main.gravityModifier = -.015f;
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 0;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
        Vector3 size = j.letter.bounds.size; size.y = Mathf.Max(size.y, 1);
        sh.scale = size * 1.1f;
        var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .4f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                     new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial();
        ps.Play();
        return ps;
    }

    static Material SoftMaterial()
    {
        if (softMat) return softMat;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f;
            float a = Mathf.Clamp01(1 - d); a *= a * a;
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        softMat = m;
        return m;
    }

    static Material MakeMat(Material src, Color c, float smooth)
    {
        var m = new Material(src);
        SetColor(m, c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Color.black);
        }
        return m;
    }

    static Color GetColor(Material m) =>
        m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

    static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void SetEmission(Material m, Color c)
    {
        if (m && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
    }
}
