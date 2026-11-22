// =====================================================================
//  NonteulHillsAndBlock.cs — 마을 가까이 둘러싼 언덕 + 모든 물체 통과 막기 + Shift 더 빠르게 (URP)
//
//  다른 논틀밭틀 스크립트들과 함께 「공간」에 붙여요.
//   1) [가까운 언덕 만들기] 멀리 있던 언덕을 숨기고, 마을(글자·미로·초가집) 바로 바깥에서
//      20~38m 높이로 솟는 둥근 언덕과 숲을 만들어요. 꼬불길이 지나는 곳은 골짜기로 갈라져요.
//   2) 플레이하면 체험 카메라가 울타리·집뿐 아니라 자모, 논, 단, 차나무, 나무, 언덕, 땅…
//      눈에 보이는 거의 모든 물체를 뚫지 못해요. (풀잎·꽃·빛가루는 그냥 지나가요)
//   3) Shift를 누르면 훨씬 빨라져요 (기본 4배).
//   4) 시작할 때 나타나는 '논틀밭틀' 글자가 단·언덕·벽에 가리지 않게 항상 맨 앞에 보여요.
//      글자가 날아가 자기 자리에 내려앉으면 그때부터는 원래처럼 보여요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(10001)] // 체험 카메라·미로 검사 다음에 충돌 검사
public class NonteulHillsAndBlock : MonoBehaviour
{
    [Header("가까운 언덕")]
    [Tooltip("언덕이 시작하는 거리 (마을 가운데에서, m)")] [Range(30f, 60f)] public float hillStart = 37f;
    [Tooltip("언덕 높이 (m)")] [Range(8f, 60f)] public float hillHeight = 28f;
    [Tooltip("언덕 위 나무 수")] [Range(0, 300)] public int treeCount = 140;

    [Header("플레이 중")]
    [Tooltip("물체를 뚫고 지나가지 못하게")] public bool blockEverything = true;
    [Tooltip("카메라 몸 크기 (m)")] [Range(0.1f, 0.6f)] public float cameraRadius = 0.3f;
    [Tooltip("Shift를 누르면 몇 배 빨라질지")] [Range(1f, 10f)] public float shiftMultiplier = 4f;
    [Tooltip("시작 글자가 벽·언덕에 가리지 않고 맨 앞에 보이게")] public bool wordAlwaysOnTop = true;

    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/언덕";
    const string HILL_ROOT = "논틀밭틀_가까운 언덕 (자동 생성)";
    const string FAR_ROOT = "논틀밭틀_먼 풍경 (자동 생성)";
    static readonly string[] FAR_HIDE = { "언덕", "나뭇잎", "나뭇잎2", "나무줄기" };
    const float CX = -4f, CZ = 3f, SEXP = 3f, OUTER = 115f;

    System.Random rng;
    Material mGrass, mLeaf, mLeaf2, mBark;
    readonly List<Vector2> gPts = new List<Vector2>(); readonly List<float> gHalf = new List<float>();

    // ==================================================================
    //  버튼: 가까운 언덕
    // ==================================================================
    public void BuildHills()
    {
#if UNITY_EDITOR
        Material L(string n) => AssetDatabase.LoadAssetAtPath<Material>(SUB + "/" + n + ".mat");
        mGrass = L("고퀄_풀_1.00"); mLeaf = L("고퀄_나뭇잎"); mLeaf2 = L("고퀄_나뭇잎2"); mBark = L("고퀄_줄기");
#endif
        if (mGrass == null || mLeaf == null || mBark == null) { Debug.LogError("[논틀밭틀 언덕] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요."); return; }
        if (mLeaf2 == null) mLeaf2 = mLeaf;
        rng = new System.Random(55);
        RemoveHills();
        SetFarHills(false);
        MakeGorges();

        var root = new GameObject(HILL_ROOT).transform; // 마을 이동 범위 계산에 안 섞이게 씬 맨 위에 둬요
        root.SetPositionAndRotation(transform.position, transform.rotation); root.localScale = transform.lossyScale;
        if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, gameObject.scene);

        Progress("언덕 만드는 중…", 0.2f);
        const int NA = 420, NR = 72;
        var mb = new MB();
        for (int ri = 0; ri <= NR; ri++)
            for (int ai = 0; ai <= NA; ai++)
            {
                float s = OUTER * Mathf.Pow(ri / (float)NR, 1.6f), ang = ai / (float)NA * Mathf.PI * 2f;
                var p = PointAt(ang, s);
                mb.v.Add(new Vector3(p.x, HeightAt(p.x, p.y) + 0.01f, p.y)); mb.uv.Add(new Vector2(p.x, p.y));
            }
        for (int ri = 0; ri < NR; ri++)
            for (int ai = 0; ai < NA; ai++)
            {
                int a = ri * (NA + 1) + ai, b = a + 1, c = a + NA + 1, d = c + 1;
                mb.Tri(a, b, c); mb.Tri(b, d, c);
            }
        Emit(root, "가까운 언덕", mb, new[] { mGrass }, ShadowCastingMode.On, true);

        Progress("언덕 숲 만드는 중…", 0.6f);
        var leaves = new MB(); var leaves2 = new MB(); var bark = new MB();
        int placed = 0;
        for (int tries = 0; tries < treeCount * 30 && placed < treeCount; tries++)
        {
            float ang = R(0, Mathf.PI * 2f), s = R(3f, 70f);
            var p = PointAt(ang, s);
            if (Gorge(p.x, p.y) < 7f) continue;
            if (Fbm(p.x * 0.03f + 8f, p.y * 0.03f) < 0.47f && rng.NextDouble() < 0.8) continue; // 숲이 군데군데 모이게
            Tree(rng.NextDouble() < 0.5 ? leaves : leaves2, bark, new Vector3(p.x, HeightAt(p.x, p.y) - 0.2f, p.y), R(7f, 12f));
            placed++;
        }
        Emit(root, "나뭇잎", leaves, new[] { mLeaf }, ShadowCastingMode.On, false);
        Emit(root, "나뭇잎2", leaves2, new[] { mLeaf2 }, ShadowCastingMode.On, false);
        Emit(root, "나무줄기", bark, new[] { mBark }, ShadowCastingMode.On, false);

        SaveAll(); ClearProgress();
        MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 언덕] 마을 가까이 언덕을 만들었어요. Ctrl+S로 저장하세요.");
    }

    public void RemoveHills()
    {
        var old = GameObject.Find(HILL_ROOT); if (old) DestroyNow(old);
        SetFarHills(true);
        MarkDirty(gameObject);
    }

    void SetFarHills(bool on)
    {
        var far = GameObject.Find(FAR_ROOT); if (far == null) return;
        foreach (Transform c in far.transform) if (System.Array.IndexOf(FAR_HIDE, c.name) >= 0) { c.gameObject.SetActive(on); MarkDirty(c.gameObject); }
    }

    // 꼬불길 두 개 (NonteulWindingPath와 같은 길) — 언덕을 골짜기로 갈라요
    void MakeGorges()
    {
        gPts.Clear(); gHalf.Clear();
        var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
        var b = new[] { new Vector3(7.4f, 0, 17), new Vector3(12, 0, 22), new Vector3(8, 0, 29), new Vector3(16, 0, 36), new Vector3(10, 0, 44), new Vector3(20, 0, 51), new Vector3(13, 0, 59), new Vector3(22, 0, 69) };
        foreach (var (pts, half) in new[] { (a, 3.0f), (b, 2.2f) })
        {
            var list = new List<Vector3>(); Spline(pts, 1f, list);
            // 끝을 언덕 밖까지 쭉 늘려요
            Vector3 s0 = pts[0], s1 = pts[1], e0 = pts[pts.Length - 1], e1 = pts[pts.Length - 2];
            if (half > 2.5f) for (int k = 1; k <= 90; k++) list.Insert(0, s0 + (s0 - s1).normalized * k);
            else for (int k = 1; k <= 90; k++) list.Add(e0 + (e0 - e1).normalized * k);
            foreach (var p in list) { gPts.Add(new Vector2(p.x, p.z)); gHalf.Add(half); }
        }
    }
    float Gorge(float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0; i < gPts.Count; i++) { float dx = x - gPts[i].x, dz = z - gPts[i].y, d = Mathf.Sqrt(dx * dx + dz * dz) - gHalf[i]; if (d < best) best = d; }
        return best;
    }

    // 마을 가장자리(언덕 시작선)에서 바깥으로 s m 간 곳
    Vector2 PointAt(float ang, float s)
    {
        float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
        float rb = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(c), SEXP) + Mathf.Pow(Mathf.Abs(sn), SEXP), 1f / SEXP) * hillStart;
        rb += 6f * (Fbm(c * 1.3f + 4f, sn * 1.3f + 9f) - 0.5f);
        return new Vector2(CX + c * (rb + s), CZ + sn * (rb + s));
    }
    float SOf(float x, float z)
    {
        float dx = x - CX, dz = z - CZ, r = Mathf.Sqrt(dx * dx + dz * dz); if (r < 1e-3f) return -hillStart;
        float ang = Mathf.Atan2(dz, dx); var b = PointAt(ang, 0f);
        return r - Vector2.Distance(b, new Vector2(CX, CZ));
    }
    float HeightAt(float x, float z)
    {
        float s = SOf(x, z); if (s <= 0f) return 0f;
        float ang = Mathf.Atan2(z - CZ, x - CX);
        float peak = hillHeight * (0.7f + 0.6f * Fbm(Mathf.Cos(ang) * 2f + 1f, Mathf.Sin(ang) * 2f + 3f));
        float h = peak * Smooth01(s / 26f) + 8f * (Fbm(x * 0.025f, z * 0.025f) - 0.5f) * Smooth01(s / 20f) + 0.06f * s * Smooth01((s - 30f) / 30f) * hillHeight / 28f;
        float g = Gorge(x, z);
        return Mathf.Max(0f, h) * Smooth01(g / 10f);
    }

    static void Spline(Vector3[] c, float step, List<Vector3> outPts)
    {
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector3 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Length - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                outPts.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        outPts.Add(c[c.Length - 1]);
    }

    void Tree(MB leaves, MB bark, Vector3 basePos, float height)
    {
        float trunkH = height * R(0.4f, 0.55f), rad = height * 0.035f; const int S = 8; int k0 = bark.v.Count;
        for (int i = 0; i <= S; i++)
        {
            float a = i / (float)S * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.v.Add(basePos + d * rad * 1.3f); bark.uv.Add(new Vector2(i / (float)S, 0));
            bark.v.Add(basePos + d * rad * 0.6f + Vector3.up * trunkH); bark.uv.Add(new Vector2(i / (float)S, 1));
        }
        for (int i = 0; i < S; i++) { int a = k0 + i * 2; bark.Tri(a, a + 1, a + 3); bark.Tri(a, a + 3, a + 2); }
        int blobs = 4 + rng.Next(4); float crown = height * 0.32f;
        for (int b = 0; b < blobs; b++) Blob(leaves, basePos + Vector3.up * (trunkH + crown * R(0.1f, 0.9f)) + new Vector3(R(-1, 1), 0, R(-1, 1)) * crown * 0.6f, crown * R(0.55f, 0.85f));
    }
    void Blob(MB m, Vector3 c, float r)
    {
        const int LA = 7, LO = 10; int k0 = m.v.Count; float seed = R(0, 100);
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI, ph = j / (float)LO * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th) * 0.85f, Mathf.Sin(th) * Mathf.Sin(ph));
                m.v.Add(c + d * r * (0.75f + 0.45f * Fbm(d.x * 2.2f + seed, d.z * 2.2f + d.y * 1.7f + seed))); m.uv.Add(new Vector2(j / (float)LO, i / (float)LA));
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++) { int a = k0 + i * (LO + 1) + j, b = a + LO + 1; m.Tri(a, a + 1, b); m.Tri(a + 1, b + 1, b); }
    }

    // ==================================================================
    //  플레이 중: Shift 속도 + 모든 물체 통과 막기
    // ==================================================================
    Byeolmalsseum.WordSpace ws; Byeolmalsseum.ExperienceCamera cam;
    Vector3 prev; bool hasPrev, jamoDone;

    void Start()
    {
        ws = FindFirstObjectByType<Byeolmalsseum.WordSpace>();
        cam = ws != null ? ws.experienceCamera : FindFirstObjectByType<Byeolmalsseum.ExperienceCamera>();
        if (cam != null) cam.fastMultiplier = shiftMultiplier;
        if (wordAlwaysOnTop) StartWordOverlay();
        if (!blockEverything) return;
        AddColliders(transform);
        var far = GameObject.Find(FAR_ROOT); if (far) AddColliders(far.transform);
        var hills = GameObject.Find(HILL_ROOT); if (hills) AddColliders(hills.transform);
    }

    // 눈에 보이는 메시마다 충돌 모양을 붙여요 (풀잎·꽃·빛가루·실행 중 연출은 빼고)
    static void AddColliders(Transform root)
    {
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh; if (mesh == null || mesh.vertexCount < 3) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            if (Skip(mf)) continue;
            try { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; }
            catch (System.Exception e) { Debug.LogWarning("[논틀밭틀 충돌] " + mf.name + " 충돌 모양을 못 붙였어요: " + e.Message); }
        }
    }
    static bool Skip(MeshFilter mf)
    {
        for (var t = mf.transform; t != null; t = t.parent) if (t.name.StartsWith("[실행 중]")) return true;
        var r = mf.GetComponent<Renderer>(); if (r == null) return true;
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            string n = m.name;
            if (n.StartsWith("잎_") || n.StartsWith("들꽃") || n.Contains("빛")) return true;
            if (m.shader != null && m.shader.name.Contains("Unlit")) return true;
        }
        return false;
    }

    // ---------------- 시작 글자 맨 앞에 보이기 ----------------
    Camera overlayCam, baseCam; int overlayLayer = -1;
    readonly List<Byeolmalsseum.JamoPiece> waiting = new List<Byeolmalsseum.JamoPiece>();
    readonly Dictionary<GameObject, int> origLayer = new Dictionary<GameObject, int>();
    readonly HashSet<Byeolmalsseum.JamoPiece> leftHome = new HashSet<Byeolmalsseum.JamoPiece>();

    void StartWordOverlay()
    {
        if (cam == null || ws == null || ws.Pieces == null) return;
        baseCam = cam.GetComponent<Camera>(); if (baseCam == null) return;
        var baseData = baseCam.GetUniversalAdditionalCameraData(); if (baseData == null || baseData.renderType != CameraRenderType.Base) return;
        // 씬에서 아무도 안 쓰는 레이어 하나를 빌려요
        var used = new HashSet<int>(); foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) used.Add(r.gameObject.layer);
        for (int l = 31; l >= 8; l--) if (!used.Contains(l) && string.IsNullOrEmpty(LayerMask.LayerToName(l))) { overlayLayer = l; break; }
        if (overlayLayer < 0) return;

        var go = new GameObject("[실행 중] 글자 맨 앞 카메라");
        go.transform.SetParent(baseCam.transform, false);
        overlayCam = go.AddComponent<Camera>();
        overlayCam.CopyFrom(baseCam);
        overlayCam.cullingMask = 1 << overlayLayer;
        var od = overlayCam.GetUniversalAdditionalCameraData(); od.renderType = CameraRenderType.Overlay;
        od.renderShadows = baseData.renderShadows;                 // 그림자도 원래 화면처럼
        od.renderPostProcessing = baseData.renderPostProcessing;   // 색 보정(톤)도 원래 화면처럼 → 글자 색이 연해지지 않게
        baseData.cameraStack.Add(overlayCam);
        baseCam.cullingMask &= ~(1 << overlayLayer);

        foreach (var p in ws.Pieces)
        {
            if (p == null || p.visual == null) continue;
            waiting.Add(p);
            foreach (var t in p.visual.GetComponentsInChildren<Transform>(true)) { origLayer[t.gameObject] = t.gameObject.layer; t.gameObject.layer = overlayLayer; }
        }
    }

    void UpdateWordOverlay()
    {
        if (overlayCam == null) return;
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            var p = waiting[i];
            if (p == null || p.visual == null) { waiting.RemoveAt(i); continue; }
            bool home = p.visual.parent == p.transform;
            if (!home) leftHome.Add(p);
            if (home && (leftHome.Contains(p) || Time.timeSinceLevelLoad > 4f))
            {
                foreach (var t in p.visual.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = origLayer.TryGetValue(t.gameObject, out var l) ? l : 0;
                waiting.RemoveAt(i);
            }
        }
        if (waiting.Count == 0)
        {
            var baseData = baseCam != null ? baseCam.GetUniversalAdditionalCameraData() : null;
            if (baseData != null) baseData.cameraStack.Remove(overlayCam);
            Destroy(overlayCam.gameObject); overlayCam = null;
        }
    }

    void LateUpdate()
    {
        UpdateWordOverlay();
        if (!blockEverything || cam == null) return;
        var t = cam.transform; var cur = t.position;
        // 글자가 다 내려앉아 체험이 시작되면 자모에도 충돌 모양을 붙여요
        if (!jamoDone && cam.MovementEnabled && ws != null)
        {
            foreach (var p in ws.Pieces) if (p != null && p.visual != null) AddColliders(p.visual);
            jamoDone = true;
        }
        if (!hasPrev || !cam.MovementEnabled || cam.Paused || (cur - prev).sqrMagnitude > 25f) { prev = cur; hasPrev = true; return; }

        var delta = cur - prev; if (delta.sqrMagnitude < 1e-10f) return;
        var pos = prev; const float SKIN = 0.02f;
        for (int it = 0; it < 4 && delta.sqrMagnitude > 1e-10f; it++)
        {
            float dist = delta.magnitude; var dir = delta / dist;
            if (Physics.SphereCast(pos, cameraRadius, dir, out var hit, dist + SKIN, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float move = Mathf.Max(0f, hit.distance - SKIN);
                pos += dir * move;
                delta = Vector3.ProjectOnPlane(delta - dir * move, hit.normal); // 벽을 따라 미끄러지게
            }
            else { pos += delta; delta = Vector3.zero; }
        }
        if ((pos - cur).sqrMagnitude > 1e-10f) t.position = pos;
        prev = pos;
    }

    // ==================================================================
    //  도우미
    // ==================================================================
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> idx = new List<int>();
        public void Tri(int a, int b, int c) { idx.Add(a); idx.Add(b); idx.Add(c); }
        public Mesh ToMesh(string name, bool tangents)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(idx, 0);
            m.RecalculateNormals();
            // 법선이 아래를 보면 삼각형 순서를 뒤집어요 (언덕·나무 모두 바깥/위를 보게)
            var n = m.normals; int down = 0; foreach (var q in n) if (q.y < -0.2f) down++;
            if (name.Contains("언덕") && down > n.Length / 2) { idx.Reverse(); m.SetTriangles(idx, 0); m.RecalculateNormals(); }
            m.RecalculateBounds(); if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    void Emit(Transform parent, string name, MB mb, Material[] mats, ShadowCastingMode cast, bool tangents)
    {
        if (mb.v.Count == 0) return;
        var mesh = mb.ToMesh(name, tangents);
        SaveAsset(mesh, "메시_언덕_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = cast; mr.receiveShadows = true;
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f; } }
    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
    }
    static float Fbm(float x, float y)
    {
        float s = 0, a = 0.5f, n = 0;
        for (int i = 0; i < 4; i++) { s += a * Noise(x, y); n += a; x = x * 2.03f + 17.1f; y = y * 2.03f - 9.3f; a *= 0.5f; }
        return s / n;
    }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "언덕");
        file = file.Replace("/", "_").Replace("·", "_").Replace("(", "").Replace(")", "").Replace(" ", "_");
        string path = OUT + "/" + file;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(o, path);
#endif
    }
    static void SaveAll()
    {
#if UNITY_EDITOR
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
#endif
    }
    void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        EditorUtility.SetDirty(o);
        if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
    static void Progress(string msg, float p)
    {
#if UNITY_EDITOR
        EditorUtility.DisplayProgressBar("논틀밭틀 언덕", msg, p);
#endif
    }
    static void ClearProgress()
    {
#if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NonteulHillsAndBlock))]
public class NonteulHillsAndBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulHillsAndBlock)target;
        EditorGUILayout.HelpBox("[가까운 언덕 만들기]: 멀리 있던 언덕을 숨기고 마을 바로 바깥에 언덕과 숲을 만들어요.\n플레이하면 물체 통과 막기와 Shift 속도는 자동으로 켜져요 (버튼 필요 없음).", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("⛰ 가까운 언덕 만들기", GUILayout.Height(36))) Run(s.BuildHills);
        if (GUILayout.Button("가까운 언덕 지우기 (먼 언덕으로)", GUILayout.Height(22))) Run(s.RemoveHills);
        GUILayout.Space(8);
        DrawDefaultInspector();
    }
    static void Run(System.Action a)
    {
        try { a(); }
        catch (System.Exception e) { Debug.LogException(e); }
        finally { EditorUtility.ClearProgressBar(); }
    }
}
#endif
