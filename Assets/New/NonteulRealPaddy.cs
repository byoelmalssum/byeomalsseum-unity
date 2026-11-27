// =====================================================================
//  NonteulRealPaddy.cs — 진짜 논 + 노을 하늘 + 길 막는 덤불 치우기 (URP)
//
//  지금까지 만든 스크립트는 하나도 건드리지 않고, 「공간」에 이 스크립트만 더 붙여요.
//  [만들기]를 누르면:
//   1) 글자 칸 논 : 계단 다원의 차나무·물논·풀을 숨기고(지우지 않아요), 그 자리를 진짜 논으로
//                   - 물 댄 논: 물에 비친 하늘 + 줄 맞춰 심은 어린 벼(모)
//                   - 익은 논: 황금빛으로 고개 숙인 벼 이삭이 빽빽하게
//                   - 넓은 칸은 작은 논두렁으로 나눠요. 자모 둑 둘레에는 풀
//   2) 마을 논   : 마을 길·집·나무를 피해서 빈 땅에 논배미를 만들어요 (논두렁 + 물/벼)
//                   논 안의 풀숲·들꽃은 걷어내요
//   3) 덤불 치우기: 마을 길·집 앞길·계단 아래 길을 막는 덤불을 걷어내요
//   4) 노을 하늘 : 낮게 뜬 주황빛 해, 노을빛 하늘·안개·빛, 붉게 물든 구름
//  [지우기]를 누르면 전부 원래대로 돌아가요 (숨긴 것은 다시 보이고, 걷어낸 덤불·풀도 돌아와요).
//  만든 파일은 Assets/논틀밭틀_생성/노을논 폴더에 저장돼요 (파일 하나 50MB 안 넘게).
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulRealPaddy : MonoBehaviour
{
    [Header("논")]
    [Tooltip("글자 칸 중 익은 벼로 채울 비율 (0이면 전부 키 작은 어린 벼 — 자모가 가장 또렷)")] [Range(0f, 1f)] public float ripeRatio = 0.6f;
    [Tooltip("익은 벼 키 (m) — 자모 둑이 잘 보이게 둑보다 낮게")] [Range(0.3f, 1.0f)] public float ripeHeight = 0.6f;
    [Tooltip("벼 포기 간격 (클수록 가볍고 성겨요)")] [Range(0.3f, 0.7f)] public float riceSpacing = 0.38f;
    [Tooltip("마을에 논배미 만들기")] public bool villagePaddies = true;
    [Tooltip("길에서 이 거리 안의 덤불은 치워요 (m)")] [Range(0f, 5f)] public float bushClear = 2.6f;

    [Header("노을")]
    public bool sunset = true;
    [Tooltip("해 높이 (도) — 낮을수록 붉어요")] [Range(0f, 30f)] public float sunHeight = 3f;
    [Tooltip("해가 있는 방향 (도, 0 = 북쪽(+z))")] [Range(-180f, 180f)] public float sunAzimuth = -25f;
    [Tooltip("구름 개수")] [Range(0, 40)] public int cloudCount = 18;
    [Tooltip("주황빛 진하기 (0 = 거의 없음, 1 = 진한 주황)")] [Range(0f, 1f)] public float orangeStrength = 0.5f;

    // ---- 되돌리기용 (버튼이 채워요) ----
    [HideInInspector] public List<GameObject> hiddenObjs = new List<GameObject>();
    [HideInInspector] public List<MeshFilter> cutFilters = new List<MeshFilter>();
    [HideInInspector] public List<Mesh> cutOriginals = new List<Mesh>();
    [HideInInspector] public bool skySaved;
    [HideInInspector] public Material oldSky; [HideInInspector] public Light oldSun;
    [HideInInspector] public Quaternion oldSunRot; [HideInInspector] public Color oldSunColor; [HideInInspector] public float oldSunIntensity;
    [HideInInspector] public float oldFogDensity; [HideInInspector] public Color oldFog, oldAmbSky, oldAmbEq, oldAmbGround; [HideInInspector] public AmbientMode oldAmbMode;

    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/노을논";
    const string ROOT_NAME = "논틀밭틀_진짜 논 (자동 생성)";
    const string SKY_ROOT = "논틀밭틀_노을 구름 (자동 생성)";
    const string TERRACE_ROOT = "논틀밭틀_계단 다원 (자동 생성)";
    const string VILLAGE_ROOT = "논틀밭틀_ㄹ길·마을 (자동 생성)";
    static readonly string[] TERRACE_HIDE = { "계단 땅", "차나무 줄", "계단 물논", "모", "풀" };

    System.Random rng;
    Material mGrass, mDirt, mMud, mWater, mSeed, mBlade, mRipe, mEar, mCloud;
    float lvScale = 1f; bool raised; float hillR = 37f;

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
        rng = new System.Random(2626);
        Remove();
        var tt = GetComponent<NonteulTeaTerrace>();
        raised = tt != null && tt.raiseLetters && transform.Find(TERRACE_ROOT) != null;
        lvScale = tt != null ? tt.levelScale : 1f;
        var hb = GetComponent<NonteulHillsAndBlock>(); hillR = hb != null ? hb.hillStart : 37f;

        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);
        Progress("재질 준비 중…", 0.03f);
        MakeOwnMaterials();
        DefineLayout();

        Progress("글자 칸을 진짜 논으로…", 0.1f);
        HideTerraceContents();
        BuildLetterPaddies(root);

        var road = RoadLoop(); var paths = VillagePaths(road);
        var cells = new List<Rect>();
        if (villagePaddies && transform.Find(VILLAGE_ROOT) != null)
        {
            Progress("마을 논배미 만드는 중…", 0.45f);
            cells = PlanVillageCells(road, paths);
            BuildVillagePaddies(root, cells);
        }
        Progress("길 막는 덤불·논 속 풀 걷어내는 중…", 0.75f);
        ClearVillage(road, paths, cells);

        if (sunset) { Progress("노을 하늘 칠하는 중…", 0.85f); ApplySunset(); }

        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 진짜 논·노을] 다 만들었어요. 논배미 " + cells.Count + "곳. Ctrl+S로 저장하세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        var sky = GameObject.Find(SKY_ROOT); if (sky) DestroyNow(sky);
        foreach (var g in hiddenObjs) if (g != null) { g.SetActive(true); MarkDirty(g); }
        hiddenObjs.Clear();
        for (int i = 0; i < cutFilters.Count && i < cutOriginals.Count; i++)
            if (cutFilters[i] != null && cutOriginals[i] != null) { cutFilters[i].sharedMesh = cutOriginals[i]; MarkDirty(cutFilters[i]); }
        cutFilters.Clear(); cutOriginals.Clear();
        RestoreSky();
        MarkDirty(this); MarkDirty(gameObject);
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string n) => AssetDatabase.LoadAssetAtPath<Material>(SUB + "/" + n + ".mat");
        mGrass = L("고퀄_풀_1.00"); mDirt = L("고퀄_흙_1.00"); mMud = L("고퀄_진흙_1.00"); mWater = L("고퀄_물");
        mSeed = L("잎_모"); mBlade = L("잎_풀");
        if (mGrass == null || mDirt == null || mWater == null || mBlade == null)
        {
            Debug.LogError("[논틀밭틀 진짜 논] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요.");
            return false;
        }
        if (mMud == null) mMud = mDirt; if (mSeed == null) mSeed = mBlade;
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  배치 (계단 다원과 같은 숫자)
    // ==================================================================
    static readonly float[,] FINAL = {
        {-10f,-8f,0f,12f,18f,.8f},{-10f,-7.5f,0f,3.2f,2.4f,.45f},{-10f,-8f,180f,12f,18f,.8f},{9f,-12f,90f,10f,8f,.6f},
        {-4.3f,4.5f,90f,6f,1f,.75f},{0f,-11.5f,0f,5f,11f,1f},{-11f,9f,0f,9f,12f,.75f},{5.5f,17f,0f,3.5f,8f,.7f},
        {9f,-1f,90f,10f,8f,.6f},{9f,10f,90f,10f,8f,.6f},{-4.3f,11f,90f,6f,1f,.75f},{0f,.4f,0f,5f,11f,1f}};
    const string JAMO = "ㄴㅗㄴㅌㅡㄹㅂㅏㅌㅌㅡㄹ";
    static readonly float[,] ZONES = {
        {-16.6f, -3.7f, -17.6f, 1.25f, 4.5f}, {-16.1f, -3.7f, 1.25f, 15.6f, 6.5f}, {-3.7f, 4.6f, -17.6f, -5.55f, 1.5f},
        {-3.7f, 4.6f, -5.55f, 6.45f, 3.0f}, {-3.7f, 4.6f, 6.45f, 21.6f, 4.0f}, {4.6f, 13.6f, 15.05f, 21.6f, 4.0f},
        {4.6f, 13.6f, -17.6f, -6.5f, 2.5f}, {4.6f, 13.6f, -6.5f, 4.5f, 4.5f}, {4.6f, 13.6f, 4.5f, 15.05f, 6.0f} };

    class Plate { public Rect r; public bool water; public float L; }
    class Stroke { public float cx, cz, hw, hd, yaw; public int j; }
    readonly List<Plate> plates = new List<Plate>();
    readonly List<Stroke> strokes = new List<Stroke>();

    int ZoneIndex(float x, float z)
    {
        if (!raised) return -1;
        for (int i = 0; i < ZONES.GetLength(0); i++) if (x >= ZONES[i, 0] && x < ZONES[i, 1] && z >= ZONES[i, 2] && z < ZONES[i, 3]) return i;
        return -1;
    }
    float ZoneLevel(float x, float z) { int i = ZoneIndex(x, z); return i < 0 ? 0f : ZONES[i, 4] * lvScale; }

    void DefineLayout()
    {
        plates.Clear(); strokes.Clear();
        void P(float x0, float x1, float z0, float z1, bool w)
        {
            var r = Rect.MinMaxRect(x0, z0, x1, z1);
            int zi = ZoneIndex(r.center.x, r.center.y);
            if (zi >= 0) r = Rect.MinMaxRect(Mathf.Max(x0, ZONES[zi, 0]), Mathf.Max(z0, ZONES[zi, 2]), Mathf.Min(x1, ZONES[zi, 1]), Mathf.Min(z1, ZONES[zi, 3]));
            plates.Add(new Plate { r = r, water = w, L = zi >= 0 ? ZONES[zi, 4] * lvScale : 0f });
        }
        P(-15.6f, -4.4f, -16.6f, 0.6f, false);
        P(-15.1f, -6.9f, 3.4f, 9f, true);
        P(-15.1f, -6.9f, 9.4f, 15f, false);
        P(6.2f, 12.5f, 14.6f, 20f, true);
        // 가운데 칸(-3.6~3.6, 7~20)은 ㄹ길·마을 스크립트의 S자 꽃길이라 그대로 둬요
        float[] zc = { -12f, -1f, 10f };
        for (int k = 0; k < 3; k++) { P(5.2f, 9f, zc[k] - 4.8f, zc[k] + 4.8f, k != 1); P(9f, 12.8f, zc[k] - 4.8f, zc[k] + 4.8f, k == 1); }
        for (int j = 0; j < 12; j++)
        {
            float X = FINAL[j, 0], Z = FINAL[j, 1], yaw = FINAL[j, 2] * Mathf.Deg2Rad, fw = FINAL[j, 3], fh = FINAL[j, 4], T = FINAL[j, 5];
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            foreach (var st in Jamo2D(JAMO[j], -fw / 2, fw / 2, -fh / 2, fh / 2, T))
                strokes.Add(new Stroke { cx = X + c * st.x + s * st.y, cz = Z - s * st.x + c * st.y, hw = st.z / 2, hd = st.w / 2, yaw = yaw, j = j });
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
    static float StrokeSdf(Stroke r, float x, float z)
    {
        float dx = x - r.cx, dz = z - r.cz, c = Mathf.Cos(r.yaw), s = Mathf.Sin(r.yaw);
        float lx = Mathf.Abs(c * dx - s * dz) - r.hw, lz = Mathf.Abs(s * dx + c * dz) - r.hd;
        float ox = Mathf.Max(lx, 0), oz = Mathf.Max(lz, 0);
        return Mathf.Sqrt(ox * ox + oz * oz) + Mathf.Min(Mathf.Max(lx, lz), 0);
    }
    float NearStroke(float x, float z) { float best = 99f; foreach (var r in strokes) best = Mathf.Min(best, StrokeSdf(r, x, z)); return best; }

    // 계단 다원의 칸 내용(차나무·물논·풀)을 숨겨요 (단 돌벽·윗면은 그대로)
    void HideTerraceContents()
    {
        var tr = transform.Find(TERRACE_ROOT); if (tr == null) return;
        foreach (var n in TERRACE_HIDE)
        {
            var c = tr.Find(n);
            if (c != null && c.gameObject.activeSelf) { c.gameObject.SetActive(false); hiddenObjs.Add(c.gameObject); MarkDirty(c.gameObject); }
        }
    }

    // ==================================================================
    //  1) 글자 칸 진짜 논
    // ==================================================================
    void BuildLetterPaddies(Transform root)
    {
        var holder = new GameObject("글자 칸 논").transform; holder.SetParent(root, false);
        var k = new Kit();
        int idx = 0;
        foreach (var p in plates)
        {
            idx++;
            bool ripe = rng.NextDouble() < (p.water ? ripeRatio * 0.4f : ripeRatio);
            var r = p.r;
            // 긴 쪽으로 3.5m마다 작은 논두렁
            bool alongX = r.width > r.height; float len = alongX ? r.width : r.height;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 3.6f));
            for (int i = 0; i < n; i++)
            {
                Rect sub = alongX ? Rect.MinMaxRect(Mathf.Lerp(r.xMin, r.xMax, i / (float)n), r.yMin, Mathf.Lerp(r.xMin, r.xMax, (i + 1f) / n), r.yMax)
                                  : Rect.MinMaxRect(r.xMin, Mathf.Lerp(r.yMin, r.yMax, i / (float)n), r.xMax, Mathf.Lerp(r.yMin, r.yMax, (i + 1f) / n));
                bool subRipe = ripe ? rng.NextDouble() > 0.15 : rng.NextDouble() < ripeRatio * 0.2;
                Paddy(k, sub, p.L, !subRipe, subRipe, true, alongX);
                if (i > 0)
                {
                    if (alongX) Dike(k, new Vector2(sub.xMin, sub.yMin + 0.05f), new Vector2(sub.xMin, sub.yMax - 0.05f), p.L);
                    else Dike(k, new Vector2(sub.xMin + 0.05f, sub.yMin), new Vector2(sub.xMax - 0.05f, sub.yMin), p.L);
                }
            }
        }
        // 자모 둑 둘레 풀 (논과 둑 사이를 부드럽게)
        foreach (var r in strokes)
        {
            float per = 4f * (r.hw + r.hd); int n = Mathf.CeilToInt(per / 0.4f);
            for (int i = 0; i < n; i++)
            {
                var o = Perim(r, i / (float)n, 0.12f);
                float L = ZoneLevel(o.x, o.y);
                if (NearStroke(o.x, o.y) < 0.05f) continue;
                k.NextGrass(); Ribbons(k.grass, new Vector3(o.x, L + 0.02f, o.y), R(0.2f, 0.45f), 6, 0.15f, 0.6f, 0.02f);
            }
        }
        k.Emit(this, holder);
    }

    static Vector2 Perim(Stroke r, float t, float off)
    {
        float W = r.hw + off, D = r.hd + off, per = 4 * (W + D), s = Mathf.Repeat(t, 1f) * per, lx, lz;
        if (s < 2 * W) { lx = -W + s; lz = -D; }
        else if ((s -= 2 * W) < 2 * D) { lx = W; lz = -D + s; }
        else if ((s -= 2 * D) < 2 * W) { lx = W - s; lz = D; }
        else { s -= 2 * W; lx = -W; lz = D - s; }
        float c = Mathf.Cos(r.yaw), sn = Mathf.Sin(r.yaw);
        return new Vector2(r.cx + c * lx + sn * lz, r.cz - sn * lx + c * lz);
    }

    // 만든 것들을 재질별로 모아 두는 상자 (풀·벼는 6만 꼭짓점마다 나눠 저장)
    class Kit
    {
        public MB mud = new MB(), water = new MB(), dike = new MB(), ear = new MB();
        public List<MB> young = new List<MB> { new MB() }, ripe = new List<MB> { new MB() }, grassL = new List<MB> { new MB() };
        public MB seedCur => young[young.Count - 1]; public MB ripeCur => ripe[ripe.Count - 1]; public MB grass => grassL[grassL.Count - 1];
        public void NextYoung() { if (seedCur.v.Count > 60000) young.Add(new MB()); }
        public void NextRipe() { if (ripeCur.v.Count > 60000) ripe.Add(new MB()); }
        public void NextGrass() { if (grass.v.Count > 60000) grassL.Add(new MB()); }
        public void Emit(NonteulRealPaddy o, Transform h)
        {
            o.Emit(h, "논바닥 진흙", mud, o.mMud, ShadowCastingMode.Off, true);
            o.Emit(h, "논물", water, o.mWater, ShadowCastingMode.Off, false);
            o.Emit(h, "논두렁", dike, o.mGrass, ShadowCastingMode.On, true);
            for (int i = 0; i < young.Count; i++) o.Emit(h, "어린 벼 " + (i + 1), young[i], o.mSeed, ShadowCastingMode.Off, false);
            for (int i = 0; i < ripe.Count; i++) o.Emit(h, "익은 벼 " + (i + 1), ripe[i], o.mRipe, ShadowCastingMode.Off, false);
            o.Emit(h, "벼 이삭", ear, o.mEar, ShadowCastingMode.Off, false);
            for (int i = 0; i < grassL.Count; i++) o.Emit(h, "논가 풀 " + (i + 1), grassL[i], o.mBlade, ShadowCastingMode.Off, false);
        }
    }

    // 논 한 배미: 진흙 바닥 + (물) + 줄 맞춘 벼
    void Paddy(Kit k, Rect r, float L, bool wet, bool ripe, bool avoidJamo, bool rowsAlongX)
    {
        var a = new Vector3(r.xMin, L + 0.012f, r.yMin); var b = new Vector3(r.xMax, L + 0.012f, r.yMin); var c = new Vector3(r.xMax, L + 0.012f, r.yMax); var d = new Vector3(r.xMin, L + 0.012f, r.yMax);
        Quad(k.mud, a, b, c, d, 1f);
        if (wet) { float wy = L + 0.06f; Quad(k.water, new Vector3(a.x, wy, a.z), new Vector3(b.x, wy, b.z), new Vector3(c.x, wy, c.z), new Vector3(d.x, wy, d.z), 3f); }
        float sx = rowsAlongX ? riceSpacing * 0.82f : riceSpacing, sz = rowsAlongX ? riceSpacing : riceSpacing * 0.82f;
        for (float x = r.xMin + sx * 0.6f; x < r.xMax - sx * 0.4f; x += sx)
            for (float z = r.yMin + sz * 0.6f; z < r.yMax - sz * 0.4f; z += sz)
            {
                float X = x + R(-0.03f, 0.03f), Z = z + R(-0.03f, 0.03f);
                if (avoidJamo && NearStroke(X, Z) < 0.45f) continue;   // 자모 둑 옆은 한 뼘 비워서 글자 선이 보이게
                if (rng.NextDouble() < 0.04) continue;                     // 군데군데 빈 포기
                var pos = new Vector3(X, L + 0.012f, Z);
                if (ripe) { k.NextRipe(); RipeClump(k.ripeCur, k.ear, pos); }
                else { k.NextYoung(); Ribbons(k.seedCur, pos, R(0.24f, 0.4f), 5, 0.06f, 0.35f, 0.013f); }
            }
    }

    // 익은 벼: 키 큰 잎 + 고개 숙인 황금 이삭
    void RipeClump(MB blades, MB ears, Vector3 pos)
    {
        float h = ripeHeight * R(0.85f, 1.1f);
        Ribbons(blades, pos, h, 6, 0.08f, 0.45f, 0.016f);
        int n = 2 + rng.Next(2);
        for (int i = 0; i < n; i++)
        {
            float dir = R(0, 6.283f); var d = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir)); var side = new Vector3(-d.z, 0, d.x);
            var top = pos + new Vector3(R(-0.03f, 0.03f), h * R(0.92f, 1.05f), R(-0.03f, 0.03f));
            const int seg = 4; int k0 = ears.v.Count; float len = R(0.18f, 0.26f);
            for (int s = 0; s <= seg; s++)
            {
                float t = s / (float)seg;
                var p = top + d * (len * Mathf.Sin(t * 1.4f)) - Vector3.up * (len * t * t * 0.9f);
                float w = 0.018f * (1f - 0.6f * t) + 0.004f;
                var nrm = (Vector3.up + d * 0.3f).normalized;
                ears.Add(p - side * w, nrm, new Vector2(0, t)); ears.Add(p + side * w, nrm, new Vector2(1, t));
            }
            for (int s = 0; s < seg; s++) { int a = k0 + s * 2; ears.Tri(a, a + 1, a + 3); ears.Tri(a, a + 3, a + 2); }
        }
    }

    // 작은 논두렁 (풀 덮인 흙둑)
    void Dike(Kit k, Vector2 a, Vector2 b, float L)
    {
        var dir = (b - a); float len = dir.magnitude; if (len < 0.1f) return; dir /= len; var n = new Vector2(-dir.y, dir.x);
        const float W = 0.17f, H = 0.2f;
        int seg = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f)); int k0 = k.dike.v.Count;
        for (int i = 0; i <= seg; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)seg); float u = len * i / seg;
            float hh = H * (0.9f + 0.2f * Noise(u * 1.3f, L));
            k.dike.Add(new Vector3(p.x + n.x * W * 1.6f, L, p.y + n.y * W * 1.6f), new Vector3(n.x, 0.6f, n.y).normalized, new Vector2(u, 0));
            k.dike.Add(new Vector3(p.x + n.x * W * 0.6f, L + hh, p.y + n.y * W * 0.6f), Vector3.up, new Vector2(u, 0.3f));
            k.dike.Add(new Vector3(p.x - n.x * W * 0.6f, L + hh, p.y - n.y * W * 0.6f), Vector3.up, new Vector2(u, 0.6f));
            k.dike.Add(new Vector3(p.x - n.x * W * 1.6f, L, p.y - n.y * W * 1.6f), new Vector3(-n.x, 0.6f, -n.y).normalized, new Vector2(u, 0.9f));
            if (rng.NextDouble() < 0.7) { k.NextGrass(); Ribbons(k.grass, new Vector3(p.x + n.x * R(-W, W), L + hh - 0.02f, p.y + n.y * R(-W, W)), R(0.15f, 0.35f), 5, 0.12f, 0.7f, 0.016f); }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < 3; j++) { int q = k0 + i * 4 + j, w = q + 4; k.dike.Tri(q, w, q + 1); k.dike.Tri(w, w + 1, q + 1); }
    }

    static void Quad(MB m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float tile)
    {
        int k = m.v.Count;
        foreach (var p in new[] { a, b, c, d }) m.Add(p, Vector3.up, new Vector2(p.x, p.z) / tile);
        m.Tri(k, k + 1, k + 2); m.Tri(k, k + 2, k + 3);
    }

    // ==================================================================
    //  2) 마을 논배미
    // ==================================================================
    static readonly Vector2[] HOUSES = { new Vector2(24.7f, 18.1f), new Vector2(-19.5f, 30.8f), new Vector2(-32.0f, 15.0f), new Vector2(-30.2f, 0.4f), new Vector2(-31.3f, -14.2f), new Vector2(25.0f, -6.7f) };
    static readonly Rect LETTERS = Rect.MinMaxRect(-16.6f, -17.6f, 13.6f, 21.6f);

    List<Vector2> RoadLoop()
    {
        var o = new List<Vector2>(); const int N = 900;
        for (int i = 0; i <= N; i++)
        {
            float th = i / (float)N * Mathf.PI * 2f, c = Mathf.Cos(th), s = Mathf.Sin(th), n = 3.5f;
            float r = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(c / 21.5f), n) + Mathf.Pow(Mathf.Abs(s / 26f), n), 1f / n);
            r += 1.8f * Mathf.Sin(5f * th + 0.7f) + 0.8f * Mathf.Sin(9f * th);
            o.Add(new Vector2(-1.5f + c * r, 3f + s * r));
        }
        return o;
    }
    // 집 앞길 · 계단 아래 길 · 옛 꼬불길 두 개 (점 목록)
    List<Vector2> VillagePaths(List<Vector2> road)
    {
        var o = new List<Vector2>();
        void Seg(Vector2 a, Vector2 b) { int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.4f); for (int i = 0; i <= n; i++) o.Add(Vector2.Lerp(a, b, i / (float)n)); }
        foreach (var h in HOUSES)
        {
            var dir = (new Vector2(-1.5f, 3f) - h).normalized; var gate = h + dir * 6f;
            Seg(h + dir * 3.6f, gate); Seg(gate, Nearest(road, gate));
        }
        Seg(new Vector2(-1.9f, 21.6f), new Vector2(-1.9f, 26.4f)); Seg(new Vector2(-1.9f, 26.4f), Nearest(road, new Vector2(-1.6f, 30f)));
        var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
        var b = new[] { new Vector3(7.4f, 0, 17), new Vector3(12, 0, 22), new Vector3(8, 0, 29), new Vector3(16, 0, 36), new Vector3(10, 0, 44), new Vector3(20, 0, 51), new Vector3(13, 0, 59), new Vector3(22, 0, 69) };
        var la = new List<Vector3>(); Spline(a, 0.8f, la); var lb = new List<Vector3>(); Spline(b, 0.8f, lb);
        foreach (var q in la) o.Add(new Vector2(q.x, q.z)); foreach (var q in lb) o.Add(new Vector2(q.x, q.z));
        return o;
    }
    static Vector2 Nearest(List<Vector2> line, Vector2 p) { Vector2 best = line[0]; float bd = float.MaxValue; foreach (var q in line) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = q; } } return best; }
    static float DistTo(List<Vector2> line, Vector2 p) { float bd = float.MaxValue; foreach (var q in line) { float d = (q - p).sqrMagnitude; if (d < bd) bd = d; } return Mathf.Sqrt(bd); }
    float HillClear(Vector2 p)
    {
        float dx = p.x + 4f, dz = p.y - 3f, r = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx), c = Mathf.Cos(a), s = Mathf.Sin(a);
        return hillR / Mathf.Pow(Mathf.Pow(Mathf.Abs(c), 3f) + Mathf.Pow(Mathf.Abs(s), 3f), 1f / 3f) - 3f - r;
    }

    // 이미 심어진 나무 줄기 자리 (마을 숲 메시에서 읽어요)
    List<Vector2> TreeSpots()
    {
        var o = new List<Vector2>();
        var vr = transform.Find(VILLAGE_ROOT); if (vr == null) return o;
        foreach (var mf in vr.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.Contains("줄기") || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var toLocal = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var seen = new HashSet<long>();
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = toLocal.MultiplyPoint3x4(v); if (p.y > 0.3f) continue;
                long key = ((long)Mathf.FloorToInt(p.x) << 32) ^ (uint)Mathf.FloorToInt(p.z);
                if (seen.Add(key)) o.Add(new Vector2(p.x, p.z));
            }
        }
        return o;
    }

    List<Rect> PlanVillageCells(List<Vector2> road, List<Vector2> paths)
    {
        var trees = TreeSpots(); var cells = new List<Rect>();
        const float CW = 5.6f, CH = 3.9f, GX = 0.45f, GZ = 0.45f;
        for (float z = -40f; z < 46f; z += CH + GZ)
        {
            float shift = (Mathf.RoundToInt(z) % 2 == 0) ? 0f : CW * 0.5f;
            for (float x = -46f + shift; x < 40f; x += CW + GX)
            {
                var r = new Rect(x, z, CW, CH);
                if (r.Overlaps(Rect.MinMaxRect(LETTERS.xMin - 3f, LETTERS.yMin - 3f, LETTERS.xMax + 3f, LETTERS.yMax + 3f))) continue;
                bool ok = true;
                for (int i = 0; i <= 2 && ok; i++)
                    for (int j = 0; j <= 2 && ok; j++)
                    {
                        var p = new Vector2(r.xMin + r.width * i / 2f, r.yMin + r.height * j / 2f);
                        if (HillClear(p) < 1.0f) ok = false;
                        else if (DistTo(road, p) < 1.9f) ok = false;
                        else if (DistTo(paths, p) < 1.6f) ok = false;
                        else foreach (var h in HOUSES) if ((h - p).sqrMagnitude < 8.6f * 8.6f) { ok = false; break; }
                    }
                if (!ok) continue;
                foreach (var t in trees) if (t.x > r.xMin - 0.9f && t.x < r.xMax + 0.9f && t.y > r.yMin - 0.9f && t.y < r.yMax + 0.9f) { ok = false; break; }
                if (ok) cells.Add(r);
            }
        }
        return cells;
    }

    void BuildVillagePaddies(Transform root, List<Rect> cells)
    {
        var holder = new GameObject("마을 논배미").transform; holder.SetParent(root, false);
        var k = new Kit();
        foreach (var r in cells)
        {
            float patch = Fbm(r.center.x * 0.05f + 7f, r.center.y * 0.05f);
            bool ripe = patch > 0.5f ? rng.NextDouble() < 0.85 : rng.NextDouble() < 0.2;
            var inner = Rect.MinMaxRect(r.xMin + 0.25f, r.yMin + 0.25f, r.xMax - 0.25f, r.yMax - 0.25f);
            Paddy(k, inner, 0f, !ripe || rng.NextDouble() < 0.3, ripe, false, true);
            Dike(k, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), 0f);
            Dike(k, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), 0f);
            Dike(k, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), 0f);
            Dike(k, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), 0f);
        }
        k.Emit(this, holder);
    }

    // ==================================================================
    //  3) 길 막는 덤불 · 논 속 풀/들꽃 걷어내기 (원래 메시는 그대로, 자른 복사본으로 바꿔 끼워요)
    // ==================================================================
    void ClearVillage(List<Vector2> road, List<Vector2> paths, List<Rect> cells)
    {
        var vr = transform.Find(VILLAGE_ROOT); if (vr == null) return;
        bool InCell(Vector2 p) { foreach (var c in cells) if (p.x > c.xMin - 0.15f && p.x < c.xMax + 0.15f && p.y > c.yMin - 0.15f && p.y < c.yMax + 0.15f) return true; return false; }
        foreach (var mf in vr.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.name;
            if (n == "덤불") CutGroups(mf, 88, p => InCell(p) || DistTo(road, p) < bushClear || DistTo(paths, p) < bushClear);
            else if (n.StartsWith("풀숲")) CutGroups(mf, 8, p => InCell(p) || DistTo(paths, p) < 0.7f);
            else if (n == "들꽃") CutGroups(mf, 12, InCell);
        }
    }

    // 같은 크기 덩어리(풀잎 한 장, 덤불 한 덩이…)마다 가운데를 보고 지울지 정해요
    void CutGroups(MeshFilter mf, int group, System.Func<Vector2, bool> remove)
    {
        var src = mf.sharedMesh; if (src == null || !src.isReadable || src.vertexCount % group != 0) return;
        var toLocal = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = src.vertices; int groups = v.Length / group; var kill = new bool[groups]; bool any = false;
        for (int g = 0; g < groups; g++)
        {
            Vector3 c = Vector3.zero; for (int i = 0; i < group; i++) c += v[g * group + i]; c /= group;
            var p = toLocal.MultiplyPoint3x4(c);
            if (remove(new Vector2(p.x, p.z))) { kill[g] = true; any = true; }
        }
        if (!any) return;
        var cut = Object.Instantiate(src); cut.name = src.name + "_걷어냄";
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s); var keep = new List<int>(t.Length);
            for (int i = 0; i < t.Length; i += 3) if (!kill[t[i] / group]) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); }
            cut.SetTriangles(keep, s);
        }
        SaveAsset(cut, "메시_노을논_걷어냄_" + mf.transform.parent.name + "_" + mf.name + ".asset");
        cutFilters.Add(mf); cutOriginals.Add(src);
        mf.sharedMesh = cut; MarkDirty(mf);
    }

    // ==================================================================
    //  4) 노을 하늘
    // ==================================================================
    void ApplySunset()
    {
        Light sun = RenderSettings.sun;
        if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (!skySaved)
        {
            oldSky = RenderSettings.skybox; oldSun = sun; oldFog = RenderSettings.fogColor; oldFogDensity = RenderSettings.fogDensity; oldAmbMode = RenderSettings.ambientMode;
            oldAmbSky = RenderSettings.ambientSkyColor; oldAmbEq = RenderSettings.ambientEquatorColor; oldAmbGround = RenderSettings.ambientGroundColor;
            if (sun != null) { oldSunRot = sun.transform.rotation; oldSunColor = sun.color; oldSunIntensity = sun.intensity; }
            skySaved = true;
        }
        // 해: 낮게, 북서쪽 하늘
        if (sun != null)
        {
#if UNITY_EDITOR
            Undo.RecordObject(sun, "노을"); Undo.RecordObject(sun.transform, "노을");
#endif
            float el = sunHeight * Mathf.Deg2Rad, az = (sunAzimuth) * Mathf.Deg2Rad;
            var toSun = transform.TransformDirection(new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el)));
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            sun.color = Soft(new Color(1f, 0.64f, 0.42f), orangeStrength, new Color(1f, 0.95f, 0.9f)); sun.intensity = 1.05f;
            RenderSettings.sun = sun; MarkDirty(sun); MarkDirty(sun.transform);
        }
        // 하늘: 노을 그라데이션을 직접 그려서 파노라마 하늘로 (해 쪽 지평선이 가장 붉어요)
        {
            float el = sunHeight * Mathf.Deg2Rad, az = sunAzimuth * Mathf.Deg2Rad;
            var toSunW = transform.TransformDirection(new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el))).normalized;
            var pano = Shader.Find("Skybox/Panoramic");
            if (pano != null)
            {
                var tex = MakeSkyTex(toSunW, orangeStrength); tex.name = "노을하늘_파노라마"; SaveAsset(tex, "노을하늘_파노라마.asset");
                var sky = GetMat("노을_하늘", pano);
                sky.SetTexture("_MainTex", tex); sky.SetFloat("_Rotation", 0f); sky.SetFloat("_Exposure", 1.0f);
                if (sky.HasProperty("_Mapping")) sky.SetFloat("_Mapping", 1f);
                if (sky.HasProperty("_ImageType")) sky.SetFloat("_ImageType", 0f);
                if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
                MarkDirty(sky);
                RenderSettings.skybox = sky;
            }
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // 맑은 노을: 위 하늘은 차분한 잿빛 하늘색, 공기는 맑게 (안개를 옅고 중성색으로)
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.52f, 0.56f);
        RenderSettings.ambientEquatorColor = Soft(new Color(0.7f, 0.52f, 0.4f), orangeStrength, new Color(0.62f, 0.6f, 0.58f));
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.15f, 0.13f);
        RenderSettings.fogColor = Soft(new Color(0.62f, 0.52f, 0.46f), orangeStrength, new Color(0.62f, 0.6f, 0.6f));
        RenderSettings.fogDensity = Mathf.Min(oldFogDensity > 0 ? oldFogDensity : 0.0036f, 0.002f);
        DynamicGI.UpdateEnvironment();

        // 붉게 물든 구름 (씬 맨 위에 둬서 이동 범위·충돌에 안 섞여요)
        var holder = new GameObject(SKY_ROOT).transform;
        holder.SetPositionAndRotation(transform.position, transform.rotation);
        if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder.gameObject, gameObject.scene);
        var mb = new MB();
        for (int i = 0; i < cloudCount; i++)
        {
            float a = (sunAzimuth + R(-80f, 80f)) * Mathf.Deg2Rad, dist = R(180f, 240f), y = R(60f, 95f);
            var c = new Vector3(Mathf.Sin(a) * dist, y, Mathf.Cos(a) * dist);
            var face = (-new Vector3(c.x, 0, c.z)).normalized; var right = new Vector3(face.z, 0, -face.x);
            float w = R(60f, 120f), h = w * R(0.12f, 0.2f); int k = mb.v.Count; int tile = rng.Next(4);
            float u0 = (tile % 2) * 0.5f, v0 = (tile / 2) * 0.5f;
            mb.Add(c - right * w / 2 - Vector3.up * h / 2, face, new Vector2(u0, v0)); mb.Add(c + right * w / 2 - Vector3.up * h / 2, face, new Vector2(u0 + 0.5f, v0));
            mb.Add(c + right * w / 2 + Vector3.up * h / 2, face, new Vector2(u0 + 0.5f, v0 + 0.5f)); mb.Add(c - right * w / 2 + Vector3.up * h / 2, face, new Vector2(u0, v0 + 0.5f));
            mb.Tri(k, k + 1, k + 2); mb.Tri(k, k + 2, k + 3);
        }
        var mesh = mb.ToMesh("노을 구름", false); SaveAsset(mesh, "메시_노을논_구름.asset");
        var go = new GameObject("노을 구름"); go.transform.SetParent(holder, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mCloud; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    void RestoreSky()
    {
        if (!skySaved) return;
        RenderSettings.skybox = oldSky; RenderSettings.fogColor = oldFog; RenderSettings.fogDensity = oldFogDensity; RenderSettings.ambientMode = oldAmbMode;
        RenderSettings.ambientSkyColor = oldAmbSky; RenderSettings.ambientEquatorColor = oldAmbEq; RenderSettings.ambientGroundColor = oldAmbGround;
        if (oldSun != null) { oldSun.transform.rotation = oldSunRot; oldSun.color = oldSunColor; oldSun.intensity = oldSunIntensity; MarkDirty(oldSun); MarkDirty(oldSun.transform); }
        skySaved = false;
    }

    // ==================================================================
    //  재질
    // ==================================================================
    void MakeOwnMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard");
        mRipe = GetMat("잎_익은벼", lit); SetLit(mRipe, new Color(0.78f, 0.6f, 0.26f), 0.15f, true);   // 이름이 '잎_'으로 시작해야 충돌 검사에서 빠져요
        mEar = GetMat("잎_벼이삭", lit); SetLit(mEar, new Color(0.95f, 0.74f, 0.3f), 0.25f, true);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit"); if (unlit == null) unlit = Shader.Find("Unlit/Transparent");
        mCloud = GetMat("빛없는_노을구름", unlit);
        var tex = GetTex("노을구름_색_옅은노을", MakeCloudTex);
        if (mCloud.HasProperty("_BaseMap")) mCloud.SetTexture("_BaseMap", tex);
        if (mCloud.HasProperty("_MainTex")) mCloud.SetTexture("_MainTex", tex);
        if (mCloud.HasProperty("_BaseColor")) mCloud.SetColor("_BaseColor", Color.white);
        if (mCloud.HasProperty("_Surface"))
        {
            mCloud.SetFloat("_Surface", 1f); mCloud.SetFloat("_Blend", 0f);
            mCloud.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mCloud.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mCloud.SetFloat("_ZWrite", 0f); mCloud.SetFloat("_Cull", 0f);
            mCloud.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mCloud.SetOverrideTag("RenderType", "Transparent");
            mCloud.renderQueue = (int)RenderQueue.Transparent;
        }
        MarkDirty(mCloud);
    }
    void SetLit(Material m, Color c, float smooth, bool twoSided)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (twoSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        m.enableInstancing = true; MarkDirty(m);
    }
    Material GetMat(string name, Shader sh)
    {
        Material m = null;
#if UNITY_EDITOR
        m = AssetDatabase.LoadAssetAtPath<Material>(OUT + "/" + name + ".mat");
#endif
        if (m == null || m.shader != sh) { if (m == null) { m = new Material(sh) { name = name }; SaveAsset(m, name + ".mat"); } else m.shader = sh; }
        return m;
    }
    Texture2D GetTex(string name, System.Func<Texture2D> make)
    {
#if UNITY_EDITOR
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(OUT + "/" + name + ".asset"); if (have != null) return have;
#endif
        var t = make(); t.name = name; SaveAsset(t, name + ".asset"); return t;
    }
    // 주황빛 옅게: 색을 밝은 중성색 쪽으로 섞어요
    static Color Soft(Color c, float strength, Color neutral) => Color.Lerp(neutral, c, Mathf.Clamp01(strength));

    // 노을 하늘 그림 (위도·경도 펼침): 위는 짙은 푸른 보라 → 분홍 → 지평선 주황, 해 쪽은 더 밝고 붉게
    static Texture2D MakeSkyTex(Vector3 toSun, float k)
    {
        const int W = 1024, H = 512; var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        // 참고 사진처럼: 지평선 근처는 진한 주황~붉은 띠, 위로 갈수록 노란 살구색 → 맑은 잿빛 하늘
        // (마을이 언덕에 둘러싸여 있어서 주황 띠를 조금 높게(약 30°까지) 올려 둬요)
        Color pale = new Color(0.9f, 0.86f, 0.82f);
        Color deep = Soft(new Color(0.98f, 0.40f, 0.16f), k, new Color(0.95f, 0.78f, 0.66f)), orange = Soft(new Color(1f, 0.56f, 0.24f), k, pale),
              peach = Soft(new Color(0.96f, 0.72f, 0.44f), k, pale), grey = new Color(0.66f, 0.66f, 0.64f), top = new Color(0.5f, 0.55f, 0.6f), ground = new Color(0.12f, 0.1f, 0.1f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float lon = (0.5f - u) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
                var d = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                float e = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
                float toward = Mathf.Clamp01(Vector3.Dot(new Vector3(d.x, 0, d.z).normalized, new Vector3(toSun.x, 0, toSun.z).normalized) * 0.5f + 0.5f);
                float lift = Mathf.Lerp(0.75f, 1.15f, toward);          // 해 쪽이 주황 띠가 더 높고 진해요
                Color c;
                if (e < 0f) c = Color.Lerp(deep * 0.7f, ground, Smooth01(-e / 8f));
                else if (e < 16f * lift) c = Color.Lerp(deep, orange, e / (16f * lift));
                else if (e < 30f * lift) c = Color.Lerp(orange, peach, (e - 16f * lift) / (14f * lift));
                else if (e < 52f) c = Color.Lerp(peach, grey, Smooth01((e - 30f * lift) / (52f - 30f * lift)));
                else c = Color.Lerp(grey, top, Smooth01((e - 52f) / 38f));
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, toSun), -1f, 1f));
                c += new Color(1f, 0.6f, 0.3f) * (0.25f * k * Mathf.Exp(-ang * ang / (2f * 0.45f * 0.45f)));   // 해가 진 자리의 은은한 빛
                // 지평선 위 얇게 깔린 잿빛 구름 띠
                // (가로로 한 바퀴 돌면 끝과 시작이 딱 이어지는 무늬라서 하늘에 세로 이음새가 안 생겨요)
                float band = Smooth01(1f - Mathf.Abs(e - 20f) / 10f) * Smooth01((FbmLoop(u * 24f, v * 140f, 24) - 0.5f) / 0.14f);
                c = Color.Lerp(c, Color.Lerp(new Color(0.62f, 0.48f, 0.46f), deep, 0.25f), band * 0.45f);
                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                px[y * W + x] = c;
            }
        t.SetPixels(px); t.Apply(false); return t;
    }

    // 구름 그림 4장 (2×2): 아래는 주황, 위는 분홍·보라, 가장자리는 투명
    static Texture2D MakeCloudTex()
    {
        const int S = 512; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int tile = (x / 256) + (y / 256) * 2; float u = (x % 256) / 256f, v = (y % 256) / 256f;
                float dx = (u - 0.5f) * 2f, dy = (v - 0.45f) * 2.6f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(u * 5f + tile * 13f, v * 5f + tile * 7f), n2 = Fbm(u * 11f + tile * 3f, v * 11f);
                float a = Mathf.Clamp01((n * 1.3f + n2 * 0.4f - 0.45f - r * 0.75f) * 2.2f);
                a *= Smooth01((v - 0.05f) / 0.25f);
                Color low = new Color(0.95f, 0.62f, 0.46f), high = new Color(0.42f, 0.36f, 0.38f), shade = new Color(0.3f, 0.26f, 0.29f);
                Color c = Color.Lerp(low, high, v); c = Color.Lerp(shade, c, 0.55f + 0.45f * n2);
                c.a = a * 0.85f; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }

    // ==================================================================
    //  도우미
    // ==================================================================
    void Ribbons(MB m, Vector3 pos, float h, int blades, float spread, float lean, float width)
    {
        float shade = (float)rng.NextDouble();
        for (int bl = 0; bl < blades; bl++)
        {
            float bx = R(-spread, spread) * 0.5f, bz = R(-spread, spread) * 0.5f, dir = R(0, 6.283f), bend = lean * R(0.4f, 1.3f), hh = h * R(0.6f, 1.1f), w0 = width * R(0.7f, 1.2f);
            var d = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir)); var side = new Vector3(-d.z, 0, d.x);
            const int seg = 3; int k0 = m.v.Count; float u = 0.1f + 0.8f * Mathf.Repeat(shade + 0.15f * R(-1, 1), 1f);
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                var c = pos + new Vector3(bx, 0, bz) + Vector3.up * (hh * t * (1 - 0.25f * bend * t)) + d * (hh * bend * t * t * 0.6f);
                float w = w0 * Mathf.Pow(1 - t, 0.6f) + 0.0015f; var nrm = (Vector3.up * 0.6f + d * -0.4f + side * 0.2f).normalized;
                m.Add(c - side * w, nrm, new Vector2(u, t * 0.85f)); m.Add(c + side * w, nrm, new Vector2(u, t * 0.85f));
            }
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.idx.Add(a); m.idx.Add(a + 1); m.idx.Add(a + 3); m.idx.Add(a); m.idx.Add(a + 3); m.idx.Add(a + 2); }
        }
    }
    static void Spline(Vector3[] c, float step, List<Vector3> outPts)
    {
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector3 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Length - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++) { float t = k / (float)n, t2 = t * t, t3 = t2 * t; outPts.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)); }
        }
        outPts.Add(c[c.Length - 1]);
    }

    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> idx = new List<int>();
        public void Add(Vector3 p, Vector3 nrm, Vector2 u) { v.Add(p); n.Add(nrm); uv.Add(u); }
        public void Tri(int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) >= 0) { idx.Add(a); idx.Add(b); idx.Add(c); } else { idx.Add(a); idx.Add(c); idx.Add(b); }
        }
        public Mesh ToMesh(string name, bool tangents)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(idx, 0); m.RecalculateBounds();
            if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    void Emit(Transform parent, string name, MB mb, Material mat, ShadowCastingMode cast, bool tangents)
    {
        if (mb.v.Count == 0) return;
        var mesh = mb.ToMesh(name, tangents);
        SaveAsset(mesh, "메시_노을논_" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = cast; mr.receiveShadows = true;
    }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f; } }
    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
    }
    // 가로 방향으로 period 칸마다 똑같이 반복되는 노이즈 (파노라마 하늘 이음새 없애기)
    static float NoiseLoop(float x, float y, int period)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, yi), Hash(x1, yi), u), Mathf.Lerp(Hash(x0, yi + 1), Hash(x1, yi + 1), u), v);
    }
    static float FbmLoop(float x, float y, int period) { float s = 0, a = 0.5f, n = 0; for (int i = 0; i < 4; i++) { s += a * NoiseLoop(x, y, period); n += a; x *= 2f; period *= 2; y = y * 2.03f - 9.3f; a *= 0.5f; } return s / n; }
    static float Fbm(float x, float y) { float s = 0, a = 0.5f, n = 0; for (int i = 0; i < 4; i++) { s += a * Noise(x, y); n += a; x = x * 2.03f + 17.1f; y = y * 2.03f - 9.3f; a *= 0.5f; } return s / n; }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "노을논");
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
        EditorUtility.DisplayProgressBar("논틀밭틀 진짜 논·노을", msg, p);
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
[CustomEditor(typeof(NonteulRealPaddy))]
public class NonteulRealPaddyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulRealPaddy)target;
        EditorGUILayout.HelpBox("지금까지 만든 것은 건드리지 않고 더해요.\n글자 칸을 진짜 논(물 댄 논·황금 벼)으로, 마을 빈 땅에 논배미, 길 막는 덤불 치우기, 노을 하늘.\n[지우기]를 누르면 전부 원래대로 돌아가요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🌾 진짜 논 + 노을 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("진짜 논 + 노을 지우기 (원래대로)", GUILayout.Height(24))) Run(s.Remove);
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
