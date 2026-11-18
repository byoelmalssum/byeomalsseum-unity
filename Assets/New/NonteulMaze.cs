// =====================================================================
//  NonteulMaze.cs — '논틀밭틀' 초록 울타리 미로 + 떨어진 초가집 (URP)
//
//  다른 논틀밭틀 스크립트들과 함께 「공간」에 붙여요.
//   - 녹차밭 골짜기 언덕은 지워요 (ㄹ 꼬불길 장식은 남겨요).
//   - 글자 북쪽 빈 땅에 키 큰 초록 울타리 미로를 만들어요.
//     갈림길 없이 한 줄로 좁고 구불구불하게 이어져서, 앞에 길 하나만 보여요.
//   - 체험 흐름: 시작 → ㄹ 꼬불길 → ㅏ 위로 → 미로 입구 → 구불구불 미로 → 출구 → 오솔길 → 초가집
//   - 미로를 빠져나와 조금 걸어가면 초가집이 있어요 (답답하지 않게 떨어뜨려 놨어요).
//   - 체험 카메라가 울타리와 집을 뚫고 지나가지 못하게 막아요. 미로 안에서는 울타리 위로 못 올라가요.
//   - 빛가루 길도 ㄹ → 미로 → 집으로 이어지게 바꿔요.
//  만든 파일은 Assets/논틀밭틀_생성/미로 폴더에 저장돼요 (파일 하나 50MB 안 넘게).
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(10000)] // 체험 카메라가 움직인 '다음'에 벽 검사를 해요
public class NonteulMaze : MonoBehaviour
{
    [Header("미로")]
    [Tooltip("길 폭 (m) — 좁을수록 답답하고 꼬불꼬불한 느낌")] [Range(0.9f, 1.8f)] public float corridorWidth = 1.2f;
    [Tooltip("울타리 높이 (m)")] [Range(3f, 10f)] public float wallHeight = 6f;
    [Tooltip("길이 좌우로 꿈틀대는 정도")] [Range(0f, 0.3f)] public float wiggle = 0.2f;

    [Header("벽에 막히기")]
    [Tooltip("체험 카메라가 울타리·집을 뚫지 못하게")] public bool blockCamera = true;
    [Tooltip("카메라 몸 크기 (m)")] [Range(0.1f, 0.5f)] public float cameraRadius = 0.3f;

    // ---- 버튼이 채우는 값 (플레이 중 벽 검사에 써요) ----
    [HideInInspector] public List<Vector2> mazeLine = new List<Vector2>();
    [HideInInspector] public Rect mazeRect;
    [HideInInspector] public Rect houseRect;
    [HideInInspector] public float builtHalf = 0.6f, builtWallH = 6f;
    [HideInInspector] public bool built;
    [HideInInspector] public List<Vector3> savedGlow = new List<Vector3>();

    // ------------------------------------------------------------------
    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/미로";
    const string ROOT_NAME = "논틀밭틀_미로와 집 (자동 생성)";
    const string VALLEY_ROOT = "논틀밭틀_녹차밭 골짜기 (자동 생성)";
    const string WP_ROOT = "논틀밭틀_꼬불꼬불 (자동 생성)";
    static readonly string[] WP_HIDDEN_BY_VALLEY = { "다랑논 바닥", "다랑논 물", "다랑논 층벽", "굽은 논둑 (윗면)", "굽은 논둑 (옆면)", "벼 (멀리)", "벼 (가장자리)", "모", "굽이 나무" };

    // 미로 자리 (글자 북쪽 빈 땅) — 세로 줄을 오가며 서쪽으로 나아가요
    const float COL_START_X = 3.4f, COL_END_X = -22.6f, STRAIGHT_Z0 = 25.2f, STRAIGHT_Z1 = 29.6f, STUB_Z = 21.6f;
    const float WALL_MIN = 0.8f; // 길 사이 울타리 최소 두께
    // 초가집 (글자 서쪽, 미로 출구에서 20m쯤)
    const float HX = -24.5f, HZ = -5.5f;

    System.Random rng;
    Material mHedge, mPath, mGrassBlade, mStraw, mPlaster, mWood, mDoor, mStone, mJar, mLeaf, mLeaf2, mBark, mPebble;

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
        rng = new System.Random(2024);
        RemoveValleyHills();
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);

        Progress("울타리·초가 질감 만드는 중…", 0.05f);
        MakeOwnMaterials();

        Progress("미로 길 그리는 중…", 0.2f);
        var line = MakeMazeLine(out Rect rect);
        mazeLine = line; mazeRect = rect; builtHalf = corridorWidth / 2f; builtWallH = wallHeight;

        Progress("울타리 세우는 중…", 0.3f);
        BuildHedges(root, line, rect);
        Progress("미로 바닥 길 까는 중…", 0.5f);
        BuildFootpath(root, "미로 속 흙길", line, 0.26f, true);

        Progress("초가집 짓는 중…", 0.65f);
        var toHouse = HousePath(line);
        BuildFootpath(root, "집 가는 오솔길", toHouse, 0.38f, false);
        BuildHouse(root);

        Progress("빛가루 길 잇는 중…", 0.9f);
        RerouteGlow(line, toHouse);

        built = true;
        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 미로] 다 만들었어요. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        RestoreGlow();
        built = false; mazeLine = new List<Vector2>();
        MarkDirty(this); MarkDirty(gameObject);
    }

    // 녹차밭 골짜기 언덕만 지우고 ㄹ 꼬불길 장식은 남겨요
    void RemoveValleyHills()
    {
        var v = transform.Find(VALLEY_ROOT);
        if (v != null)
        {
            var kill = new List<GameObject>();
            foreach (Transform c in v) if (!c.name.StartsWith("ㄹ")) kill.Add(c.gameObject);
            foreach (var g in kill) DestroyNow(g);
        }
        // 골짜기가 숨겼던 꼬불꼬불 스크립트의 다랑논·나무·둑벽을 다시 보이게
        var wp = transform.Find(WP_ROOT);
        if (wp != null)
            foreach (Transform c in wp)
            {
                if (System.Array.IndexOf(WP_HIDDEN_BY_VALLEY, c.name) >= 0) { c.gameObject.SetActive(true); MarkDirty(c.gameObject); }
                var b = c.Find("둑벽"); if (b) { b.gameObject.SetActive(true); MarkDirty(b.gameObject); }
            }
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
        mPath = L(SUB + "/고퀄_길_흙_1.00.mat"); if (mPath == null) mPath = L(SUB + "/고퀄_흙_1.00.mat");
        mGrassBlade = L(SUB + "/잎_풀.mat"); mLeaf = L(SUB + "/고퀄_나뭇잎.mat"); mLeaf2 = L(SUB + "/고퀄_나뭇잎2.mat"); mBark = L(SUB + "/고퀄_줄기.mat");
        if (mPath == null || mGrassBlade == null || mLeaf == null || mBark == null)
        {
            Debug.LogError("[논틀밭틀 미로] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요.");
            return false;
        }
        if (mLeaf2 == null) mLeaf2 = mLeaf;
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  미로 길 (갈림길 없는 한 줄 길)
    // ==================================================================
    List<Vector2> MakeMazeLine(out Rect rect)
    {
        float half = corridorWidth / 2f, pitch = corridorWidth + WALL_MIN + 2f * wiggle;
        int cols = Mathf.FloorToInt((COL_START_X - COL_END_X) / pitch) + 1;
        if (cols % 2 == 1) cols--;                 // 짝수 줄 → 출구가 입구와 같은 남쪽에
        cols = Mathf.Max(2, cols);
        var pts = new List<Vector2>();
        const float STEP = 0.1f;
        void Seg(Vector2 a, Vector2 b) { int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / STEP)); for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(a, b, k / (float)n)); }
        float Wig(int col, float z) => wiggle * Mathf.Sin(z * 1.35f + col * 1.7f) * Smooth01(Mathf.Min(z - STRAIGHT_Z0, STRAIGHT_Z1 - z) / 0.8f);

        // 입구 꼬리 (미로 밖 → 안)
        Seg(new Vector2(COL_START_X, STUB_Z), new Vector2(COL_START_X, STRAIGHT_Z0));
        for (int c = 0; c < cols; c++)
        {
            float x = COL_START_X - c * pitch; bool up = c % 2 == 0;
            float za = up ? STRAIGHT_Z0 : STRAIGHT_Z1, zb = up ? STRAIGHT_Z1 : STRAIGHT_Z0;
            int n = Mathf.CeilToInt(Mathf.Abs(zb - za) / STEP);
            for (int k = 0; k < n; k++) { float z = Mathf.Lerp(za, zb, k / (float)n); pts.Add(new Vector2(x + Wig(c, z), z)); }
            if (c == cols - 1) break;
            // 반원으로 돌아서 다음 줄로
            float cx = x - pitch / 2f, r = pitch / 2f; int m = Mathf.CeilToInt(Mathf.PI * r / STEP);
            for (int k = 0; k < m; k++)
            {
                float a = k / (float)m * Mathf.PI;
                pts.Add(up ? new Vector2(cx + r * Mathf.Cos(a), zb + r * Mathf.Sin(a)) : new Vector2(cx + r * Mathf.Cos(a), zb - r * Mathf.Sin(a)));
            }
        }
        // 출구 꼬리
        float xl = COL_START_X - (cols - 1) * pitch;
        Seg(new Vector2(xl, STRAIGHT_Z0), new Vector2(xl, STUB_Z)); pts.Add(new Vector2(xl, STUB_Z));

        float pad = half + 0.7f;
        rect = Rect.MinMaxRect(xl - pad - wiggle, STRAIGHT_Z0 - pitch / 2f - pad, COL_START_X + pad + wiggle, STRAIGHT_Z1 + pitch / 2f + pad);
        return pts;
    }

    float DistToLine(List<Vector2> line, Vector2 p, out Vector2 nearest)
    {
        float best = float.MaxValue; nearest = p;
        for (int i = 0; i < line.Count - 1; i++)
        {
            Vector2 a = line[i], b = line[i + 1], ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            var q = a + ab * t; float d = (p - q).sqrMagnitude;
            if (d < best) { best = d; nearest = q; }
        }
        return Mathf.Sqrt(best);
    }

    // ==================================================================
    //  울타리
    // ==================================================================
    void BuildHedges(Transform root, List<Vector2> line, Rect rect)
    {
        float half = corridorWidth / 2f, H = wallHeight;
        var side = new MB(1); var top = new MB(1); var grass = new MB(1);

        // 1) 길 양옆 울타리 면 (길 쪽을 바라봐요)
        // 길을 따라 0.25m 간격으로 다시 찍기
        var pl = Resample(line, 0.25f);
        foreach (float sg in new[] { -1f, 1f })
        {
            var rows = new List<int>(); var along = 0f;
            int cols = Mathf.CeilToInt(H / 0.35f);
            int prevStart = -1;
            for (int i = 0; i < pl.Count; i++)
            {
                if (i > 0) along += Vector2.Distance(pl[i - 1], pl[i]);
                var t = Tan(pl, i); var nrm = new Vector2(-t.y, t.x) * sg;   // 길 가운데 → 벽 쪽
                var basePt = pl[i] + nrm * half;
                bool inside = rect.Contains(basePt) && pl[i].y >= rect.yMin - 0.05f;
                if (!inside) { prevStart = -1; continue; }
                int start = side.v.Count;
                var face = new Vector3(-nrm.x, 0, -nrm.y);            // 벽면이 바라보는 쪽 = 길 쪽
                for (int k = 0; k <= cols; k++)
                {
                    float y = H * k / cols;
                    float bump = 0.07f * (Noise(along * 1.9f + sg * 13f, y * 1.7f) - 0.5f) * 2f + (k == 0 ? 0.05f : 0f);
                    var p = new Vector3(basePt.x, y, basePt.y) + face * bump;
                    side.Add(p, face, new Vector2(along / 1.6f, y / 1.6f));
                }
                if (prevStart >= 0)
                    for (int k = 0; k < cols; k++) { int a = prevStart + k, b = start + k; side.Tri(0, a, b, a + 1); side.Tri(0, b, b + 1, a + 1); }
                prevStart = start;

                // 윗모서리 덮개 (벽 위 틈 가리기)
                var e0 = new Vector3(basePt.x, H, basePt.y); var e1 = e0 - face * 0.35f;
                int tk = top.v.Count; top.Add(e0, Vector3.up, new Vector2(e0.x, e0.z) / 1.6f); top.Add(e1, Vector3.up, new Vector2(e1.x, e1.z) / 1.6f);
                if (tk >= 2 && i > 0 && rows.Count > 0 && rows[rows.Count - 1] == i - 1) { top.Tri(0, tk - 2, tk, tk - 1); top.Tri(0, tk, tk + 1, tk - 1); }
                rows.Add(i);

                // 울타리 밑동 풀
                if (rng.NextDouble() < 0.6) Ribbons(grass, new Vector3(basePt.x, 0.01f, basePt.y) - face * 0.03f + face * R(0f, 0.08f), R(0.12f, 0.3f), 5, 0.12f, 0.6f, 0.016f);
            }
        }

        // 2) 울타리 윗면 (안쪽이 꽉 찬 덩어리처럼 보이게)
        const float C = 0.2f;
        int nx = Mathf.CeilToInt(rect.width / C), nz = Mathf.CeilToInt(rect.height / C);
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x0 = rect.xMin + ix * C, z0 = rect.yMin + iz * C;
                bool all = true;
                for (int q = 0; q < 4 && all; q++)
                {
                    var p = new Vector2(x0 + (q & 1) * C, z0 + (q >> 1) * C);
                    if (DistToLineFast(pl, p) < half + 0.12f) all = false;
                }
                if (!all) continue;
                int k = top.v.Count;
                for (int q = 0; q < 4; q++) { var p = new Vector3(x0 + (q & 1) * C, H, z0 + (q >> 1) * C); top.Add(p, Vector3.up, new Vector2(p.x, p.z) / 1.6f); }
                top.Tri(0, k, k + 2, k + 1); top.Tri(0, k + 1, k + 2, k + 3);
            }

        // 3) 바깥 테두리 면 (입구·출구 자리는 비워요)
        var openings = new List<float> { line[0].x, line[line.Count - 1].x };
        OuterWall(side, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector3(0, 0, -1), openings, half);
        OuterWall(side, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), new Vector3(0, 0, 1), null, half);
        OuterWall(side, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), new Vector3(-1, 0, 0), null, half);
        OuterWall(side, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector3(1, 0, 0), null, half);
        // 테두리 위 둘레 띠 (네 변)
        RimStrip(top, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(0, 0.4f), openings, half);
        RimStrip(top, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(0, -0.4f), null, half);
        RimStrip(top, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(0.4f, 0), null, half);
        RimStrip(top, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(-0.4f, 0), null, half);

        var holder = new GameObject("초록 울타리 미로").transform; holder.SetParent(root, false);
        var sideGo = Emit(holder, "울타리 벽", side, new[] { mHedge }, ShadowCastingMode.On, true);
        Emit(holder, "울타리 윗면", top, new[] { mHedge }, ShadowCastingMode.On, true);
        Emit(holder, "울타리 밑동 풀", grass, new[] { mGrassBlade }, ShadowCastingMode.Off, false);
        if (sideGo != null) { var mc = sideGo.AddComponent<MeshCollider>(); mc.sharedMesh = sideGo.GetComponent<MeshFilter>().sharedMesh; }
    }

    void RimStrip(MB m, Vector2 a, Vector2 b, Vector2 inward, List<float> openings, float half)
    {
        float H = wallHeight; int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.3f);
        for (int i = 0; i < n; i++)
        {
            var p0 = Vector2.Lerp(a, b, i / (float)n); var p1 = Vector2.Lerp(a, b, (i + 1) / (float)n);
            bool gap = false;
            if (openings != null) foreach (var ox in openings) if (Mathf.Abs((p0.x + p1.x) * 0.5f - ox) < half + 0.15f) gap = true;
            if (gap) continue;
            int k = m.v.Count;
            foreach (var q in new[] { p0, p1, p1 + inward, p0 + inward }) m.Add(new Vector3(q.x, H, q.y), Vector3.up, q / 1.6f);
            m.Tri(0, k, k + 1, k + 2); m.Tri(0, k, k + 2, k + 3);
        }
    }

    void OuterWall(MB m, Vector2 a, Vector2 b, Vector3 face, List<float> openings, float half)
    {
        float H = wallHeight, len = Vector2.Distance(a, b); int n = Mathf.CeilToInt(len / 0.3f), cols = Mathf.CeilToInt(H / 0.35f);
        int prev = -1;
        for (int i = 0; i <= n; i++)
        {
            var p2 = Vector2.Lerp(a, b, i / (float)n);
            bool gap = false;
            if (openings != null) foreach (var ox in openings) if (Mathf.Abs(p2.x - ox) < half) gap = true;
            if (gap) { prev = -1; continue; }
            int start = m.v.Count;
            for (int k = 0; k <= cols; k++)
            {
                float y = H * k / cols, bump = 0.07f * (Noise(i * 0.55f + face.x * 7 + face.z * 3, y * 1.7f) - 0.5f) * 2f;
                m.Add(new Vector3(p2.x, y, p2.y) + face * bump, face, new Vector2(i * len / n / 1.6f, y / 1.6f));
            }
            if (prev >= 0) for (int k = 0; k < cols; k++) { int a0 = prev + k, b0 = start + k; m.Tri(0, a0, b0, a0 + 1); m.Tri(0, b0, b0 + 1, a0 + 1); }
            prev = start;
        }
    }

    static List<Vector2> Resample(List<Vector2> line, float step)
    {
        var o = new List<Vector2> { line[0] }; float acc = 0;
        for (int i = 1; i < line.Count; i++)
        {
            acc += Vector2.Distance(line[i - 1], line[i]);
            if (acc >= step) { o.Add(line[i]); acc = 0; }
        }
        if (o[o.Count - 1] != line[line.Count - 1]) o.Add(line[line.Count - 1]);
        return o;
    }
    static Vector2 Tan(List<Vector2> p, int i) { var d = p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(i - 1, 0)]; return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.up; }
    static float DistToLineFast(List<Vector2> pl, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pl.Count - 1; i++)
        {
            Vector2 a = pl[i], ab = pl[i + 1] - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            float d = (p - (a + ab * t)).sqrMagnitude; if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    // ==================================================================
    //  흙길 (미로 속 · 집 가는 오솔길)
    // ==================================================================
    void BuildFootpath(Transform root, string name, List<Vector2> line, float halfW, bool narrow)
    {
        var path = new MB(1); var stones = new MB(1); var grass = new MB(1);
        var pl = Resample(line, 0.12f); int n = pl.Count; float along = 0;
        var L = new Vector3[n]; var Rr = new Vector3[n]; var A = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i > 0) along += Vector2.Distance(pl[i - 1], pl[i]); A[i] = along;
            var t = Tan(pl, i); var nr = new Vector2(-t.y, t.x);
            float hw = halfW * (1f + 0.15f * (Noise(along * 1.3f, 5.5f) - 0.5f) * 2f);
            var c = pl[i] + nr * (0.05f * (Noise(along * 0.7f, 9.1f) - 0.5f) * 2f);
            L[i] = new Vector3(c.x - nr.x * hw, 0.025f, c.y - nr.y * hw); Rr[i] = new Vector3(c.x + nr.x * hw, 0.025f, c.y + nr.y * hw);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int k = path.v.Count;
            path.Add(L[i], Vector3.up, new Vector2(-halfW, A[i])); path.Add(L[i + 1], Vector3.up, new Vector2(-halfW, A[i + 1]));
            path.Add(Rr[i + 1], Vector3.up, new Vector2(halfW, A[i + 1])); path.Add(Rr[i], Vector3.up, new Vector2(halfW, A[i]));
            path.Tri(0, k, k + 1, k + 2); path.Tri(0, k, k + 2, k + 3);
        }
        // 디딤돌 (오솔길) / 자갈 (미로)
        if (!narrow)
            for (float d = 0.5f; d < along - 0.3f; d += R(0.75f, 1.0f))
            {
                int i = IndexAt(A, d); var p = (L[i] + Rr[i]) * 0.5f;
                FlatStone(stones, p + new Vector3(R(-0.06f, 0.06f), 0.004f, R(-0.06f, 0.06f)), R(0.2f, 0.27f), R(0.15f, 0.2f), R(0, 6.28f));
            }
        int pebbles = Mathf.RoundToInt(along * (narrow ? 3f : 6f));
        for (int k = 0; k < pebbles; k++) { int i = IndexAt(A, R(0, along)); Pebble(stones, Vector3.Lerp(L[i], Rr[i], R(0.05f, 0.95f)), R(0.012f, 0.035f)); }
        // 길섶 풀
        for (int i = 0; i < n; i += narrow ? 2 : 1)
            foreach (var e in new[] { L[i], Rr[i] })
                if (rng.NextDouble() < 0.7) Ribbons(grass, e - Vector3.up * 0.01f, R(0.08f, 0.2f), 5, 0.1f, 0.6f, 0.013f);
        var holder = new GameObject(name).transform; holder.SetParent(root, false);
        Emit(holder, "흙길", path, new[] { mPath }, ShadowCastingMode.Off, true);
        Emit(holder, "돌", stones, new[] { mPebble }, ShadowCastingMode.On, true);
        Emit(holder, "길섶 풀", grass, new[] { mGrassBlade }, ShadowCastingMode.Off, false);
    }

    static int IndexAt(float[] A, float d) { int i = System.Array.BinarySearch(A, d); if (i < 0) i = ~i; return Mathf.Clamp(i, 0, A.Length - 1); }

    List<Vector2> HousePath(List<Vector2> maze)
    {
        var exit = maze[maze.Count - 1];
        var c = new[] { exit, new Vector2(exit.x - 0.6f, 17.5f), new Vector2(HX + 0.8f, 11f), new Vector2(HX - 0.6f, 5f), new Vector2(HX, HZ + 4.2f) };
        var o = new List<Vector2>();
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Length - 1)];
            int n = Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.2f);
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        o.Add(c[c.Length - 1]);
        return o;
    }

    // ==================================================================
    //  초가집
    // ==================================================================
    void BuildHouse(Transform root)
    {
        var holder = new GameObject("초가집").transform; holder.SetParent(root, false);
        var stone = new MB(1); var plaster = new MB(1); var wood = new MB(1); var door = new MB(1); var straw = new MB(1); var jar = new MB(1);
        Vector3 O = new Vector3(HX, 0, HZ);
        const float BW = 3.7f, BD = 2.5f, BH = 0.55f;   // 기단 반너비·반깊이·높이
        const float WX = 3.3f, WZ = 2.0f, WTOP = 2.75f;  // 벽 선, 벽 윗높이
        // 기단 (돌)
        Box(stone, O + new Vector3(0, BH / 2, -0.2f), new Vector3(BW * 2, BH, BD * 2 + 0.4f), 0.9f);
        // 섬돌 (계단)
        Box(stone, O + new Vector3(0, 0.14f, BD + 0.55f), new Vector3(1.4f, 0.28f, 0.6f), 0.9f);
        // 마루 (앞쪽 툇마루)
        Box(wood, O + new Vector3(0, BH + 0.04f, WZ + 0.45f), new Vector3(WX * 2 + 0.3f, 0.08f, 0.9f), 1.2f);
        // 기둥
        float[] px = { -WX, -WX / 3f, WX / 3f, WX };
        foreach (float x in px)
        {
            Cyl(wood, O + new Vector3(x, BH, -WZ), 0.11f, WTOP - BH);
            Cyl(wood, O + new Vector3(x, BH, WZ), 0.11f, WTOP - BH);
            Cyl(wood, O + new Vector3(x, BH, WZ + 0.85f), 0.1f, WTOP - BH);
        }
        Cyl(wood, O + new Vector3(-WX, BH, 0), 0.11f, WTOP - BH); Cyl(wood, O + new Vector3(WX, BH, 0), 0.11f, WTOP - BH);
        // 도리 (윗 가로 나무)
        foreach (float z in new[] { -WZ, WZ, WZ + 0.85f }) Box(wood, O + new Vector3(0, WTOP - 0.08f, z), new Vector3(WX * 2 + 0.4f, 0.16f, 0.18f), 1.2f);
        foreach (float x in new[] { -WX, WX }) Box(wood, O + new Vector3(x, WTOP - 0.08f, 0.42f), new Vector3(0.18f, 0.16f, WZ * 2 + 0.85f), 1.2f);
        // 흙벽 (뒤·옆·앞)
        float wh = WTOP - 0.16f - BH, wy = BH + wh / 2;
        Box(plaster, O + new Vector3(0, wy, -WZ), new Vector3(WX * 2, wh, 0.14f), 1.5f);
        Box(plaster, O + new Vector3(-WX, wy, 0), new Vector3(0.14f, wh, WZ * 2), 1.5f);
        Box(plaster, O + new Vector3(WX, wy, 0), new Vector3(0.14f, wh, WZ * 2), 1.5f);
        Box(plaster, O + new Vector3(0, wy, WZ), new Vector3(WX * 2, wh, 0.14f), 1.5f);
        // 앞쪽 창호 문 (가운데 두 짝) + 옆 칸 창
        float fz = WZ + 0.075f;
        PanelQuad(door, O + new Vector3(-0.72f, BH + 0.12f, fz), 0.7f, 1.75f);
        PanelQuad(door, O + new Vector3(0.02f, BH + 0.12f, fz), 0.7f, 1.75f);
        PanelQuad(door, O + new Vector3(-WX * 2f / 3f - 0.4f, BH + 0.85f, fz), 0.8f, 0.75f);
        PanelQuad(door, O + new Vector3(WX * 2f / 3f - 0.4f, BH + 0.85f, fz), 0.8f, 0.75f);
        // 초가 지붕 (둥근 볏짚 지붕)
        Roof(straw, O + new Vector3(0, WTOP + 0.3f, 0.42f), WX + 1.15f, WZ + 1.35f, 1.9f);
        // 장독대
        var jo = O + new Vector3(BW + 2.2f, 0, 0.6f);
        Box(stone, jo + new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 1.7f), 0.9f);
        for (int i = 0; i < 6; i++) Jar(jar, jo + new Vector3(-0.8f + (i % 3) * 0.8f + R(-0.08f, 0.08f), 0.3f, -0.4f + (i / 3) * 0.8f), R(0.6f, 0.95f));
        // 감나무 한 그루
        var leaves = new MB(1); var bark = new MB(1);
        Tree(leaves, bark, O + new Vector3(-BW - 3f, 0, 3.2f), 6.5f);

        Emit(holder, "기단·돌", stone, new[] { mStone }, ShadowCastingMode.On, true);
        Emit(holder, "흙벽", plaster, new[] { mPlaster }, ShadowCastingMode.On, true);
        Emit(holder, "나무", wood, new[] { mWood }, ShadowCastingMode.On, true);
        Emit(holder, "창호", door, new[] { mDoor }, ShadowCastingMode.On, false);
        Emit(holder, "초가 지붕", straw, new[] { mStraw }, ShadowCastingMode.On, true);
        Emit(holder, "장독", jar, new[] { mJar }, ShadowCastingMode.On, false);
        Emit(holder, "감나무 잎", leaves, new[] { mLeaf2 }, ShadowCastingMode.On, false);
        Emit(holder, "감나무 줄기", bark, new[] { mBark }, ShadowCastingMode.On, false);

        // 충돌 상자 (집 + 마루)
        var col = new GameObject("집 충돌").AddComponent<BoxCollider>();
        col.transform.SetParent(holder, false);
        col.center = O + new Vector3(0, 1.6f, 0.1f); col.size = new Vector3(BW * 2 + 0.2f, 3.2f, BD * 2 + 1.2f);
        houseRect = Rect.MinMaxRect(HX - BW - 0.15f, HZ - BD - 0.45f, HX + BW + 0.15f, HZ + BD + 0.75f);
    }

    static void Box(MB m, Vector3 c, Vector3 s, float tile)
    {
        var h = s * 0.5f;
        Vector3[] nrm = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var n in nrm)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized * -1f;
            Vector3 v = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
            float hu = Mathf.Abs(Vector3.Dot(u, h)), hv = Mathf.Abs(Vector3.Dot(v, h)), hn = Mathf.Abs(Vector3.Dot(n, h));
            var cc = c + n * hn; int k = m.v.Count;
            for (int q = 0; q < 4; q++)
            {
                float a = (q == 0 || q == 3) ? -1 : 1, b = q < 2 ? -1 : 1;
                var p = cc + u * (a * hu) + v * (b * hv);
                m.Add(p, n, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) / tile);
            }
            m.Tri(0, k, k + 1, k + 2); m.Tri(0, k, k + 2, k + 3);
        }
    }

    static void Cyl(MB m, Vector3 b, float r, float h)
    {
        const int S = 10; int k0 = m.v.Count;
        for (int i = 0; i <= S; i++)
        {
            float a = i / (float)S * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            m.Add(b + d * r, d, new Vector2(i / (float)S * 0.6f, 0)); m.Add(b + d * r * 0.94f + Vector3.up * h, d, new Vector2(i / (float)S * 0.6f, h));
        }
        for (int i = 0; i < S; i++) { int a = k0 + i * 2; m.Tri(0, a, a + 1, a + 3); m.Tri(0, a, a + 3, a + 2); }
    }

    // 문·창 한 장 (앞쪽 +z를 봐요)
    static void PanelQuad(MB m, Vector3 bottomLeft, float w, float h)
    {
        int k = m.v.Count; var n = Vector3.forward;
        m.Add(bottomLeft, n, new Vector2(0, 0)); m.Add(bottomLeft + Vector3.right * w, n, new Vector2(1, 0));
        m.Add(bottomLeft + new Vector3(w, h, 0), n, new Vector2(1, 1)); m.Add(bottomLeft + Vector3.up * h, n, new Vector2(0, 1));
        m.Tri(0, k, k + 1, k + 2); m.Tri(0, k, k + 2, k + 3);
    }

    // 둥근 볏짚 지붕: 윗면(둥근 언덕) + 처마 두께 + 처마 밑면
    void Roof(MB m, Vector3 c, float ax, float az, float h)
    {
        const int RINGS = 14, SEG = 48; const float N = 3.2f, THICK = 0.28f;
        int k0 = m.v.Count;
        for (int i = 0; i <= RINGS; i++)
        {
            float rho = 1f - i / (float)RINGS;                       // 1 = 처마 끝, 0 = 꼭대기
            float y = h * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(rho, 1.7f)), 0.62f);
            for (int j = 0; j <= SEG; j++)
            {
                float a = j / (float)SEG * Mathf.PI * 2, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float ex = Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 2f / N), ez = Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 2f / N);
                float lump = 1f + 0.025f * (Noise(j * 0.9f, i * 0.7f) - 0.5f);
                var p = c + new Vector3(ex * ax * rho * lump, y, ez * az * rho * lump);
                var nr = new Vector3(ex / ax, 0, ez / az).normalized * (1.2f * rho) + Vector3.up; nr.Normalize();
                m.Add(p, nr, new Vector2(j / (float)SEG * 9f, (1 - rho) * 2.2f));
            }
        }
        for (int i = 0; i < RINGS; i++)
            for (int j = 0; j < SEG; j++) { int a = k0 + i * (SEG + 1) + j, b = a + SEG + 1; m.Tri(0, a, a + 1, b); m.Tri(0, a + 1, b + 1, b); }
        // 처마 두께 (둥근 띠) + 밑면
        int e0 = m.v.Count;
        for (int j = 0; j <= SEG; j++)
        {
            float a = j / (float)SEG * Mathf.PI * 2, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float ex = Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 2f / N), ez = Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 2f / N);
            var o = new Vector3(ex * ax, 0, ez * az); var nr = new Vector3(ex / ax, 0, ez / az).normalized;
            m.Add(c + o, nr, new Vector2(j / (float)SEG * 9f, 0)); m.Add(c + o * 1.02f - Vector3.up * THICK, nr, new Vector2(j / (float)SEG * 9f, 0.25f));
            m.Add(c + o * 1.02f - Vector3.up * THICK, Vector3.down, new Vector2(j / (float)SEG * 9f, 0.25f)); m.Add(c + o * 0.6f - Vector3.up * THICK, Vector3.down, new Vector2(j / (float)SEG * 9f, 0.9f));
        }
        for (int j = 0; j < SEG; j++)
        {
            int a = e0 + j * 4, b = a + 4;
            m.Tri(0, a, b, a + 1); m.Tri(0, b, b + 1, a + 1);
            m.Tri(0, a + 2, b + 2, a + 3); m.Tri(0, b + 2, b + 3, a + 3);
        }
    }

    // 옹기 장독 (돌려 깎은 모양)
    void Jar(MB m, Vector3 b, float s)
    {
        float[] hy = { 0f, 0.08f, 0.3f, 0.5f, 0.66f, 0.72f, 0.78f, 0.8f };
        float[] rr = { 0.17f, 0.24f, 0.33f, 0.34f, 0.25f, 0.18f, 0.19f, 0.0f };
        const int S = 16; int k0 = m.v.Count;
        for (int i = 0; i < hy.Length; i++)
            for (int j = 0; j <= S; j++)
            {
                float a = j / (float)S * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float dr = i > 0 && i < hy.Length - 1 ? (rr[i + 1] - rr[i - 1]) / Mathf.Max(0.01f, hy[i + 1] - hy[i - 1]) : 0f;
                var nr = (d - Vector3.up * dr).normalized; if (i == hy.Length - 1) nr = Vector3.up;
                m.Add(b + (d * rr[i] + Vector3.up * hy[i]) * s, nr, new Vector2(j / (float)S, hy[i]));
            }
        for (int i = 0; i < hy.Length - 1; i++)
            for (int j = 0; j < S; j++) { int a = k0 + i * (S + 1) + j, c = a + S + 1; m.Tri(0, a, a + 1, c); m.Tri(0, a + 1, c + 1, c); }
    }

    void Tree(MB leaves, MB bark, Vector3 basePos, float height)
    {
        float trunkH = height * 0.5f, rad = height * 0.035f; const int sides = 8; int k0 = bark.v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.Add(basePos + d * rad * 1.3f, d, new Vector2(i / (float)sides, 0)); bark.Add(basePos + d * rad * 0.6f + Vector3.up * trunkH, d, new Vector2(i / (float)sides, 1));
        }
        for (int i = 0; i < sides; i++) { int a = k0 + i * 2; bark.Tri(0, a, a + 1, a + 3); bark.Tri(0, a, a + 3, a + 2); }
        float crown = height * 0.32f;
        for (int b = 0; b < 6; b++) Blob(leaves, basePos + Vector3.up * (trunkH + crown * R(0.1f, 0.9f)) + new Vector3(R(-1, 1), 0, R(-1, 1)) * crown * 0.6f, crown * R(0.55f, 0.85f));
    }
    void Blob(MB m, Vector3 c, float r)
    {
        const int LA = 7, LO = 10; int k0 = m.v.Count; float seed = R(0, 100);
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI, ph = j / (float)LO * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th) * 0.85f, Mathf.Sin(th) * Mathf.Sin(ph));
                m.Add(c + d * r * (0.75f + 0.45f * Fbm(d.x * 2.2f + seed, d.z * 2.2f + d.y * 1.7f + seed)), d.normalized, new Vector2(j / (float)LO, i / (float)LA));
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++) { int a = k0 + i * (LO + 1) + j, b = a + LO + 1; m.Tri(0, a, a + 1, b); m.Tri(0, a + 1, b + 1, b); }
    }

    void FlatStone(MB m, Vector3 c, float rx, float rz, float rot)
    {
        const int N = 9; float th = R(0.03f, 0.05f);
        var dx = new Vector3(Mathf.Cos(rot), 0, Mathf.Sin(rot)); var dz = new Vector3(-dx.z, 0, dx.x);
        var rim = new Vector3[N]; float seed = R(0, 50);
        for (int i = 0; i < N; i++) { float a = i / (float)N * Mathf.PI * 2, k = 0.8f + 0.35f * Noise(Mathf.Cos(a) * 1.5f + seed, Mathf.Sin(a) * 1.5f + seed); rim[i] = dx * (Mathf.Cos(a) * rx * k) + dz * (Mathf.Sin(a) * rz * k); }
        int ct = m.v.Count; m.Add(c + Vector3.up * (th + 0.01f), Vector3.up, new Vector2(c.x, c.z) * 3f);
        for (int i = 0; i < N; i++) { var p = c + rim[i] + Vector3.up * th; m.Add(p, (Vector3.up * 3f + rim[i].normalized).normalized, new Vector2(p.x, p.z) * 3f); }
        for (int i = 0; i < N; i++) m.Tri(0, ct, ct + 1 + i, ct + 1 + (i + 1) % N);
        int sb = m.v.Count;
        for (int i = 0; i < N; i++) { var o = rim[i].normalized; m.Add(c + rim[i] + Vector3.up * th, (o + Vector3.up * 0.4f).normalized, new Vector2(i / (float)N, 0)); m.Add(c + rim[i] * 1.08f - Vector3.up * 0.02f, o, new Vector2(i / (float)N, 0.3f)); }
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
                m.Add(p, (ax * dx + az * dz + Vector3.up * dy * 1.5f).normalized, new Vector2(p.x, p.z) * 3f);
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++) { int a = k0 + i * (LO + 1) + j, b = a + LO + 1; m.Tri(0, a, a + 1, b); m.Tri(0, a + 1, b + 1, b); }
    }

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
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(a + 3); m.sub[0].Add(a); m.sub[0].Add(a + 3); m.sub[0].Add(a + 2); }
        }
    }

    // ==================================================================
    //  빛가루 길: 들어오는 길 → ㄹ → ㅏ → 미로 → 집
    // ==================================================================
    System.Reflection.FieldInfo GlowField(out Component wp)
    {
        wp = GetComponent("NonteulWindingPath");
        if (wp == null) return null;
        return wp.GetType().GetField("glowRoute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
    }
    void RerouteGlow(List<Vector2> maze, List<Vector2> toHouse)
    {
        var f = GlowField(out var wp); if (f == null) return;
        var list = f.GetValue(wp) as List<Vector3>; if (list == null) return;
        if (savedGlow == null || savedGlow.Count == 0) savedGlow = new List<Vector3>(list);
        // 들어오는 꼬불길 (꼬불꼬불 스크립트와 같은 곡선)
        var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
        var pa = new List<Vector3>(); Spline(a, 0.35f, pa);
        list.Clear();
        void AddW(Vector3 p) => list.Add(transform.TransformPoint(p));
        for (int i = 0; i < pa.Count; i += 3) AddW(pa[i] + Vector3.up * 0.9f);
        float[,] L = { { 2, -16.5f }, { -2, -16.5f }, { -2, -11.5f }, { 2, -11.5f }, { 2, -6.5f }, { -2, -6.5f }, { -2, -4.6f }, { -2, 0.4f }, { 2, 0.4f }, { 2, 5.4f }, { -2, 5.4f }, { 4.4f, 9.5f }, { 4.4f, 13f }, { 4.4f, 17f }, { 4.1f, 21f } };
        for (int i = 0; i < L.GetLength(0); i++) AddW(new Vector3(L[i, 0], 1.0f, L[i, 1]));
        for (int i = 0; i < maze.Count; i += 12) AddW(new Vector3(maze[i].x, 1.1f, maze[i].y));
        for (int i = 0; i < toHouse.Count; i += 6) AddW(new Vector3(toHouse[i].x, 1.0f, toHouse[i].y));
        MarkDirty(wp);
    }
    void RestoreGlow()
    {
        var f = GlowField(out var wp); if (f == null || savedGlow == null || savedGlow.Count == 0) return;
        var list = f.GetValue(wp) as List<Vector3>; if (list == null) return;
        list.Clear(); list.AddRange(savedGlow); savedGlow = new List<Vector3>(); MarkDirty(wp);
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

    // ==================================================================
    //  플레이 중: 체험 카메라가 울타리·집을 못 뚫게
    // ==================================================================
    Transform camT; bool inCorridor;
    void Start()
    {
        if (!blockCamera || !built) return;
        var ws = FindFirstObjectByType<Byeolmalsseum.WordSpace>();
        if (ws != null && ws.experienceCamera != null) camT = ws.experienceCamera.transform;
        else if (Camera.main != null) camT = Camera.main.transform;
    }
    void LateUpdate()
    {
        if (camT == null || mazeLine == null || mazeLine.Count < 2) return;
        var lp = transform.InverseTransformPoint(camT.position);
        var p = new Vector2(lp.x, lp.z); var orig = p; float y = lp.y;
        float allow = Mathf.Max(0.05f, builtHalf - cameraRadius);

        // 미로
        var box = new Rect(mazeRect.xMin - cameraRadius, mazeRect.yMin - cameraRadius, mazeRect.width + cameraRadius * 2, mazeRect.height + cameraRadius * 2);
        if (box.Contains(p))
        {
            float d = DistToLine(mazeLine, p, out var q);
            if (d <= allow) inCorridor = true;
            else if (inCorridor)
            {
                var dir = (p - q); dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                p = q + dir * allow;                                    // 길 안쪽으로 밀어 넣기
            }
            else p = PushOut(box, p);                                   // 바깥에서 벽으로 → 바깥으로
            if (inCorridor) y = Mathf.Clamp(y, 0.6f, Mathf.Max(1.2f, builtWallH - 1.0f)); // 울타리 위로 못 올라가게
        }
        else if (DistToLine(mazeLine, p, out _) > allow + 0.5f) inCorridor = false;

        // 집
        var hb = new Rect(houseRect.xMin - cameraRadius, houseRect.yMin - cameraRadius, houseRect.width + cameraRadius * 2, houseRect.height + cameraRadius * 2);
        if (hb.Contains(p) && y < 3.6f) p = PushOut(hb, p);

        if (p != orig || !Mathf.Approximately(y, lp.y)) camT.position = transform.TransformPoint(new Vector3(p.x, y, p.y));
    }
    static Vector2 PushOut(Rect r, Vector2 p)
    {
        float l = p.x - r.xMin, rr = r.xMax - p.x, b = p.y - r.yMin, t = r.yMax - p.y, m = Mathf.Min(Mathf.Min(l, rr), Mathf.Min(b, t));
        if (m == l) p.x = r.xMin - 0.01f; else if (m == rr) p.x = r.xMax + 0.01f; else if (m == b) p.y = r.yMin - 0.01f; else p.y = r.yMax + 0.01f;
        return p;
    }

    // ==================================================================
    //  질감
    // ==================================================================
    void MakeOwnMaterials()
    {
        mHedge = GetMat("미로_울타리", Color.white, GetTex("울타리_색", MakeHedgeAlbedo), GetTex("울타리_노멀", () => NormalFrom(HedgeHeight(256), 256, 6f, "울타리_노멀")), 0.25f, 1.2f, Vector2.one);
        mStraw = GetMat("집_초가", Color.white, GetTex("초가_색", MakeStrawAlbedo), GetTex("초가_노멀", () => NormalFrom(StrawHeight(256), 256, 5f, "초가_노멀")), 0.1f, 1.3f, Vector2.one);
        mPlaster = GetMat("집_흙벽", Color.white, GetTex("흙벽_색", MakePlasterAlbedo), null, 0.08f, 1f, Vector2.one);
        mWood = GetMat("집_나무", Color.white, GetTex("나무_색", MakeWoodAlbedo), GetTex("나무_노멀", () => NormalFrom(WoodHeight(256), 256, 2.5f, "나무_노멀")), 0.22f, 1f, Vector2.one);
        mDoor = GetMat("집_창호", Color.white, GetTex("창호_색", MakeDoorAlbedo), null, 0.1f, 1f, Vector2.one);
        mStone = GetMat("집_돌", Color.white, GetTex("돌_색", MakeStoneAlbedo), GetTex("돌_노멀", () => NormalFrom(StoneHeight(256), 256, 6f, "돌_노멀")), 0.18f, 1.2f, Vector2.one);
        mPebble = mStone;
        mJar = GetMat("집_옹기", new Color(0.30f, 0.17f, 0.10f), null, null, 0.72f, 1f, Vector2.one);
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
        if (tex != null && m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", scale); }
        if (tex != null && m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", scale); }
        if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (isNew) SaveAsset(m, name + ".mat"); else MarkDirty(m);
        return m;
    }
    static Texture2D NewTex(int S, bool linear) =>
        new Texture2D(S, S, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
    static Texture2D Paint(int S, System.Func<float, float, Color> f)
    {
        var t = NewTex(S, false); var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { var c = f(x / (float)S, y / (float)S); c.a = 1; px[y * S + x] = c; }
        t.SetPixels(px); t.Apply(true); return t;
    }
    static float[] HeightMap(int S, System.Func<float, float, float> f)
    {
        var h = new float[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) h[y * S + x] = f(x / (float)S, y / (float)S);
        return h;
    }

    // 울타리 잎: 잔잎이 빽빽한 생울타리
    static float HedgeH(float u, float v) { float w = FbmP(u * 6, v * 6, 6, 3); return FbmP(u * 28 + w * 3f, v * 28 + w * 2f, 28, 3); }
    static float[] HedgeHeight(int S) => HeightMap(S, HedgeH);
    static Texture2D MakeHedgeAlbedo() => Paint(256, (u, v) =>
    {
        float n = HedgeH(u, v), leaf = Smooth01((n - 0.5f) / 0.2f), gap = Smooth01((0.42f - n) / 0.15f);
        Color c = Color.Lerp(new Color(0.12f, 0.27f, 0.08f), new Color(0.24f, 0.42f, 0.13f), FbmP(u * 3, v * 3, 3, 2));
        c *= 1f + 0.55f * leaf; c = Color.Lerp(c, new Color(0.40f, 0.55f, 0.18f), leaf * 0.18f);
        return c * (1f - 0.6f * gap);
    });
    // 볏짚: 결 방향 줄무늬 + 새끼줄 그물 + 오래된 회색빛
    static float StrawH(float u, float v) => FbmP2(u * 64, v * 4, 64, 4, 3) * 0.8f + 0.2f * FbmP(u * 8, v * 8, 8, 2);
    static float[] StrawHeight(int S) => HeightMap(S, (u, v) => StrawH(u, v) - 0.5f * RopeLine(u, v));
    static float RopeLine(float u, float v)
    {
        float a = Mathf.Repeat((u + v) * 6f, 1f), b = Mathf.Repeat((u - v) * 6f, 1f);
        return Mathf.Max(Smooth01(1f - Mathf.Abs(a - 0.5f) * 2f / 0.08f), Smooth01(1f - Mathf.Abs(b - 0.5f) * 2f / 0.08f));
    }
    static Texture2D MakeStrawAlbedo() => Paint(256, (u, v) =>
    {
        float s = StrawH(u, v), age = FbmP(u * 3, v * 3, 3, 3);
        Color c = Color.Lerp(new Color(0.42f, 0.35f, 0.22f), new Color(0.74f, 0.63f, 0.40f), s);
        c = Color.Lerp(c, new Color(0.45f, 0.43f, 0.38f), Smooth01((age - 0.45f) / 0.25f) * 0.55f);
        return Color.Lerp(c, new Color(0.30f, 0.24f, 0.15f), RopeLine(u, v) * 0.6f);
    });
    static Texture2D MakePlasterAlbedo() => Paint(128, (u, v) =>
    {
        float n = FbmP(u * 6, v * 6, 6, 4), straw = FbmP2(u * 48, v * 6, 48, 6, 2);
        Color c = Color.Lerp(new Color(0.78f, 0.70f, 0.56f), new Color(0.88f, 0.82f, 0.70f), n);
        return c * (0.93f + 0.1f * straw);
    });
    static float WoodH(float u, float v) => FbmP2(u * 3, v * 40 + FbmP(u * 2, v * 2, 2, 2) * 6f, 3, 40, 3);
    static float[] WoodHeight(int S) => HeightMap(S, WoodH);
    static Texture2D MakeWoodAlbedo() => Paint(256, (u, v) => Color.Lerp(new Color(0.33f, 0.21f, 0.12f), new Color(0.55f, 0.38f, 0.22f), WoodH(u, v)));
    // 창호: 한지 + 나무 살
    static Texture2D MakeDoorAlbedo()
    {
        const int S = 256; var t = NewTex(S, false); t.wrapMode = TextureWrapMode.Clamp; var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                bool frame = u < 0.07f || u > 0.93f || v < 0.05f || v > 0.95f;
                float gu = Mathf.Repeat(u * 4f, 1f), gv = Mathf.Repeat(v * 7f, 1f);
                bool bar = gu < 0.06f || gu > 0.94f || gv < 0.04f || gv > 0.96f;
                Color paper = new Color(0.92f, 0.88f, 0.78f) * (0.95f + 0.08f * FbmP(u * 16, v * 16, 16, 2));
                Color c = frame ? new Color(0.36f, 0.23f, 0.13f) : (bar ? new Color(0.45f, 0.30f, 0.17f) : paper);
                c.a = 1; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true); return t;
    }
    // 돌 (기단·디딤돌)
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
    static float StoneH(float u, float v)
    {
        Voronoi(u * 5, v * 5, 5, out float f1, out float f2, out _);
        return Smooth01((f2 - f1 - 0.03f) / 0.12f) * (0.5f + 0.35f * Mathf.Sqrt(Mathf.Clamp01(1f - f1 * 1.3f)) + 0.15f * FbmP(u * 32, v * 32, 32, 3));
    }
    static float[] StoneHeight(int S) => HeightMap(S, StoneH);
    static Texture2D MakeStoneAlbedo() => Paint(256, (u, v) =>
    {
        Voronoi(u * 5, v * 5, 5, out float f1, out float f2, out int id);
        float stone = Smooth01((f2 - f1 - 0.03f) / 0.12f);
        Color sc = Color.Lerp(new Color(0.38f, 0.36f, 0.32f), new Color(0.60f, 0.57f, 0.50f), Hash(id, 7)) * (0.85f + 0.3f * FbmP(u * 24, v * 24, 24, 3));
        return Color.Lerp(new Color(0.20f, 0.17f, 0.13f), sc, stone);
    });
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
        unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f; }
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
    static float NoiseP2(float x, float y, int Px, int Py)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        int x0 = ((xi % Px) + Px) % Px, y0 = ((yi % Py) + Py) % Py, x1 = (x0 + 1) % Px, y1 = (y0 + 1) % Py;
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), u), Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), u), v);
    }
    static float FbmP2(float x, float y, int Px, int Py, int oct)
    {
        float s = 0, a = 0.5f, n = 0;
        for (int i = 0; i < oct; i++) { s += a * NoiseP2(x, y, Px, Py); n += a; x *= 2; y *= 2; Px *= 2; Py *= 2; a *= 0.5f; }
        return s / n;
    }
    static float FbmP(float x, float y, int P, int oct) => FbmP2(x, y, P, P, oct);

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
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length; for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            m.RecalculateBounds(); if (tangents) m.RecalculateTangents();
            return m;
        }
    }

    GameObject Emit(Transform parent, string name, MB mb, Material[] mats, ShadowCastingMode cast, bool tangents)
    {
        if (mb.v.Count == 0) return null;
        var mesh = mb.ToMesh(name, tangents);
        int idx = 0; foreach (var s in mb.sub) idx += s.Count;
        float mbSize = (mb.v.Count * (tangents ? 48f : 32f) * 2f + idx * 4f * 2f) / (1024f * 1024f);
        if (mbSize > 45f) Debug.LogWarning("[논틀밭틀 미로] '" + name + "' 메시가 커요 (약 " + mbSize.ToString("0") + "MB).");
        SaveAsset(mesh, "메시_미로_" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = cast; mr.receiveShadows = true;
        return go;
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "미로");
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
        EditorUtility.DisplayProgressBar("논틀밭틀 미로", msg, p);
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
[CustomEditor(typeof(NonteulMaze))]
public class NonteulMazeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulMaze)target;
        EditorGUILayout.HelpBox("녹차밭 골짜기 언덕을 지우고, 글자 북쪽에 초록 울타리 미로와 떨어진 초가집을 만들어요.\n(ㄹ 꼬불길 장식은 남겨요. NonteulTeaValley의 [만들기]는 다시 누르지 마세요.)\n값을 바꾼 뒤에는 [만들기]를 다시 누르면 돼요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🌿 미로 + 초가집 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("미로 + 초가집 지우기", GUILayout.Height(24))) Run(s.Remove);
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
