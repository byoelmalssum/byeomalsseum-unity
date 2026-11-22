// =====================================================================
//  NonteulRiulVillage.cs — ㄹ 길 → 가운데 단 S자 꽃길 → 글자 둘레 풍성한 마을 (URP)
//
//  지금까지 만든 것(계단 다원·가까운 언덕)은 그대로 두고, 「공간」에 이 스크립트만 더 붙여요.
//   1) ㄹ 길 : 두 ㄹ 위 흙길·길섶 풀 (ㄹ이 내려앉은 뒤 나타나요) + ㄹ 둘레 키 큰 풀숲, ㄹ 사이 돌계단
//   2) 가운데 단 : 둥근 차밭 언덕만 걷어내고 S자로 굽이치는 꽃길 (키 큰 풀·들꽃·덤불·작은 나무)
//                  끝에서 돌계단으로 마을로 내려가요
//   3) 마을 : 글자 둘레를 한 바퀴 도는 구불구불한 마을 길, 돌담 두른 초가집 7채(장독대·감나무),
//             집마다 마을 길로 이어지는 앞길, 키 큰 활엽수·소나무 숲, 덤불, 풀숲, 들꽃
//   4) 플레이 중 : ㄹ 길 위에서 출발해 마을로 내려가는 계단 끝까지 길에서 떨어지지 않게 붙잡아 줘요.
//                  움직일 수 있는 폭은 넓게(ㄹ 위 양옆 1.2m, 꽃길 1.3m). 계단 끝부터는 자유롭게. Shift = 빠르게.
//  만든 파일은 Assets/논틀밭틀_생성/미로 폴더에 저장돼요 (파일 하나 50MB 안 넘게).
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(10002)] // 체험 카메라 · 충돌 검사가 끝난 다음에 길 붙잡기
public class NonteulRiulVillage : MonoBehaviour
{
    [Header("길에서 움직일 수 있는 폭")]
    [Tooltip("ㄹ 위: 길 가운데에서 양옆으로 몇 m까지")] [Range(0.3f, 2.5f)] public float riulHalf = 1.2f;
    [Tooltip("가운데 단 꽃길: 양옆으로 몇 m까지")] [Range(0.3f, 2.5f)] public float pathHalf = 1.3f;
    // (예전 미로 코드가 쓰던 값 — 지금은 안 써요)
    [HideInInspector] public float corridorWidth = 1.1f, wallHeight = 4.5f, wiggle = 0.12f;

    [Header("플레이 중")]
    [Tooltip("시작하면 길 위에 올려놓고 길에서 벗어나지 않게")] public bool lockToPath = true;
    [Tooltip("길 바닥에서 눈까지 높이 (m)")] [Range(0.8f, 3f)] public float eyeHeight = 1.6f;
    [Tooltip("시작 위치가 ㄹ 길 시작점에서 이 거리 안이면 길 위로 옮겨 놓아요 (m)")] [Range(0f, 40f)] public float snapRadius = 15f;
    [Tooltip("Shift를 누르면 몇 배 빨라질지")] [Range(1f, 10f)] public float shiftMultiplier = 4f;

    // ---- 버튼이 채우는 값 ----
    [HideInInspector] public List<Vector3> route = new List<Vector3>();   // (x, 길바닥 높이, z) — 「공간」 기준
    [HideInInspector] public List<float> routeHalf = new List<float>();
    [HideInInspector] public Rect houseRect;
    [HideInInspector] public List<Vector3> savedGlow = new List<Vector3>();
    [HideInInspector] public Transform riulDecor;
    [HideInInspector] public Byeolmalsseum.JamoPiece[] riulPieces = new Byeolmalsseum.JamoPiece[0];
    [HideInInspector] public bool built;

    // ------------------------------------------------------------------
    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/미로";
    const string ROOT_NAME = "논틀밭틀_ㄹ길·마을 (자동 생성)";
    const string WP_ROOT = "논틀밭틀_꼬불꼬불 (자동 생성)";
    static readonly string[] WP_HIDE = { "다랑논 바닥", "다랑논 물", "다랑논 층벽", "굽은 논둑 (윗면)", "굽은 논둑 (옆면)", "벼 (멀리)", "벼 (가장자리)", "모" };

    const float WALL_MIN = 0.6f; // 길 사이 울타리 최소 두께

    System.Random rng;
    Material mHedge, mPath, mGrassBlade, mStraw, mPlaster, mWood, mDoor, mStone, mJar, mLeaf, mLeaf2, mBark, mPebble;

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
#if UNITY_EDITOR
        mWild = AssetDatabase.LoadAssetAtPath<Material>("Assets/논틀밭틀_생성/꼬불/들꽃.mat");
#endif
        rng = new System.Random(8181);
        Remove();
        SetWindingTerraces(false);
        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);

        Progress("질감 준비 중…", 0.03f);
        MakeOwnMaterials();

        var tt = GetComponent<NonteulTeaTerrace>();
        raised = tt != null && tt.raiseLetters && transform.Find(TERRACE_ROOT) != null;
        lvScale = tt != null ? tt.levelScale : 1f;
        float z3 = ZoneLevel(0f, -11f), z4 = ZoneLevel(0f, 0f), z5 = ZoneLevel(0f, 14f);
        var p6 = FindPiece(0f, -11.5f, z3 + 0.4f, out float t6);
        var p12 = FindPiece(0f, 0.4f, z4 + 0.4f, out float t12);
        var hb = GetComponent<NonteulHillsAndBlock>(); hillR = hb != null ? hb.hillStart : 37f;

        Progress("가운데 단 차밭 걷어내는 중…", 0.06f);
        CutCenterTerrace();

        // ---- 가운데 단 위 S자 꼬불길 ----
        var mid = new List<Vector2>();
        for (float z = 6.3f; z <= 21.55f; z += 0.1f)
        {
            float t = Mathf.Clamp01((z - 7.0f) / 14.6f), env = Smooth01(Mathf.Min(z - 6.3f, 21.55f - z) / 1.2f);
            mid.Add(new Vector2(-1.9f + env * 1.9f * (1f + Mathf.Sin(t * Mathf.PI * 4f - Mathf.PI / 2f)), z));
        }

        // ---- 걷는 길 (ㄹ → 계단 → ㄹ → 계단 → S자 길 → 계단) ----
        route.Clear(); routeHalf.Clear();
        var riulA = new[] { V(2.35f, t6, -16.5f), V(-2f, t6, -16.5f), V(-2f, t6, -11.5f), V(2f, t6, -11.5f), V(2f, t6, -6.5f), V(-2f, t6, -6.5f) };
        var riulB = new[] { V(-2f, t12, -4.6f), V(-2f, t12, 0.4f), V(2f, t12, 0.4f), V(2f, t12, 5.4f), V(-2.4f, t12, 5.4f) };
        var c1 = new List<Vector3>(riulA); c1.Add(V(-2f, t6, -6.1f)); c1.Add(V(-2f, t12, -5.0f)); c1.AddRange(riulB);
        c1.Add(V(-1.9f, t12, 6.1f)); c1.Add(V(-1.9f, z5, 7.0f));
        foreach (var p in Fillet3(c1, 0.3f, 0.1f)) { route.Add(p); routeHalf.Add(Mathf.Abs(p.y - t6) < 0.01f || Mathf.Abs(p.y - t12) < 0.01f ? riulHalf : 0.5f); }
        foreach (var m in mid) if (m.y > 7.0f) { route.Add(V(m.x, z5, m.y)); routeHalf.Add(pathHalf); }
        for (int k = 1; k <= 20; k++) { float t = k / 20f; route.Add(V(-1.9f, Mathf.Lerp(z5, 0f, t), Mathf.Lerp(21.6f, 26.4f, t))); routeHalf.Add(0.5f); }

        Progress("ㄹ 길 꾸미는 중…", 0.1f);
        var decor = new GameObject("ㄹ 길 (ㄹ이 내려앉은 뒤 보여요)").transform; decor.SetParent(root, false);
        BuildRiulStrip(decor, Fillet3(new List<Vector3>(riulA), 0.3f, 0.1f), "아래 ㄹ");
        BuildRiulStrip(decor, Fillet3(new List<Vector3>(riulB), 0.3f, 0.1f), "위 ㄹ");
        riulDecor = decor; riulPieces = new[] { p6, p12 };
        BuildTallGrass(root);

        Progress("돌계단 쌓는 중…", 0.15f);
        var stair = new MB(1);
        StairsZ(stair, -2f, -6.0f, -5.1f, t6, t12, 0.95f, 5);
        StairsZ(stair, -1.9f, 6.15f, 7.0f, t12, z5, 1.0f, 2);
        if (z5 > 0.3f) StairsZ(stair, -1.9f, 21.6f, 26.4f, z5, 0.02f, 1.2f, 13);
        var sh = new GameObject("돌계단").transform; sh.SetParent(root, false);
        Emit(sh, "돌계단", stair, new[] { mStone }, ShadowCastingMode.On, true);

        Progress("가운데 단 꽃밭 길 만드는 중…", 0.2f);
        var md = new GameObject("가운데 단 꼬불길").transform; md.SetParent(root, false); md.localPosition = new Vector3(0, z5, 0);
        BuildFootpath(md, "S자 흙길", mid, 0.6f, false);
        BuildMeadow(md, mid);

        Progress("마을 길 내는 중…", 0.3f);
        var road = RoadLoop();
        var vil = new GameObject("마을").transform; vil.SetParent(root, false);
        BuildFootpath(vil, "마을 길", road, 1.05f, true);
        BuildFootpath(vil, "계단 아래 길", SmoothLine(new List<Vector2> { new Vector2(-1.9f, 26.4f), new Vector2(-1.7f, 27.6f), NearestOn(road, new Vector2(-1.6f, 30f)) }, 0.2f), 0.7f, false);

        Progress("마을 집 짓는 중…", 0.4f);
        var houses = new List<Vector2>();
        int hi = 0;
        foreach (var hp in HOUSES)
        {
            var pos = new Vector2(hp.x, hp.y);
            var toC = new Vector2(-1.5f, 3f) - pos; float yaw = Mathf.Atan2(toC.x, toC.y) * Mathf.Rad2Deg;
            BuildHouse(vil, new Vector3(pos.x, 0, pos.y), yaw, R(0.9f, 1.08f), ++hi);
            houses.Add(pos);
            var gate = pos + toC.normalized * 6.0f;
            BuildFootpath(vil, "집 " + hi + " 앞길", SmoothLine(new List<Vector2> { pos + toC.normalized * 3.6f, gate, NearestOn(road, gate) }, 0.2f), 0.5f, false);
        }

        Progress("나무 심는 중…", 0.6f);
        BuildForest(vil, road, houses);
        Progress("풀·꽃 심는 중…", 0.8f);
        BuildRingGrass(vil, road, houses);

        Progress("빛가루 길 잇는 중…", 0.93f);
        RerouteGlow();

        built = true;
        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 ㄹ길·마을] 다 만들었어요. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        RestoreCut();
        RestoreGlow();
        SetWindingTerraces(true);
        built = false; route = new List<Vector3>(); routeHalf = new List<float>(); riulDecor = null; riulPieces = new Byeolmalsseum.JamoPiece[0];
        MarkDirty(this); MarkDirty(gameObject);
    }

    // ---------------- 마을 배치 ----------------
    // 집 자리 (글자 둘레 마을 길 바깥, 언덕 발치) — 대문은 글자 쪽을 봐요
    static readonly Vector2[] HOUSES = { new Vector2(24.7f, 18.1f), new Vector2(-19.5f, 30.8f), new Vector2(-32.0f, 15.0f), new Vector2(-30.2f, 0.4f), new Vector2(-31.3f, -14.2f), new Vector2(25.0f, -6.7f) };
    float hillR = 37f;
    Material mWild;

    // 글자 둘레를 한 바퀴 도는 구불구불한 마을 길
    List<Vector2> RoadLoop()
    {
        var o = new List<Vector2>();
        const int N = 900;
        for (int i = 0; i <= N; i++)
        {
            float th = i / (float)N * Mathf.PI * 2f, c = Mathf.Cos(th), s = Mathf.Sin(th), n = 3.5f;
            float r = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(c / 21.5f), n) + Mathf.Pow(Mathf.Abs(s / 26f), n), 1f / n);
            r += 1.8f * Mathf.Sin(5f * th + 0.7f) + 0.8f * Mathf.Sin(9f * th);
            o.Add(new Vector2(-1.5f + c * r, 3f + s * r));
        }
        return o;
    }
    static Vector2 NearestOn(List<Vector2> line, Vector2 p)
    {
        Vector2 best = line[0]; float bd = float.MaxValue;
        foreach (var q in line) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = q; } }
        return best;
    }
    static float DistTo(List<Vector2> line, Vector2 p, int stride = 3)
    {
        float bd = float.MaxValue;
        for (int i = 0; i < line.Count; i += stride) { float d = (line[i] - p).sqrMagnitude; if (d < bd) bd = d; }
        return Mathf.Sqrt(bd);
    }
    // 언덕 시작선까지 남은 거리 (+면 마을 쪽, -면 언덕 위)
    float HillClear(Vector2 p)
    {
        float dx = p.x + 4f, dz = p.y - 3f, r = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx), c = Mathf.Cos(a), s = Mathf.Sin(a);
        float rb = hillR / Mathf.Pow(Mathf.Pow(Mathf.Abs(c), 3f) + Mathf.Pow(Mathf.Abs(s), 3f), 1f / 3f) - 3f;
        return rb - r;
    }
    List<Vector2> pathA, pathB;
    float NearOldPaths(Vector2 p)
    {
        if (pathA == null)
        {
            var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
            var b = new[] { new Vector3(7.4f, 0, 17), new Vector3(12, 0, 22), new Vector3(8, 0, 29), new Vector3(16, 0, 36), new Vector3(10, 0, 44), new Vector3(20, 0, 51), new Vector3(13, 0, 59), new Vector3(22, 0, 69) };
            var la = new List<Vector3>(); var lb = new List<Vector3>(); Spline(a, 1f, la); Spline(b, 1f, lb);
            pathA = new List<Vector2>(); foreach (var q in la) pathA.Add(new Vector2(q.x, q.z));
            pathB = new List<Vector2>(); foreach (var q in lb) pathB.Add(new Vector2(q.x, q.z));
        }
        return Mathf.Min(DistTo(pathA, p, 1) - 3f, DistTo(pathB, p, 1) - 2.2f);
    }
    static readonly Rect LETTERS = Rect.MinMaxRect(-16.6f, -17.6f, 13.6f, 21.6f);
    bool FreeGround(Vector2 p, List<Vector2> road, List<Vector2> houses, float roadGap, float houseGap, float letterGap)
    {
        if (Grow(LETTERS, letterGap).Contains(p)) return false;
        if (HillClear(p) < 0.5f) return false;
        if (DistTo(road, p) < roadGap) return false;
        foreach (var h in houses) if ((h - p).sqrMagnitude < houseGap * houseGap) return false;
        if (NearOldPaths(p) < 1.2f) return false;
        if (Mathf.Abs(p.x + 1.9f) < 1.2f && p.y > 21f && p.y < 30f) return false; // 계단 아래 길
        return true;
    }

    // ---------------- 숲 (키 큰 활엽수 + 소나무 + 덤불) ----------------
    void BuildForest(Transform parent, List<Vector2> road, List<Vector2> houses)
    {
        var leaves = new MB(1); var leaves2 = new MB(1); var bark = new MB(1); var pine = new MB(1); var bush = new MB(1);
        var placed = new List<Vector2>();
        int want = 95;
        for (int tries = 0; tries < 6000 && placed.Count < want; tries++)
        {
            var p = new Vector2(R(-44f, 38f), R(-38f, 45f));
            if (!FreeGround(p, road, houses, 2.6f, 7.5f, 2.5f)) continue;
            bool close = false; foreach (var q in placed) if ((q - p).sqrMagnitude < 9f) { close = true; break; }
            if (close) continue;
            placed.Add(p);
            var bp = new Vector3(p.x, -0.1f, p.y);
            if (rng.NextDouble() < 0.4) Pine(pine, bark, bp, R(10f, 16f));
            else TallTree(rng.NextDouble() < 0.5 ? leaves : leaves2, bark, bp, R(10f, 17f));
        }
        // 덤불: 길가·담장 옆
        for (int i = 0, n = 0; i < 4000 && n < 160; i++)
        {
            var p = new Vector2(R(-44f, 38f), R(-38f, 45f));
            if (!FreeGround(p, road, houses, 1.4f, 6.8f, 1.2f)) continue;
            if (DistTo(road, p) > 3.2f && rng.NextDouble() < 0.7) continue;
            Blob(bush, new Vector3(p.x, R(0.1f, 0.3f), p.y), R(0.45f, 0.9f)); n++;
        }
        var h = new GameObject("숲").transform; h.SetParent(parent, false);
        Emit(h, "활엽수 잎", leaves, new[] { mLeaf }, ShadowCastingMode.On, false);
        Emit(h, "활엽수 잎2", leaves2, new[] { mLeaf2 }, ShadowCastingMode.On, false);
        Emit(h, "소나무 잎", pine, new[] { mLeaf2 }, ShadowCastingMode.On, false);
        Emit(h, "나무 줄기", bark, new[] { mBark }, ShadowCastingMode.On, false);
        Emit(h, "덤불", bush, new[] { mLeaf }, ShadowCastingMode.On, false);
    }

    void TallTree(MB leaves, MB bark, Vector3 b, float height)
    {
        float trunkH = height * R(0.5f, 0.62f), rad = height * 0.03f; const int S = 9; int k0 = bark.v.Count;
        var lean = new Vector3(R(-0.4f, 0.4f), 0, R(-0.4f, 0.4f));
        for (int i = 0; i <= S; i++)
        {
            float a = i / (float)S * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.Add(b + d * rad * 1.5f, d, new Vector2(i / (float)S, 0)); bark.Add(b + lean + d * rad * 0.55f + Vector3.up * trunkH, d, new Vector2(i / (float)S, 3));
        }
        for (int i = 0; i < S; i++) { int a = k0 + i * 2; bark.Tri(0, a, a + 1, a + 3); bark.Tri(0, a, a + 3, a + 2); }
        float crown = height * 0.3f; int blobs = 6 + rng.Next(4);
        var top = b + lean + Vector3.up * trunkH;
        for (int k = 0; k < blobs; k++)
            Blob(leaves, top + Vector3.up * (crown * R(-0.1f, 0.9f)) + new Vector3(R(-1, 1), 0, R(-1, 1)) * crown * 0.7f, crown * R(0.5f, 0.8f));
    }

    // 소나무: 곧은 줄기 + 층층이 얹힌 납작한 잎 덩어리
    void Pine(MB leaves, MB bark, Vector3 b, float height)
    {
        float rad = height * 0.025f; const int S = 8; int k0 = bark.v.Count;
        for (int i = 0; i <= S; i++)
        {
            float a = i / (float)S * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.Add(b + d * rad * 1.4f, d, new Vector2(i / (float)S, 0)); bark.Add(b + d * rad * 0.4f + Vector3.up * height * 0.92f, d, new Vector2(i / (float)S, 4));
        }
        for (int i = 0; i < S; i++) { int a = k0 + i * 2; bark.Tri(0, a, a + 1, a + 3); bark.Tri(0, a, a + 3, a + 2); }
        int layers = 4 + rng.Next(3);
        for (int l = 0; l < layers; l++)
        {
            float t = 0.45f + 0.5f * l / (layers - 1f), w = height * 0.22f * (1.15f - t);
            var c = b + Vector3.up * (height * t) + new Vector3(R(-0.3f, 0.3f), 0, R(-0.3f, 0.3f));
            FlatBlob(leaves, c, w, w * 0.35f);
        }
    }
    void FlatBlob(MB m, Vector3 c, float r, float h)
    {
        const int LA = 5, LO = 12; int k0 = m.v.Count; float seed = R(0, 100);
        for (int i = 0; i <= LA; i++)
            for (int j = 0; j <= LO; j++)
            {
                float th = i / (float)LA * Mathf.PI, ph = j / (float)LO * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                float k = 0.75f + 0.45f * Fbm(d.x * 2f + seed, d.z * 2f + seed);
                m.Add(c + new Vector3(d.x * r * k, d.y * h, d.z * r * k), d, new Vector2(j / (float)LO, i / (float)LA));
            }
        for (int i = 0; i < LA; i++)
            for (int j = 0; j < LO; j++) { int a = k0 + i * (LO + 1) + j, bb = a + LO + 1; m.Tri(0, a, a + 1, bb); m.Tri(0, a + 1, bb + 1, bb); }
    }

    // ---------------- 풀숲 · 들꽃 (마을 둘레) ----------------
    void BuildRingGrass(Transform parent, List<Vector2> road, List<Vector2> houses)
    {
        var chunks = new List<MB> { new MB(1) }; var wild = new MB(1);
        for (int i = 0, n = 0; i < 30000 && n < 3600; i++)
        {
            var p = new Vector2(R(-44f, 38f), R(-38f, 45f));
            if (!FreeGround(p, road, houses, 1.25f, 5.2f, 0.4f)) continue;
            float patch = Fbm(p.x * 0.09f + 3f, p.y * 0.09f - 2f);
            bool nearRoad = DistTo(road, p) < 2.6f;
            if (patch < 0.42f && !nearRoad && rng.NextDouble() < 0.75) continue;
            var mb = chunks[chunks.Count - 1]; if (mb.v.Count > 60000) { mb = new MB(1); chunks.Add(mb); }
            float tall = patch > 0.55f ? R(0.9f, 1.6f) : R(0.35f, 0.8f);
            Ribbons(mb, new Vector3(p.x, 0.01f, p.y), tall, patch > 0.55f ? 10 : 8, 0.3f, 0.5f, 0.026f);
            if (mWild != null && (nearRoad || rng.NextDouble() < 0.2) && rng.NextDouble() < 0.35)
                for (int f = 0; f < 3; f++) Flower(wild, new Vector3(p.x + R(-0.4f, 0.4f), 0, p.y + R(-0.4f, 0.4f)), R(0.15f, 0.4f), rng.Next(3));
            n++;
        }
        var h = new GameObject("풀숲·들꽃").transform; h.SetParent(parent, false);
        for (int c = 0; c < chunks.Count; c++) Emit(h, "풀숲 " + (c + 1), chunks[c], new[] { mGrassBlade }, ShadowCastingMode.Off, false);
        if (mWild != null) Emit(h, "들꽃", wild, new[] { mWild }, ShadowCastingMode.Off, false);
    }

    // 가운데 단 꼬불길 양옆: 키 큰 풀, 들꽃, 덤불, 작은 나무
    void BuildMeadow(Transform parent, List<Vector2> mid)
    {
        var g = new MB(1); var wild = new MB(1); var bush = new MB(1); var leaves = new MB(1); var bark = new MB(1);
        for (int i = 0, n = 0; i < 9000 && n < 900; i++)
        {
            var p = new Vector2(R(-3.5f, 3.5f), R(7.0f, 21.4f));
            float d = DistTo(mid, p, 1); if (d < 0.75f) continue;
            float tall = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01((d - 0.75f) / 1.5f)) * R(0.8f, 1.15f);
            Ribbons(g, new Vector3(p.x, 0.01f, p.y), tall, 9, 0.25f, 0.5f, 0.026f);
            if (mWild != null && rng.NextDouble() < 0.25) Flower(wild, new Vector3(p.x, 0, p.y), R(0.2f, 0.5f), rng.Next(3));
            n++;
        }
        foreach (var tp in new[] { new Vector2(2.4f, 9.6f), new Vector2(-3.0f, 13.4f), new Vector2(2.6f, 17.2f) })
            if (DistTo(mid, tp, 1) > 1.5f) TallTree(leaves, bark, new Vector3(tp.x, 0, tp.y), R(5.5f, 7.5f));
        for (int i = 0; i < 18; i++)
        {
            var p = new Vector2(R(-3.3f, 3.3f), R(7.2f, 21.2f)); if (DistTo(mid, p, 1) < 1.2f) continue;
            Blob(bush, new Vector3(p.x, 0.15f, p.y), R(0.35f, 0.6f));
        }
        Emit(parent, "꽃밭 풀", g, new[] { mGrassBlade }, ShadowCastingMode.Off, false);
        if (mWild != null) Emit(parent, "꽃밭 들꽃", wild, new[] { mWild }, ShadowCastingMode.Off, false);
        Emit(parent, "꽃밭 덤불", bush, new[] { mLeaf }, ShadowCastingMode.On, false);
        Emit(parent, "꽃밭 나무 잎", leaves, new[] { mLeaf2 }, ShadowCastingMode.On, false);
        Emit(parent, "꽃밭 나무 줄기", bark, new[] { mBark }, ShadowCastingMode.On, false);
    }

    void Flower(MB m, Vector3 p, float h, int kind)
    {
        int k0 = m.v.Count; var lean = new Vector3(R(-0.03f, 0.03f), 0, R(-0.03f, 0.03f)); var top = p + Vector3.up * h + lean;
        m.Add(p + Vector3.left * 0.003f, Vector3.up, new Vector2(0.1f, 0.1f)); m.Add(p + Vector3.right * 0.003f, Vector3.up, new Vector2(0.1f, 0.1f));
        m.Add(top + Vector3.right * 0.003f, Vector3.up, new Vector2(0.1f, 0.4f)); m.Add(top + Vector3.left * 0.003f, Vector3.up, new Vector2(0.1f, 0.4f));
        m.sub[0].Add(k0); m.sub[0].Add(k0 + 1); m.sub[0].Add(k0 + 2); m.sub[0].Add(k0); m.sub[0].Add(k0 + 2); m.sub[0].Add(k0 + 3);
        float u = kind == 0 ? 0.55f : (kind == 1 ? 0.75f : 0.92f);
        int c = m.v.Count; m.Add(top + Vector3.up * 0.004f, Vector3.up, new Vector2(u, 0.98f));
        const int petals = 6; float pr = R(0.02f, 0.04f);
        for (int i = 0; i <= petals; i++) { float a = i / (float)petals * Mathf.PI * 2; m.Add(top + new Vector3(Mathf.Cos(a) * pr, 0, Mathf.Sin(a) * pr), Vector3.up, new Vector2(u, 0.8f)); }
        for (int i = 0; i < petals; i++) m.Tri(0, c, c + 1 + i, c + 2 + i);
    }

    // ---------------- 단 높이 (계단 다원과 같은 표) ----------------
    const string TERRACE_ROOT = "논틀밭틀_계단 다원 (자동 생성)";
    bool raised; float lvScale = 1f;
    static readonly float[,] ZONES = {
        {-16.6f, -3.7f, -17.6f, 1.25f, 4.5f}, {-16.1f, -3.7f, 1.25f, 15.6f, 6.5f}, {-3.7f, 4.6f, -17.6f, -5.55f, 1.5f},
        {-3.7f, 4.6f, -5.55f, 6.45f, 3.0f}, {-3.7f, 4.6f, 6.45f, 21.6f, 4.0f}, {4.6f, 13.6f, 15.05f, 21.6f, 4.0f},
        {4.6f, 13.6f, -17.6f, -6.5f, 2.5f}, {4.6f, 13.6f, -6.5f, 4.5f, 4.5f}, {4.6f, 13.6f, 4.5f, 15.05f, 6.0f} };
    float ZoneLevel(float x, float z)
    {
        if (!raised) return 0f;
        for (int i = 0; i < ZONES.GetLength(0); i++)
            if (x >= ZONES[i, 0] && x < ZONES[i, 1] && z >= ZONES[i, 2] && z < ZONES[i, 3]) return ZONES[i, 4] * lvScale;
        return 0f;
    }

    // ---------------- 가운데 단의 계단 차밭(언덕)을 잘라내요 (원래 메시는 그대로 두고, 자른 복사본으로 바꿔 끼워요) ----------------
    [HideInInspector] public List<MeshFilter> cutFilters = new List<MeshFilter>();
    [HideInInspector] public List<Mesh> cutOriginals = new List<Mesh>();
    static readonly Rect CUT = Rect.MinMaxRect(-3.62f, 6.9f, 3.62f, 20.2f);

    void CutCenterTerrace()
    {
        var tr = transform.Find(TERRACE_ROOT); if (tr == null) return;
        foreach (var mf in tr.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.transform.parent != null && mf.transform.parent.name.StartsWith("글자 단")) continue;
            var src = mf.sharedMesh; if (src == null || !src.isReadable) continue;
            var v = src.vertices; var m = mf.transform.localToWorldMatrix; var toLocal = transform.worldToLocalMatrix * m;
            var cut = Object.Instantiate(src); cut.name = src.name + "_가운데잘림";
            bool changed = false;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s); var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = toLocal.MultiplyPoint3x4((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    if (CUT.Contains(new Vector2(c.x, c.z))) { changed = true; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                cut.SetTriangles(keep, s);
            }
            if (!changed) { DestroyNow(cut); continue; }
            SaveAsset(cut, "메시_마을_잘라낸_" + mf.name + ".asset");
            cutFilters.Add(mf); cutOriginals.Add(src);
            mf.sharedMesh = cut; MarkDirty(mf);
        }
    }
    void RestoreCut()
    {
        for (int i = 0; i < cutFilters.Count && i < cutOriginals.Count; i++)
            if (cutFilters[i] != null && cutOriginals[i] != null) { cutFilters[i].sharedMesh = cutOriginals[i]; MarkDirty(cutFilters[i]); }
        cutFilters.Clear(); cutOriginals.Clear();
    }

    // ---------------- ㄹ 옆 키 큰 풀숲 ----------------
    void BuildTallGrass(Transform root)
    {
        var g = new MB(1);
        foreach (var J in new[] { (0f, -11.5f), (0f, 0.4f) })
        {
            float X = J.Item1, Z = J.Item2, x0 = X - 2.5f, x1 = X + 2.5f, y0 = Z - 5.5f, y1 = Z + 5.5f, m = Z, T = 1f;
            var bars = new List<Rect> {
                Rect.MinMaxRect(x0, y1 - T, x1, y1), Rect.MinMaxRect(x1 - T, m, x1, y1), Rect.MinMaxRect(x0, m - T / 2, x1, m + T / 2),
                Rect.MinMaxRect(x0, y0, x0 + T, m), Rect.MinMaxRect(x0, y0, x1, y0 + T) };
            float floor = ZoneLevel(X, Z);
            foreach (var b in bars)
            {
                float per = 2 * (b.width + b.height); int n = Mathf.CeilToInt(per / 0.22f);
                for (int k = 0; k < n; k++)
                {
                    float off = R(0.12f, 0.7f);
                    var p = RectPerimeter(b, k / (float)n, off);
                    bool bad = false;
                    foreach (var o in bars) if (o.Contains(p) || Grow(o, 0.08f).Contains(p)) { bad = true; break; }
                    if (bad || Mathf.Abs(ZoneLevel(p.x, p.y) - floor) > 0.01f) continue;
                    if (Mathf.Abs(p.x + 2f) < 0.7f && p.y > -6.1f && p.y < -5.0f) continue;   // 계단 자리
                    float hgt = Mathf.Lerp(1.4f, 0.7f, (off - 0.12f) / 0.58f) * R(0.8f, 1.15f);
                    Ribbons(g, new Vector3(p.x, floor + 0.01f, p.y), hgt, 9, 0.25f, 0.45f, 0.03f);
                }
            }
        }
        var h = new GameObject("ㄹ 옆 키 큰 풀").transform; h.SetParent(root, false);
        Emit(h, "키 큰 풀", g, new[] { mGrassBlade }, ShadowCastingMode.Off, false);
    }
    static Rect Grow(Rect r, float d) => Rect.MinMaxRect(r.xMin - d, r.yMin - d, r.xMax + d, r.yMax + d);
    static Vector2 RectPerimeter(Rect r, float t, float off)
    {
        float W = r.width + off * 2, H = r.height + off * 2, per = 2 * (W + H), s = Mathf.Repeat(t, 1f) * per;
        float x0 = r.xMin - off, y0 = r.yMin - off;
        if (s < W) return new Vector2(x0 + s, y0); s -= W;
        if (s < H) return new Vector2(x0 + W, y0 + s); s -= H;
        if (s < W) return new Vector2(x0 + W - s, y0 + H); s -= W;
        return new Vector2(x0, y0 + H - s);
    }

    static List<Vector2> SmoothLine(List<Vector2> c, float step)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < c.Count - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Count - 1)];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        o.Add(c[c.Count - 1]); return o;
    }

    // 미로 자리와 겹치는 꼬불꼬불 스크립트의 바깥 다랑논을 숨겨요 (언덕에 거의 묻혀 있던 것들)
    void SetWindingTerraces(bool on)
    {
        // 고퀄 스크립트가 만든 바깥 바둑판 논밭·농로 (언덕에 반쯤 묻혀 이상하게 보여요) — 넓은 땅만 남겨요
        var hq = transform.Find("논틀밭틀_고퀄 (자동 생성)");
        var near = hq != null ? hq.Find("주변 논밭 (고퀄)") : null;
        if (near != null && !on)   // 숨기기만 해요 (원래 꼬불꼬불 스크립트도 숨겨 두던 것들이라 되살리지 않아요)
            foreach (Transform c in near)
                if (c.name != "넓은 땅") { c.gameObject.SetActive(on); MarkDirty(c.gameObject); }
        var wp = transform.Find(WP_ROOT); if (wp == null) return;
        foreach (Transform c in wp)
        {
            if (System.Array.IndexOf(WP_HIDE, c.name) >= 0) { c.gameObject.SetActive(on); MarkDirty(c.gameObject); }
            var b = c.Find("둑벽"); if (b) { b.gameObject.SetActive(on); MarkDirty(b.gameObject); }
        }
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    Byeolmalsseum.JamoPiece FindPiece(float X, float Z, float fallbackTop, out float top)
    {
        top = fallbackTop; Byeolmalsseum.JamoPiece best = null; float bd = 4f;
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

    // 꺾인 선의 모서리를 둥글게 깎고 촘촘히 나눠요 (높이도 같이)
    static List<Vector3> Fillet3(List<Vector3> c, float radius, float step)
    {
        var o = new List<Vector3>();
        void Line(Vector3 a, Vector3 b) { int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)) / step)); for (int k = 0; k < n; k++) o.Add(Vector3.Lerp(a, b, k / (float)n)); }
        Vector3 cur = c[0];
        for (int i = 1; i < c.Count - 1; i++)
        {
            var din = c[i] - c[i - 1]; din.y = 0; var dout = c[i + 1] - c[i]; dout.y = 0;
            float li = din.magnitude, lo = dout.magnitude; if (li < 1e-4f || lo < 1e-4f) { Line(cur, c[i]); cur = c[i]; continue; }
            din /= li; dout /= lo;
            float r = Mathf.Min(radius, li * 0.45f, lo * 0.45f);
            var a = c[i] - din * r; a.y = Mathf.Lerp(c[i - 1].y, c[i].y, 1f - r / li);
            var b = c[i] + dout * r; b.y = Mathf.Lerp(c[i].y, c[i + 1].y, r / lo);
            Line(cur, a);
            for (int k = 0; k < 6; k++) { float t = k / 6f; o.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c[i] + t * t * b); }
            cur = b;
        }
        Line(cur, c[c.Count - 1]); o.Add(c[c.Count - 1]);
        return o;
    }

    // ㄹ 위 흙길 + 길섶 풀
    void BuildRiulStrip(Transform parent, List<Vector3> line, string name)
    {
        var path = new MB(1); var grass = new MB(1);
        int n = line.Count; float along = 0;
        for (int i = 0; i < n - 1; i++)
        {
            var a = line[i]; var b = line[i + 1];
            var ta = Tan3(line, i); var tb = Tan3(line, i + 1);
            var sa = new Vector3(ta.z, 0, -ta.x); var sb = new Vector3(tb.z, 0, -tb.x);
            float wa = 0.3f + 0.04f * (Noise(along * 1.6f, 7f) - 0.5f) * 2f; float seg = Vector3.Distance(a, b); float wb = 0.3f + 0.04f * (Noise((along + seg) * 1.6f, 7f) - 0.5f) * 2f;
            int k = path.v.Count; var up = Vector3.up * 0.012f;
            path.Add(a - sa * wa + up, Vector3.up, new Vector2(-wa, along)); path.Add(b - sb * wb + up, Vector3.up, new Vector2(-wb, along + seg));
            path.Add(b + sb * wb + up, Vector3.up, new Vector2(wb, along + seg)); path.Add(a + sa * wa + up, Vector3.up, new Vector2(wa, along));
            path.Tri(0, k, k + 1, k + 2); path.Tri(0, k, k + 2, k + 3);
            if (i % 2 == 0)
                foreach (float sg in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < 0.7) Ribbons(grass, a + sa * (sg * (wa + R(0f, 0.08f))), R(0.08f, 0.2f), 5, 0.1f, 0.6f, 0.013f);
                    if (rng.NextDouble() < 0.25) Ribbons(grass, a + sa * (sg * 0.47f) - Vector3.up * 0.01f, R(0.2f, 0.35f), 6, 0.12f, 1.1f, 0.018f);
                }
            along += seg;
        }
        var h = new GameObject(name).transform; h.SetParent(parent, false);
        Emit(h, "흙길", path, new[] { mPath }, ShadowCastingMode.Off, true);
        Emit(h, "길섶 풀", grass, new[] { mGrassBlade }, ShadowCastingMode.Off, false);
    }
    static Vector3 Tan3(List<Vector3> p, int i) { var d = p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(i - 1, 0)]; d.y = 0; return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward; }

    // 돌계단 (z 방향 / x 방향). 계단마다 땅(0)부터 쌓아 올린 돌덩이예요
    void StairsZ(MB m, float x, float z0, float z1, float y0, float y1, float width, int n)
    {
        for (int k = 0; k < n; k++)
        {
            float za = Mathf.Lerp(z0, z1, k / (float)n), zb = Mathf.Lerp(z0, z1, (k + 1) / (float)n), y = Mathf.Max(0.05f, y1 >= y0 ? Mathf.Lerp(y0, y1, (k + 1) / (float)n) : Mathf.Lerp(y0, y1, k / (float)n));
            Box(m, new Vector3(x + R(-0.02f, 0.02f), y / 2f, (za + zb) / 2f), new Vector3(width + R(-0.04f, 0.04f), y, Mathf.Abs(zb - za) + 0.02f), 0.9f);
        }
    }
    void StairsX(MB m, float x0, float x1, float z, float y0, float y1, float width, int n)
    {
        for (int k = 0; k < n; k++)
        {
            float xa = Mathf.Lerp(x0, x1, k / (float)n), xb = Mathf.Lerp(x0, x1, (k + 1) / (float)n), y = Mathf.Max(0.05f, Mathf.Lerp(y0, y1, k / (float)n));
            Box(m, new Vector3((xa + xb) / 2f, y / 2f, z + R(-0.02f, 0.02f)), new Vector3(Mathf.Abs(xb - xa) + 0.02f, y, width + R(-0.04f, 0.04f)), 0.9f);
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
            Debug.LogError("[논틀밭틀 ㄹ길·마을] 고퀄 재질을 못 찾았어요. NonteulFieldUpgrade의 [★ 전부 한 번에]를 먼저 눌러 주세요.");
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
        var openings = new List<float> { line[0].x }; var openN = new List<float> { line[line.Count - 1].x };
        OuterWall(side, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector3(0, 0, -1), openings, half);
        OuterWall(side, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), new Vector3(0, 0, 1), openN, half);
        OuterWall(side, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), new Vector3(-1, 0, 0), null, half);
        OuterWall(side, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector3(1, 0, 0), null, half);
        // 테두리 위 둘레 띠 (네 변)
        RimStrip(top, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(0, 0.4f), openings, half);
        RimStrip(top, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(0, -0.4f), openN, half);
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

    // ==================================================================
    //  초가집
    // ==================================================================
    void BuildHouse(Transform root, Vector3 pos, float yaw, float scale, int idx)
    {
        var holder = new GameObject("초가집 " + idx).transform; holder.SetParent(root, false);
        holder.localPosition = pos; holder.localRotation = Quaternion.Euler(0, yaw, 0); holder.localScale = Vector3.one * scale; // 대문(+z)이 마을 가운데를 보게
        var stone = new MB(1); var plaster = new MB(1); var wood = new MB(1); var door = new MB(1); var straw = new MB(1); var jar = new MB(1);
        Vector3 O = Vector3.zero;
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
        var jo = O + new Vector3(4.0f, 0, -3.9f);
        Box(stone, jo + new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 1.7f), 0.9f);
        for (int i = 0; i < 6; i++) Jar(jar, jo + new Vector3(-0.8f + (i % 3) * 0.8f + R(-0.08f, 0.08f), 0.3f, -0.4f + (i / 3) * 0.8f), R(0.6f, 0.95f));
        // 감나무 한 그루
        var leaves = new MB(1); var bark = new MB(1);
        Tree(leaves, bark, O + new Vector3(-4.6f, 0, 3.6f), R(6f, 8f));
        // 돌담 (마당 둘레, 앞쪽에 대문 자리)
        const float YX = 5.6f, YB = -4.9f, YF = 5.4f;
        for (float x = -YX; x < YX - 0.05f; x += 1.12f) { float w = Mathf.Min(1.12f, YX - x); Box(stone, O + new Vector3(x + w / 2f, 0.45f, YB), new Vector3(w, R(0.8f, 1.0f), 0.45f), 0.9f); }
        for (float z = YB; z < YF - 0.05f; z += 1.12f) { float w = Mathf.Min(1.12f, YF - z); Box(stone, O + new Vector3(-YX, 0.45f, z + w / 2f), new Vector3(0.45f, R(0.8f, 1.0f), w), 0.9f); Box(stone, O + new Vector3(YX, 0.45f, z + w / 2f), new Vector3(0.45f, R(0.8f, 1.0f), w), 0.9f); }
        for (float x = -YX; x < YX - 0.05f; x += 1.12f) { float w = Mathf.Min(1.12f, YX - x); if (Mathf.Abs(x + w / 2f) > 1.3f) Box(stone, O + new Vector3(x + w / 2f, 0.45f, YF), new Vector3(w, R(0.8f, 1.0f), 0.45f), 0.9f); }

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
    //  빛가루 길: 들어오는 꼬불길 → ㄹ → 계단 → 둑길 → 미로 → 집
    // ==================================================================
    System.Reflection.FieldInfo GlowField(out Component wp)
    {
        wp = GetComponent("NonteulWindingPath");
        if (wp == null) return null;
        return wp.GetType().GetField("glowRoute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
    }
    void RerouteGlow()
    {
        var f = GlowField(out var wp); if (f == null) return;
        var list = f.GetValue(wp) as List<Vector3>; if (list == null) return;
        if (savedGlow == null || savedGlow.Count == 0) savedGlow = new List<Vector3>(list);
        var a = new[] { new Vector3(-6, 0, -69), new Vector3(3, 0, -61), new Vector3(-7, 0, -52), new Vector3(4, 0, -44), new Vector3(-5, 0, -36), new Vector3(6, 0, -28), new Vector3(1, 0, -21), new Vector3(2, 0, -17.6f) };
        var pa = new List<Vector3>(); Spline(a, 0.35f, pa);
        list.Clear();
        for (int i = 0; i < pa.Count; i += 3) list.Add(transform.TransformPoint(pa[i] + Vector3.up * 0.9f));
        float acc = 99f;
        for (int i = 0; i < route.Count; i++)
        {
            if (i > 0) acc += Vector3.Distance(route[i - 1], route[i]);
            if (acc >= 1.2f) { list.Add(transform.TransformPoint(route[i] + Vector3.up * 1.0f)); acc = 0f; }
        }
        MarkDirty(wp);
    }
    void RestoreGlow()
    {
        var f = GlowField(out var wp); if (f == null || savedGlow == null || savedGlow.Count == 0) return;
        var list = f.GetValue(wp) as List<Vector3>; if (list == null) return;
        list.Clear(); list.AddRange(savedGlow); savedGlow = new List<Vector3>(); MarkDirty(wp);
    }

    // ==================================================================
    //  플레이 중: Shift 속도 · ㄹ 길 장식 보이기 · 길에서 안 떨어지게 붙잡기
    // ==================================================================
    Byeolmalsseum.WordSpace ws; Byeolmalsseum.ExperienceCamera cam;
    float[] cum; bool locked, finished, wasMoving; float sCur;
    NonteulHillsAndBlock blocker; bool blockerWas;
    bool[] riulAway; bool riulShown;

    void Start()
    {
        ws = FindFirstObjectByType<Byeolmalsseum.WordSpace>();
        cam = ws != null ? ws.experienceCamera : FindFirstObjectByType<Byeolmalsseum.ExperienceCamera>();
        if (cam != null) cam.fastMultiplier = shiftMultiplier;
        blocker = GetComponent<NonteulHillsAndBlock>(); if (blocker != null) blockerWas = blocker.blockEverything;
        if (route != null && route.Count > 1)
        {
            cum = new float[route.Count];
            for (int i = 1; i < route.Count; i++) cum[i] = cum[i - 1] + Flat(route[i] - route[i - 1]);
        }
        riulAway = new bool[riulPieces != null ? riulPieces.Length : 0];
        if (riulDecor != null && riulPieces != null && riulPieces.Length > 0) riulDecor.gameObject.SetActive(false); else riulShown = true;
    }
    static float Flat(Vector3 d) { d.y = 0; return d.magnitude; }

    void LateUpdate()
    {
        ShowRiulWhenLanded();
        if (cam != null && cam.fastMultiplier != shiftMultiplier) cam.fastMultiplier = shiftMultiplier;
        if (!lockToPath || cam == null || cum == null || finished) return;
        bool moving = cam.MovementEnabled && !cam.Paused;
        var t = cam.transform; var lp = transform.InverseTransformPoint(t.position);

        if (!locked)
        {
            if (!moving) { wasMoving = false; return; }
            bool justStarted = !wasMoving; wasMoving = true;
            var start = route[0];
            if (justStarted && Flat(lp - start) < snapRadius)
            {
                // 체험이 시작되면 ㄹ 길 첫머리에 올려놓아요
                Lock(true); sCur = 0f;
                t.position = transform.TransformPoint(new Vector3(start.x, start.y + eyeHeight, start.z));
                return;
            }
            // 걸어서 ㄹ 길 첫머리에 닿으면 그때부터 붙잡아요
            int near = Nearest(lp, 0f, 3f, out var q, out float s, out float lat, out float dist);
            if (near >= 0 && dist < 1.5f) { Lock(true); sCur = s; }
            return;
        }
        if (!moving) return;

        int idx = Nearest(lp, sCur - 3f, sCur + 3f, out var qq, out float ss, out float lateral, out _);
        if (idx < 0) return;
        float half = routeHalf[idx];
        var tan = route[Mathf.Min(idx + 1, route.Count - 1)] - route[Mathf.Max(idx - 1, 0)]; tan.y = 0; tan = tan.sqrMagnitude > 1e-8f ? tan.normalized : Vector3.forward;
        var side = new Vector3(tan.z, 0, -tan.x);
        lateral = Mathf.Clamp(lateral, -half, half);
        float targetY = qq.y + eyeHeight;
        float y = Mathf.Lerp(lp.y, targetY, 1f - Mathf.Exp(-14f * Time.deltaTime));
        var np = new Vector3(qq.x + side.x * lateral, y, qq.z + side.z * lateral);
        t.position = transform.TransformPoint(np);
        sCur = ss;
        if (sCur >= cum[cum.Length - 1] - 0.8f) { finished = true; Lock(false); }   // 집 앞에 닿으면 자유롭게
    }

    // 길에 붙잡혀 있는 동안에는 물체 충돌 검사를 잠깐 꺼요 (길이 알아서 막아 주니까 앞으로 잘 가요)
    void Lock(bool on)
    {
        locked = on;
        if (blocker != null) blocker.blockEverything = on ? false : blockerWas;
    }
    void OnDisable() { if (blocker != null && locked) blocker.blockEverything = blockerWas; }

    // s 범위 안에서 가장 가까운 길 위 점 (q: 길 위 점·높이 포함, lateral: 길 가운데에서 옆으로 떨어진 거리)
    int Nearest(Vector3 p, float sMin, float sMax, out Vector3 q, out float s, out float lateral, out float dist)
    {
        q = Vector3.zero; s = 0; lateral = 0; dist = float.MaxValue; int best = -1;
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (cum[i + 1] < sMin || cum[i] > sMax) continue;
            Vector3 a = route[i], b = route[i + 1]; var ab = b - a; ab.y = 0; float len2 = ab.sqrMagnitude;
            var ap = p - a; ap.y = 0;
            float tt = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / len2) : 0f;
            var c = Vector3.Lerp(a, b, tt); var d = p - c; d.y = 0; float dm = d.magnitude;
            if (dm < dist)
            {
                dist = dm; best = i; q = c; s = Mathf.Lerp(cum[i], cum[i + 1], tt);
                var dir = len2 > 1e-8f ? ab / Mathf.Sqrt(len2) : Vector3.forward; var sd = new Vector3(dir.z, 0, -dir.x);
                lateral = Vector3.Dot(d, sd);
            }
        }
        return best;
    }

    void ShowRiulWhenLanded()
    {
        if (riulShown || riulDecor == null || riulPieces == null) return;
        bool all = true;
        for (int i = 0; i < riulPieces.Length; i++)
        {
            var p = riulPieces[i]; if (p == null || p.visual == null) continue;
            bool home = p.visual.parent == p.transform;
            if (!home) riulAway[i] = true;
            if (!(home && (riulAway[i] || Time.timeSinceLevelLoad > 3f))) all = false;
        }
        if (all) { riulShown = true; riulDecor.gameObject.SetActive(true); }
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
        if (mbSize > 45f) Debug.LogWarning("[논틀밭틀 ㄹ길·마을] '" + name + "' 메시가 커요 (약 " + mbSize.ToString("0") + "MB).");
        SaveAsset(mesh, "메시_마을_" + parent.name + "_" + name + ".asset");
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
        EditorUtility.DisplayProgressBar("논틀밭틀 ㄹ길·마을", msg, p);
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
[CustomEditor(typeof(NonteulRiulVillage))]
public class NonteulRiulVillageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulRiulVillage)target;
        EditorGUILayout.HelpBox("계단 다원을 만든 뒤에 눌러요. 지금까지 만든 것은 그대로 두고\nㄹ 길 꾸미기 · ㄹ 옆 키 큰 풀 · 가운데 단 S자 꽃길 · 글자 둘레 마을(집·마을 길·숲·풀꽃)을 더해요.\n플레이하면 ㄹ 길 위에서 출발해, 계단을 내려올 때까지 길을 따라 걸어요 (Shift = 빠르게).", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🏡 ㄹ 길 · 꽃길 · 마을 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("ㄹ 길 · 꽃길 · 마을 지우기 (차밭 언덕 되돌리기)", GUILayout.Height(24))) Run(s.Remove);
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
