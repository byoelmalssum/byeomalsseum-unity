// =====================================================================
//  NonteulFieldUpgrade.cs — '논틀밭틀' 공간 고퀄리티 업그레이드 (URP)
//
//  NonteulFieldMaker 위에 덧붙여 쓰는 스크립트예요. (둘 다 「공간」에 붙여 두세요)
//   A) 질감 고퀄로      : 픽셀 느낌 없는 자연스러운 고해상도 풀·흙·진흙 + 반복 무늬 감추기(디테일 맵)
//   B) 논·벼 다시 만들기 : 논을 더 높게, 모서리는 둥글게, 진짜 같은 벼(이삭 달린)·모·풀, 둑 가장자리 풀
//   C) 공간 넓히기       : 주변 논·두렁 그물, 농로, 나무, 언덕, 먼 산 (체험 이동 범위도 넓어져요)
//   D) 화면 품질 올리기  : 그림자 거리, 안개, 색 보정
//  Inspector의 [전부 한 번에] 버튼을 누르면 A→B→C→D 순서로 다 해요.
//  만든 파일은 Assets/논틀밭틀_생성/고퀄 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulFieldUpgrade : MonoBehaviour
{
    [Header("자모가 들어 있는 그룹 (비워 두면 '글자'로 시작하는 오브젝트를 찾아요)")]
    public Transform jamoRoot;

    [Header("A) 질감")]
    [Tooltip("풀·흙 색 얼룩 하나의 크기 (m). 클수록 반복 무늬가 안 보여요")] public float macroTile = 14f;
    [Tooltip("가까이서 보이는 잔무늬 크기 (m)")] public float detailTile = 1.3f;

    [Header("B) 논·벼")]
    [Tooltip("논 높이 배율 (1 = 처음 높이)")] [Range(1f, 2.5f)] public float paddyHeight = 1.6f;
    [Range(0.6f, 1.3f)] public float riceHeight = 0.95f;
    [Tooltip("벼 포기 간격 (m) — 작을수록 빽빽 (컴퓨터가 느려지면 키우세요)")] [Range(0.18f, 0.45f)] public float riceSpacing = 0.26f;

    [Header("C) 공간 넓히기")]
    [Tooltip("걸어 다닐 수 있는 평평한 논밭 크기 (가운데에서 ±m)")] [Range(30f, 90f)] public float fieldRange = 62f;
    [Range(0, 200)] public int treeCount = 70;

    // ------------------------------------------------------------------
    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string HQ_NAME = "논틀밭틀_고퀄 (자동 생성)";
    const string SCENERY_NAME = "논틀밭틀_먼 풍경 (자동 생성)";
    const string MAKER_ENV = "논틀밭틀_환경 (자동 생성)";

    // 가운데 배치 (NonteulFieldMaker와 같은 숫자): X, Z, 회전, 가로, 세로, 획 굵기, 높이 종류(0 둑 1 길 2 작은 둑)
    static readonly float[,] FINAL = {
        {-10f,-8f,0f,12f,18f,.8f,0},{-10f,-7.5f,0f,3.2f,2.4f,.45f,2},{-10f,-8f,180f,12f,18f,.8f,0},{9f,-12f,90f,10f,8f,.6f,0},
        {-4.3f,4.5f,90f,6f,1f,.75f,0},{0f,-11.5f,0f,5f,11f,1f,1},{-11f,9f,0f,9f,12f,.75f,0},{5.5f,17f,0f,3.5f,8f,.7f,1},
        {9f,-1f,90f,10f,8f,.6f,0},{9f,10f,90f,10f,8f,.6f,0},{-4.3f,11f,90f,6f,1f,.75f,0},{0f,.4f,0f,5f,11f,1f,1}};
    static readonly string JAMO = "ㄴㅗㄴㅌㅡㄹㅂㅏㅌㅌㅡㄹ";
    static readonly float[] JAMO_H = { 1.0f, 0.4f, 1.0f }; // 둑 · 길 · 작은 둑 (논 윗면 위로 솟는 높이)

    class Plate { public float x0, x1, z0, z1, top; public bool water; }
    class R2 { public float cx, cz, hw, hd, yaw, top; }
    readonly List<Plate> plates = new List<Plate>();
    readonly List<R2> strokes = new List<R2>();
    System.Random rng;

    // 만든 재질
    Material mGrass, mDirt, mPath, mMud, mWater, mCanopy, mTreeLeaf, mTreeLeaf2, mBark, mMountain, mMountainFar;
    Texture2D tGrassMacro, tGrassN, tGrassDetail, tGrassDetailN, tDirt, tDirtN, tMud, tMudN, tCanopy, tCanopyN;

    // ==================================================================
    //  버튼들
    // ==================================================================
    public void DoAll() { UpgradeTextures(); RebuildPaddies(); ExpandSpace(); UpgradeQuality(); }

    // ---------------- A) 질감 ----------------
    public void UpgradeTextures()
    {
        Progress("질감 그리는 중… (풀)", 0.05f);
        rng = new System.Random(11);
        MakeTextures();
        MakeMaterials();
        // 장면에서 처음 재질을 쓰는 곳을 고퀄 재질로 바꿔요 (재질마다 UV 크기에 맞춰 따로 만들어요)
        Progress("재질 바꾸는 중…", 0.85f);
        int changed = 0;
        foreach (var r in SceneRenderers())
        {
            var mats = r.sharedMaterials; bool any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name; Material baseM = null;
                if (n.StartsWith("땅_풀") || n.StartsWith("둑_윗면_풀")) baseM = mGrass;
                else if (n.StartsWith("둑_옆면_흙")) baseM = mDirt;
                else if (n.StartsWith("꼬불길_흙")) baseM = mPath;
                else if (n.StartsWith("논바닥_진흙")) baseM = mMud;
                else if (n.StartsWith("물논_물")) { mats[i] = mWater; any = true; continue; }
                else if (n.StartsWith("고퀄_")) { baseM = n.Contains("풀") ? mGrass : n.Contains("길") ? mPath : n.Contains("진흙") ? mMud : n.Contains("흙") ? mDirt : null; }
                if (baseM == null) continue;
                mats[i] = Variant(baseM, MetersPerUV(r)); any = true;
            }
            if (any) { r.sharedMaterials = mats; MarkDirty(r); changed++; }
        }
        SaveAll(); ClearProgress();
        Debug.Log("[논틀밭틀 고퀄] 질감을 바꿨어요. 바뀐 물체: " + changed + "개");
    }

    // ---------------- B) 논·벼 ----------------
    public void RebuildPaddies()
    {
        EnsureMaterials();
        rng = new System.Random(21);
        DefineLayout();
        var hq = HqRoot();
        DestroyChild(hq, "논·벼 (고퀄)");
        var root = Child(hq, "논·벼 (고퀄)");
        // NonteulFieldMaker가 만든 낮은 논·벼는 숨겨요 (지우지는 않아요)
        var env = transform.Find(MAKER_ENV);
        if (env)
            foreach (Transform c in env)
                if (c.name == "논 칸" || c.name == "벼·모·풀") { c.gameObject.SetActive(false); MarkDirty(c.gameObject); }

        Progress("논 만드는 중…", 0.1f);
        RaiseJamoRidges();

        // 논 칸 (모서리 둥근 상자)
        var mb = new MB(2);
        var wmb = new MB(2);
        foreach (var p in plates)
        {
            var target = p.water ? wmb : mb;
            BevelBox(target, new Vector3((p.x0 + p.x1) / 2, 0, (p.z0 + p.z1) / 2), p.x1 - p.x0, p.top, p.z1 - p.z0, 0, 0.12f, p.water ? 3f : 1f, 1f);
        }
        Emit(root, "논바닥", mb.ToMesh("논바닥"), new[] { Variant(mMud, 1f), Variant(mDirt, 1f) }, true);
        Emit(root, "물논", wmb.ToMesh("물논"), new[] { mWater, Variant(mDirt, 1f) }, true);

        // 벼·모·풀
        Progress("벼 심는 중…", 0.35f);
        var rice = new MB(1); var seed = new MB(1); var grass = new MB(1);
        foreach (var p in plates)
        {
            if (!p.water)
            {
                // 벼는 줄 맞춰 심어요 (줄 간격 > 포기 간격)
                bool alongX = (p.x1 - p.x0) > (p.z1 - p.z0);
                float rowGap = riceSpacing * 1.4f, plantGap = riceSpacing * 0.8f;
                for (float a = (alongX ? p.z0 : p.x0) + 0.3f; a < (alongX ? p.z1 : p.x1) - 0.25f; a += rowGap)
                    for (float b = (alongX ? p.x0 : p.z0) + 0.3f; b < (alongX ? p.x1 : p.z1) - 0.25f; b += plantGap)
                    {
                        float X = (alongX ? b : a) + R(-0.04f, 0.04f), Z = (alongX ? a : b) + R(-0.04f, 0.04f);
                        if (InStroke(X, Z, 0.3f)) continue;
                        RiceClump(rice, new Vector3(X, p.top, Z), riceHeight * R(0.85f, 1.1f), true);
                    }
            }
            else
            {
                for (float x = p.x0 + 0.4f; x < p.x1 - 0.35f; x += 0.42f)
                    for (float z = p.z0 + 0.4f; z < p.z1 - 0.35f; z += 0.3f)
                    {
                        if (rng.NextDouble() < 0.15) continue;
                        float X = x + R(-0.03f, 0.03f), Z = z + R(-0.03f, 0.03f);
                        if (InStroke(X, Z, 0.25f)) continue;
                        Ribbons(seed, new Vector3(X, p.top, Z), R(0.18f, 0.28f), 6, 0.05f, 0.25f, 0.011f, 0.2f, 0.75f);
                    }
            }
        }
        Progress("둑 가장자리 풀 심는 중…", 0.7f);
        // 둑(자모) 가장자리: 바닥과 윗모서리에 풀을 둘러서 각진 느낌을 없애요
        for (int i = 0; i < strokes.Count; i++)
        {
            var r = strokes[i];
            float per = 2 * (r.hw + r.hd); int n = Mathf.CeilToInt(per / 0.32f);
            for (int k = 0; k < n; k++)
            {
                Vector2 o = PerimeterPoint(r, k / (float)n, 0.12f);
                float baseY = PlateTopAt(o.x, o.y);
                if (!InStroke(o.x, o.y, 0.02f)) Ribbons(grass, new Vector3(o.x, baseY, o.y), R(0.3f, 0.6f), 9, 0.18f, 0.5f, 0.022f, 0f, 0.8f);
                if (rng.NextDouble() < 0.55)
                {
                    Vector2 t = PerimeterPoint(r, (k + 0.5f) / n, -0.08f);
                    Ribbons(grass, new Vector3(t.x, r.top - 0.02f, t.y), R(0.15f, 0.3f), 7, 0.12f, 0.6f, 0.018f, 0f, 0.8f);
                }
            }
        }
        // 길가와 빈 땅의 풀
        for (int i = 0, placed = 0; i < 40000 && placed < 3500; i++)
        {
            float X = R(-24, 24), Z = R(-24, 26);
            if (InPlate(X, Z, 0.2f) || InStroke(X, Z, 0.15f)) continue;
            if (rng.NextDouble() > 0.6) continue;
            Ribbons(grass, new Vector3(X, 0, Z), R(0.25f, 0.6f), 12, 0.22f, 0.5f, 0.024f, 0f, 0.85f); placed++;
        }
        Progress("저장하는 중…", 0.9f);
        Emit(root, "벼", rice.ToMesh("벼"), new[] { BladeMat("벼", true) }, true, ShadowCastingMode.TwoSided);
        Emit(root, "모", seed.ToMesh("모"), new[] { BladeMat("모", false) }, false, ShadowCastingMode.Off);
        Emit(root, "풀", grass.ToMesh("풀"), new[] { BladeMat("풀", false) }, true, ShadowCastingMode.Off);
        SaveAll(); ClearProgress();
        Debug.Log("[논틀밭틀 고퀄] 논을 높이고 벼·풀을 새로 심었어요.");
    }

    // ---------------- C) 공간 넓히기 ----------------
    public void ExpandSpace()
    {
        EnsureMaterials();
        rng = new System.Random(31);
        DefineLayout();
        var hq = HqRoot();
        DestroyChild(hq, "주변 논밭 (고퀄)");
        var near = Child(hq, "주변 논밭 (고퀄)");
        var oldScenery = GameObject.Find(SCENERY_NAME); if (oldScenery) DestroyNow(oldScenery);
        var far = new GameObject(SCENERY_NAME).transform; // 체험 이동 범위에 안 들어가게 바깥(씬 맨 위)에 둬요
        far.position = transform.position; far.rotation = transform.rotation;
        if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(far.gameObject, gameObject.scene);

        float F = fieldRange;
        // 1) 넓은 땅
        Progress("넓은 땅 만드는 중…", 0.05f);
        var g = new MB(1);
        g.Quad(0, new Vector3(-F - 4, -0.006f, -F - 4), new Vector3(-F - 4, -0.006f, F + 4), new Vector3(F + 4, -0.006f, F + 4), new Vector3(F + 4, -0.006f, -F - 4), Vector3.up,
               new Vector2(-F - 4, -F - 4), new Vector2(-F - 4, F + 4), new Vector2(F + 4, F + 4), new Vector2(F + 4, -F - 4));
        Emit(near, "넓은 땅", g.ToMesh("넓은 땅"), new[] { Variant(mGrass, 1f) }, true, ShadowCastingMode.Off);

        // 2) 농로 2개 (가로·세로로 시원하게)
        var road = new MB(1); float rw = 2.6f;
        float roadX = 30f, roadZ = -38f;
        road.Quad(0, new Vector3(roadX - rw / 2, 0.02f, -F), new Vector3(roadX - rw / 2, 0.02f, F), new Vector3(roadX + rw / 2, 0.02f, F), new Vector3(roadX + rw / 2, 0.02f, -F), Vector3.up,
                  new Vector2(roadX - rw / 2, -F), new Vector2(roadX - rw / 2, F), new Vector2(roadX + rw / 2, F), new Vector2(roadX + rw / 2, -F));
        road.Quad(0, new Vector3(-F, 0.021f, roadZ - rw / 2), new Vector3(-F, 0.021f, roadZ + rw / 2), new Vector3(F, 0.021f, roadZ + rw / 2), new Vector3(F, 0.021f, roadZ - rw / 2), Vector3.up,
                  new Vector2(-F, roadZ - rw / 2), new Vector2(-F, roadZ + rw / 2), new Vector2(F, roadZ + rw / 2), new Vector2(F, roadZ - rw / 2));
        Emit(near, "농로", road.ToMesh("농로"), new[] { Variant(mPath, 1f) }, true, ShadowCastingMode.Off);

        // 3) 주변 논 그물: 논 칸 + 둑(논두렁)
        Progress("주변 논 그물 만드는 중…", 0.2f);
        var ridges = new MB(2); var mud = new MB(2); var water = new MB(2); var canopy = new MB(1); var fringe = new MB(1); var tufts = new MB(1);
        float cw = 9f, ch = 6.5f;
        for (float x0 = -F; x0 < F - 2; x0 += cw)
            for (float z0 = -F; z0 < F - 2; z0 += ch)
            {
                float x1 = Mathf.Min(x0 + cw, F), z1 = Mathf.Min(z0 + ch, F);
                if (x1 > -26 && x0 < 26 && z1 > -28 && z0 < 30) continue;                 // 가운데 논틀밭틀 자리
                if ((x0 < roadX + rw && x1 > roadX - rw) || (z0 < roadZ + rw && z1 > roadZ - rw)) continue; // 농로
                // 칸마다 크기를 조금씩 흔들어 자연스럽게
                float jx = R(-0.6f, 0.6f), jz = R(-0.5f, 0.5f);
                float ax = x0 + 0.35f + Mathf.Max(jx, 0), bx = x1 - 0.35f + Mathf.Min(jx, 0), az = z0 + 0.35f + Mathf.Max(jz, 0), bz = z1 - 0.35f + Mathf.Min(jz, 0);
                double k = rng.NextDouble();
                var c = new Vector3((ax + bx) / 2, 0, (az + bz) / 2);
                // 둑: 칸을 두르는 낮은 둑 (모서리 둥글게)
                BevelBox(ridges, new Vector3(c.x, 0, z0 + 0.2f), x1 - x0, 0.42f, 0.5f, 0, 0.12f, 1f, 1f);
                BevelBox(ridges, new Vector3(x0 + 0.2f, 0, c.z), 0.5f, 0.42f, z1 - z0, 0, 0.12f, 1f, 1f);
                if (k < 0.32)
                {
                    BevelBox(water, c, bx - ax, 0.24f, bz - az, 0, 0.06f, 3f, 1f);
                    for (float x = ax + 0.4f; x < bx - 0.3f; x += 0.9f) for (float z = az + 0.4f; z < bz - 0.3f; z += 0.7f)
                            if (rng.NextDouble() < 0.5) Ribbons(tufts, new Vector3(x, 0.24f, z), R(0.15f, 0.25f), 5, 0.05f, 0.25f, 0.012f, 0.2f, 0.75f);
                }
                else if (k < 0.85)
                {
                    float top = 0.3f, h = riceHeight * 0.92f;
                    BevelBox(mud, c, bx - ax, top, bz - az, 0, 0.06f, 1f, 1f);
                    // 멀리서는 벼 윗면, 가장자리에는 진짜 벼 한 줄
                    BevelBox(canopy, c, bx - ax - 0.5f, top + h * 0.8f, bz - az - 0.5f, 0, 0.18f, 2.2f, 2.2f, true);
                    float per = 2 * ((bx - ax) + (bz - az)); int n = Mathf.CeilToInt(per / 0.45f);
                    var rr = new R2 { cx = c.x, cz = c.z, hw = (bx - ax) / 2 - 0.25f, hd = (bz - az) / 2 - 0.25f };
                    for (int i = 0; i < n; i++) { var o = PerimeterPoint(rr, i / (float)n, 0f); Ribbons(fringe, new Vector3(o.x, top, o.y), h * R(0.85f, 1.0f), 6, 0.1f, 0.45f, 0.013f, 0.15f, 0.88f); }
                }
                else
                {
                    for (int i = 0; i < 18; i++) Ribbons(tufts, new Vector3(R(ax, bx), 0, R(az, bz)), R(0.3f, 0.7f), 12, 0.25f, 0.5f, 0.024f, 0f, 0.85f);
                }
            }
        Progress("주변 논 저장하는 중…", 0.45f);
        Emit(near, "논두렁", ridges.ToMesh("논두렁"), new[] { Variant(mGrass, 1f), Variant(mDirt, 1f) }, true);
        Emit(near, "논바닥", mud.ToMesh("주변 논바닥"), new[] { Variant(mMud, 1f), Variant(mDirt, 1f) }, true);
        Emit(near, "물논", water.ToMesh("주변 물논"), new[] { mWater, Variant(mDirt, 1f) }, true);
        Emit(near, "벼 (멀리)", canopy.ToMesh("벼 윗면"), new[] { mCanopy }, true, ShadowCastingMode.On);
        Emit(near, "벼 (가장자리)", fringe.ToMesh("벼 가장자리"), new[] { BladeMat("벼", true) }, true, ShadowCastingMode.Off);
        Emit(near, "풀·모", tufts.ToMesh("주변 풀"), new[] { BladeMat("풀", false) }, true, ShadowCastingMode.Off);

        // 4) 언덕 (멀리, 이동 범위 밖)
        Progress("언덕 만드는 중…", 0.55f);
        var hill = new MB(1);
        int A = 160; float r0 = F + 3, r1 = 260f; int RN = 40;
        var pts = new Vector3[A, RN + 1];
        for (int ri = 0; ri <= RN; ri++)
            for (int ai = 0; ai < A; ai++)
            {
                float t = ri / (float)RN, r = Mathf.Lerp(r0, r1, t * t), ang = ai / (float)A * Mathf.PI * 2;
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float hgt = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(r0, r0 + 60, r)) * (6 + 22 * WorldFbm(x * 0.012f, z * 0.012f)) + Mathf.Pow(t, 2) * 25;
                pts[ai, ri] = new Vector3(x, hgt - 0.01f, z);
            }
        for (int ri = 0; ri < RN; ri++)
            for (int ai = 0; ai < A; ai++)
            {
                int an = (ai + 1) % A; Vector3 a = pts[ai, ri], b = pts[an, ri], c2 = pts[an, ri + 1], d = pts[ai, ri + 1];
                var nrm = Vector3.Cross(c2 - a, b - a).normalized; if (nrm.y < 0) nrm = -nrm;
                hill.Quad(0, a, b, c2, d, nrm, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c2.x, c2.z), new Vector2(d.x, d.z));
            }
        var hillMesh = hill.ToMesh("언덕", true);
        Emit(far, "언덕", hillMesh, new[] { Variant(mGrass, 1f) }, true, ShadowCastingMode.On);

        // 5) 나무 (논밭 가장자리 + 언덕)
        Progress("나무 심는 중…", 0.7f);
        var leaves = new MB(1); var leaves2 = new MB(1); var bark = new MB(1);
        for (int i = 0; i < treeCount; i++)
        {
            float ang = R(0, Mathf.PI * 2), r = i < treeCount * 0.45f ? R(F + 2, F + 10) : R(F + 15, 150);
            float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
            float y = HillHeight(x, z, r0);
            Tree(rng.NextDouble() < 0.5 ? leaves : leaves2, bark, new Vector3(x, y - 0.2f, z), R(6f, 11f));
        }
        // 가운데 가까이 큰 나무 몇 그루 (쉼터 느낌)
        Tree(leaves, bark, new Vector3(-22f, 0, 22f), 10f); Tree(leaves2, bark, new Vector3(20f, 0, -24f), 9f); Tree(leaves, bark, new Vector3(23f, 0, 20f), 8f);
        Emit(far, "나뭇잎", leaves.ToMesh("나뭇잎", true), new[] { mTreeLeaf }, true, ShadowCastingMode.On);
        Emit(far, "나뭇잎2", leaves2.ToMesh("나뭇잎2", true), new[] { mTreeLeaf2 }, true, ShadowCastingMode.On);
        Emit(far, "나무줄기", bark.ToMesh("나무줄기", true), new[] { mBark }, true, ShadowCastingMode.On);

        // 6) 먼 산 두 겹
        Progress("먼 산 만드는 중…", 0.85f);
        Emit(far, "산 (가까운)", Mountains(330f, 45f, 95f, 3), new[] { mMountain }, false, ShadowCastingMode.Off);
        Emit(far, "산 (먼)", Mountains(520f, 90f, 170f, 7), new[] { mMountainFar }, false, ShadowCastingMode.Off);

        SaveAll(); ClearProgress();
        Debug.Log("[논틀밭틀 고퀄] 공간을 넓혔어요. 걸어 다닐 수 있는 범위: ±" + F + "m");
    }

    // ---------------- D) 화면 품질 ----------------
    public void UpgradeQuality()
    {
        RenderSettings.fogDensity = 0.0036f;
        RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.70f);
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.shadowDistance = 140f; urp.shadowCascadeCount = 4; urp.msaaSampleCount = 4;
            MarkDirty(urp);
        }
        foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (cam.gameObject.scene != gameObject.scene) continue;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1200f);
            var d = cam.GetUniversalAdditionalCameraData();
            if (d != null) { d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; d.antialiasingQuality = AntialiasingQuality.High; MarkDirty(d); }
            MarkDirty(cam);
        }
        // 보정: 기존 보정 볼륨이 있으면 고쳐 쓰고, 없으면 새로
        Volume vol = null;
        foreach (var v in Resources.FindObjectsOfTypeAll<Volume>()) if (v.gameObject.scene == gameObject.scene && v.isGlobal) { vol = v; break; }
        if (vol == null) { var go = Child(HqRoot(), "보정 (고퀄)"); vol = go.gameObject.AddComponent<Volume>(); vol.isGlobal = true; }
        if (vol.sharedProfile == null) { var p = ScriptableObject.CreateInstance<VolumeProfile>(); p.name = "보정_고퀄"; SaveAsset(p, SUB, "보정_고퀄.asset"); vol.sharedProfile = p; }
        var prof = vol.sharedProfile;
        T Get<T>() where T : VolumeComponent { if (!prof.TryGet<T>(out var c)) { c = prof.Add<T>(true); AddSub(c, prof); } return c; }
        Get<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var bl = Get<Bloom>(); bl.intensity.Override(0.45f); bl.threshold.Override(0.95f); bl.scatter.Override(0.7f);
        var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0.3f); ca.contrast.Override(12f); ca.saturation.Override(6f);
        var wb = Get<WhiteBalance>(); wb.temperature.Override(14f); wb.tint.Override(3f);
        var smh = Get<ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(0.95f, 0.97f, 1.08f, 0f)); smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0f));
        var vg = Get<Vignette>(); vg.intensity.Override(0.24f); vg.smoothness.Override(0.5f);
        var fg = Get<FilmGrain>(); fg.intensity.Override(0.12f);
        MarkDirty(prof); MarkDirty(vol);
        SaveAll();
        Debug.Log("[논틀밭틀 고퀄] 화면 품질을 올렸어요. (그림자 거리 140m, 안개 옅게, 색 보정)\n더 좋게: Project 창에서 URP Renderer 파일 → Add Renderer Feature → Screen Space Ambient Occlusion");
    }

    // ==================================================================
    //  배치
    // ==================================================================
    void DefineLayout()
    {
        plates.Clear(); strokes.Clear();
        float H = paddyHeight;
        void P(float x0, float x1, float z0, float z1, float top, bool w) => plates.Add(new Plate { x0 = x0, x1 = x1, z0 = z0, z1 = z1, top = top * H, water = w });
        P(-15.6f, -4.4f, -16.6f, 0.6f, 0.3f, false);
        P(-15.1f, -6.9f, 3.4f, 9f, 0.26f, true);
        P(-15.1f, -6.9f, 9.4f, 15f, 0.3f, false);
        P(6.2f, 12.5f, 14.6f, 20f, 0.24f, true);
        P(-3.6f, 3.6f, 7f, 20f, 0.28f, false);
        float[] zc = { -12f, -1f, 10f };
        for (int k = 0; k < 3; k++) { P(5.2f, 9f, zc[k] - 4.8f, zc[k] + 4.8f, 0.2f, k != 1); P(9f, 12.8f, zc[k] - 4.8f, zc[k] + 4.8f, 0.42f, k == 1); }
        for (int j = 0; j < 12; j++)
        {
            float X = FINAL[j, 0], Z = FINAL[j, 1], yaw = FINAL[j, 2] * Mathf.Deg2Rad, fw = FINAL[j, 3], fh = FINAL[j, 4], T = FINAL[j, 5];
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float top = RidgeHeight(j);
            foreach (var st in Jamo2D(JAMO[j], -fw / 2, fw / 2, -fh / 2, fh / 2, T))
                strokes.Add(new R2 { cx = X + c * st.x + s * st.y, cz = Z - s * st.x + c * st.y, hw = st.z / 2, hd = st.w / 2, yaw = yaw, top = top });
        }
    }

    float MaxPlateTop() { float m = 0; foreach (var p in plates) m = Mathf.Max(m, p.top); return m; }
    float RidgeHeight(int j) { int kind = (int)FINAL[j, 6]; return kind == 1 ? 0.4f : (kind == 2 ? 0.3f * paddyHeight + 0.35f : 0.42f * paddyHeight + 0.55f); }

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

    static bool InR(R2 r, float x, float z, float pad)
    {
        float dx = x - r.cx, dz = z - r.cz, c = Mathf.Cos(r.yaw), s = Mathf.Sin(r.yaw);
        return Mathf.Abs(c * dx - s * dz) <= r.hw + pad && Mathf.Abs(s * dx + c * dz) <= r.hd + pad;
    }
    bool InStroke(float x, float z, float pad) { foreach (var r in strokes) if (InR(r, x, z, pad)) return true; return false; }
    bool InPlate(float x, float z, float pad) { foreach (var p in plates) if (x >= p.x0 - pad && x <= p.x1 + pad && z >= p.z0 - pad && z <= p.z1 + pad) return true; return false; }
    float PlateTopAt(float x, float z) { float h = 0; foreach (var p in plates) if (x >= p.x0 && x <= p.x1 && z >= p.z0 && z <= p.z1) h = Mathf.Max(h, p.top); return h; }

    // 직사각형 둘레 위의 점 (t: 0~1, off: 바깥(+)/안쪽(-)으로 얼마나)
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

    // 자모 둑을 논보다 확실히 높게 (별말씀 JamoPiece 껍데기의 높이만 늘려요)
    void RaiseJamoRidges()
    {
        var root = FindJamoRoot(); if (root == null) return;
        var shells = root.GetComponentsInChildren<Byeolmalsseum.JamoPiece>(true);
        foreach (var jp in shells)
        {
            var t = jp.transform;
            if (!MeasureBounds(t, Matrix4x4.identity, out var wb)) continue;
            var lp = transform.InverseTransformPoint(wb.center);
            int best = 0; float bd = float.MaxValue;
            for (int j = 0; j < 12; j++) { float d = (lp.x - FINAL[j, 0]) * (lp.x - FINAL[j, 0]) + (lp.z - FINAL[j, 1]) * (lp.z - FINAL[j, 1]); if (d < bd) { bd = d; best = j; } }
            float want = RidgeHeight(best), now = wb.size.y;
            if (now < 1e-3f || Mathf.Abs(want - now) < 0.01f) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(t, "둑 높이기");
#endif
            var s = t.localScale; s.z *= want / now; t.localScale = s; // 눕힌 자모는 z가 높이
            if (MeasureBounds(t, Matrix4x4.identity, out var nb)) t.position += Vector3.up * (transform.position.y + 0.01f - nb.min.y);
            MarkDirty(t);
        }
    }

    Transform FindJamoRoot()
    {
        if (jamoRoot != null) return jamoRoot;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.gameObject.scene == gameObject.scene && t.name.StartsWith("글자")) { jamoRoot = t; return t; }
        return null;
    }

    static bool MeasureBounds(Transform t, Matrix4x4 space, out Bounds b)
    {
        b = new Bounds(); bool has = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds; var m = space * mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return has;
    }

    float HillHeight(float x, float z, float r0)
    {
        float r = Mathf.Sqrt(x * x + z * z); float t = Mathf.Clamp01((r - r0) / (260f - r0));
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(r0, r0 + 60, r)) * (6 + 22 * WorldFbm(x * 0.012f, z * 0.012f)) + t * t * 25;
    }

    // ==================================================================
    //  풀·벼 모양
    // ==================================================================
    // 휘어진 리본 잎 여러 장 (uvY: 아래 → 위 색 위치, 0~topUV)
    void Ribbons(MB m, Vector3 pos, float h, int blades, float spread, float lean, float width, float uvVar, float topUV)
    {
        float shade = (float)rng.NextDouble();
        for (int b = 0; b < blades; b++)
        {
            float bx = R(-spread, spread) * 0.5f, bz = R(-spread, spread) * 0.5f, dir = R(0, 6.283f), bend = lean * R(0.4f, 1.3f), hh = h * R(0.6f, 1.1f), w0 = width * R(0.7f, 1.2f);
            Vector3 d = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir)), side = new Vector3(-d.z, 0, d.x);
            int seg = 5, k0 = m.v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                Vector3 c = pos + new Vector3(bx, 0, bz) + Vector3.up * (hh * t * (1 - 0.25f * bend * t)) + d * (hh * bend * t * t * 0.6f);
                float w = w0 * Mathf.Pow(1 - t, 0.6f) + 0.0015f;
                Vector3 nrm = (Vector3.up * 0.6f + d * -0.4f + side * 0.2f).normalized;
                m.Add(c - side * w, nrm, new Vector2(0.1f + 0.8f * Mathf.Repeat(shade + uvVar * R(-1, 1), 1f), t * topUV));
                m.Add(c + side * w, nrm, new Vector2(0.1f + 0.8f * Mathf.Repeat(shade + uvVar * R(-1, 1), 1f), t * topUV));
            }
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(a + 3); m.sub[0].Add(a); m.sub[0].Add(a + 3); m.sub[0].Add(a + 2); }
        }
    }

    // 벼 한 포기: 잎 + 고개 숙인 이삭
    void RiceClump(MB m, Vector3 pos, float h, bool panicles)
    {
        Ribbons(m, pos, h * 0.9f, 8, 0.1f, 0.45f, 0.012f, 0.15f, 0.8f);
        if (!panicles) return;
        int n = 3 + rng.Next(3);
        for (int p = 0; p < n; p++)
        {
            float dir = R(0, 6.283f); Vector3 d = new Vector3(Mathf.Cos(dir), 0, Mathf.Sin(dir)), side = new Vector3(-d.z, 0, d.x);
            Vector3 top = pos + new Vector3(R(-0.03f, 0.03f), h * R(0.95f, 1.08f), R(-0.03f, 0.03f));
            // 줄기
            int k0 = m.v.Count;
            m.Add(pos - side * 0.003f, Vector3.up, new Vector2(0.5f, 0.1f)); m.Add(pos + side * 0.003f, Vector3.up, new Vector2(0.5f, 0.1f));
            m.Add(top - side * 0.002f, Vector3.up, new Vector2(0.5f, 0.82f)); m.Add(top + side * 0.002f, Vector3.up, new Vector2(0.5f, 0.82f));
            m.sub[0].Add(k0); m.sub[0].Add(k0 + 1); m.sub[0].Add(k0 + 3); m.sub[0].Add(k0); m.sub[0].Add(k0 + 3); m.sub[0].Add(k0 + 2);
            // 이삭: 꼭대기에서 바깥으로 휘어 내려가는 알곡 띠
            int seg = 5; k0 = m.v.Count; float len = h * R(0.18f, 0.26f);
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg; float a = t * 1.6f;
                Vector3 c = top + d * (Mathf.Sin(a) * len * 0.75f) + Vector3.up * ((Mathf.Cos(a) - 1) * len * 0.6f + len * 0.15f * (1 - t));
                float w = 0.016f * Mathf.Sin(Mathf.PI * Mathf.Clamp(t * 1.05f + 0.05f, 0, 1)) + 0.003f;
                m.Add(c - side * w, (Vector3.up + d * 0.3f).normalized, new Vector2(0.2f + 0.6f * (float)rng.NextDouble(), 0.93f + 0.06f * t));
                m.Add(c + side * w, (Vector3.up + d * 0.3f).normalized, new Vector2(0.2f + 0.6f * (float)rng.NextDouble(), 0.93f + 0.06f * t));
            }
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(a + 3); m.sub[0].Add(a); m.sub[0].Add(a + 3); m.sub[0].Add(a + 2); }
        }
    }

    // 나무: 줄기 + 울퉁불퉁한 잎 덩어리 여러 개
    void Tree(MB leaves, MB bark, Vector3 basePos, float height)
    {
        float trunkH = height * R(0.45f, 0.6f), rad = height * 0.035f; int sides = 8;
        int k0 = bark.v.Count;
        for (int i = 0; i <= sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            bark.Add(basePos + d * rad * 1.3f, d, new Vector2(i / (float)sides, 0)); bark.Add(basePos + d * rad * 0.6f + Vector3.up * trunkH, d, new Vector2(i / (float)sides, 1));
        }
        for (int i = 0; i < sides; i++) { int a = k0 + i * 2; bark.sub[0].Add(a); bark.sub[0].Add(a + 1); bark.sub[0].Add(a + 3); bark.sub[0].Add(a); bark.sub[0].Add(a + 3); bark.sub[0].Add(a + 2); }
        int blobs = 4 + rng.Next(4); float crown = height * 0.32f;
        for (int b = 0; b < blobs; b++)
        {
            var c = basePos + Vector3.up * (trunkH + crown * R(0.1f, 0.9f)) + new Vector3(R(-1, 1), 0, R(-1, 1)) * crown * 0.6f;
            Blob(leaves, c, crown * R(0.55f, 0.85f));
        }
    }

    void Blob(MB m, Vector3 c, float r)
    {
        int la = 9, lo = 14; int k0 = m.v.Count; float seed = R(0, 100);
        for (int i = 0; i <= la; i++)
            for (int j = 0; j <= lo; j++)
            {
                float th = i / (float)la * Mathf.PI, ph = j / (float)lo * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th) * 0.85f, Mathf.Sin(th) * Mathf.Sin(ph));
                float n = 0.75f + 0.45f * WorldFbm(d.x * 2.2f + seed, d.z * 2.2f + d.y * 1.7f + seed);
                m.Add(c + d * r * n, d.normalized, new Vector2(j / (float)lo, i / (float)la));
            }
        for (int i = 0; i < la; i++)
            for (int j = 0; j < lo; j++)
            {
                int a = k0 + i * (lo + 1) + j, b = a + lo + 1;
                m.sub[0].Add(a); m.sub[0].Add(a + 1); m.sub[0].Add(b); m.sub[0].Add(a + 1); m.sub[0].Add(b + 1); m.sub[0].Add(b);
            }
    }

    Mesh Mountains(float radius, float hMin, float hMax, int seedOff)
    {
        var m = new MB(1); int A = 220;
        var top = new Vector3[A];
        for (int i = 0; i < A; i++)
        {
            float a = i / (float)A * Mathf.PI * 2, x = Mathf.Cos(a), z = Mathf.Sin(a);
            float n = WorldFbm(x * 2.3f + seedOff, z * 2.3f - seedOff) * 0.75f + WorldFbm(x * 7f + seedOff, z * 7f) * 0.25f;
            top[i] = new Vector3(x * radius, Mathf.Lerp(hMin, hMax, n), z * radius);
        }
        for (int i = 0; i < A; i++)
        {
            var a = top[i]; var b = top[(i + 1) % A];
            var a0 = new Vector3(a.x * 0.97f, -15f, a.z * 0.97f); var b0 = new Vector3(b.x * 0.97f, -15f, b.z * 0.97f);
            var nrm = -new Vector3(a.x + b.x, 0, a.z + b.z).normalized + Vector3.up * 0.4f;
            m.Quad(0, a0, b0, b, a, nrm.normalized, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }
        return m.ToMesh("먼 산", true);
    }

    // ==================================================================
    //  모서리 둥근 상자 (윗면 = 0번, 옆면 = 1번 재질)
    // ==================================================================
    static void BevelBox(MB m, Vector3 center, float w, float h, float d, float yawDeg, float bevel, float tileTop, float tileSide, bool singleSub = false)
    {
        float hw = w / 2, hd = d / 2; bevel = Mathf.Min(bevel, Mathf.Min(hw, hd) * 0.9f, h * 0.9f);
        var q = Quaternion.Euler(0, yawDeg, 0);
        Vector3 W(Vector3 l) => center + q * l;
        int sTop = 0, sSide = singleSub ? 0 : 1;
        // 윗면
        Vector3 t0 = new Vector3(-hw + bevel, h, -hd + bevel), t1 = new Vector3(-hw + bevel, h, hd - bevel), t2 = new Vector3(hw - bevel, h, hd - bevel), t3 = new Vector3(hw - bevel, h, -hd + bevel);
        m.Quad(sTop, W(t0), W(t1), W(t2), W(t3), q * Vector3.up, UVt(W(t0), tileTop), UVt(W(t1), tileTop), UVt(W(t2), tileTop), UVt(W(t3), tileTop));
        // 네 변: 경사(둥근 모서리) + 옆면
        Vector3[] ns = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (var n in ns)
        {
            Vector3 t = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x)); // 변 방향
            float Do = Mathf.Abs(n.x) > 0 ? hw : hd, Lo = Mathf.Abs(n.x) > 0 ? hd : hw, Di = Do - bevel, Li = Lo - bevel;
            Vector3 a = n * Di - t * Li + Vector3.up * h, b = n * Di + t * Li + Vector3.up * h;
            Vector3 c = n * Do + t * Lo + Vector3.up * (h - bevel), e = n * Do - t * Lo + Vector3.up * (h - bevel);
            Vector3 c0 = n * Do + t * Lo, e0 = n * Do - t * Lo;
            Vector3 nb = q * (n + Vector3.up).normalized, nsd = q * n;
            float ua = Vector3.Dot(W(a), q * t) / tileSide, ub = Vector3.Dot(W(b), q * t) / tileSide;
            m.Quad(sTop, W(a), W(b), W(c), W(e), nb, new Vector2(ua, (h + bevel) / tileSide), new Vector2(ub, (h + bevel) / tileSide), new Vector2(ub, h / tileSide), new Vector2(ua, h / tileSide));
            if (h - bevel > 0.001f)
                m.Quad(sSide, W(e), W(c), W(c0), W(e0), nsd, new Vector2(ua, (h - bevel) / tileSide), new Vector2(ub, (h - bevel) / tileSide), new Vector2(ub, 0), new Vector2(ua, 0));
        }
    }
    static Vector2 UVt(Vector3 w, float tile) => new Vector2(w.x, w.z) / tile;

    // 메시 만드는 도우미
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int>[] sub;
        public MB(int subs) { sub = new List<int>[subs]; for (int i = 0; i < subs; i++) sub[i] = new List<int>(); }
        public void Add(Vector3 p, Vector3 nrm, Vector2 u) { v.Add(p); n.Add(nrm); uv.Add(u); }
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int k = v.Count; Add(a, nrm, ua); Add(b, nrm, ub); Add(c, nrm, uc); Add(d, nrm, ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0) { sub[s].Add(k); sub[s].Add(k + 1); sub[s].Add(k + 2); sub[s].Add(k); sub[s].Add(k + 2); sub[s].Add(k + 3); }
            else { sub[s].Add(k); sub[s].Add(k + 2); sub[s].Add(k + 1); sub[s].Add(k); sub[s].Add(k + 3); sub[s].Add(k + 2); }
        }
        public Mesh ToMesh(string name, bool smooth = false)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length; for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            if (smooth) m.RecalculateNormals();
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }

    // ==================================================================
    //  질감 그리기 (고해상도 · 부드러운 노이즈 · 비틀린 무늬로 자연스럽게)
    // ==================================================================
    static float Lattice(int x, int y, int seed)
    {
        unchecked { int h = x * 374761393 + y * 668265263 + seed * 1442695041; h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16; return (h & 0xffffff) / 16777215f; }
    }
    static float TileNoise(float u, float v, int freq, int seed)
    {
        float x = u * freq, y = v * freq; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        int x0 = ((xi % freq) + freq) % freq, x1 = (x0 + 1) % freq, y0 = ((yi % freq) + freq) % freq, y1 = (y0 + 1) % freq;
        float sx = fx * fx * fx * (fx * (fx * 6 - 15) + 10), sy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
        return Mathf.Lerp(Mathf.Lerp(Lattice(x0, y0, seed), Lattice(x1, y0, seed), sx), Mathf.Lerp(Lattice(x0, y1, seed), Lattice(x1, y1, seed), sx), sy);
    }
    static float Fbm(float u, float v, int seed, int baseFreq = 4, int oct = 5)
    {
        float s = 0, a = 0.5f, norm = 0; int f = baseFreq;
        for (int i = 0; i < oct; i++) { s += TileNoise(u, v, f, seed + i * 31) * a; norm += a; a *= 0.5f; f *= 2; }
        return s / norm;
    }
    // 비틀린 노이즈: 무늬가 격자처럼 안 보이고 자연스럽게 흘러요
    static float Warp(float u, float v, int seed, int f = 4)
    {
        float wx = Fbm(u, v, seed + 101, 2, 3) - 0.5f, wy = Fbm(u, v, seed + 202, 2, 3) - 0.5f;
        return Fbm(Mathf.Repeat(u + wx * 0.35f, 1f), Mathf.Repeat(v + wy * 0.35f, 1f), seed, f);
    }
    static float WorldFbm(float x, float z)
    {
        float s = 0, a = 0.5f, norm = 0, f = 1;
        for (int i = 0; i < 5; i++) { s += Mathf.PerlinNoise(x * f + 31.7f * i, z * f - 17.3f * i) * a; norm += a; a *= 0.5f; f *= 2; }
        return s / norm;
    }

    void MakeTextures()
    {
        const int S = 1024, D = 512;
        // 1) 풀 색 얼룩 (넓은 범위 색 변화: 초록·누런 풀·마른 흙빛)
        tGrassMacro = Paint(S, false, "풀_얼룩", (u, v) =>
        {
            float a = Warp(u, v, 11, 3), b = Warp(u, v, 12, 6), c = Fbm(u, v, 13, 16, 4);
            Color deep = new Color(0.20f, 0.30f, 0.10f), mid = new Color(0.33f, 0.44f, 0.16f), dry = new Color(0.52f, 0.52f, 0.26f), bare = new Color(0.42f, 0.36f, 0.22f);
            Color col = Color.Lerp(deep, mid, Mathf.SmoothStep(0.3f, 0.7f, a));
            col = Color.Lerp(col, dry, Mathf.SmoothStep(0.55f, 0.8f, b) * 0.7f);
            col = Color.Lerp(col, bare, Mathf.SmoothStep(0.72f, 0.88f, Warp(u, v, 14, 5)) * 0.45f);
            return col * (0.9f + 0.2f * c);
        });
        Progress("질감 그리는 중… (풀 잎)", 0.2f);
        // 2) 풀잎 잔무늬 (회색 0.5 = 원래 색 그대로, 가까이서 잎이 보이게)
        var gh = new float[D * D];
        for (int i = 0; i < 26000; i++)
        {
            float x = R(0, D), y = R(0, D), ang = R(0, 6.283f), len = R(4, 14), wv = R(0.6f, 1.6f), val = R(0.35f, 1f);
            SoftLine(gh, D, x, y, ang, len, wv, val);
        }
        Blur(gh, D, 1);
        tGrassDetail = FromHeight(gh, D, "풀_잔무늬", (h) => { float g = 0.40f + 0.24f * h; return new Color(g, g * 1.02f, g * 0.97f); }, true);
        tGrassDetailN = NormalTex(gh, D, 3.5f, "풀_잔무늬_울퉁불퉁");
        tGrassN = NormalTex(HeightOf(tGrassMacro), S, 1.2f, "풀_얼룩_울퉁불퉁");

        Progress("질감 그리는 중… (흙)", 0.4f);
        // 3) 흙: 부드러운 흙덩이 + 그늘진 자갈 + 고운 모래
        var dh = new float[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float u = x / (float)S, v = y / (float)S; dh[y * S + x] = Warp(u, v, 21, 6) * 0.6f + Fbm(u, v, 22, 32, 3) * 0.25f; }
        for (int i = 0; i < 2600; i++) Pebble(dh, S, R(0, S), R(0, S), R(1.5f, 6f), R(0.15f, 0.45f));
        Blur(dh, S, 1);
        tDirt = FromHeight(dh, S, "흙_질감", (h) =>
        {
            Color lo = new Color(0.30f, 0.21f, 0.13f), hi = new Color(0.58f, 0.45f, 0.31f);
            return Color.Lerp(lo, hi, Mathf.SmoothStep(0.2f, 0.95f, h));
        }, false);
        tDirtN = NormalTex(dh, S, 5f, "흙_울퉁불퉁");

        Progress("질감 그리는 중… (진흙)", 0.55f);
        // 4) 논 진흙: 젖은 흙 + 물기 + 볏짚 조각
        var mh = new float[S * S]; var mc = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S; float h = Warp(u, v, 31, 5) * 0.7f + Fbm(u, v, 32, 24, 3) * 0.3f; mh[y * S + x] = h;
                float wet = Mathf.SmoothStep(0.45f, 0.25f, Warp(u, v, 33, 3));
                Color c = Color.Lerp(new Color(0.20f, 0.15f, 0.10f), new Color(0.36f, 0.29f, 0.19f), h);
                mc[y * S + x] = Color.Lerp(c, new Color(0.14f, 0.12f, 0.09f), wet * 0.6f);
            }
        for (int i = 0; i < 1800; i++)
        {
            float x = R(0, S), y = R(0, S), ang = R(0, 6.283f), len = R(6, 22);
            for (float s = 0; s < len; s += 0.5f) { int px = Wrap((int)(x + Mathf.Cos(ang) * s), S), py = Wrap((int)(y + Mathf.Sin(ang) * s), S); mc[py * S + px] = Color.Lerp(mc[py * S + px], new Color(0.62f, 0.52f, 0.30f), 0.6f); mh[py * S + px] += 0.08f; }
        }
        tMud = ToTex(mc, S, "진흙_질감", false);
        tMudN = NormalTex(mh, S, 3f, "진흙_울퉁불퉁");

        Progress("질감 그리는 중… (멀리 보이는 벼)", 0.7f);
        // 5) 멀리 보이는 벼 윗면: 황금빛 이삭 무늬
        var ch = new float[D * D]; var cc = new Color[D * D];
        for (int i = 0; i < cc.Length; i++) cc[i] = new Color(0.30f, 0.30f, 0.10f);
        for (int i = 0; i < 9000; i++)
        {
            float x = R(0, D), y = R(0, D), ang = R(0, 6.283f), len = R(5, 13);
            Color col = Color.Lerp(new Color(0.62f, 0.52f, 0.16f), new Color(0.86f, 0.70f, 0.30f), R(0, 1)) * R(0.8f, 1.1f);
            for (float s = 0; s < len; s += 0.5f)
            {
                int px = Wrap((int)(x + Mathf.Cos(ang) * s), D), py = Wrap((int)(y + Mathf.Sin(ang) * s), D);
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) { int qx = Wrap(px + ox, D), qy = Wrap(py + oy, D); float wgt = (ox == 0 && oy == 0) ? 0.8f : 0.25f; cc[qy * D + qx] = Color.Lerp(cc[qy * D + qx], col, wgt); ch[qy * D + qx] += wgt * 0.2f; }
            }
        }
        tCanopy = ToTex(cc, D, "벼_원경", false);
        tCanopyN = NormalTex(ch, D, 2.5f, "벼_원경_울퉁불퉁");

        foreach (var t in new[] { tGrassMacro, tGrassN, tGrassDetail, tGrassDetailN, tDirt, tDirtN, tMud, tMudN, tCanopy, tCanopyN }) SaveAsset(t, SUB, t.name + ".asset");
    }

    void MakeMaterials()
    {
        mGrass = Lit("고퀄_풀", Color.white, tGrassMacro, tGrassN, 0.06f, 1.0f); Detail(mGrass, tGrassDetail, tGrassDetailN, 1f);
        mDirt = Lit("고퀄_흙", new Color(0.95f, 0.9f, 0.86f), tDirt, tDirtN, 0.08f, 1.2f);
        mPath = Lit("고퀄_길_흙", new Color(1.08f, 1.02f, 0.95f), tDirt, tDirtN, 0.12f, 1.0f);
        mMud = Lit("고퀄_진흙", Color.white, tMud, tMudN, 0.42f, 0.8f);
        mWater = Lit("고퀄_물", new Color(0.07f, 0.09f, 0.07f), null, LoadTex(FOLDER + "/물결.asset"), 0.97f, 0.45f);
        if (mWater.HasProperty("_BaseMap")) mWater.SetTextureScale("_BaseMap", new Vector2(1f / 3f, 1f / 3f));
        mCanopy = Lit("고퀄_벼_원경", Color.white, tCanopy, tCanopyN, 0.15f, 1f);
        mTreeLeaf = Lit("고퀄_나뭇잎", new Color(0.20f, 0.32f, 0.12f), null, null, 0.18f, 1f);
        mTreeLeaf2 = Lit("고퀄_나뭇잎2", new Color(0.27f, 0.36f, 0.14f), null, null, 0.18f, 1f);
        mBark = Lit("고퀄_줄기", new Color(0.28f, 0.21f, 0.15f), null, null, 0.1f, 1f);
        mMountain = Lit("고퀄_산", new Color(0.20f, 0.29f, 0.26f), null, null, 0.05f, 1f);
        mMountainFar = Lit("고퀄_먼산", new Color(0.32f, 0.40f, 0.48f), null, null, 0.05f, 1f);
        foreach (var m in new[] { mGrass, mDirt, mPath, mMud, mWater, mCanopy, mTreeLeaf, mTreeLeaf2, mBark, mMountain, mMountainFar }) SaveAsset(m, SUB, m.name + ".mat");
    }

    void EnsureMaterials()
    {
#if UNITY_EDITOR
        Material L(string n) => AssetDatabase.LoadAssetAtPath<Material>(SUB + "/" + n + ".mat");
        mGrass = L("고퀄_풀"); mDirt = L("고퀄_흙"); mPath = L("고퀄_길_흙"); mMud = L("고퀄_진흙"); mWater = L("고퀄_물"); mCanopy = L("고퀄_벼_원경");
        mTreeLeaf = L("고퀄_나뭇잎"); mTreeLeaf2 = L("고퀄_나뭇잎2"); mBark = L("고퀄_줄기"); mMountain = L("고퀄_산"); mMountainFar = L("고퀄_먼산");
#endif
        if (mGrass == null || mDirt == null || mPath == null || mMud == null || mWater == null || mCanopy == null || mTreeLeaf == null || mTreeLeaf2 == null || mBark == null || mMountain == null || mMountainFar == null)
        { rng = new System.Random(11); MakeTextures(); MakeMaterials(); }
    }

    // UV 1칸이 몇 m인지에 맞춰 같은 재질을 크기별로 따로 만들어요
    Material Variant(Material baseM, float metersPerUV)
    {
        float mpu = Mathf.Round(metersPerUV * 100f) / 100f; if (mpu <= 0) mpu = 1;
        string name = baseM.name + "_" + mpu.ToString("0.00");
#if UNITY_EDITOR
        var exist = AssetDatabase.LoadAssetAtPath<Material>(SUB + "/" + name + ".mat"); if (exist != null && !Application.isPlaying) { exist.shader = baseM.shader; exist.CopyPropertiesFromMaterial(baseM); exist.shaderKeywords = baseM.shaderKeywords; exist.name = name; SetTiling(exist, baseM, mpu); return exist; }
#endif
        var m = new Material(baseM) { name = name };
        SetTiling(m, baseM, mpu);
        SaveAsset(m, SUB, name + ".mat");
        return m;
    }
    void SetTiling(Material m, Material baseM, float mpu)
    {
        float macro = baseM == mGrass ? macroTile : (baseM == mMud ? 4f : (baseM == mCanopy ? 2.2f : 2.6f));
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", Vector2.one * (mpu / macro));
        if (m.HasProperty("_DetailAlbedoMap")) m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (macro / detailTile));
        MarkDirty(m);
    }

    static float MetersPerUV(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return 1f;
        var mesh = mf.sharedMesh; var uv = mesh.uv; if (uv == null || uv.Length == 0) return 1f;
        Vector2 mn = uv[0], mx = uv[0]; foreach (var q in uv) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
        var size = Vector3.Scale(mesh.bounds.size, r.transform.lossyScale);
        float worldMax = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)); float uvMax = Mathf.Max(mx.x - mn.x, mx.y - mn.y);
        return uvMax > 1e-4f ? worldMax / uvMax : 1f;
    }

    Material BladeMat(string kind, bool rice)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { var have = AssetDatabase.LoadAssetAtPath<Material>(SUB + "/잎_" + kind + ".mat"); if (have != null && have.shader != null && have.shader.isSupported) return have; }
#endif
        var tex = new Texture2D(16, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "잎_" + kind + "_색" };
        Color bot, mid, top, grain;
        if (rice) { bot = new Color(0.26f, 0.34f, 0.10f); mid = new Color(0.55f, 0.58f, 0.18f); top = new Color(0.80f, 0.66f, 0.26f); grain = new Color(0.86f, 0.64f, 0.22f); }
        else if (kind == "모") { bot = new Color(0.16f, 0.30f, 0.08f); mid = new Color(0.32f, 0.55f, 0.18f); top = new Color(0.48f, 0.70f, 0.26f); grain = top; }
        else { bot = new Color(0.12f, 0.20f, 0.06f); mid = new Color(0.30f, 0.42f, 0.13f); top = new Color(0.58f, 0.62f, 0.28f); grain = top; }
        for (int y = 0; y < 128; y++) for (int x = 0; x < 16; x++)
            {
                float t = y / 127f, var = x / 15f; Color c;
                if (t < 0.9f) { float k = t / 0.9f; c = k < 0.55f ? Color.Lerp(bot, mid, k / 0.55f) : Color.Lerp(mid, top, (k - 0.55f) / 0.45f); c *= Mathf.Lerp(0.55f, 1f, Mathf.Pow(k, 0.5f)); }
                else c = grain;
                c *= 0.82f + 0.36f * var; c.a = 1; tex.SetPixel(x, y, c);
            }
        tex.Apply(); SaveAsset(tex, SUB, tex.name + ".asset");
        var m = Lit("잎_" + kind, Color.white, tex, null, 0.2f, 1f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        m.doubleSidedGI = true;
        SaveAsset(m, SUB, m.name + ".mat");
        return m;
    }

    static Material Lit(string name, Color c, Texture2D tex, Texture2D nrm, float smooth, float bump)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name, enableInstancing = true };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
        return m;
    }
    static void Detail(Material m, Texture2D albedo, Texture2D nrm, float strength)
    {
        if (!m.HasProperty("_DetailAlbedoMap")) return;
        m.SetTexture("_DetailAlbedoMap", albedo); m.SetFloat("_DetailAlbedoMapScale", strength);
        if (nrm != null) { m.SetTexture("_DetailNormalMap", nrm); m.SetFloat("_DetailNormalMapScale", 0.8f); }
        m.EnableKeyword("_DETAIL_MULX2");
    }

    // ---- 질감 도우미 ----
    static int Wrap(int i, int S) => ((i % S) + S) % S;
    Texture2D Paint(int S, bool linear, string name, System.Func<float, float, Color> f)
    {
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { var c = f(x / (float)S, y / (float)S); c.a = 1; px[y * S + x] = c; }
        return ToTex(px, S, name, linear);
    }
    static Texture2D ToTex(Color[] px, int S, string name, bool linear)
    {
        var t = new Texture2D(S, S, TextureFormat.RGBA32, true, linear) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 16 };
        t.SetPixels(px); t.Apply(true); return t;
    }
    static Texture2D FromHeight(float[] h, int S, string name, System.Func<float, Color> f, bool linear)
    {
        float mn = float.MaxValue, mx = float.MinValue; foreach (var q in h) { mn = Mathf.Min(mn, q); mx = Mathf.Max(mx, q); }
        var px = new Color[S * S]; for (int i = 0; i < px.Length; i++) { var c = f((h[i] - mn) / Mathf.Max(mx - mn, 1e-5f)); c.a = 1; px[i] = c; }
        return ToTex(px, S, name, linear);
    }
    static float[] HeightOf(Texture2D t)
    {
        var p = t.GetPixels(); var h = new float[p.Length]; for (int i = 0; i < p.Length; i++) h[i] = p[i].r * 0.3f + p[i].g * 0.59f + p[i].b * 0.11f; return h;
    }
    static Texture2D NormalTex(float[] h, int S, float strength, string name)
    {
        var o = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = (h[y * S + Wrap(x + 1, S)] - h[y * S + Wrap(x - 1, S)]) * strength, dy = (h[Wrap(y + 1, S) * S + x] - h[Wrap(y - 1, S) * S + x]) * strength;
                var n = new Vector3(-dx, -dy, 1).normalized; o[y * S + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
        return ToTex(o, S, name, true);
    }
    static void Blur(float[] h, int S, int passes)
    {
        var tmp = new float[h.Length];
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tmp[y * S + x] = (h[y * S + Wrap(x - 1, S)] + 2 * h[y * S + x] + h[y * S + Wrap(x + 1, S)]) * 0.25f;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) h[y * S + x] = (tmp[Wrap(y - 1, S) * S + x] + 2 * tmp[y * S + x] + tmp[Wrap(y + 1, S) * S + x]) * 0.25f;
        }
    }
    static void SoftLine(float[] h, int S, float x, float y, float ang, float len, float w, float val)
    {
        float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang); int r = Mathf.CeilToInt(w + 1);
        for (float s = 0; s < len; s += 0.5f)
        {
            float px = x + cx * s, py = y + cy * s, ww = w * (1 - s / len * 0.8f);
            for (int oy = -r; oy <= r; oy++) for (int ox = -r; ox <= r; ox++)
                {
                    int qx = Wrap((int)px + ox, S), qy = Wrap((int)py + oy, S); float d = Mathf.Sqrt(ox * ox + oy * oy);
                    float k = Mathf.Clamp01(1 - d / (ww + 0.5f)); if (k <= 0) continue; int i = qy * S + qx; h[i] = Mathf.Max(h[i], val * k * (0.6f + 0.4f * s / len));
                }
        }
    }
    static void Pebble(float[] h, int S, float x, float y, float r, float height)
    {
        int R = Mathf.CeilToInt(r + 1);
        for (int oy = -R; oy <= R; oy++) for (int ox = -R; ox <= R; ox++)
            {
                float d = Mathf.Sqrt(ox * ox + oy * oy) / r; if (d >= 1) continue;
                int i = Wrap((int)y + oy, S) * S + Wrap((int)x + ox, S); h[i] += height * Mathf.Sqrt(1 - d * d);
            }
    }
    static Texture2D LoadTex(string path)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
        return null;
#endif
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    // ==================================================================
    //  오브젝트·저장 도우미
    // ==================================================================
    Transform HqRoot()
    {
        var t = transform.Find(HQ_NAME); if (t) return t;
        t = new GameObject(HQ_NAME).transform; t.SetParent(transform, false); return t;
    }
    static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    void DestroyChild(Transform parent, string name) { var c = parent.Find(name); if (c) DestroyNow(c.gameObject); }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    List<Renderer> SceneRenderers()
    {
        var list = new List<Renderer>();
        foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>()) if (r.gameObject.scene == gameObject.scene && (r is MeshRenderer)) list.Add(r);
        return list;
    }

    void Emit(Transform parent, string name, Mesh mesh, Material[] mats, bool receive, ShadowCastingMode cast = ShadowCastingMode.On)
    {
        if (mesh == null || mesh.vertexCount == 0) return;
        SaveAsset(mesh, SUB, "메시_" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = cast; mr.receiveShadows = receive;
    }

    static void SaveAsset(Object o, string folder, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(SUB)) AssetDatabase.CreateFolder(FOLDER, "고퀄");
        file = file.Replace("/", "_").Replace("·", "_").Replace("(", "").Replace(")", "").Replace(" ", "_");
        string path = folder + "/" + file;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(o, path);
#endif
    }
    static void AddSub(Object sub, Object main)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && AssetDatabase.Contains(main)) { sub.name = sub.GetType().Name; AssetDatabase.AddObjectToAsset(sub, main); }
#endif
    }
    static void SaveAll()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
#endif
    }
    void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        if (o != null && !Application.isPlaying) EditorUtility.SetDirty(o);
        if (!Application.isPlaying && gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
    static void Progress(string msg, float p)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorUtility.DisplayProgressBar("논틀밭틀 고퀄", msg, p);
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
[CustomEditor(typeof(NonteulFieldUpgrade))]
public class NonteulFieldUpgradeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulFieldUpgrade)target;
        EditorGUILayout.HelpBox("NonteulFieldMaker로 ①②③을 끝낸 뒤에 눌러요.\n시작 전에 [별말씀] 논틀밭틀의 '지금 배치 저장 (백업)'을 눌러 두세요.\n버튼마다 몇 초~몇십 초 걸릴 수 있어요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("★ 전부 한 번에 (A → B → C → D)", GUILayout.Height(40))) Run(s.DoAll);
        GUILayout.Space(4);
        if (GUILayout.Button("A) 질감 고퀄로 (자연스러운 풀·흙·진흙)", GUILayout.Height(28))) Run(s.UpgradeTextures);
        if (GUILayout.Button("B) 논 높이고 · 고퀄 벼·풀 다시 심기", GUILayout.Height(28))) Run(s.RebuildPaddies);
        if (GUILayout.Button("C) 공간 넓히기 (주변 논밭·농로·나무·언덕·먼 산)", GUILayout.Height(28))) Run(s.ExpandSpace);
        if (GUILayout.Button("D) 화면 품질 올리기 (그림자·안개·색 보정)", GUILayout.Height(28))) Run(s.UpgradeQuality);
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
