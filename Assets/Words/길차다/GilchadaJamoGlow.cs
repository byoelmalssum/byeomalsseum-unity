using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 길차다 씬 전용: 자모가 자리에 내려앉으면
//  ① 글자 색이 숲의 어두운 나무색으로 천천히 바뀌고
//  ② 이끼와 버섯(선반버섯·작은 버섯)이 자라나고
//  ③ 가까이 지나가면 버섯·이끼가 민트빛으로 은은하게 깨어나며 포자가 떠오름
// 쓰는 법: '길차다 숲' 오브젝트에 붙이고 플레이. (빛 번짐은 GilchadaForest의 ④ 고퀄 모드에서 켜짐)
public class GilchadaJamoGlow : MonoBehaviour
{
    [Header("글자 색 (내려앉은 뒤)")]
    public Color letterColor = new Color32(0x26, 0x31, 0x2c, 255);
    [Tooltip("원래 색 → 나무색으로 바뀌는 시간 (초)")] public float darkenSeconds = 2.5f;

    [Header("이끼 · 버섯")]
    public Color mossColor = new Color32(0x3a, 0x5e, 0x3c, 255);
    public Color mushroomColor = new Color32(0xc9, 0xe4, 0xdc, 255);
    [Range(0, 3)] public float mossAmount = 1f;
    [Range(0, 3)] public float mushroomAmount = 1f;
    [Tooltip("자라나는 시간 (초)")] public float growSeconds = 3f;

    [Header("민트빛")]
    public Color glowColor = new Color32(0x7f, 0xf5, 0xd6, 255);
    [Tooltip("이 거리 안이면 가장 밝음 (m)")] public float glowNear = 6f;
    [Tooltip("이 거리 밖이면 꺼짐 (m)")] public float glowFar = 18f;
    [Tooltip("버섯 발광 세기 (높을수록 빛 번짐이 커짐)")] public float mushroomGlow = 3.5f;
    [Tooltip("이끼 발광 세기")] public float mossGlow = .25f;
    [Tooltip("주변을 비추는 빛 세기")] public float lightIntensity = 2.5f;
    [Tooltip("주변을 비추는 빛 범위 (m)")] public float lightRange = 9f;
    [Tooltip("켜지고 꺼지는 빠르기")] public float fadeSpeed = 1.2f;
    [Tooltip("빛나는 포자 떠오르기")] public bool spores = true;

    class Jamo
    {
        public Transform visual;
        public Renderer letter;
        public Material letterMat;
        public Color fromColor, fromEmission;
        public Material mush, moss;
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
    static Material sporeMat;

    void Start()
    {
        ws = FindAnyObjectByType<WordSpace>();
        ec = FindAnyObjectByType<ExperienceCamera>();
        // 빛 번짐(Bloom)이 보이도록 카메라 후처리 켜기
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
            // ① 글자 색 → 어두운 나무색
            SetColor(j.letterMat, Color.Lerp(j.fromColor, letterColor, dark));
            SetEmission(j.letterMat, Color.Lerp(j.fromEmission, Color.black, dark));

            // ② 이끼 · 버섯 자라나기
            if (grow < 1.001f)
                foreach (var g in j.grown) g.t.localScale = g.scale * grow;

            // ③ 가까이 오면 민트빛
            float target = 0;
            if (cam && j.letter)
            {
                float d = Vector3.Distance(cam.position, j.letter.bounds.ClosestPoint(cam.position));
                target = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(glowNear, glowFar, d));
            }
            target *= grow;
            j.v = Mathf.MoveTowards(j.v, target, fadeSpeed * Time.deltaTime);
            float v = j.v * j.v * (3 - 2 * j.v); // 부드럽게
            float pulse = 1 + .12f * Mathf.Sin(Time.time * 1.6f + j.phase) + .05f * Mathf.Sin(Time.time * 3.7f + j.phase * 2);

            SetEmission(j.mush, glowColor * (.03f + v * mushroomGlow * pulse));
            SetColor(j.mush, Color.Lerp(mushroomColor, glowColor, v * .5f));
            SetEmission(j.moss, glowColor * (v * mossGlow * pulse));
            if (j.light) j.light.intensity = v * lightIntensity * pulse;
            if (j.ps) { var em = j.ps.emission; em.rateOverTime = v * 18f; }
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
            if (j.letterMat.HasProperty("_Smoothness")) j.letterMat.SetFloat("_Smoothness", .15f);
            j.letter.sharedMaterial = j.letterMat;

            j.moss = MakeMat(j.letterMat, mossColor, .1f);
            j.mush = MakeMat(j.letterMat, mushroomColor, .55f);

            Decorate(p, j);

            // 은은하게 주변을 비추는 빛
            var lgo = new GameObject("민트빛");
            lgo.transform.SetParent(j.visual, false);
            lgo.transform.position = j.letter.bounds.center;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.color = glowColor; l.range = lightRange; l.intensity = 0; l.shadows = LightShadows.None;
            j.light = l;

            if (spores) j.ps = MakeSpores(j);
            jamos.Add(j);
        }
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
                Vector2 dir = seg / L, perp = new Vector2(-dir.y, dir.x);
                float worldLen = L * s;

                // 이끼: 앞·뒷면과 윗쪽 모서리에 납작하게
                int nMoss = Mathf.RoundToInt(worldLen / 1.1f * mossAmount);
                for (int k = 0; k < nMoss; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, true, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    float sz = Random.Range(.35f, .8f);
                    var m = Prim(PrimitiveType.Sphere, j.visual, j.moss, wp + wn * .02f,
                                 Quaternion.LookRotation(wn) * Quaternion.Euler(0, 0, Random.Range(0, 360f)),
                                 new Vector3(sz, sz * Random.Range(.6f, 1f), sz * .28f), s);
                    j.grown.Add((m, m.localScale));
                }

                // 선반버섯: 옆면에서 납작하게 튀어나옴
                int nShelf = Mathf.RoundToInt(worldLen / 1.4f * mushroomAmount);
                for (int k = 0; k < nShelf; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, false, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    Vector3 flat = Vector3.ProjectOnPlane(wn, Vector3.up);
                    if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
                    int cluster = Random.Range(1, 4);
                    for (int c = 0; c < cluster; c++)
                    {
                        float w = Random.Range(.18f, .4f) * (1 - c * .25f);
                        var sh = Prim(PrimitiveType.Sphere, j.visual, j.mush, wp + wn * w * .3f + Vector3.up * c * w * .45f,
                                      Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(Random.Range(-8, 8f), 0, Random.Range(-8, 8f)),
                                      new Vector3(w, w * .22f, w * .75f), s);
                        j.grown.Add((sh, sh.localScale));
                    }
                }

                // 작은 버섯: 위를 향한 면에 옹기종기
                int nCap = Mathf.RoundToInt(worldLen / 2.2f * mushroomAmount);
                for (int k = 0; k < nCap; k++)
                {
                    Surface(a + seg * Random.value, perp, th, dp, out Vector3 local, out Vector3 n, true, j);
                    Vector3 wp = j.visual.TransformPoint(local), wn = j.visual.TransformDirection(n).normalized;
                    if (wn.y < .4f) continue;
                    int cnt = Random.Range(2, 5);
                    for (int c = 0; c < cnt; c++)
                    {
                        Vector3 off = new Vector3(Random.Range(-.18f, .18f), 0, Random.Range(-.18f, .18f));
                        float h = Random.Range(.08f, .2f), r = Random.Range(.05f, .11f);
                        var tilt = Quaternion.Euler(Random.Range(-15, 15f), Random.Range(0, 360f), Random.Range(-15, 15f));
                        var stem = Prim(PrimitiveType.Cylinder, j.visual, j.mush, wp + off + tilt * Vector3.up * h * .5f, tilt, new Vector3(r * .35f, h * .5f, r * .35f), s);
                        var cap = Prim(PrimitiveType.Sphere, j.visual, j.mush, wp + off + tilt * Vector3.up * h, tilt, new Vector3(r * 2, r * .9f, r * 2), s);
                        j.grown.Add((stem, stem.localScale)); j.grown.Add((cap, cap.localScale));
                    }
                }
            }
        }
        foreach (var g in j.grown) g.t.localScale = Vector3.zero; // 처음엔 안 보이다가 자라남
    }

    // 획 표면 위의 한 점과 바깥 방향
    void Surface(Vector2 c, Vector2 perp, float th, float dp, out Vector3 local, out Vector3 n, bool preferTop, Jamo j)
    {
        float r = Random.value;
        // 월드에서 위를 향하는 모서리 쪽
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
        main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .25f);
        main.startSize = new ParticleSystem.MinMaxCurve(.04f, .1f);
        main.startColor = glowColor * 1.6f;
        main.gravityModifier = -.015f;
        main.maxParticles = 200;
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
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SporeMaterial();
        ps.Play();
        return ps;
    }

    static Material SporeMaterial()
    {
        if (sporeMat) return sporeMat;
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
            float a = Mathf.Clamp01(1 - d); a *= a;
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        sporeMat = m;
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
