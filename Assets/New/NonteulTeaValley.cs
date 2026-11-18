// =====================================================================
//  NonteulTeaValley.cs — 글자를 둘러싼 '보성 녹차밭 골짜기' + 고퀄 ㄹ 꼬불길 (URP)
//
//  NonteulFieldMaker · Upgrade · WindingPath (· TeaTerrace) 위에 덧붙여요. 「공간」에 붙이세요.
//   1) 녹차밭 골짜기 : 가운데 글자 자리는 평평한 바닥 그대로 두고, 바깥으로 갈수록
//                      계단 밭이 관중석처럼 층층이 높아져요 (기본 12층, 약 13m).
//                      층마다 둥글게 다듬은 차나무 줄이 등고선을 따라 이어져요.
//                      꼬불길이 지나가는 곳은 골짜기처럼 갈라져서 길은 그대로 걸을 수 있어요.
//   2) 능선 나무      : 맨 위 능선에 나무를 띄엄띄엄 심어 하늘선을 만들어요.
//   3) ㄹ 꼬불길 고퀄  : ㄹ 둑 위에 굽은 흙길, 밟혀 짙어진 가운데, 디딤돌, 자갈, 길섶 풀·들꽃.
//                      플레이하면 ㄹ 자모가 날아와 내려앉은 뒤에 길 장식이 나타나요.
//  메시는 깃허브에 올릴 수 있게 여러 개로 나눠 저장해요 (파일 하나 50MB 넘지 않게).
//  만든 파일은 Assets/논틀밭틀_생성/다원 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulTeaValley : MonoBehaviour
{
    [Header("녹차밭 골짜기")]
    [Tooltip("한 층 높이 (m)")] [Range(0.5f, 2f)] public float stepHeight = 1.1f;
    [Tooltip("층수")] [Range(3, 18)] public int tiers = 12;
    [Tooltip("한 층 너비 (m)")] [Range(1.4f, 3.5f)] public float stepWidth = 2.0f;
    [Tooltip("층 선이 굽이치는 정도")] [Range(0f, 2f)] public float waviness = 1.0f;
    [Tooltip("차나무 줄 굵기")] [Range(0.6f, 1.4f)] public float hedgeSize = 1.0f;
    [Tooltip("능선 나무 수")] [Range(0, 120)] public int ridgeTrees = 40;

    [Header("ㄹ 꼬불길")]
    [Tooltip("ㄹ 둑 위에 고퀄 길 장식 만들기")] public bool riulPath = true;

    // 플레이 중에 ㄹ이 내려앉으면 길 장식을 보여 주기 위한 연결 (버튼이 자동으로 채워요)
    [HideInInspector] public Transform[] riulDecor = new Transform[0];
    [HideInInspector] public Byeolmalsseum.JamoPiece[] riulPieces = new Byeolmalsseum.JamoPiece[0];

    // ------------------------------------------------------------------
    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string SUB2 = "Assets/논틀밭틀_생성/꼬불";
    const string OUT = "Assets/논틀밭틀_생성/다원";
    const string ROOT_NAME = "논틀밭틀_녹차밭 골짜기 (자동 생성)";
    const string WP_ROOT = "논틀밭틀_꼬불꼬불 (자동 생성)";
    static readonly string[] WP_HIDE = { "다랑논 바닥", "다랑논 물", "다랑논 층벽", "굽은 논둑 (윗면)", "굽은 논둑 (옆면)", "벼 (멀리)", "벼 (가장자리)", "모", "굽이 나무" };
    static readonly string[] WP_PATHS = { "들어오는 꼬불길", "나가는 꼬불길" };

    const float CX = -1.3f, CZ = 2f, EA = 21f, EB = 24.5f, SEXP = 4f, R0 = 22.5f; // 골짜기 바닥(글자 자리) 모양
    const float RISER = 0.4f, BACK = 14f, LIFT = 0.02f, CELL = 0.35f;
    const float GMINX = -58f, GMAXX = 56f, GMINZ = -66f, GMAXZ = 70f;

    static readonly float[,] FINAL = {
        {-10f,-8f,0f,12f,18f,.8f,0},{-10f,-7.5f,0f,3.2f,2.4f,.45f,2},{-10f,-8f,180f,12f,18f,.8f,0},{9f,-12f,90f,10f,8f,.6f,0},
        {-4.3f,4.5f,90f,6f,1f,.75f,0},{0f,-11.5f,0f,5f,11f,1f,1},{-11f,9f,0f,9f,12f,.75f,0},{5.5f,17f,0f,3.5f,8f,.7f,1},
        {9f,-1f,90f,10f,8f,.6f,0},{9f,10f,90f,10f,8f,.6f,0},{-4.3f,11f,90f,6f,1f,.75f,0},{0f,.4f,0f,5f,11f,1f,1}};

    System.Random rng;
    readonly List<Vector2> gorgePts = new List<Vector2>(); readonly List<float> gorgeHalf = new List<float>();
    Material mGrass, mPath, mDirt, mBladeGrass, mLeaf, mLeaf2, mBark, mWild, mPebble;
    readonly Material[] mTea = new Material[3];

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
        rng = new System.Random(123);
        Remove();
        HideWindingTerraces();
        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);

        Progress("차잎 질감 준비 중…", 0.03f);
        MakeOwnMaterials();
        MakeGorges();

        Progress("계단 언덕 만드는 중…", 0.1f);
        BuildHill(root);
        Progress("차나무 줄 심는 중…", 0.45f);
        BuildHedgeRings(root);
        Progress("능선 나무 심는 중…", 0.7f);
        BuildRidgeTrees(root);
        if (riulPath) { Progress("ㄹ 꼬불길 꾸미는 중…", 0.82f); BuildRiul(root); }

        Progress("저장하는 중…", 0.96f);
        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 녹차밭 골짜기] 다 만들었어요. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        riulDecor = new Transform[0]; riulPieces = new Byeolmalsseum.JamoPiece[0];
        SetWindingTerraces(true);
        MarkDirty(this); MarkDirty(gameObject);
    }

    void HideWindingTerraces() => SetWindingTerraces(false);
    void SetWindingTerraces(bool on)
    {
        var wp = transform.Find(WP_ROOT); if (wp == null) return;
        foreach (Transform c in wp)
        {
            if (System.Array.IndexOf(WP_HIDE, c.name) >= 0) { c.gameObject.SetActive(on); MarkDirty(c.gameObject); }
            if (System.Array.IndexOf(WP_PATHS, c.name) >= 0) { var b = c.Find("둑벽"); if (b) { b.gameObject.SetActive(on); MarkDirty(b.gameObject); } }
        }
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
        mGrass = L(SUB + "/고퀄_풀_1.00.mat"); mPath = L(SUB + "/고퀄_길_흙_1.00.mat"); mDirt = L(SUB + "/고퀄_흙_1.00.mat");
        mBladeGrass = L(SUB + "/잎_풀.mat"); mLeaf = L(SUB + "/고퀄_나뭇잎.mat"); mLeaf2 = L(SUB + "/고퀄_나뭇잎2.mat"); mBark = L(SUB + "/고퀄_줄기.mat");
        mWild = L(SUB2 + "/들꽃.mat");
        if (mPath == null) mPath = mDirt; if (mDirt == null) mDirt = mPath;
        if (mGrass == null || mPath == null || mBladeGrass == null || mLeaf == null || mBark == null)
        {
            Debug.LogError("[논틀밭틀 녹차밭 골짜기] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요.");
            return false;
        }
        if (mLeaf2 == null) mLeaf2 = mLeaf;
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  꼬불길 자리 (NonteulWindingPath와 같은 길) → 골짜기로 갈라 줘요
    // ==================================================================
    void MakeGorges()
    {
        gorgePts.Clear(); gorgeHalf.Clear();
        var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
        var b = new[] { new Vector3(7.4f, 0, 17), new Vector3(12, 0, 22), new Vector3(8, 0, 29), new Vector3(16, 0, 36), new Vector3(10, 0, 44), new Vector3(20, 0, 51), new Vector3(13, 0, 59), new Vector3(22, 0, 69) };
        var pa = new List<Vector3>(); var pb = new List<Vector3>();
        Spline(a, 0.8f, pa); Spline(b, 0.8f, pb);
        // 길 끝을 언덕 밖으로 조금 더 늘려 골짜기가 바깥까지 열리게
        pa.Insert(0, a[0] + (a[0] - a[1]).normalized * 12f); pb.Add(b[b.Length - 1] + (b[b.Length - 1] - b[b.Length - 2]).normalized * 12f);
        foreach (var p in pa) { gorgePts.Add(new Vector2(p.x, p.z)); gorgeHalf.Add(3.0f); }
        foreach (var p in pb) { gorgePts.Add(new Vector2(p.x, p.z)); gorgeHalf.Add(2.2f); }
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

    // 길 가장자리에서 얼마나 떨어졌는지 (m)
    float GorgeG(float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0; i < gorgePts.Count; i++)
        {
            float dx = x - gorgePts[i].x, dz = z - gorgePts[i].y;
            float d = Mathf.Sqrt(dx * dx + dz * dz) - gorgeHalf[i];
            if (d < best) best = d;
        }
        return best;
    }

    // ==================================================================
    //  높이 계산
    // ==================================================================
    // 골짜기 바닥 가장자리에서 바깥으로 몇 m 왔는지 (굽이치는 등고선)
    float HillS(float x, float z)
    {
        float dx = Mathf.Abs((x - CX) / EA), dz = Mathf.Abs((z - CZ) / EB);
        float rho = Mathf.Pow(Mathf.Pow(dx, SEXP) + Mathf.Pow(dz, SEXP), 1f / SEXP);
        float s = (rho - 1f) * R0;
        float w = waviness * 2.2f * (Fbm(x * 0.045f + 5.3f, z * 0.045f - 2.1f) - 0.5f) * 2f;
        return s + w;
    }
    float TerrH(float c)
    {
        if (c <= 0) return 0;
        float lv = c / stepWidth; int k = Mathf.FloorToInt(lv); float f = lv - k;
        if (k >= tiers) return stepHeight * tiers;
        return stepHeight * (k + Smooth01(f / RISER));
    }
    float HillH(float s)
    {
        float S1 = stepWidth * tiers;
        if (s <= S1) return TerrH(s);
        return stepHeight * tiers * (1f - Smooth01((s - S1) / BACK));
    }
    float HeightAt(float x, float z, out bool ring)
    {
        float h1 = HillH(HillS(x, z)), hg = TerrH(GorgeG(x, z));
        ring = h1 <= hg + 0.001f;
        return Mathf.Min(h1, hg);
    }

    // ==================================================================
    //  1) 계단 언덕 (가로 띠 4개로 나눠 저장)
    // ==================================================================
    void BuildHill(Transform root)
    {
        int nx = Mathf.CeilToInt((GMAXX - GMINX) / CELL) + 1, nz = Mathf.CeilToInt((GMAXZ - GMINZ) / CELL) + 1;
        var h = new float[nx * nz]; var ring = new bool[nx * nz];
        for (int iz = 0; iz < nz; iz++)
        {
            if (iz % 40 == 0) Progress("계단 언덕 만드는 중…", 0.1f + 0.25f * iz / nz);
            for (int ix = 0; ix < nx; ix++)
            {
                float x = GMINX + ix * CELL, z = GMINZ + iz * CELL;
                h[iz * nx + ix] = HeightAt(x, z, out bool r); ring[iz * nx + ix] = r;
            }
        }
        var holder = new GameObject("계단 언덕").transform; holder.SetParent(root, false);
        const int BANDS = 4;
        for (int band = 0; band < BANDS; band++)
        {
            int z0 = band * (nz - 1) / BANDS, z1 = (band + 1) * (nz - 1) / BANDS;
            var mb = new MB(2);   // 0 고랑 흙  1 풀
            var map = new Dictionary<int, int>();
            int V(int ix, int iz)
            {
                int key = iz * nx + ix;
                if (map.TryGetValue(key, out int idx)) return idx;
                float x = GMINX + ix * CELL, z = GMINZ + iz * CELL;
                float hl = h[iz * nx + Mathf.Max(ix - 1, 0)], hr = h[iz * nx + Mathf.Min(ix + 1, nx - 1)];
                float hd = h[Mathf.Max(iz - 1, 0) * nx + ix], hu = h[Mathf.Min(iz + 1, nz - 1) * nx + ix];
                var n = new Vector3(-(hr - hl) / (2 * CELL), 1f, -(hu - hd) / (2 * CELL)).normalized;
                idx = mb.v.Count; mb.Add(new Vector3(x, h[key] + LIFT, z), n, new Vector2(x, z)); map[key] = idx; return idx;
            }
            for (int iz = z0; iz < z1; iz++)
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                    float mx = Mathf.Max(Mathf.Max(h[a], h[b]), Mathf.Max(h[c], h[d]));
                    if (mx < 0.001f) continue;
                    float mn = Mathf.Min(Mathf.Min(h[a], h[b]), Mathf.Min(h[c], h[d]));
                    bool flat = mx - mn < 0.1f, onRing = ring[a] && ring[b] && ring[c] && ring[d];
                    int s = flat && onRing && mn > 0.05f ? 0 : 1;
                    int va = V(ix, iz), vb = V(ix + 1, iz), vc = V(ix, iz + 1), vd = V(ix + 1, iz + 1);
                    mb.Tri(s, va, vc, vb); mb.Tri(s, vb, vc, vd);
                }
            Emit(holder, "계단 언덕 " + (band + 1), mb, new[] { mPath, mGrass }, ShadowCastingMode.On, true);
        }
    }

    // ==================================================================
    //  2) 차나무 줄 (층마다 등고선을 따라 한 줄씩, 둥글게 다듬은 모양)
    // ==================================================================
    Vector3 RingPoint(float ang, float sTarget)
    {
        float c = Mathf.Cos(ang), s = Mathf.Sin(ang), lo = 2f, hi = 160f;
        for (int i = 0; i < 26; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (HillS(CX + c * mid, CZ + s * mid) < sTarget) lo = mid; else hi = mid;
        }
        float r = (lo + hi) * 0.5f; return new Vector3(CX + c * r, 0, CZ + s * r);
    }

    // 등고선 하나를 따라 점을 찍고, 골짜기에 끊기는 곳마다 조각으로 나눠요
    List<List<Vector3>> RingSegments(float sTarget, float spacing, System.Func<Vector3, bool> ok, out bool closed)
    {
        var pts = new List<Vector3>(); var act = new List<bool>();
        float ang = 0f;
        while (ang < Mathf.PI * 2f)
        {
            var p = RingPoint(ang, sTarget);
            pts.Add(p); act.Add(ok(p));
            float r = Mathf.Max(5f, new Vector2(p.x - CX, p.z - CZ).magnitude);
            ang += spacing / r * 0.8f;
        }
        var segs = new List<List<Vector3>>();
        int first = act.IndexOf(false);
        closed = first < 0;
        if (closed) { segs.Add(pts); return segs; }
        List<Vector3> cur = null;
        for (int k = 1; k <= pts.Count; k++)
        {
            int i = (first + k) % pts.Count;
            if (act[i]) { if (cur == null) { cur = new List<Vector3>(); segs.Add(cur); } cur.Add(pts[i]); }
            else cur = null;
        }
        segs.RemoveAll(sg => sg.Count < 3);
        return segs;
    }

    void BuildHedgeRings(Transform root)
    {
        var mb = new MB(3); var grass = new MB(1);
        float tread = stepWidth * (1f - RISER);
        float w = Mathf.Min(0.55f, tread * 0.38f) * hedgeSize, hh = 0.62f * hedgeSize;
        for (int k = 0; k < tiers; k++)
        {
            if (k % 2 == 0) Progress("차나무 줄 심는 중…", 0.45f + 0.24f * k / tiers);
            float y = stepHeight * (k + 1);
            int kk = k;
            bool OnTread(Vector3 p) => TerrH(GorgeG(p.x, p.z) - 0.7f) >= stepHeight * (kk + 1) - 0.01f;
            var segs = RingSegments(stepWidth * (k + (1f + RISER) / 2f), 0.3f, OnTread, out bool closed);
            foreach (var sg in segs) HedgeTube(mb, sg, closed, y + LIFT, w, hh, k);

            // 층 끝선(경사 위 모서리)에 늘어진 풀
            var lips = RingSegments(stepWidth * (k + RISER) + 0.1f, 0.75f, p => TerrH(GorgeG(p.x, p.z) - 0.3f) >= stepHeight * (kk + 1) - 0.01f, out _);
            foreach (var sg in lips)
                foreach (var p in sg)
                    if (rng.NextDouble() < 0.8) Ribbons(grass, new Vector3(p.x, y + LIFT - 0.03f, p.z), R(0.25f, 0.45f), 5, 0.25f, 0.9f, 0.024f);
        }
        Emit(root, "차나무 줄", mb, mTea, ShadowCastingMode.On, true);
        Emit(root, "층 끝 풀", grass, new[] { mBladeGrass }, ShadowCastingMode.Off, false);
    }

    // 반달 모양 단면을 등고선을 따라 쭉 이어요 (겉은 몽글몽글, 양 끝은 둥글게 줄어들어요)
    void HedgeTube(MB m, List<Vector3> pts, bool closed, float y, float w, float h, int ringIdx)
    {
        const int CS = 8;
        int n = pts.Count; if (n < 2) return;
        var len = new float[n]; for (int i = 1; i < n; i++) len[i] = len[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        float total = len[n - 1];
        int baseIdx = m.v.Count;
        int count = closed ? n + 1 : n;
        for (int ii = 0; ii < count; ii++)
        {
            int i = ii % n;
            float along = ii < n ? len[i] : total + Vector3.Distance(pts[n - 1], pts[0]);
            var prev = pts[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)]; var next = pts[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)];
            var tan = next - prev; tan.y = 0; if (tan.sqrMagnitude < 1e-8f) tan = Vector3.forward; tan.Normalize();
            var side = new Vector3(tan.z, 0, -tan.x);
            float taper = closed ? 1f : Mathf.Max(0.05f, Smooth01(Mathf.Min(along, total - along) / 0.7f));
            var c = new Vector3(pts[i].x, y - 0.1f, pts[i].z);
            for (int j = 0; j <= CS; j++)
            {
                float ph = j / (float)CS * Mathf.PI, cs = Mathf.Cos(ph), sn = Mathf.Sin(ph);
                float bump = 0.86f + 0.28f * Noise(along * 1.7f + ringIdx * 17.3f, ph * 1.6f + ringIdx);
                if (j == 0 || j == CS) bump = 1f;
                var p = c + side * (cs * w * bump * taper) + Vector3.up * (sn * h * bump * taper + 0.1f * (1 - taper));
                var nr = (side * (cs / w) + Vector3.up * (sn / h)).normalized; nr = (nr + Vector3.up * 0.3f).normalized;
                m.Add(p, nr, new Vector2(along / 1.1f, Mathf.Abs(cs)));
            }
        }
        // 구간마다 차잎 색을 조금씩 다르게
        for (int ii = 0; ii < count - 1; ii++)
        {
            float px = pts[ii % n].x, pz = pts[ii % n].z, patch = Fbm(px * 0.12f + 3f, pz * 0.12f + ringIdx * 0.7f);
            int sub = patch < 0.43f ? 0 : (patch < 0.57f ? 1 : 2);
            for (int j = 0; j < CS; j++)
            {
                int a = baseIdx + ii * (CS + 1) + j, b = a + 1, c2 = a + CS + 1, d = c2 + 1;
                m.Tri(sub, a, c2, b); m.Tri(sub, b, c2, d);
            }
        }
    }

    // ==================================================================
    //  3) 능선 나무
    // ==================================================================
    void BuildRidgeTrees(Transform root)
    {
        if (ridgeTrees <= 0) return;
        var leaves = new MB(1); var leaves2 = new MB(1); var bark = new MB(1);
        float S1 = stepWidth * tiers;
        int placed = 0;
        for (int tries = 0; tries < ridgeTrees * 20 && placed < ridgeTrees; tries++)
        {
            float ang = R(0, Mathf.PI * 2f);
            var p = RingPoint(ang, S1 + R(1.5f, 6f));
            if (GorgeG(p.x, p.z) < 6f) continue;
            float y = HeightAt(p.x, p.z, out _);
            Tree(rng.NextDouble() < 0.5 ? leaves : leaves2, bark, new Vector3(p.x, y, p.z), R(6f, 10f));
            placed++;
        }
        var r = new GameObject("능선 나무").transform; r.SetParent(root, false);
        Emit(r, "나뭇잎", leaves, new[] { mLeaf }, ShadowCastingMode.On, true);
        Emit(r, "나뭇잎2", leaves2, new[] { mLeaf2 }, ShadowCastingMode.On, true);
        Emit(r, "나무줄기", bark, new[] { mBark }, ShadowCastingMode.On, true);
    }

    void Tree(MB leaves, MB bark, Vector3 basePos, float height)
    {
        float trunkH = height * R(0.45f, 0.6f), rad = height * 0.035f; const int sides = 8; int k0 = bark.v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.Add(basePos + d * rad * 1.3f - Vector3.up * 0.3f, d, new Vector2(i / (float)sides, 0));
            bark.Add(basePos + d * rad * 0.6f + Vector3.up * trunkH, d, new Vector2(i / (float)sides, 1));
        }
        for (int i = 0; i < sides; i++) { int a = k0 + i * 2; bark.Tri(0, a, a + 1, a + 3); bark.Tri(0, a, a + 3, a + 2); }
        int blobs = 4 + rng.Next(4); float crown = height * 0.32f;
        for (int b = 0; b < blobs; b++)
            Blob(leaves, basePos + Vector3.up * (trunkH + crown * R(0.1f, 0.9f)) + new Vector3(R(-1, 1), 0, R(-1, 1)) * crown * 0.6f, crown * R(0.55f, 0.85f));
    }

    void Blob(MB m, Vector3 c, float r)
    {
        const int LA = 7, LO = 10; int k0 = m.v.Count; float seed = R(0, 100);
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI, ph = j / (float)LO * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th) * 0.85f, Mathf.Sin(th) * Mathf.Sin(ph));
                float nz = 0.75f + 0.45f * Fbm(d.x * 2.2f + seed, d.z * 2.2f + d.y * 1.7f + seed);
                m.Add(c + d * r * nz, d.normalized, new Vector2(j / (float)LO, i / (float)LA));
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++)
            {
                int a = k0 + i * (LO + 1) + j, b = a + LO + 1;
                m.Tri(0, a, a + 1, b); m.Tri(0, a + 1, b + 1, b);
            }
    }

    // ==================================================================
    //  4) ㄹ 꼬불길 (ㄹ 둑 위를 따라 굽이치는 흙길)
    // ==================================================================
    void BuildRiul(Transform root)
    {
        var decorList = new List<Transform>(); var pieceList = new List<Byeolmalsseum.JamoPiece>();
        foreach (int j in new[] { 5, 11 })
        {
            float X = FINAL[j, 0], Z = FINAL[j, 1], yaw = FINAL[j, 2] * Mathf.Deg2Rad, fw = FINAL[j, 3], fh = FINAL[j, 4], T = FINAL[j, 5];
            var piece = FindPiece(X, Z, out float top);
            float x0 = -fw / 2, x1 = fw / 2, y0 = -fh / 2, y1 = fh / 2, m = 0;
            // ㄹ을 아래에서부터 한 붓으로: 아래 가로 → 왼쪽 세로 → 가운데 가로 → 오른쪽 세로 → 위 가로
            var local = new[] { new Vector2(x1 - 0.15f, y0 + T / 2), new Vector2(x0 + T / 2, y0 + T / 2), new Vector2(x0 + T / 2, m), new Vector2(x1 - T / 2, m), new Vector2(x1 - T / 2, y1 - T / 2), new Vector2(x0 + 0.15f, y1 - T / 2) };
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            var corners = new List<Vector3>();
            foreach (var q in local) corners.Add(new Vector3(X + c * q.x + s * q.y, 0, Z - s * q.x + c * q.y));
            var line = Fillet(corners, 0.34f, 0.08f);

            var holder = new GameObject(j == 5 ? "ㄹ 꼬불길 (아래)" : "ㄹ 꼬불길 (위)").transform; holder.SetParent(root, false);
            BuildRiulOne(holder, line, top, T, j);
            decorList.Add(holder); pieceList.Add(piece);
        }
        riulDecor = decorList.ToArray(); riulPieces = pieceList.ToArray();
    }

    Byeolmalsseum.JamoPiece FindPiece(float X, float Z, out float top)
    {
        top = 0.4f; Byeolmalsseum.JamoPiece best = null; float bd = 4f;
        Transform jr = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.gameObject.scene == gameObject.scene && t.name.StartsWith("글자")) { jr = t; break; }
        if (jr == null) return null;
        foreach (var jp in jr.GetComponentsInChildren<Byeolmalsseum.JamoPiece>(true))
        {
            if (!MeasureBounds(jp.transform, out var wb)) continue;
            var lc = transform.InverseTransformPoint(wb.center);
            float d = (lc.x - X) * (lc.x - X) + (lc.z - Z) * (lc.z - Z);
            if (d < bd) { bd = d; best = jp; top = transform.InverseTransformPoint(new Vector3(wb.center.x, wb.max.y, wb.center.z)).y; }
        }
        return best;
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

    // 꺾인 선의 모서리를 둥글게 깎고 촘촘히 나눠요
    static List<Vector3> Fillet(List<Vector3> c, float radius, float step)
    {
        var outP = new List<Vector3>();
        void Line(Vector3 a, Vector3 b) { int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step)); for (int k = 0; k < n; k++) outP.Add(Vector3.Lerp(a, b, k / (float)n)); }
        Vector3 cur = c[0];
        for (int i = 1; i < c.Count - 1; i++)
        {
            var din = (c[i] - c[i - 1]).normalized; var dout = (c[i + 1] - c[i]).normalized;
            float r = Mathf.Min(radius, Vector3.Distance(c[i - 1], c[i]) * 0.45f, Vector3.Distance(c[i], c[i + 1]) * 0.45f);
            var a = c[i] - din * r; var b = c[i] + dout * r;
            Line(cur, a);
            for (int k = 0; k < 8; k++) { float t = k / 8f; outP.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c[i] + t * t * b); }
            cur = b;
        }
        Line(cur, c[c.Count - 1]); outP.Add(c[c.Count - 1]);
        return outP;
    }

    void BuildRiulOne(Transform holder, List<Vector3> line, float top, float T, int seedJ)
    {
        var path = new MB(1); var worn = new MB(1); var stones = new MB(1); var grass = new MB(1); var wild = new MB(1);
        float y = top + 0.012f, along = 0;
        int n = line.Count;
        var L = new Vector3[n]; var Rt = new Vector3[n]; var Lw = new Vector3[n]; var Rw = new Vector3[n]; var A = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i > 0) along += Vector3.Distance(line[i - 1], line[i]);
            A[i] = along;
            var tan = line[Mathf.Min(i + 1, n - 1)] - line[Mathf.Max(i - 1, 0)]; tan.y = 0; tan.Normalize();
            var side = new Vector3(tan.z, 0, -tan.x);
            float meander = 0.06f * (Noise(along * 0.9f + seedJ, 3.3f) - 0.5f) * 2f;
            float half = T * (0.3f + 0.05f * (Noise(along * 1.6f + seedJ * 3, 7.1f) - 0.5f) * 2f);
            var cpt = line[i] + side * meander;
            L[i] = cpt - side * half + Vector3.up * y; Rt[i] = cpt + side * half + Vector3.up * y;
            float wh = half * 0.42f;
            Lw[i] = cpt - side * wh + Vector3.up * (y + 0.004f); Rw[i] = cpt + side * wh + Vector3.up * (y + 0.004f);
        }
        for (int i = 0; i < n - 1; i++)
        {
            StripQuad(path, L[i], L[i + 1], Rt[i + 1], Rt[i], A[i], A[i + 1], -0.3f, 0.3f);
            StripQuad(worn, Lw[i], Lw[i + 1], Rw[i + 1], Rw[i], A[i], A[i + 1], -0.12f, 0.12f);
        }
        // 디딤돌 (넓적한 돌이 띄엄띄엄 박혀 있어요)
        for (float d = 0.6f; d < along - 0.4f; d += R(1.1f, 1.7f))
        {
            int i = IndexAt(A, d); var tan = (line[Mathf.Min(i + 1, n - 1)] - line[Mathf.Max(i - 1, 0)]).normalized; var side = new Vector3(tan.z, 0, -tan.x);
            var p = line[i] + side * R(-0.08f, 0.08f) + Vector3.up * (y + 0.004f);
            FlatStone(stones, p, R(0.15f, 0.21f), R(0.11f, 0.16f), R(0, 6.28f));
        }
        // 자갈
        int pebbles = Mathf.RoundToInt(along * 9f);
        for (int k = 0; k < pebbles; k++)
        {
            float d = R(0, along); int i = IndexAt(A, d);
            var tan = (line[Mathf.Min(i + 1, n - 1)] - line[Mathf.Max(i - 1, 0)]).normalized; var side = new Vector3(tan.z, 0, -tan.x);
            float off = R(-1f, 1f); off = Mathf.Sign(off) * Mathf.Pow(Mathf.Abs(off), 0.6f) * T * 0.3f; // 가장자리 쪽에 더 많이
            Pebble(stones, line[i] + side * off + Vector3.up * y, R(0.012f, 0.04f));
        }
        // 길섶 풀 (흙길 가장자리를 부드럽게 덮어요) + 둑 모서리에 늘어진 풀 + 들꽃
        for (int i = 0; i < n; i += 1)
        {
            var tan = (line[Mathf.Min(i + 1, n - 1)] - line[Mathf.Max(i - 1, 0)]).normalized; var side = new Vector3(tan.z, 0, -tan.x);
            foreach (float sg in new[] { -1f, 1f })
            {
                var edge = (sg < 0 ? L[i] : Rt[i]) + side * (sg * R(0.0f, 0.06f));
                if (rng.NextDouble() < 0.75) Ribbons(grass, edge - Vector3.up * 0.005f, R(0.07f, 0.18f), 5, 0.1f, 0.6f, 0.012f);
                if (rng.NextDouble() < 0.35)
                {
                    var rim = line[i] + side * (sg * T * R(0.44f, 0.5f)) + Vector3.up * (top - 0.01f);
                    Ribbons(grass, rim, R(0.18f, 0.35f), 6, 0.12f, 1.1f, 0.018f);
                }
                if (mWild != null && rng.NextDouble() < 0.05)
                    Flower(wild, line[i] + side * (sg * T * R(0.36f, 0.46f)) + Vector3.up * top, R(0.08f, 0.18f), rng.Next(3));
            }
        }
        Emit(holder, "흙길", path, new[] { mPath }, ShadowCastingMode.Off, true);
        Emit(holder, "밟힌 가운데", worn, new[] { mDirt }, ShadowCastingMode.Off, true);
        Emit(holder, "디딤돌·자갈", stones, new[] { mPebble }, ShadowCastingMode.On, true);
        Emit(holder, "길섶 풀", grass, new[] { mBladeGrass }, ShadowCastingMode.Off, false);
        if (mWild != null) Emit(holder, "들꽃", wild, new[] { mWild }, ShadowCastingMode.Off, false);
    }

    static int IndexAt(float[] A, float d) { int i = System.Array.BinarySearch(A, d); if (i < 0) i = ~i; return Mathf.Clamp(i, 0, A.Length - 1); }

    static void StripQuad(MB m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float v0, float v1, float u0, float u1)
    {
        int k = m.v.Count;
        m.Add(a, Vector3.up, new Vector2(u0, v0)); m.Add(b, Vector3.up, new Vector2(u0, v1)); m.Add(c, Vector3.up, new Vector2(u1, v1)); m.Add(d, Vector3.up, new Vector2(u1, v0));
        m.Tri(0, k, k + 1, k + 2); m.Tri(0, k, k + 2, k + 3);
    }

    void FlatStone(MB m, Vector3 c, float rx, float rz, float rot)
    {
        const int N = 9; float th = R(0.03f, 0.05f);
        var dirX = new Vector3(Mathf.Cos(rot), 0, Mathf.Sin(rot)); var dirZ = new Vector3(-dirX.z, 0, dirX.x);
        var rim = new Vector3[N]; float seed = R(0, 50);
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2, k = 0.8f + 0.35f * Noise(Mathf.Cos(a) * 1.5f + seed, Mathf.Sin(a) * 1.5f + seed);
            rim[i] = dirX * (Mathf.Cos(a) * rx * k) + dirZ * (Mathf.Sin(a) * rz * k);
        }
        int ct = m.v.Count; m.Add(c + Vector3.up * (th + 0.01f), Vector3.up, new Vector2(c.x, c.z) * 3f);
        for (int i = 0; i < N; i++) { var p = c + rim[i] + Vector3.up * th; m.Add(p, (Vector3.up * 3f + rim[i].normalized).normalized, new Vector2(p.x, p.z) * 3f); }
        for (int i = 0; i < N; i++) m.Tri(0, ct, ct + 1 + i, ct + 1 + (i + 1) % N);
        int sb = m.v.Count;
        for (int i = 0; i < N; i++)
        {
            var o = rim[i].normalized; var p0 = c + rim[i] + Vector3.up * th; var p1 = c + rim[i] * 1.08f - Vector3.up * 0.02f;
            m.Add(p0, (o + Vector3.up * 0.4f).normalized, new Vector2(i / (float)N, 0)); m.Add(p1, o, new Vector2(i / (float)N, 0.3f));
        }
        for (int i = 0; i < N; i++) { int a = sb + i * 2, b = sb + ((i + 1) % N) * 2; m.Tri(0, a, b, a + 1); m.Tri(0, b, b + 1, a + 1); }
    }

    void Pebble(MB m, Vector3 c, float r)
    {
        const int LA = 3, LO = 6; int k0 = m.v.Count; float sx = R(0.8f, 1.3f), sz = R(0.8f, 1.3f), rot = R(0, 6.28f);
        var ax = new Vector3(Mathf.Cos(rot), 0, Mathf.Sin(rot)); var az = new Vector3(-ax.z, 0, ax.x);
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI * 0.55f, ph = j / (float)LO * Mathf.PI * 2;
                float dx = Mathf.Sin(th) * Mathf.Cos(ph), dy = Mathf.Cos(th), dz = Mathf.Sin(th) * Mathf.Sin(ph);
                var p = c + ax * (dx * r * sx) + az * (dz * r * sz) + Vector3.up * (dy * r * 0.55f - r * 0.15f);
                var nr = (ax * dx + az * dz + Vector3.up * dy * 1.5f).normalized;
                m.Add(p, nr, new Vector2(p.x, p.z) * 3f);
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++) { int a = k0 + i * (LO + 1) + j, b = a + LO + 1; m.Tri(0, a, a + 1, b); m.Tri(0, a + 1, b + 1, b); }
    }

    void Flower(MB m, Vector3 p, float h, int kind)
    {
        int k0 = m.v.Count; var lean = new Vector3(R(-0.03f, 0.03f), 0, R(-0.03f, 0.03f)); var top = p + Vector3.up * h + lean;
        m.Add(p + Vector3.left * 0.003f, Vector3.up, new Vector2(0.1f, 0.1f)); m.Add(p + Vector3.right * 0.003f, Vector3.up, new Vector2(0.1f, 0.1f));
        m.Add(top + Vector3.right * 0.003f, Vector3.up, new Vector2(0.1f, 0.4f)); m.Add(top + Vector3.left * 0.003f, Vector3.up, new Vector2(0.1f, 0.4f));
        m.sub[0].Add(k0); m.sub[0].Add(k0 + 1); m.sub[0].Add(k0 + 2); m.sub[0].Add(k0); m.sub[0].Add(k0 + 2); m.sub[0].Add(k0 + 3);
        float u = kind == 0 ? 0.55f : (kind == 1 ? 0.75f : 0.92f);
        int c = m.v.Count; m.Add(top + Vector3.up * 0.004f, Vector3.up, new Vector2(u, 0.98f));
        const int petals = 6; float pr = R(0.02f, 0.035f);
        for (int i = 0; i <= petals; i++) { float a = i / (float)petals * Mathf.PI * 2; m.Add(top + new Vector3(Mathf.Cos(a) * pr, 0, Mathf.Sin(a) * pr), Vector3.up, new Vector2(u, 0.8f)); }
        for (int i = 0; i < petals; i++) m.Tri(0, c, c + 1 + i, c + 2 + i);
    }

    // 휘어진 풀잎 여러 장
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
                m.Add(c - side * w, nrm, new Vector2(u, t * 0.85f)); m.Add(c + side * w, nrm, new Vector2(u, t * 0.85f));
            }
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(a + 3); m.sub[0].Add(a); m.sub[0].Add(a + 3); m.sub[0].Add(a + 2); }
        }
    }

    // ==================================================================
    //  플레이 중: ㄹ이 내려앉은 뒤에 길 장식 보이기
    // ==================================================================
    bool[] riulShown, riulAway;
    void Start()
    {
        if (riulDecor == null) return;
        int n = riulDecor.Length; riulShown = new bool[n]; riulAway = new bool[n];
        for (int i = 0; i < n; i++)
        {
            bool canWait = riulDecor[i] && i < riulPieces.Length && riulPieces[i] && riulPieces[i].visual;
            if (canWait) riulDecor[i].gameObject.SetActive(false); else riulShown[i] = true;
        }
    }
    void Update()
    {
        if (riulShown == null) return;
        for (int i = 0; i < riulShown.Length; i++)
        {
            if (riulShown[i] || riulDecor[i] == null) continue;
            var p = riulPieces[i]; var vis = p ? p.visual : null;
            bool home = vis == null || vis.parent == p.transform;
            if (!home) riulAway[i] = true;
            // ㄹ 자모가 날아와 껍데기 자리에 내려앉으면 길 장식이 나타나요
            if (home && (riulAway[i] || Time.timeSinceLevelLoad > 3f)) { riulShown[i] = true; riulDecor[i].gameObject.SetActive(true); }
        }
    }

    // ==================================================================
    //  질감 (계단 다원과 같은 차잎 재질을 함께 써요)
    // ==================================================================
    void MakeOwnMaterials()
    {
        var teaTex = GetTex("차잎_색", MakeTeaAlbedo);
        var teaN = GetTex("차잎_노멀", () => NormalFrom(TeaHeight(256), 256, 5f, "차잎_노멀"));
        Color[] tints = { new Color(1f, 1f, 1f), new Color(0.86f, 0.95f, 0.82f), new Color(1.08f, 1.05f, 0.88f) };
        for (int i = 0; i < 3; i++) mTea[i] = GetMat("다원_차잎_" + (char)('A' + i), tints[i], teaTex, teaN, 0.42f, 1f, Vector2.one);
        var pebN = GetTex("자갈_노멀", () => NormalFrom(PebbleHeight(128), 128, 3f, "자갈_노멀"));
        var pebTex = GetTex("자갈_색", MakePebbleAlbedo);
        mPebble = GetMat("골짜기_자갈", Color.white, pebTex, pebN, 0.28f, 1f, Vector2.one);
    }

    Texture2D GetTex(string name, System.Func<Texture2D> make)
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

    static float[] TeaHeight(int S)
    {
        var h = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S, w = FbmP(u * 8, v * 8, 8, 3);
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
                c *= 1f - 0.55f * gap; c *= 0.88f + 0.24f * FbmP(u * 4, v * 4, 4, 2);
                c.a = 1; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }
    static float[] PebbleHeight(int S)
    {
        var h = new float[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) h[y * S + x] = FbmP(x / (float)S * 8, y / (float)S * 8, 8, 4);
        return h;
    }
    static Texture2D MakePebbleAlbedo()
    {
        const int S = 128; var t = NewTex(S, false); var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S, n = FbmP(u * 4, v * 4, 4, 4), sp = FbmP(u * 32, v * 32, 32, 2);
                Color c = Color.Lerp(new Color(0.40f, 0.38f, 0.34f), new Color(0.62f, 0.58f, 0.51f), n);
                c *= 0.85f + 0.3f * sp; c.a = 1; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }
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
        // 삼각형 앞면이 노멀 쪽을 보게 자동으로 순서를 맞춰요
        public void Tri(int s, int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) >= 0) { sub[s].Add(a); sub[s].Add(b); sub[s].Add(c); }
            else { sub[s].Add(a); sub[s].Add(c); sub[s].Add(b); }
        }
        public Mesh ToMesh(string name, bool tangents)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length; for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds(); if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    void Emit(Transform parent, string name, MB mb, Material[] mats, ShadowCastingMode cast, bool tangents)
    {
        if (mb.v.Count == 0) return;
        var mesh = mb.ToMesh(name, tangents);
        // 깃허브용 크기 확인 (텍스트 저장 기준 대략값)
        float mbSize = (mb.v.Count * (tangents ? 48f : 32f) * 2f + IndexCount(mb) * 4f * 2f) / (1024f * 1024f);
        if (mbSize > 45f) Debug.LogWarning("[논틀밭틀 녹차밭 골짜기] '" + name + "' 메시가 커요 (약 " + mbSize.ToString("0") + "MB). 풀·나무 양을 줄여 주세요.");
        SaveAsset(mesh, "메시_골짜기_" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = cast; mr.receiveShadows = true;
    }
    static int IndexCount(MB mb) { int c = 0; foreach (var s in mb.sub) c += s.Count; return c; }

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
        EditorUtility.DisplayProgressBar("논틀밭틀 녹차밭 골짜기", msg, p);
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
[CustomEditor(typeof(NonteulTeaValley))]
public class NonteulTeaValleyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulTeaValley)target;
        EditorGUILayout.HelpBox("NonteulFieldUpgrade의 [★ 전부 한 번에]와 NonteulWindingPath의 [꼬불꼬불 만들기]를 한 뒤에 눌러요.\n글자 둘레에 보성 녹차밭 같은 높은 계단 언덕을 만들고, ㄹ 둑 위에 고퀄 흙길을 깔아요.\n값을 바꾼 뒤에는 [만들기]를 다시 누르면 돼요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("⛰ 녹차밭 골짜기 + ㄹ 꼬불길 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("골짜기 지우기 (원래대로)", GUILayout.Height(24))) Run(s.Remove);
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
