// =====================================================================
//  NonteulTeaTerrace.cs — '논틀밭틀' 글자 자리 논을 보성 녹차밭처럼 계단식으로 (URP)
//
//  NonteulFieldMaker · NonteulFieldUpgrade · NonteulWindingPath 위에 덧붙여요.
//  (모두 「공간」 오브젝트에 같이 붙여 두세요)
//   - 글자 자리의 평평한 논을 숨기고, 그 자리에 층층이 올라가는 계단 밭을 만들어요.
//   - 마른 칸 → 보성 녹차밭처럼 등고선을 따라 둥근 차나무 줄이 겹겹이
//   - 물 칸   → 돌담으로 쌓은 계단식 물논 + 모
//   - 계단 끝선 풀, 자모 둑 가장자리 풀, 빈 땅 풀도 새로 심어요.
//   - 자모(글자)는 건드리지 않아요. 계단은 자모에서 조금 떨어져서 올라가요.
//   - [글자 자리 층층이 높이기] 자모마다 돌벽으로 쌓은 단 위에 올려요. 걷는 순서(ㄹ→ㄹ→ㅏ)대로
//     점점 높아지고, 둘레 자모 단은 더 높아요 (최대 6.5m). 자모·ㄹ 길 장식·시작 지점도 같이 올라가요.
//  만든 파일은 Assets/논틀밭틀_생성/다원 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulTeaTerrace : MonoBehaviour
{
    [Header("계단 모양")]
    [Tooltip("한 층 높이 (m)")] [Range(0.3f, 1.2f)] public float stepHeight = 0.7f;
    [Tooltip("최대 층수")] [Range(1, 8)] public int maxTiers = 6;
    [Tooltip("한 층 너비 (m) — 작을수록 층이 촘촘")] [Range(0.7f, 2.6f)] public float stepWidth = 0.95f;
    [Tooltip("층 선이 구불구불한 정도")] [Range(0f, 1.5f)] public float waviness = 0.5f;
    [Tooltip("자모 둑에서 계단이 시작하기까지 띄우는 거리 (m)")] [Range(0f, 1.5f)] public float jamoMargin = 0.25f;

    [Header("차나무·풀")]
    [Tooltip("차나무 줄 굵기")] [Range(0.6f, 1.4f)] public float hedgeSize = 1.0f;
    [Tooltip("물 칸 계단 벽을 돌담으로")] public bool stoneWalls = true;
    [Tooltip("풀 양 (컴퓨터가 느려지면 줄이세요)")] [Range(0.2f, 1.5f)] public float grassAmount = 1.0f;

    [Header("글자 자리 층층이 높이기")]
    [Tooltip("자모마다 서 있는 단을 층층이 높여요")] public bool raiseLetters = true;
    [Tooltip("단 높이 배율 (1 = 가장 높은 단 6.5m)")] [Range(0.3f, 2f)] public float levelScale = 1f;

    // 버튼이 채우는 값 (원래 위치로 되돌리기용)
    [HideInInspector] public List<Transform> liftedShells = new List<Transform>();
    [HideInInspector] public List<Vector3> shellOrigPos = new List<Vector3>();
    [HideInInspector] public Transform liftedStart; [HideInInspector] public Vector3 startOrigPos;

    // ------------------------------------------------------------------
    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/다원";
    const string ROOT_NAME = "논틀밭틀_계단 다원 (자동 생성)";
    const string HQ_NAME = "논틀밭틀_고퀄 (자동 생성)";
    const string PADDY_NAME = "논·벼 (고퀄)";
    const float LIFT = 0.015f;   // 바닥과 겹쳐 깜빡이지 않게 살짝 띄우기
    const float RISER = 0.38f;   // 한 층 너비 중 경사(벽)가 차지하는 비율
    const float CELL = 0.15f;    // 계단 땅 격자 크기

    // NonteulFieldMaker·Upgrade와 같은 배치 숫자
    static readonly float[,] FINAL = {
        {-10f,-8f,0f,12f,18f,.8f,0},{-10f,-7.5f,0f,3.2f,2.4f,.45f,2},{-10f,-8f,180f,12f,18f,.8f,0},{9f,-12f,90f,10f,8f,.6f,0},
        {-4.3f,4.5f,90f,6f,1f,.75f,0},{0f,-11.5f,0f,5f,11f,1f,1},{-11f,9f,0f,9f,12f,.75f,0},{5.5f,17f,0f,3.5f,8f,.7f,1},
        {9f,-1f,90f,10f,8f,.6f,0},{9f,10f,90f,10f,8f,.6f,0},{-4.3f,11f,90f,6f,1f,.75f,0},{0f,.4f,0f,5f,11f,1f,1}};
    static readonly string JAMO = "ㄴㅗㄴㅌㅡㄹㅂㅏㅌㅌㅡㄹ";

    class Plate { public float x0, x1, z0, z1, baseY; public bool water; public List<R2> near = new List<R2>(); }
    class R2 { public float cx, cz, hw, hd, yaw, top; public int j; }
    readonly List<Plate> plates = new List<Plate>();

    // 글자 자리 단: 걷는 순서(ㄹ → ㄹ → ㅏ)대로 올라가고, 둘레 자모 단은 더 높게
    struct Zone { public Rect r; public float L; public string name; public Zone(float x0, float x1, float z0, float z1, float l, string n) { r = Rect.MinMaxRect(x0, z0, x1, z1); L = l; name = n; } }
    static readonly Zone[] ZONES = {
        new Zone(-16.6f, -3.7f, -17.6f, 1.25f, 4.5f, "ㄴ 논 단"),
        new Zone(-16.1f, -3.7f, 1.25f, 15.6f, 6.5f, "ㅂ·ㅡ 단"),
        new Zone(-3.7f, 4.6f, -17.6f, -5.55f, 1.5f, "ㄹ 아래 단"),
        new Zone(-3.7f, 4.6f, -5.55f, 6.45f, 3.0f, "ㄹ 위 단"),
        new Zone(-3.7f, 4.6f, 6.45f, 21.6f, 4.0f, "가운데 논·ㅏ 단"),
        new Zone(4.6f, 13.6f, 15.05f, 21.6f, 4.0f, "ㅏ 물논 단"),
        new Zone(4.6f, 13.6f, -17.6f, -6.5f, 2.5f, "ㅌ 첫째 단"),
        new Zone(4.6f, 13.6f, -6.5f, 4.5f, 4.5f, "ㅌ 둘째 단"),
        new Zone(4.6f, 13.6f, 4.5f, 15.05f, 6.0f, "ㅌ 셋째 단") };
    int ZoneIndex(float x, float z) { if (!raiseLetters) return -1; for (int i = 0; i < ZONES.Length; i++) if (ZONES[i].r.Contains(new Vector2(x, z))) return i; return -1; }
    float ZoneLevel(float x, float z) { int i = ZoneIndex(x, z); return i < 0 ? 0f : ZONES[i].L * levelScale; }
    readonly List<R2> strokes = new List<R2>();
    System.Random rng;

    Material mGrass, mPath, mMud, mWater, mBladeGrass, mBladeSeed, mStone;
    Material[] mTea = new Material[3];

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
        rng = new System.Random(77);
        Remove();
        HideFlatPaddies();
        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);

        Progress("차잎·돌담 질감 만드는 중…", 0.05f);
        MakeOwnMaterials();
        LiftLetters();
        DefineLayout();
        if (raiseLetters) BuildZoneBlocks(root);

        Progress("계단 땅 만드는 중…", 0.2f);
        var grids = new List<Grid>();
        foreach (var p in plates) grids.Add(MakeGrid(p));
        BuildGround(root, grids);

        Progress("차나무 줄 심는 중…", 0.45f);
        BuildHedges(root, grids);

        Progress("물논·모 만드는 중…", 0.6f);
        BuildWater(root, grids);

        Progress("풀 심는 중…", 0.75f);
        BuildGrass(root, grids);

        RaiseGlowRoute();
        Progress("저장하는 중…", 0.95f);
        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 계단 다원] 다 만들었어요. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        RestoreLetters();
        var hq = transform.Find(HQ_NAME);
        var pad = hq ? hq.Find(PADDY_NAME) : null;
        if (pad) foreach (Transform c in pad) { c.gameObject.SetActive(true); MarkDirty(c.gameObject); }
        MarkDirty(gameObject);
    }

    void HideFlatPaddies()
    {
        var hq = transform.Find(HQ_NAME);
        var pad = hq ? hq.Find(PADDY_NAME) : null;
        if (pad == null) { Debug.LogWarning("[논틀밭틀 계단 다원] '" + PADDY_NAME + "'을 못 찾았어요. (Upgrade의 B를 안 눌렀다면 괜찮아요)"); return; }
        foreach (Transform c in pad) { c.gameObject.SetActive(false); MarkDirty(c.gameObject); }
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string n) => AssetDatabase.LoadAssetAtPath<Material>(SUB + "/" + n + ".mat");
        mGrass = L("고퀄_풀_1.00"); mPath = L("고퀄_길_흙_1.00"); mMud = L("고퀄_진흙_1.00"); mWater = L("고퀄_물");
        mBladeGrass = L("잎_풀"); mBladeSeed = L("잎_모");
        if (mPath == null) mPath = L("고퀄_흙_1.00");
        if (mGrass == null || mPath == null || mMud == null || mWater == null || mBladeGrass == null)
        {
            Debug.LogError("[논틀밭틀 계단 다원] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요.");
            return false;
        }
        if (mBladeSeed == null) mBladeSeed = mBladeGrass;
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  글자 자리 층층이 높이기 (자모 껍데기·ㄹ 길 장식·시작 지점을 단 높이만큼 올려요)
    // ==================================================================
    const string VALLEY_ROOT = "논틀밭틀_녹차밭 골짜기 (자동 생성)";

    void LiftLetters()
    {
        RestoreLetters();
        if (!raiseLetters) return;
        var up = transform.up;
        var jr = FindJamoRoot();
        if (jr != null)
            foreach (var jp in jr.GetComponentsInChildren<Byeolmalsseum.JamoPiece>(true))
            {
                if (!MeasureBounds(jp.transform, out var wb)) continue;
                var lc = transform.InverseTransformPoint(wb.center);
                float L = ZoneLevel(lc.x, lc.z); if (L <= 0f) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RecordObject(jp.transform, "글자 단 높이기");
#endif
                liftedShells.Add(jp.transform); shellOrigPos.Add(jp.transform.position);
                jp.transform.position += up * L;
                MarkDirty(jp.transform);
            }
        // ㄹ 꼬불길 장식 (녹차밭 골짜기 스크립트가 만든 것)
        var v = transform.Find(VALLEY_ROOT);
        if (v != null)
            foreach (Transform c in v)
            {
                if (!c.name.StartsWith("ㄹ")) continue;
                var rs = c.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var lc = transform.InverseTransformPoint(b.center);
                c.localPosition = new Vector3(0, ZoneLevel(lc.x, lc.z), 0); MarkDirty(c);
            }
        // 체험 시작 지점
        var ws = FindFirstObjectByType<Byeolmalsseum.WordSpace>();
        var sp = ws != null && ws.experienceCamera != null ? ws.experienceCamera.startPoint : null;
        if (sp != null)
        {
            var a = transform.InverseTransformPoint(sp.position); var f = transform.InverseTransformDirection(sp.forward);
            float L = Mathf.Max(ZoneLevel(a.x, a.z), ZoneLevel(a.x + f.x * 1.2f, a.z + f.z * 1.2f));
            if (L > 0f)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RecordObject(sp, "시작 지점 높이기");
#endif
                liftedStart = sp; startOrigPos = sp.position; sp.position += up * L; MarkDirty(sp);
            }
        }
    }

    void RestoreLetters()
    {
        for (int i = 0; i < liftedShells.Count && i < shellOrigPos.Count; i++)
        {
            var t = liftedShells[i]; if (t == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(t, "글자 단 되돌리기");
#endif
            t.position = shellOrigPos[i]; MarkDirty(t);
        }
        liftedShells.Clear(); shellOrigPos.Clear();
        if (liftedStart != null) { liftedStart.position = startOrigPos; MarkDirty(liftedStart); liftedStart = null; }
        var v = transform.Find(VALLEY_ROOT);
        if (v != null) foreach (Transform c in v) if (c.name.StartsWith("ㄹ")) { c.localPosition = Vector3.zero; MarkDirty(c); }
    }

    bool NearZoneEdge(float x, float z, float m)
    {
        float L = ZoneLevel(x, z);
        return !Mathf.Approximately(ZoneLevel(x + m, z), L) || !Mathf.Approximately(ZoneLevel(x - m, z), L)
            || !Mathf.Approximately(ZoneLevel(x, z + m), L) || !Mathf.Approximately(ZoneLevel(x, z - m), L);
    }

    // 단 덩어리: 윗면(풀) + 돌로 쌓은 옆벽 + 모서리 풀
    void BuildZoneBlocks(Transform root)
    {
        var top = new MB(1); var wall = new MB(1); var grass = new MB(1);
        foreach (var Z in ZONES)
        {
            float L = Z.L * levelScale; var r = Z.r;
            int k = top.v.Count;
            top.Add(new Vector3(r.xMin, L, r.yMin), Vector3.up, new Vector2(r.xMin, r.yMin)); top.Add(new Vector3(r.xMax, L, r.yMin), Vector3.up, new Vector2(r.xMax, r.yMin));
            top.Add(new Vector3(r.xMax, L, r.yMax), Vector3.up, new Vector2(r.xMax, r.yMax)); top.Add(new Vector3(r.xMin, L, r.yMax), Vector3.up, new Vector2(r.xMin, r.yMax));
            top.Tri(0, k, k + 2, k + 1); top.Tri(0, k, k + 3, k + 2);
            var edges = new[] {
                (new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(0, -1)),
                (new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(1, 0)),
                (new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(0, 1)),
                (new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), new Vector2(-1, 0)) };
            foreach (var e in edges)
            {
                Vector2 a = e.Item1, b = e.Item2, o = e.Item3; float len = Vector2.Distance(a, b);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                var nrm = new Vector3(o.x, 0, o.y);
                for (int i = 0; i < n; i++)
                {
                    var p0 = Vector2.Lerp(a, b, i / (float)n); var p1 = Vector2.Lerp(a, b, (i + 1) / (float)n); var mid = (p0 + p1) * 0.5f;
                    float low = ZoneLevel(mid.x + o.x * 0.05f, mid.y + o.y * 0.05f);
                    if (low >= L - 0.01f) continue;
                    float u0 = Vector2.Distance(a, p0), u1 = u0 + Vector2.Distance(p0, p1);
                    int q = wall.v.Count;
                    wall.Add(new Vector3(p0.x, low - 0.02f, p0.y), nrm, new Vector2(u0, low)); wall.Add(new Vector3(p1.x, low - 0.02f, p1.y), nrm, new Vector2(u1, low));
                    wall.Add(new Vector3(p1.x, L, p1.y), nrm, new Vector2(u1, L)); wall.Add(new Vector3(p0.x, L, p0.y), nrm, new Vector2(u0, L));
                    wall.Tri(0, q, q + 1, q + 2); wall.Tri(0, q, q + 2, q + 3);
                    // 위 모서리에 늘어진 풀, 아래 밑동 풀
                    for (int g = 0; g < 2; g++)
                    {
                        var pt = Vector2.Lerp(p0, p1, R(0f, 1f));
                        if (rng.NextDouble() < 0.85 * grassAmount) Ribbons(grass, new Vector3(pt.x - o.x * 0.06f, L - 0.02f, pt.y - o.y * 0.06f), R(0.25f, 0.5f), 6, 0.15f, 1.2f, 0.022f);
                        if (rng.NextDouble() < 0.6 * grassAmount) Ribbons(grass, new Vector3(pt.x + o.x * 0.1f, low + LIFT, pt.y + o.y * 0.1f), R(0.3f, 0.6f), 7, 0.2f, 0.5f, 0.024f);
                    }
                }
            }
        }
        var holder = new GameObject("글자 단 (층층이)").transform; holder.SetParent(root, false);
        Emit(holder, "단 윗면", top, new[] { mGrass }, ShadowCastingMode.On, true);
        Emit(holder, "단 돌벽", wall, new[] { mStone != null ? mStone : mPath }, ShadowCastingMode.On, true);
        Emit(holder, "단 모서리 풀", grass, new[] { mBladeGrass }, ShadowCastingMode.Off, false, false);
    }

    // 빛가루 길이 단 속에 묻히지 않게 단 높이만큼 올려요
    void RaiseGlowRoute()
    {
        if (!raiseLetters) return;
        var wp = GetComponent("NonteulWindingPath"); if (wp == null) return;
        var f = wp.GetType().GetField("glowRoute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var list = f != null ? f.GetValue(wp) as List<Vector3> : null; if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var lp = transform.InverseTransformPoint(list[i]); float L = ZoneLevel(lp.x, lp.z);
            if (L > 0f && lp.y < L + 0.7f) { lp.y = L + 1.0f; list[i] = transform.TransformPoint(lp); }
        }
        MarkDirty(wp);
    }

    // ==================================================================
    //  배치 (논 칸 · 자모 획)
    // ==================================================================
    void DefineLayout()
    {
        plates.Clear(); strokes.Clear();
        void P(float x0, float x1, float z0, float z1, bool w) => plates.Add(new Plate { x0 = x0, x1 = x1, z0 = z0, z1 = z1, water = w });
        P(-15.6f, -4.4f, -16.6f, 0.6f, false);
        P(-15.1f, -6.9f, 3.4f, 9f, true);
        P(-15.1f, -6.9f, 9.4f, 15f, false);
        P(6.2f, 12.5f, 14.6f, 20f, true);
        P(-3.6f, 3.6f, 7f, 20f, false);
        float[] zc = { -12f, -1f, 10f };
        for (int k = 0; k < 3; k++) { P(5.2f, 9f, zc[k] - 4.8f, zc[k] + 4.8f, k != 1); P(9f, 12.8f, zc[k] - 4.8f, zc[k] + 4.8f, k == 1); }

        foreach (var p in plates)
        {
            int zi = ZoneIndex((p.x0 + p.x1) / 2, (p.z0 + p.z1) / 2);
            if (zi < 0) continue;
            var zr = ZONES[zi].r;
            p.x0 = Mathf.Max(p.x0, zr.xMin); p.x1 = Mathf.Min(p.x1, zr.xMax); p.z0 = Mathf.Max(p.z0, zr.yMin); p.z1 = Mathf.Min(p.z1, zr.yMax);
            p.baseY = ZONES[zi].L * levelScale;
        }
        float[] tops = MeasureJamoTops();
        for (int j = 0; j < 12; j++)
        {
            float X = FINAL[j, 0], Z = FINAL[j, 1], yaw = FINAL[j, 2] * Mathf.Deg2Rad, fw = FINAL[j, 3], fh = FINAL[j, 4], T = FINAL[j, 5];
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            foreach (var st in Jamo2D(JAMO[j], -fw / 2, fw / 2, -fh / 2, fh / 2, T))
                strokes.Add(new R2 { cx = X + c * st.x + s * st.y, cz = Z - s * st.x + c * st.y, hw = st.z / 2, hd = st.w / 2, yaw = yaw, top = tops[j], j = j });
        }
        foreach (var p in plates)
            foreach (var r in strokes)
            {
                float rad = Mathf.Sqrt(r.hw * r.hw + r.hd * r.hd) + 4f;
                float dx = Mathf.Max(p.x0 - r.cx, 0, r.cx - p.x1), dz = Mathf.Max(p.z0 - r.cz, 0, r.cz - p.z1);
                if (dx * dx + dz * dz < rad * rad) p.near.Add(r);
            }
    }

    static List<Vector4> Jamo2D(char ch, float x0, float x1, float y0, float y1, float T)
    {
        var S = new List<Vector4>(); float m = (y0 + y1) / 2;
        void hb(float y, float a, float b) => S.Add(new Vector4((a + b) / 2, y, b - a, T));
        void vb(float x, float a, float b) => S.Add(new Vector4(x, (a + b) / 2, T, b - a));
        switch (ch)
        {
            case 'ㄴ': vb(x0 + T / 2, y0, y1); hb(y0 + T / 2, x0, x1); break;
            case 'ㅗ': vb((x0 + x1) / 2, y0 + T, y1); hb(y0 + T / 2, x0, x1); break;
            case 'ㅡ': hb(m, x0, x1); break;
            case 'ㅌ': hb(y1 - T / 2, x0, x1); hb(m, x0, x1); hb(y0 + T / 2, x0, x1); vb(x0 + T / 2, y0, y1); break;
            case 'ㄹ': hb(y1 - T / 2, x0, x1); vb(x1 - T / 2, m, y1); hb(m, x0, x1); vb(x0 + T / 2, y0, m); hb(y0 + T / 2, x0, x1); break;
            case 'ㅂ': vb(x0 + T / 2, y0, y1); vb(x1 - T / 2, y0, y1); hb(m, x0, x1); hb(y0 + T / 2, x0, x1); break;
            case 'ㅏ': vb(x0 + T / 2, y0, y1); hb(m, x0 + T, x1); break;
        }
        return S;
    }

    // 자모 껍데기의 실제 윗면 높이 (못 찾으면 대략값)
    float[] MeasureJamoTops()
    {
        var tops = new float[12];
        for (int j = 0; j < 12; j++) { int kind = (int)FINAL[j, 6]; tops[j] = kind == 1 ? 0.4f : (kind == 2 ? 0.8f : 1.2f); }
        var root = FindJamoRoot(); if (root == null) return tops;
        var best = new float[12]; for (int j = 0; j < 12; j++) best[j] = float.MaxValue;
        foreach (var jp in root.GetComponentsInChildren<Byeolmalsseum.JamoPiece>(true))
        {
            if (!MeasureBounds(jp.transform, out var wb)) continue;
            var lc = transform.InverseTransformPoint(wb.center);
            var lt = transform.InverseTransformPoint(new Vector3(wb.center.x, wb.max.y, wb.center.z));
            for (int j = 0; j < 12; j++)
            {
                float d = (lc.x - FINAL[j, 0]) * (lc.x - FINAL[j, 0]) + (lc.z - FINAL[j, 1]) * (lc.z - FINAL[j, 1]);
                if (d < best[j] && d < 4f) { best[j] = d; tops[j] = Mathf.Max(0.1f, lt.y); }
            }
        }
        return tops;
    }

    Transform FindJamoRoot()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.gameObject.scene == gameObject.scene && t.name.StartsWith("글자")) return t;
        return null;
    }

    static bool MeasureBounds(Transform t, out Bounds b)
    {
        b = new Bounds(); bool has = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return has;
    }

    // ==================================================================
    //  계단 모양 계산
    // ==================================================================
    // 회전된 직사각형까지의 거리 (안쪽이면 음수)
    static float RectSdf(R2 r, float x, float z)
    {
        float dx = x - r.cx, dz = z - r.cz, c = Mathf.Cos(r.yaw), s = Mathf.Sin(r.yaw);
        float lx = Mathf.Abs(c * dx - s * dz) - r.hw, lz = Mathf.Abs(s * dx + c * dz) - r.hd;
        float ox = Mathf.Max(lx, 0), oz = Mathf.Max(lz, 0);
        return Mathf.Sqrt(ox * ox + oz * oz) + Mathf.Min(Mathf.Max(lx, lz), 0);
    }

    // 논 칸 가장자리·자모에서 얼마나 안쪽인지 (모서리는 둥글게, 선은 살짝 구불구불)
    float Dist(Plate p, float x, float z)
    {
        const float K = 0.45f;
        float s = E(x - p.x0, K) + E(p.x1 - x, K) + E(z - p.z0, K) + E(p.z1 - z, K);
        foreach (var r in p.near) s += E(RectSdf(r, x, z) - jamoMargin, K);
        float d = -K * Mathf.Log(Mathf.Max(s, 1e-12f));
        float wob = waviness * 0.55f * (Fbm(x * 0.17f + 3.1f, z * 0.17f - 7.7f) - 0.5f) * 2f;
        return d + wob * Smooth01(d / 1.6f);
    }
    static float E(float a, float k) => Mathf.Exp(Mathf.Clamp(-a / k, -40f, 40f));

    float Height(float d)
    {
        if (d <= 0) return 0;
        float lv = d / stepWidth; int k = Mathf.FloorToInt(lv); float f = lv - k;
        if (k >= maxTiers) return stepHeight * maxTiers;
        return stepHeight * (k + Smooth01(f / RISER));
    }
    // 평평한 층(물을 담을 수 있는 곳)이면 층 번호, 경사면이면 -1
    int TreadLevel(float d, float margin)
    {
        if (d <= 0) return -1;
        float lv = d / stepWidth; int k = Mathf.FloorToInt(lv); float f = lv - k;
        if (k >= maxTiers) return maxTiers;
        return f > RISER + margin ? k + 1 : -1;
    }

    class Grid { public Plate p; public int nx, nz; public float dx, dz; public float[] d, h; }

    Grid MakeGrid(Plate p)
    {
        var g = new Grid { p = p };
        g.nx = Mathf.Max(2, Mathf.CeilToInt((p.x1 - p.x0) / CELL) + 1);
        g.nz = Mathf.Max(2, Mathf.CeilToInt((p.z1 - p.z0) / CELL) + 1);
        g.dx = (p.x1 - p.x0) / (g.nx - 1); g.dz = (p.z1 - p.z0) / (g.nz - 1);
        g.d = new float[g.nx * g.nz]; g.h = new float[g.nx * g.nz];
        for (int iz = 0; iz < g.nz; iz++)
            for (int ix = 0; ix < g.nx; ix++)
            {
                float x = p.x0 + ix * g.dx, z = p.z0 + iz * g.dz;
                bool edge = ix == 0 || iz == 0 || ix == g.nx - 1 || iz == g.nz - 1;
                float d = edge ? -1f : Dist(p, x, z);
                g.d[iz * g.nx + ix] = d; g.h[iz * g.nx + ix] = Height(d);
            }
        return g;
    }

    // ==================================================================
    //  1) 계단 땅
    // ==================================================================
    void BuildGround(Transform root, List<Grid> grids)
    {
        // 0 차밭 고랑(흙) 1 차밭 경사(풀) 2 물논 바닥(진흙) 3 물논 벽(돌담/풀)
        var mb = new MB(4);
        foreach (var g in grids)
        {
            int k0 = mb.v.Count;
            for (int iz = 0; iz < g.nz; iz++)
                for (int ix = 0; ix < g.nx; ix++)
                {
                    float x = g.p.x0 + ix * g.dx, z = g.p.z0 + iz * g.dz;
                    float hl = g.h[iz * g.nx + Mathf.Max(ix - 1, 0)], hr = g.h[iz * g.nx + Mathf.Min(ix + 1, g.nx - 1)];
                    float hd = g.h[Mathf.Max(iz - 1, 0) * g.nx + ix], hu = g.h[Mathf.Min(iz + 1, g.nz - 1) * g.nx + ix];
                    var n = new Vector3(-(hr - hl) / (2 * g.dx), 1f, -(hu - hd) / (2 * g.dz)).normalized;
                    mb.Add(new Vector3(x, g.h[iz * g.nx + ix] + LIFT + g.p.baseY, z), n, new Vector2(x, z));
                }
            int wallSub = g.p.water ? 3 : 1, flatSub = g.p.water ? 2 : 0;
            for (int iz = 0; iz < g.nz - 1; iz++)
                for (int ix = 0; ix < g.nx - 1; ix++)
                {
                    int a = k0 + iz * g.nx + ix, b = a + 1, c = a + g.nx, d = c + 1;
                    float h0 = mb.v[a].y, h1 = mb.v[b].y, h2 = mb.v[c].y, h3 = mb.v[d].y;
                    float span = Mathf.Max(Mathf.Max(h0, h1), Mathf.Max(h2, h3)) - Mathf.Min(Mathf.Min(h0, h1), Mathf.Min(h2, h3));
                    int s = span > 0.05f ? wallSub : flatSub;
                    mb.sub[s].Add(a); mb.sub[s].Add(c); mb.sub[s].Add(b);
                    mb.sub[s].Add(b); mb.sub[s].Add(c); mb.sub[s].Add(d);
                }
        }
        var wallMat = stoneWalls && mStone != null ? mStone : mGrass;
        Emit(root, "계단 땅", mb, new[] { mPath, mGrass, mMud, wallMat }, ShadowCastingMode.On, true);
    }

    // ==================================================================
    //  2) 차나무 줄 (등고선을 따라 둥글게 이어진 줄)
    // ==================================================================
    void BuildHedges(Transform root, List<Grid> grids)
    {
        var mb = new MB(3);
        float off = (1f + RISER) / 2f;   // 층 평평한 곳의 한가운데
        float spacing = 0.27f * hedgeSize;
        foreach (var g in grids)
        {
            if (g.p.water) continue;
            var pts = RowPoints(g, i => stepWidth * (i + off), spacing);
            foreach (var pt in pts)
            {
                float x = pt.x, z = pt.y, L = pt.z;
                // 줄 방향 = 등고선 방향
                float e = 0.06f;
                float gx = Dist(g.p, x + e, z) - Dist(g.p, x - e, z), gz = Dist(g.p, x, z + e) - Dist(g.p, x, z - e);
                var tan = new Vector3(-gz, 0, gx); if (tan.sqrMagnitude < 1e-8f) tan = Vector3.right; tan.Normalize();
                float tread = stepWidth * (1f - RISER);
                float rH = Mathf.Min(0.46f, tread * 0.48f) * hedgeSize * R(0.9f, 1.1f);
                float rV = 0.38f * hedgeSize * R(0.85f, 1.15f);
                float y = Height(L) + LIFT + g.p.baseY;
                float patch = Fbm(x * 0.22f + 11f, z * 0.22f - 5f);
                int sub = patch < 0.42f ? 0 : (patch < 0.58f ? 1 : 2);
                if (rng.NextDouble() < 0.18) sub = rng.Next(3);
                TeaClump(mb, sub, new Vector3(x, y, z), tan, rH, rV);
            }
        }
        Emit(root, "차나무 줄", mb, mTea, ShadowCastingMode.On, false);
    }

    // 등고선(level(i)) 위의 점들을 격자에서 찾아 간격(spacing)을 맞춰 골라요. (x, z, 등고선 값)
    List<Vector3> RowPoints(Grid g, System.Func<int, float> level, float spacing)
    {
        var raw = new List<Vector3>();
        void Edge(int i0, int i1, float x0, float z0, float x1, float z1)
        {
            float d0 = g.d[i0], d1 = g.d[i1]; if (d0 <= 0 || d1 <= 0) return;
            float lo = Mathf.Min(d0, d1), hi = Mathf.Max(d0, d1);
            for (int i = 0; i < 200; i++)
            {
                float L = level(i); if (L > hi) break; if (L <= lo) continue;
                float t = (L - d0) / (d1 - d0);
                raw.Add(new Vector3(Mathf.Lerp(x0, x1, t), Mathf.Lerp(z0, z1, t), L));
            }
        }
        for (int iz = 0; iz < g.nz; iz++)
            for (int ix = 0; ix < g.nx; ix++)
            {
                int a = iz * g.nx + ix; float x = g.p.x0 + ix * g.dx, z = g.p.z0 + iz * g.dz;
                if (ix < g.nx - 1) Edge(a, a + 1, x, z, x + g.dx, z);
                if (iz < g.nz - 1) Edge(a, a + g.nx, x, z, x, z + g.dz);
            }
        // 골고루 섞은 뒤 너무 가까운 점은 버려요
        for (int i = raw.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = raw[i]; raw[i] = raw[j]; raw[j] = tmp; }
        var keep = new List<Vector3>(); var hash = new Dictionary<long, List<Vector2>>();
        float s2 = spacing * spacing;
        foreach (var p in raw)
        {
            int cx = Mathf.FloorToInt(p.x / spacing), cz = Mathf.FloorToInt(p.y / spacing); bool ok = true;
            for (int a = -1; a <= 1 && ok; a++)
                for (int b = -1; b <= 1 && ok; b++)
                    if (hash.TryGetValue(Key(cx + a, cz + b), out var list))
                        foreach (var q in list) if ((q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y) < s2) { ok = false; break; }
            if (!ok) continue;
            long k = Key(cx, cz); if (!hash.TryGetValue(k, out var l2)) { l2 = new List<Vector2>(); hash[k] = l2; }
            l2.Add(new Vector2(p.x, p.y)); keep.Add(p);
        }
        return keep;
    }
    static long Key(int a, int b) => ((long)a << 32) ^ (uint)b;

    // 둥근 차나무 덩어리 하나 (줄 방향으로 조금 길쭉, 아래는 땅에 묻힘)
    void TeaClump(MB m, int sub, Vector3 basePos, Vector3 tan, float rH, float rV)
    {
        const int LA = 6, LO = 10;
        var side = new Vector3(-tan.z, 0, tan.x);
        float a = rH * 1.35f, b = rV, c = rH;
        var center = basePos + Vector3.up * (rV * 0.42f);
        float seed = R(0, 100);
        int k0 = m.v.Count;
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI * 0.66f, ph = j / (float)LO * Mathf.PI * 2f;
                float sx = Mathf.Sin(th) * Mathf.Cos(ph), sy = Mathf.Cos(th), sz = Mathf.Sin(th) * Mathf.Sin(ph);
                float bump = 0.84f + 0.32f * Noise(sx * 2.3f + seed, sz * 2.3f + sy * 1.9f + seed);
                if (i == LA) bump = 1f;
                var p = center + (tan * (sx * a) + Vector3.up * (sy * b) + side * (sz * c)) * bump;
                var n = (tan * (sx / a) + Vector3.up * (sy / b) + side * (sz / c)).normalized;
                n = (n + Vector3.up * 0.35f).normalized;
                m.Add(p, n, new Vector2(j / (float)LO * 3f, i / (float)LA));
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++)
            {
                int q = k0 + i * (LO + 1) + j, r = q + LO + 1;
                m.sub[sub].Add(q); m.sub[sub].Add(q + 1); m.sub[sub].Add(r);
                m.sub[sub].Add(q + 1); m.sub[sub].Add(r + 1); m.sub[sub].Add(r);
            }
    }

    // ==================================================================
    //  3) 계단식 물논 + 모
    // ==================================================================
    void BuildWater(Transform root, List<Grid> grids)
    {
        var wmb = new MB(1); var seed = new MB(1);
        foreach (var g in grids)
        {
            if (!g.p.water) continue;
            for (int iz = 0; iz < g.nz - 1; iz++)
                for (int ix = 0; ix < g.nx - 1; ix++)
                {
                    int a = iz * g.nx + ix;
                    int t0 = TreadLevel(g.d[a], 0.02f), t1 = TreadLevel(g.d[a + 1], 0.02f), t2 = TreadLevel(g.d[a + g.nx], 0.02f), t3 = TreadLevel(g.d[a + g.nx + 1], 0.02f);
                    if (t0 < 1 || t0 != t1 || t0 != t2 || t0 != t3) continue;
                    float y = stepHeight * t0 + LIFT + 0.035f + g.p.baseY;
                    float x = g.p.x0 + ix * g.dx, z = g.p.z0 + iz * g.dz;
                    var A = new Vector3(x, y, z); var B = new Vector3(x + g.dx, y, z); var C = new Vector3(x, y, z + g.dz); var D = new Vector3(x + g.dx, y, z + g.dz);
                    int k = wmb.v.Count;
                    wmb.Add(A, Vector3.up, new Vector2(A.x, A.z) / 3f); wmb.Add(B, Vector3.up, new Vector2(B.x, B.z) / 3f);
                    wmb.Add(C, Vector3.up, new Vector2(C.x, C.z) / 3f); wmb.Add(D, Vector3.up, new Vector2(D.x, D.z) / 3f);
                    wmb.sub[0].Add(k); wmb.sub[0].Add(k + 2); wmb.sub[0].Add(k + 1);
                    wmb.sub[0].Add(k + 1); wmb.sub[0].Add(k + 2); wmb.sub[0].Add(k + 3);
                }
            // 모: 줄 맞춰 꽂기
            for (float x = g.p.x0 + 0.3f; x < g.p.x1 - 0.2f; x += 0.46f)
                for (float z = g.p.z0 + 0.3f; z < g.p.z1 - 0.2f; z += 0.34f)
                {
                    float X = x + R(-0.03f, 0.03f), Z = z + R(-0.03f, 0.03f);
                    int t = TreadLevel(Dist(g.p, X, Z), 0.1f); if (t < 1) continue;
                    if (rng.NextDouble() < 0.1) continue;
                    Ribbons(seed, new Vector3(X, stepHeight * t + LIFT + g.p.baseY, Z), R(0.2f, 0.32f), 5, 0.05f, 0.25f, 0.012f);
                }
        }
        Emit(root, "계단 물논", wmb, new[] { mWater }, ShadowCastingMode.Off, false);
        Emit(root, "모", seed, new[] { mBladeSeed }, ShadowCastingMode.Off, false, false);
    }

    // ==================================================================
    //  4) 풀 (계단 끝선 · 자모 둑 가장자리 · 빈 땅)
    // ==================================================================
    void BuildGrass(Transform root, List<Grid> grids)
    {
        var grass = new MB(1);
        float amt = grassAmount;
        // 계단 끝선(층 위 모서리)에 풀을 늘어뜨려요
        foreach (var g in grids)
        {
            var pts = RowPoints(g, i => i < maxTiers ? stepWidth * (i + RISER) + 0.03f : float.MaxValue, 0.42f / Mathf.Max(0.3f, amt));
            foreach (var p in pts)
                Ribbons(grass, new Vector3(p.x, Height(p.z) + LIFT - 0.02f + g.p.baseY, p.y), R(0.22f, 0.42f), 6, 0.2f, 0.75f, 0.022f);
            // 계단 아래쪽(자모 옆) 풀
            var foot = RowPoints(g, i => i == 0 ? 0.12f : float.MaxValue, 0.38f / Mathf.Max(0.3f, amt));
            foreach (var p in foot)
                Ribbons(grass, new Vector3(p.x, Height(p.z) + LIFT + g.p.baseY, p.y), R(0.3f, 0.55f), 7, 0.2f, 0.5f, 0.024f);
        }
        // 자모 둑 둘레: 아래쪽과 윗모서리
        foreach (var r in strokes)
        {
            float per = 4 * (r.hw + r.hd); int n = Mathf.CeilToInt(per / (0.36f / Mathf.Max(0.3f, amt)));
            for (int k = 0; k < n; k++)
            {
                Vector2 o = PerimeterPoint(r, k / (float)n, 0.1f);
                if (!InStroke(o.x, o.y, 0.02f)) Ribbons(grass, new Vector3(o.x, GroundAt(o.x, o.y), o.y), R(0.28f, 0.55f), 7, 0.18f, 0.5f, 0.022f);
                if (rng.NextDouble() < 0.5)
                {
                    Vector2 t = PerimeterPoint(r, (k + 0.5f) / n, -0.07f);
                    Ribbons(grass, new Vector3(t.x, r.top - 0.02f, t.y), R(0.14f, 0.28f), 5, 0.12f, 0.6f, 0.018f);
                }
            }
        }
        // 논 칸 밖 빈 땅
        int want = Mathf.RoundToInt(1800 * amt);
        for (int i = 0, placed = 0; i < 40000 && placed < want; i++)
        {
            float X = R(-24, 24), Z = R(-24, 26);
            if (InPlate(X, Z, 0.1f) || InStroke(X, Z, 0.15f) || NearZoneEdge(X, Z, 0.25f)) continue;
            Ribbons(grass, new Vector3(X, LIFT + ZoneLevel(X, Z), Z), R(0.25f, 0.55f), 8, 0.22f, 0.5f, 0.024f); placed++;
        }
        Emit(root, "풀", grass, new[] { mBladeGrass }, ShadowCastingMode.Off, false, false);
    }

    float GroundAt(float x, float z)
    {
        foreach (var p in plates) if (x > p.x0 && x < p.x1 && z > p.z0 && z < p.z1) return Height(Dist(p, x, z)) + LIFT + p.baseY;
        return LIFT + ZoneLevel(x, z);
    }
    bool InStroke(float x, float z, float pad) { foreach (var r in strokes) if (RectSdf(r, x, z) < pad) return true; return false; }
    bool InPlate(float x, float z, float pad) { foreach (var p in plates) if (x >= p.x0 - pad && x <= p.x1 + pad && z >= p.z0 - pad && z <= p.z1 + pad) return true; return false; }

    static Vector2 PerimeterPoint(R2 r, float t, float off)
    {
        float W = r.hw + off, D = r.hd + off, per = 4 * (W + D); float s = Mathf.Repeat(t, 1f) * per; float lx, lz;
        if (s < 2 * W) { lx = -W + s; lz = -D; }
        else if ((s -= 2 * W) < 2 * D) { lx = W; lz = -D + s; }
        else if ((s -= 2 * D) < 2 * W) { lx = W - s; lz = D; }
        else { s -= 2 * W; lx = -W; lz = D - s; }
        float c = Mathf.Cos(r.yaw), sn = Mathf.Sin(r.yaw);
        return new Vector2(r.cx + c * lx + sn * lz, r.cz - sn * lx + c * lz);
    }

    // 휘어진 잎 여러 장
    void Ribbons(MB m, Vector3 pos, float h, int blades, float spread, float lean, float width)
    {
        float shade = (float)rng.NextDouble();
        for (int bl = 0; bl < blades; bl++)
        {
            float bx = R(-spread, spread) * 0.5f, bz = R(-spread, spread) * 0.5f, dir = R(0, 6.283f), bend = lean * R(0.4f, 1.3f), hh = h * R(0.6f, 1.1f), w0 = width * R(0.7f, 1.2f);
            var d = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir)); var side = new Vector3(-d.z, 0, d.x);
            const int seg = 3; int k0 = m.v.Count;
            float u = 0.1f + 0.8f * Mathf.Repeat(shade + 0.15f * R(-1, 1), 1f);
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                var c = pos + new Vector3(bx, 0, bz) + Vector3.up * (hh * t * (1 - 0.25f * bend * t)) + d * (hh * bend * t * t * 0.6f);
                float w = w0 * Mathf.Pow(1 - t, 0.6f) + 0.0015f;
                var nrm = (Vector3.up * 0.6f + d * -0.4f + side * 0.2f).normalized;
                m.Add(c - side * w, nrm, new Vector2(u, t * 0.85f));
                m.Add(c + side * w, nrm, new Vector2(u, t * 0.85f));
            }
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(a + 3); m.sub[0].Add(a); m.sub[0].Add(a + 3); m.sub[0].Add(a + 2); }
        }
    }

    // ==================================================================
    //  질감: 차잎 · 돌담
    // ==================================================================
    void MakeOwnMaterials()
    {
        var teaTex = GetTex("차잎_색", false, MakeTeaAlbedo);
        var teaN = GetTex("차잎_노멀", true, () => NormalFrom(TeaHeight(256), 256, 5f, "차잎_노멀"));
        Color[] tints = { new Color(1f, 1f, 1f), new Color(0.86f, 0.95f, 0.82f), new Color(1.08f, 1.05f, 0.88f) };
        for (int i = 0; i < 3; i++) mTea[i] = GetMat("다원_차잎_" + (char)('A' + i), tints[i], teaTex, teaN, 0.42f, 1f, Vector2.one);
        {
            var stTex = GetTex("돌담_색", false, MakeStoneAlbedo);
            var stN = GetTex("돌담_노멀", true, () => NormalFrom(StoneHeight(512), 512, 8f, "돌담_노멀"));
            mStone = GetMat("다원_돌담", Color.white, stTex, stN, 0.18f, 1.2f, Vector2.one * 0.55f);
        }
    }

    Texture2D GetTex(string name, bool linear, System.Func<Texture2D> make)
    {
#if UNITY_EDITOR
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(OUT + "/" + name + ".asset");
        if (have != null) return have;
#endif
        var t = make(); t.name = name; SaveAsset(t, name + ".asset"); return t;
    }

    Material GetMat(string name, Color c, Texture2D tex, Texture2D nrm, float smooth, float bump, Vector2 scale)
    {
        Material m = null;
#if UNITY_EDITOR
        m = AssetDatabase.LoadAssetAtPath<Material>(OUT + "/" + name + ".mat");
#endif
        bool isNew = m == null || m.shader == null || !m.shader.isSupported;
        if (isNew)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = name, enableInstancing = true };
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", scale); }
        if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", scale); }
        if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (isNew) SaveAsset(m, name + ".mat"); else MarkDirty(m);
        return m;
    }

    static Texture2D NewTex(int S, bool linear) =>
        new Texture2D(S, S, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };

    // 차잎: 윗부분은 연둣빛 새순, 아래로 갈수록 짙은 녹색, 잎 하나하나 반짝임
    static float[] TeaHeight(int S)
    {
        var h = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float w = FbmP(u * 8, v * 8, 8, 3);
                h[y * S + x] = FbmP(u * 24 + w * 2.5f, v * 24 + w * 1.5f, 24, 3);
            }
        return h;
    }
    static Texture2D MakeTeaAlbedo()
    {
        const int S = 256; var h = TeaHeight(S); var t = NewTex(S, false); var px = new Color[S * S];
        Color top = new Color(0.50f, 0.64f, 0.17f), mid = new Color(0.20f, 0.38f, 0.10f), bot = new Color(0.06f, 0.15f, 0.05f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S, n = h[y * S + x];
                Color c = v < 0.45f ? Color.Lerp(top, mid, v / 0.45f) : Color.Lerp(mid, bot, (v - 0.45f) / 0.55f);
                float leaf = Smooth01((n - 0.52f) / 0.2f), gap = Smooth01((0.42f - n) / 0.16f);
                c *= 1f + 0.5f * leaf; c = Color.Lerp(c, new Color(0.62f, 0.72f, 0.24f), leaf * 0.22f * (1 - v));
                c *= 1f - 0.55f * gap;
                c *= 0.88f + 0.24f * FbmP(u * 4, v * 4, 4, 2);
                c.a = 1; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }

    // 돌담: 둥근 돌 + 흙 틈 + 이끼
    static void Voronoi(float px, float py, int G, out float f1, out float f2, out int id)
    {
        int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(py); f1 = 9; f2 = 9; id = 0;
        for (int a = -1; a <= 1; a++)
            for (int b = -1; b <= 1; b++)
            {
                int gx = cx + a, gy = cy + b, wx = ((gx % G) + G) % G, wy = ((gy % G) + G) % G;
                float fx = gx + 0.15f + 0.7f * Hash(wx, wy), fy = gy + 0.15f + 0.7f * Hash(wx + 31, wy + 57);
                float dx = (px - fx) * 0.8f, dy = py - fy, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < f1) { f2 = f1; f1 = d; id = wx * 131 + wy; } else if (d < f2) f2 = d;
            }
    }
    static float[] StoneHeight(int S)
    {
        var h = new float[S * S]; const int G = 6;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                Voronoi(u * G, v * G, G, out float f1, out float f2, out _);
                float stone = Smooth01((f2 - f1 - 0.03f) / 0.12f);
                float dome = Mathf.Sqrt(Mathf.Clamp01(1f - f1 * 1.3f));
                h[y * S + x] = stone * (0.45f + 0.4f * dome + 0.15f * FbmP(u * 32, v * 32, 32, 3));
            }
        return h;
    }
    static Texture2D MakeStoneAlbedo()
    {
        const int S = 512, G = 6; var t = NewTex(S, false); var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                Voronoi(u * G, v * G, G, out float f1, out float f2, out int id);
                float stone = Smooth01((f2 - f1 - 0.03f) / 0.12f);
                float tone = Hash(id, 7);
                Color sc = Color.Lerp(new Color(0.34f, 0.32f, 0.28f), new Color(0.55f, 0.51f, 0.44f), tone) * (0.85f + 0.3f * FbmP(u * 24, v * 24, 24, 3));
                Color mortar = new Color(0.16f, 0.13f, 0.10f);
                Color c = Color.Lerp(mortar, sc, stone);
                float moss = Smooth01((FbmP(u * 5, v * 5, 5, 3) - 0.5f) / 0.18f);
                c = Color.Lerp(c, new Color(0.20f, 0.29f, 0.10f), moss * (0.75f - 0.35f * stone));
                c.a = 1; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }

    // 높이 → 노멀 맵 (RGB, A=1, 선형)
    static Texture2D NormalFrom(float[] h, int S, float strength, string name)
    {
        var t = NewTex(S, true); var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float l = h[y * S + (x + S - 1) % S], r = h[y * S + (x + 1) % S], d = h[((y + S - 1) % S) * S + x], u = h[((y + 1) % S) * S + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * S + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        t.SetPixels(px); t.Apply(true); t.name = name; return t;
    }

    // ==================================================================
    //  노이즈
    // ==================================================================
    static float Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }
    }
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
    // 이어 붙여도 이음새가 없는 노이즈 (주기 P)
    static float NoiseP(float x, float y, int P)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        int x0 = ((xi % P) + P) % P, y0 = ((yi % P) + P) % P, x1 = (x0 + 1) % P, y1 = (y0 + 1) % P;
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), u), Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), u), v);
    }
    static float FbmP(float x, float y, int P, int oct)
    {
        float s = 0, a = 0.5f, n = 0;
        for (int i = 0; i < oct; i++) { s += a * NoiseP(x, y, P); n += a; x *= 2; y *= 2; P *= 2; a *= 0.5f; }
        return s / n;
    }

    // ==================================================================
    //  메시 · 저장 도우미
    // ==================================================================
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int>[] sub;
        public MB(int subs) { sub = new List<int>[subs]; for (int i = 0; i < subs; i++) sub[i] = new List<int>(); }
        public void Add(Vector3 p, Vector3 nrm, Vector2 u) { v.Add(p); n.Add(nrm); uv.Add(u); }
        public void Tri(int s, int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) >= 0) { sub[s].Add(a); sub[s].Add(b); sub[s].Add(c); }
            else { sub[s].Add(a); sub[s].Add(c); sub[s].Add(b); }
        }
        public Mesh ToMesh(string name, bool tangents)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length; for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds(); if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    void Emit(Transform parent, string name, MB mb, Material[] mats, ShadowCastingMode cast, bool receive, bool tangents = true)
    {
        if (mb.v.Count == 0) return;
        var mesh = mb.ToMesh(name, tangents);
        SaveAsset(mesh, "메시_다원_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = cast; mr.receiveShadows = true;
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "다원");
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
        EditorUtility.DisplayProgressBar("논틀밭틀 계단 다원", msg, p);
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
[CustomEditor(typeof(NonteulTeaTerrace))]
public class NonteulTeaTerraceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulTeaTerrace)target;
        EditorGUILayout.HelpBox("NonteulFieldUpgrade의 [★ 전부 한 번에]를 한 뒤에 눌러요.\n글자 자리의 평평한 논을 숨기고, 보성 녹차밭 같은 계단 밭을 만들어요.\n값을 바꾼 뒤에는 [만들기]를 다시 누르면 돼요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🍵 계단 다원 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("계단 다원 지우기 (원래 논으로)", GUILayout.Height(24))) Run(s.Remove);
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
