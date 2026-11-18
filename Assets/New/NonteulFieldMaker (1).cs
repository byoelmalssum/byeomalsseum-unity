// =====================================================================
//  NonteulFieldMaker.cs — 지금 씬 그대로 '논틀밭틀' 환경·질감 입히기 (URP)
//
//  이 스크립트는 기존 [별말씀] 논틀밭틀 시스템(자모 날아가기, 체험 카메라)을
//  건드리지 않아요. 그 위에 땅 질감·논·물·벼·풀·하늘·빛·보정만 만들어 얹어요.
//
//  쓰는 법
//   1) 이 파일을 Assets 폴더에 넣어요.
//   2) Hierarchy의 「공간 (소품·조명 등 여기에)」를 클릭 → Add Component → NonteulFieldMaker
//   3) Inspector에 생긴 버튼을 차례로 눌러요.
//        ① 환경 만들기   ② 자모 자동 배치   ③ 자모에 둑 질감 입히기
//   4) Ctrl + S 로 저장. 만든 질감·재질은 Assets/논틀밭틀_생성 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulFieldMaker : MonoBehaviour
{
    [Header("자모가 들어 있는 그룹 (비워 두면 '글자'로 시작하는 오브젝트를 찾아요)")]
    public Transform jamoRoot;

    [Header("질감 크기 (m) — 그림 한 장이 덮는 크기")]
    public float grassTile = 2.6f;
    public float dirtTile = 1.6f;
    public float mudTile = 2f;

    [Header("심는 양")]
    [Range(0.25f, 0.6f)] public float riceSpacing = 0.36f;
    [Range(300, 5000)] public int grassTufts = 2500;
    [Tooltip("풀을 심는 범위 (가운데에서 ±m)")]
    public float grassRange = 20f;

    [Header("빛과 분위기")]
    public bool setupSkyAndLight = true;
    public bool setupPostProcessing = true;

    [Header("Scene 화면에 자모 자리 안내선 보이기")]
    public bool showGuides = true;

    // ---- 만들어진 것 (자동으로 채워져요) ----
    [SerializeField, HideInInspector] Material waterMat;
    [SerializeField, HideInInspector] List<MeshFilter> origFilters = new List<MeshFilter>();
    [SerializeField, HideInInspector] List<Mesh> origMeshes = new List<Mesh>();
    [SerializeField, HideInInspector] List<Material> origMats = new List<Material>();

    const string ENV_NAME = "논틀밭틀_환경 (자동 생성)";
    const string FOLDER = "Assets/논틀밭틀_생성";
    static readonly Vector3 SUN = new Vector3(-0.45f, 0.26f, 1.0f).normalized;

    // ------------------------------------------------------------------
    //  배치표 (가이드 문서의 자모 자리표와 같은 숫자)
    //  X, Z, 회전(도), 가로, 세로, 획 굵기, 높이, 길(1)/둑(0)
    // ------------------------------------------------------------------
    static readonly float[,] FINAL = {
        {-10f,-8f,   0f, 12f, 18f, 0.8f, 0.9f, 0}, // 1 ㄴ 큰 논 둑
        {-10f,-7.5f, 0f, 3.2f,2.4f,0.45f,0.45f,0}, // 2 ㅗ 물꼬 둑
        {-10f,-8f, 180f, 12f, 18f, 0.8f, 0.9f, 0}, // 3 ㄴ 큰 논 둑 (뒤집힘)
        {  9f,-12f, 90f, 10f,  8f, 0.6f, 0.8f, 0}, // 4 ㅌ 다랑논 1
        {-4.3f,4.5f,90f,  6f,  1f, 0.75f,0.9f, 0}, // 5 ㅡ 길가 논둑
        {  0f,-11.5f,0f,  5f, 11f, 1.0f, 0.35f,1}, // 6 ㄹ 꼬불길 1
        {-11f, 9f,   0f,  9f, 12f, 0.75f,0.9f, 0}, // 7 ㅂ 두 칸 논
        {5.5f,17f,   0f, 3.5f, 8f, 0.7f, 0.4f, 1}, // 8 ㅏ 갈림 두렁
        {  9f,-1f,  90f, 10f,  8f, 0.6f, 0.8f, 0}, // 9 ㅌ 다랑논 2
        {  9f,10f,  90f, 10f,  8f, 0.6f, 0.8f, 0}, // 10 ㅌ 다랑논 3
        {-4.3f,11f, 90f,  6f,  1f, 0.75f,0.9f, 0}, // 11 ㅡ 길가 논둑
        {  0f, 0.4f, 0f,  5f, 11f, 1.0f, 0.35f,1}, // 12 ㄹ 꼬불길 2
    };
    static readonly string JAMO = "ㄴㅗㄴㅌㅡㄹㅂㅏㅌㅌㅡㄹ";

    class Plate { public float x0, x1, z0, z1, top; public bool water; }
    class Rect2 { public float cx, cz, hw, hd, yaw; }
    readonly List<Plate> plates = new List<Plate>();
    readonly List<Rect2> strokeRects = new List<Rect2>();
    readonly List<Vector2> route = new List<Vector2>();
    System.Random rng;
    Transform env;

    // 재질
    Material mGrass, mDirt, mPath, mMud, mGround;

    // ==================================================================
    //  플레이 중: 물결만 움직여요
    // ==================================================================
    void Update()
    {
        if (waterMat != null && waterMat.HasProperty("_BaseMap"))
            waterMat.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.012f, Time.time * 0.008f));
    }

    // ==================================================================
    //  ① 환경 만들기
    // ==================================================================
    public void BuildEnvironment()
    {
        RemoveEnvironment();
        rng = new System.Random(7);
        DefineLayout();
        env = new GameObject(ENV_NAME).transform;
        env.SetParent(transform, false);
        env.localPosition = Vector3.zero; env.localRotation = Quaternion.identity; env.localScale = Vector3.one;

        MakeMaterials();
        ApplyFloor();

        // 논 칸
        var pRoot = Child(env, "논 칸");
        int pi = 0;
        foreach (var p in plates)
        {
            var mesh = BoxMesh(p.x1 - p.x0, p.top, p.z1 - p.z0, p.water ? 3f : mudTile, dirtTile);
            SaveAsset(mesh, "논칸_" + (pi++) + ".asset");
            var go = MeshObject(p.water ? "물논" : "벼논", mesh, p.water ? waterMat : mMud, mDirt, pRoot);
            go.transform.localPosition = new Vector3((p.x0 + p.x1) / 2, p.top / 2, (p.z0 + p.z1) / 2);
        }

        BuildPlants();
        if (setupSkyAndLight) SetupSkyAndLight();
        if (setupPostProcessing) SetupPostFX();
        SaveAll();
        Debug.Log("[논틀밭틀] 환경을 만들었어요. Ctrl+S로 씬을 저장하세요.");
    }

    public void RemoveEnvironment()
    {
        var old = transform.Find(ENV_NAME);
        if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
    }

    // ==================================================================
    //  ③ 자모에 둑 질감 입히기 (윗면 풀, 옆면 흙 / ㄹ·ㅏ는 흙길)
    //  자모를 제자리에 다 옮긴 다음에 눌러요.
    // ==================================================================
    public void ApplyJamoTextures()
    {
        var root = FindJamoRoot();
        if (root == null) { Debug.LogWarning("[논틀밭틀] '글자' 그룹을 못 찾았어요. Inspector의 Jamo Root 칸에 '글자 (자모를 옮겨서 공간 만들기)'를 끌어다 넣어 주세요."); return; }
        if (mGrass == null) { rng = new System.Random(7); MakeMaterials(); }

        int done = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;
            int idx = origFilters.IndexOf(mf);
            Mesh src;
            if (idx < 0) { src = mf.sharedMesh; origFilters.Add(mf); origMeshes.Add(src); origMats.Add(mr.sharedMaterial); }
            else src = origMeshes[idx] != null ? origMeshes[idx] : mf.sharedMesh;
            if (!src.isReadable) { Debug.LogWarning("[논틀밭틀] 읽을 수 없는 메시라 건너뛰어요: " + mf.name); continue; }

            bool isPath = IsPathJamo(root, mf.transform);
            var mesh = ProjectMesh(src, mf.transform, isPath ? dirtTile : grassTile * 0.7f, dirtTile * 0.9f);
            mesh.name = "자모질감_" + mf.name + "_" + done;
            SaveAsset(mesh, "자모질감_" + done + ".asset");
            mf.sharedMesh = mesh;
            mr.sharedMaterials = new[] { isPath ? mPath : mGrass, mDirt };
            MarkDirty(mf); MarkDirty(mr);
            done++;
        }
        SaveAll();
        Debug.Log("[논틀밭틀] 자모 " + done + "개 조각에 질감을 입혔어요. '자모 모양 다시 만들기'를 누르면 다시 이 버튼을 눌러 주세요.");
    }

    // ==================================================================
    //  ② 자모 자동 배치
    //  글자 모양으로 서 있는 자모 조각들을 위치와 색(초성 노랑·중성 파랑·종성 분홍)으로
    //  12개 자모로 묶은 다음, 자모마다 노란 선 자리에 눕히고 크기를 맞춰요.
    //  여러 번 눌러도 항상 '처음 글자 모양'에서 다시 계산해요.
    // ==================================================================
    [SerializeField, HideInInspector] List<Transform> snapT = new List<Transform>();
    [SerializeField, HideInInspector] List<Vector3> snapP = new List<Vector3>();
    [SerializeField, HideInInspector] List<Quaternion> snapR = new List<Quaternion>();
    [SerializeField, HideInInspector] List<Vector3> snapS = new List<Vector3>();
    [SerializeField, HideInInspector] List<Transform> pathPieces = new List<Transform>();

    class Piece { public Transform t; public Bounds b; public int role = -1; public bool hasCol; public float hue; }

    public void AutoPlaceJamo()
    {
        var root = FindJamoRoot();
        if (root == null) { Debug.LogWarning("[논틀밭틀] '글자' 그룹을 못 찾았어요. Inspector의 Jamo Root 칸에 '글자 (자모를 옮겨서 공간 만들기)'를 끌어다 넣어 주세요."); return; }

        // 별말씀 시스템의 자모 '껍데기'(JamoPiece)를 옮겨요.
        // 플레이하면 WordSpace가 알맹이를 글자 모양으로 띄웠다가 이 껍데기 자리로 날려 보내요.
        var shells = new List<Byeolmalsseum.JamoPiece>(root.GetComponentsInChildren<Byeolmalsseum.JamoPiece>(true));
        if (shells.Count == 0) { Debug.LogWarning("[논틀밭틀] '글자' 안에서 JamoPiece를 못 찾았어요."); return; }

        // 1) 예전 버전이 알맹이만 옮겨 놓았을 수 있으니, 알맹이를 껍데기에 딱 붙여요 (플레이 때도 이렇게 붙어요)
        foreach (var jp in shells)
        {
            if (jp.visual == null || jp.visual.parent != jp.transform) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(jp.visual, "자모 자동 배치");
#endif
            jp.visual.localPosition = Vector3.zero; jp.visual.localRotation = Quaternion.identity; jp.visual.localScale = Vector3.one;
            MarkDirty(jp.visual);
        }

        // 2) 글자 모양 정보(points)로 12개 자모가 각각 무엇인지 알아내기
        var items = new List<Piece>(); var byT = new Dictionary<Transform, Byeolmalsseum.JamoPiece>();
        foreach (var jp in shells)
        {
            if (jp.points == null || jp.points.Length == 0) continue;
            Vector2 mn = jp.points[0], mx = jp.points[0];
            foreach (var q in jp.points) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
            items.Add(new Piece { t = jp.transform, b = new Bounds((Vector3)((mn + mx) / 2), (Vector3)(mx - mn)) });
            byT[jp.transform] = jp;
        }
        var groups = GroupPieces(items, out string report);
        if (groups == null) { Debug.LogError("[논틀밭틀] 자모를 12개로 나누지 못했어요.\n" + report); return; }
        Debug.Log("[논틀밭틀] 자모 찾기 결과 (글자 모양 기준)\n" + report);

        // 3) 자모마다 눕히고 · 노란 선 크기로 늘리고 · 자리에 놓기
        pathPieces.Clear();
        for (int j = 0; j < 12; j++)
        {
            var g = groups[j];
            float fw = FINAL[j, 3], fh = FINAL[j, 4], ht = FINAL[j, 6];
            var rot = transform.rotation * Quaternion.Euler(90f, FINAL[j, 2], 0f);
            Bounds gb = g[0].b; foreach (var p in g) gb.Encapsulate(p.b); // 글자 좌표에서 묶음 크기
            foreach (var p in g)
            {
                var t = p.t;
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RecordObject(t, "자모 자동 배치");
#endif
                if (!MeasureBounds(t, t.worldToLocalMatrix, out var lb)) continue; // 껍데기 기준 모양 크기
                float sx, sy;
                if (g.Count == 1) { sx = fw / Mathf.Max(lb.size.x, 1e-4f); sy = fh / Mathf.Max(lb.size.y, 1e-4f); }
                else { sx = fw / Mathf.Max(gb.size.x, 1e-4f); sy = fh / Mathf.Max(gb.size.y, 1e-4f); }
                float sz = ht / Mathf.Max(lb.size.z, 1e-4f);
                float ps = t.parent != null ? Mathf.Max(Mathf.Abs(t.parent.lossyScale.x), 1e-4f) : 1f;
                t.rotation = rot;
                t.localScale = new Vector3(sx, sy, sz) / ps;
                // 묶음 안에서의 상대 위치 (자모 하나 = 껍데기 하나면 0)
                Vector3 off = g.Count == 1 ? Vector3.zero : rot * new Vector3((p.b.center.x - gb.center.x) * sx, (p.b.center.y - gb.center.y) * sy, 0);
                var target = transform.TransformPoint(new Vector3(FINAL[j, 0], 0f, FINAL[j, 1])) + new Vector3(off.x, 0, off.z);
                if (MeasureBounds(t, Matrix4x4.identity, out var wb))
                    t.position += new Vector3(target.x - wb.center.x, transform.position.y + 0.01f - wb.min.y, target.z - wb.center.z);
                if (j == 5 || j == 7 || j == 11) pathPieces.Add(t);
                MarkDirty(t);
            }
        }
        MarkDirty(this);
        Debug.Log("[논틀밭틀] 자모 12개 껍데기를 노란 선 자리에 놓았어요. 플레이하면 이 자리로 날아와요.");
    }

    public void ResetJamoToWord() { RestoreSnapshot(true); }

    void TakeSnapshot(List<Transform> pieces)
    {
        snapT.Clear(); snapP.Clear(); snapR.Clear(); snapS.Clear();
        foreach (var t in pieces) { snapT.Add(t); snapP.Add(t.localPosition); snapR.Add(t.localRotation); snapS.Add(t.localScale); }
        MarkDirty(this);
    }

    void RestoreSnapshot(bool log)
    {
        for (int i = 0; i < snapT.Count; i++)
        {
            var t = snapT[i]; if (t == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(t, "자모 글자 모양으로");
#endif
            t.localPosition = snapP[i]; t.localRotation = snapR[i]; t.localScale = snapS[i]; MarkDirty(t);
        }
        if (log) Debug.Log(snapT.Count > 0 ? "[논틀밭틀] 자모를 처음 글자 모양으로 되돌렸어요." : "[논틀밭틀] 기억해 둔 글자 모양이 없어요. ②를 먼저 눌러 주세요.");
    }

    static void ApplyMatrix(Transform t, Matrix4x4 w)
    {
        Vector3 c0 = w.GetColumn(0), c1 = w.GetColumn(1), c2 = w.GetColumn(2);
        var sc = new Vector3(c0.magnitude, c1.magnitude, c2.magnitude);
        t.position = w.GetColumn(3);
        t.rotation = Quaternion.LookRotation(c2 / Mathf.Max(sc.z, 1e-6f), c1 / Mathf.Max(sc.y, 1e-6f));
        var ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(sc.x / Mathf.Max(Mathf.Abs(ps.x), 1e-6f), sc.y / Mathf.Max(Mathf.Abs(ps.y), 1e-6f), sc.z / Mathf.Max(Mathf.Abs(ps.z), 1e-6f));
    }

    // 모양이 있는 가장 바깥 오브젝트들 (안에 들어 있는 조각은 같이 움직이니까 빼요)
    static List<Transform> GetPieces(Transform root)
    {
        var all = root.GetComponentsInChildren<MeshFilter>(true);
        var set = new HashSet<Transform>(); foreach (var mf in all) if (mf.sharedMesh != null) set.Add(mf.transform);
        var meshes = new List<Transform>();
        foreach (var mf in all)
        {
            var t = mf.transform; if (!set.Contains(t) || meshes.Contains(t)) continue;
            bool nested = false; for (var p = t.parent; p != null && p != root; p = p.parent) if (set.Contains(p)) { nested = true; break; }
            if (!nested) meshes.Add(t);
        }
        // 모양을 감싸는 '자모 오브젝트'가 따로 있으면 그걸 옮겨요 (기존 시스템이 그 오브젝트를 기억하니까)
        var res = new List<Transform>();
        foreach (var t in meshes)
        {
            var top = t;
            for (var p = t.parent; p != null && p != root; p = p.parent)
            {
                int count = 0; foreach (var m in meshes) if (m.IsChildOf(p)) count++;
                if (count == 1) top = p; else break;
            }
            if (!res.Contains(top)) res.Add(top);
        }
        return res;
    }

    // 조각들을 [글자 4개] → [초성·중성·종성] 으로 나눠 자모 12묶음 만들기
    List<List<Piece>> GroupIntoJamo(Transform root, List<Transform> ts, out string report)
    {
        var ps = new List<Piece>();
        foreach (var t in ts)
        {
            if (!MeasureBounds(t, root.worldToLocalMatrix, out var b)) continue;
            var p = new Piece { t = t, b = b };
            var mr = t.GetComponentInChildren<MeshRenderer>(true);
            var m = mr != null ? mr.sharedMaterial : null;
            if (m != null && !m.name.StartsWith("둑_") && !m.name.StartsWith("꼬불길_"))
            {
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                Color.RGBToHSV(c, out float h, out float sat, out float val);
                if (sat > 0.15f && val > 0.15f) { p.hasCol = true; p.hue = h * 360f; }
            }
            ps.Add(p);
        }
        return GroupPieces(ps, out report);
    }

    List<List<Piece>> GroupPieces(List<Piece> ps, out string report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("모양 조각 " + ps.Count + "개를 찾았어요.");

        // 1) 가로(x) 방향으로 글자 4개 나누기
        var syl = Cluster(ps, p => p.b.min.x, p => p.b.max.x, 4);
        sb.AppendLine("글자 묶음: " + syl.Count + "개 (4개여야 해요)");
        if (syl.Count != 4) { report = sb.ToString(); return null; }

        var result = new List<List<Piece>>(); for (int i = 0; i < 12; i++) result.Add(new List<Piece>());
        string[] sylName = { "논", "틀", "밭", "틀" };
        for (int s = 0; s < 4; s++)
        {
            var g = syl[s];
            // 2) 색으로 초성(노랑)·중성(파랑)·종성(분홍) 나누기
            bool colorOk = g.TrueForAll(p => p.hasCol);
            if (colorOk)
            {
                foreach (var p in g) p.role = HueRole(p.hue);
                colorOk = g.Exists(p => p.role == 0) && g.Exists(p => p.role == 1) && g.Exists(p => p.role == 2);
            }
            // 색으로 안 되면 자리로 나누기
            if (!colorOk)
            {
                var rows = Cluster(g, p => p.b.min.y, p => p.b.max.y, s == 2 ? 2 : 3);
                rows.Reverse(); // 위에서부터
                if (s != 2)
                {
                    if (rows.Count != 3) { sb.AppendLine(sylName[s] + ": 위·가운데·아래 3줄로 못 나눴어요 (" + rows.Count + "줄)"); report = sb.ToString(); return null; }
                    for (int r = 0; r < 3; r++) foreach (var p in rows[r]) p.role = r;
                }
                else
                {
                    if (rows.Count != 2) { sb.AppendLine("밭: 위·아래 2줄로 못 나눴어요 (" + rows.Count + "줄)"); report = sb.ToString(); return null; }
                    foreach (var p in rows[1]) p.role = 2;
                    var cols = Cluster(rows[0], p => p.b.min.x, p => p.b.max.x, 2);
                    if (cols.Count != 2) { sb.AppendLine("밭: ㅂ과 ㅏ를 못 나눴어요"); report = sb.ToString(); return null; }
                    foreach (var p in cols[0]) p.role = 0; foreach (var p in cols[1]) p.role = 1;
                }
            }
            for (int r = 0; r < 3; r++)
            {
                var list = g.FindAll(p => p.role == r);
                result[s * 3 + r].AddRange(list);
                sb.AppendLine((s * 3 + r + 1) + "번 " + JAMO[s * 3 + r] + " (" + sylName[s] + "): 조각 " + list.Count + "개" + (colorOk ? " · 색으로 찾음" : " · 자리로 찾음"));
            }
        }
        report = sb.ToString();
        if (result.Exists(l => l.Count == 0)) return null;
        return result;
    }

    static int HueRole(float h)
    {
        if (h >= 20f && h < 90f) return 0;    // 노랑·주황 = 초성
        if (h >= 150f && h < 270f) return 1;  // 파랑 = 중성
        return 2;                              // 분홍·빨강 = 종성
    }

    // 한 방향으로 겹치는 것끼리 묶고, 원하는 개수가 될 때까지 가장 가까운 묶음끼리 합쳐요
    static List<List<Piece>> Cluster(List<Piece> items, System.Func<Piece, float> lo, System.Func<Piece, float> hi, int want)
    {
        var sorted = new List<Piece>(items); sorted.Sort((a, b) => lo(a).CompareTo(lo(b)));
        var groups = new List<List<Piece>>(); var gLo = new List<float>(); var gHi = new List<float>();
        foreach (var p in sorted)
        {
            if (groups.Count > 0 && lo(p) <= gHi[gHi.Count - 1] + 1e-4f) { groups[groups.Count - 1].Add(p); gHi[gHi.Count - 1] = Mathf.Max(gHi[gHi.Count - 1], hi(p)); }
            else { groups.Add(new List<Piece> { p }); gLo.Add(lo(p)); gHi.Add(hi(p)); }
        }
        while (groups.Count > want)
        {
            int best = 0; float bestGap = float.MaxValue;
            for (int i = 0; i < groups.Count - 1; i++) { float gap = gLo[i + 1] - gHi[i]; if (gap < bestGap) { bestGap = gap; best = i; } }
            groups[best].AddRange(groups[best + 1]); gHi[best] = Mathf.Max(gHi[best], gHi[best + 1]);
            groups.RemoveAt(best + 1); gLo.RemoveAt(best + 1); gHi.RemoveAt(best + 1);
        }
        return groups;
    }

    // 자모 안의 모든 메시를 감싸는 상자 (space = 어느 좌표계로 잴지)
    static bool MeasureBounds(Transform t, Matrix4x4 space, out Bounds b)
    {
        b = new Bounds(); bool has = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds; var m = space * mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return has;
    }

    public void RestoreJamoTextures()
    {
        for (int i = 0; i < origFilters.Count; i++)
        {
            var mf = origFilters[i]; if (mf == null) continue;
            if (origMeshes[i] != null) mf.sharedMesh = origMeshes[i];
            var mr = mf.GetComponent<MeshRenderer>(); if (mr != null && origMats[i] != null) mr.sharedMaterials = new[] { origMats[i] };
            MarkDirty(mf); MarkDirty(mr);
        }
        origFilters.Clear(); origMeshes.Clear(); origMats.Clear();
        Debug.Log("[논틀밭틀] 자모를 원래 모습으로 되돌렸어요.");
    }

    Transform FindJamoRoot()
    {
        if (jamoRoot != null) return jamoRoot;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.gameObject.scene == gameObject.scene && t.name.StartsWith("글자")) { jamoRoot = t; return t; }
        return null;
    }

    bool IsPathJamo(Transform root, Transform t)
    {
        for (var c = t; c != null && c != root; c = c.parent) if (pathPieces.Contains(c)) return true;
        return pathPieces.Count == 0 && (t.name.Contains("ㄹ") || t.name.Contains("ㅏ"));
    }

    // 지금 놓인 자세를 기준으로: 위를 보는 면 = 0번(풀/흙길), 나머지 = 1번(흙). UV는 미터 단위.
    static Mesh ProjectMesh(Mesh src, Transform tr, float tileTop, float tileSide)
    {
        var v = src.vertices; var tris = src.triangles; var l2w = tr.localToWorldMatrix;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var top = new List<int>(); var side = new List<int>();
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 wa = l2w.MultiplyPoint3x4(a), wb = l2w.MultiplyPoint3x4(b), wc = l2w.MultiplyPoint3x4(c);
            Vector3 wn = Vector3.Cross(wb - wa, wc - wa); if (wn.sqrMagnitude < 1e-12f) continue; wn.Normalize();
            bool isTop = wn.y > 0.6f;
            Vector3 an = new Vector3(Mathf.Abs(wn.x), Mathf.Abs(wn.y), Mathf.Abs(wn.z));
            float tile = isTop ? tileTop : tileSide;
            Vector2 P(Vector3 w)
            {
                if (an.y >= an.x && an.y >= an.z) return new Vector2(w.x, w.z) / tile;
                if (an.x >= an.z) return new Vector2(w.z, w.y) / tile;
                return new Vector2(w.x, w.y) / tile;
            }
            Vector3 ln = Vector3.Cross(b - a, c - a).normalized;
            int k = nv.Count; nv.Add(a); nv.Add(b); nv.Add(c); nn.Add(ln); nn.Add(ln); nn.Add(ln);
            uv.Add(P(wa)); uv.Add(P(wb)); uv.Add(P(wc));
            var list = isTop ? top : side; list.Add(k); list.Add(k + 1); list.Add(k + 2);
        }
        var m = new Mesh { indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, uv);
        m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1);
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    // ==================================================================
    //  배치
    // ==================================================================
    void DefineLayout()
    {
        plates.Clear(); strokeRects.Clear(); route.Clear();
        AddPlate(-15.6f, -4.4f, -16.6f, 0.6f, 0.3f, false);
        AddPlate(-15.1f, -6.9f, 3.4f, 9f, 0.3f, true);
        AddPlate(-15.1f, -6.9f, 9.4f, 15f, 0.3f, false);
        AddPlate(6.2f, 12.5f, 14.6f, 20f, 0.25f, true);
        AddPlate(-3.6f, 3.6f, 7f, 20f, 0.15f, false);
        float[] zc = { -12f, -1f, 10f };
        for (int k = 0; k < 3; k++)
        {
            AddPlate(5.2f, 9f, zc[k] - 4.8f, zc[k] + 4.8f, 0.2f, k != 1);
            AddPlate(9f, 12.8f, zc[k] - 4.8f, zc[k] + 4.8f, 0.5f, k == 1);
        }
        for (int j = 0; j < 12; j++)
        {
            float X = FINAL[j, 0], Z = FINAL[j, 1], yaw = FINAL[j, 2] * Mathf.Deg2Rad, fw = FINAL[j, 3], fh = FINAL[j, 4], T = FINAL[j, 5];
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            foreach (var st in Jamo2D(JAMO[j], -fw / 2, fw / 2, -fh / 2, fh / 2, T))
                strokeRects.Add(new Rect2 { cx = X + c * st.x + s * st.y, cz = Z - s * st.x + c * st.y, hw = st.z / 2, hd = st.w / 2, yaw = yaw });
        }
        route.AddRange(LRoute(0f, -11.5f, 5f, 11f, 1f));
        var r2 = LRoute(0f, 0.4f, 5f, 11f, 1f); r2.RemoveAt(0); route.AddRange(r2);
    }

    void AddPlate(float x0, float x1, float z0, float z1, float top, bool water) { plates.Add(new Plate { x0 = x0, x1 = x1, z0 = z0, z1 = z1, top = top, water = water }); }

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

    static List<Vector2> LRoute(float cx, float cz, float fw, float fh, float T)
    {
        float hx = fw / 2 - T / 2, hz = fh / 2 - T / 2;
        return new List<Vector2> { new Vector2(cx + hx, cz - hz), new Vector2(cx - hx, cz - hz), new Vector2(cx - hx, cz), new Vector2(cx + hx, cz), new Vector2(cx + hx, cz + hz), new Vector2(cx - hx, cz + hz) };
    }

    float RouteDist(float x, float z)
    {
        var p = new Vector2(x, z); float d = 1e9f;
        for (int i = 0; i < route.Count - 1; i++)
        {
            Vector2 a = route[i], ab = route[i + 1] - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
        }
        return d;
    }
    static bool InRect(Rect2 r, float x, float z, float pad)
    {
        float dx = x - r.cx, dz = z - r.cz, c = Mathf.Cos(r.yaw), s = Mathf.Sin(r.yaw);
        return Mathf.Abs(c * dx - s * dz) <= r.hw + pad && Mathf.Abs(s * dx + c * dz) <= r.hd + pad;
    }
    bool InStroke(float x, float z, float pad) { foreach (var r in strokeRects) if (InRect(r, x, z, pad)) return true; return false; }
    bool InPlate(float x, float z, float pad) { foreach (var p in plates) if (x >= p.x0 - pad && x <= p.x1 + pad && z >= p.z0 - pad && z <= p.z1 + pad) return true; return false; }

    // ==================================================================
    //  바닥 (기존 '바닥' 판이나 Terrain에 풀 질감)
    // ==================================================================
    void ApplyFloor()
    {
        bool did = false;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.name.StartsWith("바닥")) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var size = Vector3.Scale(mf.sharedMesh.bounds.size, mr.transform.lossyScale);
            mGround.SetTextureScale("_BaseMap", new Vector2(Mathf.Max(size.x, 1) / grassTile, Mathf.Max(size.z, 1) / grassTile));
            mr.sharedMaterial = mGround; MarkDirty(mr); MarkDirty(mGround); did = true;
        }
        foreach (var terrain in FindTerrains())
        {
            var td = terrain.terrainData; if (td == null) continue;
            if (td.terrainLayers != null && td.terrainLayers.Length > 0) { Debug.Log("[논틀밭틀] Terrain에 이미 질감 레이어가 있어서 그대로 뒀어요."); continue; }
            var layer = new TerrainLayer { diffuseTexture = (Texture2D)mGrass.GetTexture("_BaseMap"), normalMapTexture = (Texture2D)mGrass.GetTexture("_BumpMap"), tileSize = new Vector2(grassTile, grassTile), normalScale = 1f, smoothness = 0.05f };
            SaveAsset(layer, "땅_풀.terrainlayer");
            td.terrainLayers = new[] { layer }; MarkDirty(td); did = true;
        }
        if (!did)
        {
            var go = MeshObject("땅 (풀)", PlaneMesh(-30, 30, -30, 30, -0.005f, grassTile), mGround, null, env);
            SaveAsset(go.GetComponent<MeshFilter>().sharedMesh, "땅.asset");
        }
    }

    List<Terrain> FindTerrains()
    {
        var list = new List<Terrain>();
        foreach (var t in Resources.FindObjectsOfTypeAll<Terrain>()) if (t.gameObject.scene == gameObject.scene) list.Add(t);
        return list;
    }

    // ==================================================================
    //  벼·모·풀
    // ==================================================================
    void BuildPlants()
    {
        var pr = Child(env, "벼·모·풀");
        var riceMat = BladeMat("벼", new Color(0.36f, 0.42f, 0.12f), new Color(0.88f, 0.68f, 0.26f));
        var seedMat = BladeMat("모", new Color(0.2f, 0.36f, 0.1f), new Color(0.45f, 0.72f, 0.26f));
        var grassMat = BladeMat("풀", new Color(0.16f, 0.26f, 0.07f), new Color(0.5f, 0.64f, 0.22f));
        var rice = new BladeBuilder(); var seed = new BladeBuilder(); var grass = new BladeBuilder();
        foreach (var p in plates)
        {
            if (!p.water)
            {
                for (float x = p.x0 + 0.35f; x < p.x1 - 0.3f; x += riceSpacing)
                    for (float z = p.z0 + 0.35f; z < p.z1 - 0.3f; z += riceSpacing)
                    {
                        float X = x + R(-0.07f, 0.07f), Z = z + R(-0.07f, 0.07f);
                        if (InStroke(X, Z, 0.35f)) continue;
                        rice.Clump(rng, new Vector3(X, p.top, Z), R(0, 6.28f), R(0.5f, 0.66f), 9, 0.16f, 0.22f, 0.018f);
                    }
            }
            else
            {
                for (float x = p.x0 + 0.5f; x < p.x1 - 0.4f; x += 0.55f)
                    for (float z = p.z0 + 0.5f; z < p.z1 - 0.4f; z += 0.55f)
                    {
                        if (rng.NextDouble() < 0.35) continue;
                        float X = x + R(-0.05f, 0.05f), Z = z + R(-0.05f, 0.05f);
                        if (InStroke(X, Z, 0.25f)) continue;
                        seed.Clump(rng, new Vector3(X, p.top, Z), R(0, 6.28f), R(0.16f, 0.24f), 6, 0.06f, 0.08f, 0.014f);
                    }
            }
        }
        int placed = 0;
        for (int i = 0; i < grassTufts * 8 && placed < grassTufts; i++)
        {
            float X = R(-grassRange, grassRange), Z = R(-grassRange, grassRange);
            if (InPlate(X, Z, 0.25f) || InStroke(X, Z, 0.2f)) continue;
            float d = RouteDist(X, Z);
            float keep = d < 0.45f ? 0 : (d < 2.6f ? 0.9f : 0.22f);
            if (rng.NextDouble() > keep) continue;
            grass.Clump(rng, new Vector3(X, 0, Z), R(0, 6.28f), R(0.22f, 0.5f), 12, 0.22f, 0.18f, 0.03f);
            placed++;
        }
        Emit(rice, "벼", pr, riceMat, true); Emit(seed, "모", pr, seedMat, false); Emit(grass, "풀", pr, grassMat, false);
    }

    void Emit(BladeBuilder b, string name, Transform parent, Material m, bool shadows)
    {
        var mesh = b.Build(name); if (mesh == null) return;
        SaveAsset(mesh, name + ".asset");
        var go = MeshObject(name, mesh, m, null, parent);
        go.GetComponent<MeshRenderer>().shadowCastingMode = shadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    class BladeBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> idx = new List<int>();
        public void Clump(System.Random r, Vector3 pos, float rot, float height, int blades, float spread, float lean, float width)
        {
            float rv() => (float)r.NextDouble();
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot), shade = rv();
            for (int b = 0; b < blades; b++)
            {
                float bx = (rv() - 0.5f) * spread, bz = (rv() - 0.5f) * spread, a = rv() * 6.283f, la = rv() * 6.283f, ln = lean * (0.5f + rv()), hh = 0.75f + rv() * 0.35f;
                float px = Mathf.Cos(a), pz = Mathf.Sin(a), lx = Mathf.Cos(la) * ln, lz = Mathf.Sin(la) * ln; int bi = v.Count;
                for (int i = 0; i <= 3; i++)
                {
                    float t = i / 3f, yy = t * hh, ox = bx + lx * t * t, oz = bz + lz * t * t, w = width * (1 - t * 0.92f);
                    AddV(new Vector3(ox - px * w, yy, oz - pz * w), t); AddV(new Vector3(ox + px * w, yy, oz + pz * w), t);
                }
                for (int i = 0; i < 3; i++) { int k = bi + i * 2; idx.Add(k); idx.Add(k + 1); idx.Add(k + 3); idx.Add(k); idx.Add(k + 3); idx.Add(k + 2); }
            }
            void AddV(Vector3 l, float t)
            {
                l *= height; v.Add(pos + new Vector3(cr * l.x + sr * l.z, l.y, -sr * l.x + cr * l.z));
                n.Add(new Vector3(sr * 0.35f, 1f, cr * 0.35f).normalized); uv.Add(new Vector2(0.1f + shade * 0.8f, t));
            }
        }
        public Mesh Build(string name)
        {
            if (v.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }

    Material BladeMat(string name, Color bottom, Color tip)
    {
        var tex = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name + "_색" };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 8; x++)
            {
                float t = y / 63f, shade = 0.8f + 0.4f * (x / 7f), ao = Mathf.Lerp(0.5f, 1f, t);
                var c = Color.Lerp(bottom, tip, Mathf.SmoothStep(0.15f, 1f, t)) * shade * ao; c.a = 1; tex.SetPixel(x, y, c);
            }
        tex.Apply();
        SaveAsset(tex, name + "_색.asset");
        var m = Lit(name, Color.white, tex, null, 0.15f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        m.doubleSidedGI = true;
        SaveAsset(m, name + ".mat");
        return m;
    }

    // ==================================================================
    //  하늘·빛·안개·반사·보정
    // ==================================================================
    void SetupSkyAndLight()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                if (l.gameObject.scene == gameObject.scene && l.type == LightType.Directional) { sun = l; break; }
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SUN);
            sun.color = new Color(1f, 0.8f, 0.6f); sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun; MarkDirty(sun); MarkDirty(sun.transform);
        }
        var skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            var sky = new Material(skyShader) { name = "하늘_노을" };
            sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.35f); sky.SetColor("_SkyTint", new Color(0.62f, 0.56f, 0.6f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.38f, 0.33f)); sky.SetFloat("_Exposure", 1.15f);
            SaveAsset(sky, "하늘_노을.mat");
            RenderSettings.skybox = sky;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.5f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.5f, 0.4f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.2f, 0.15f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.0075f;
        RenderSettings.fogColor = new Color(0.86f, 0.7f, 0.58f);

        var pg = Child(env, "반사 (물에 하늘 비치기)"); pg.localPosition = new Vector3(0, 2, 0);
        var probe = pg.gameObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(70, 20, 70); probe.resolution = 256;
        probe.RenderProbe();
    }

    void SetupPostFX()
    {
        var go = Child(env, "보정 (Global Volume)");
        var vol = go.gameObject.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>(); prof.name = "보정_논틀밭틀";
        SaveAsset(prof, "보정_논틀밭틀.asset");
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.35f); bl.threshold.Override(1.0f); bl.scatter.Override(0.6f);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.25f); ca.saturation.Override(8f); ca.contrast.Override(8f);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(12f);
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.22f); vg.smoothness.Override(0.45f);
#if UNITY_EDITOR
        if (!Application.isPlaying) foreach (var c in prof.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, prof); }
#endif
        vol.sharedProfile = prof;
        // 씬의 모든 카메라(체험 카메라·작업용 카메라)에 보정 켜기
        foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (cam.gameObject.scene != gameObject.scene) continue;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; MarkDirty(data); }
        }
    }

    // ==================================================================
    //  재질 (그림 파일 없이 코드로 질감을 그려요)
    // ==================================================================
    void MakeMaterials()
    {
        if (rng == null) rng = new System.Random(7);
#if UNITY_EDITOR
        // 이미 만든 재질이 있으면 그대로 써요 (자모에 입힌 재질이 깨지지 않게)
        if (!Application.isPlaying)
        {
            Material L(string f) => AssetDatabase.LoadAssetAtPath<Material>(FOLDER + "/" + f);
            var g0 = L("땅_풀.mat"); var g1 = L("둑_윗면_풀.mat"); var d0 = L("둑_옆면_흙.mat"); var p0 = L("꼬불길_흙.mat"); var m0 = L("논바닥_진흙.mat"); var w0 = L("물논_물.mat");
            if (g0 && g1 && d0 && p0 && m0 && w0) { mGround = g0; mGrass = g1; mDirt = d0; mPath = p0; mMud = m0; waterMat = w0; return; }
        }
#endif
        var grassTex = MakeGrassTex(); var dirtTex = MakeDirtTex(); var mudTex = MakeMudTex();
        SaveAsset(grassTex, "풀_질감.asset"); SaveAsset(dirtTex, "흙_질감.asset"); SaveAsset(mudTex, "진흙_질감.asset");
        var grassN = NormalFromHeight(grassTex, 2.5f); var dirtN = NormalFromHeight(dirtTex, 3.5f); var mudN = NormalFromHeight(mudTex, 2f); var waterN = MakeWaterNormal();
        SaveAsset(grassN, "풀_울퉁불퉁.asset"); SaveAsset(dirtN, "흙_울퉁불퉁.asset"); SaveAsset(mudN, "진흙_울퉁불퉁.asset"); SaveAsset(waterN, "물결.asset");

        mGround = Lit("땅_풀", Color.white, grassTex, grassN, 0.05f); SaveAsset(mGround, "땅_풀.mat");
        mGrass = Lit("둑_윗면_풀", Color.white, grassTex, grassN, 0.05f); SaveAsset(mGrass, "둑_윗면_풀.mat");
        mDirt = Lit("둑_옆면_흙", new Color(0.92f, 0.86f, 0.8f), dirtTex, dirtN, 0.08f); SaveAsset(mDirt, "둑_옆면_흙.mat");
        mPath = Lit("꼬불길_흙", new Color(1.1f, 1.03f, 0.95f), dirtTex, dirtN, 0.1f); SaveAsset(mPath, "꼬불길_흙.mat");
        mMud = Lit("논바닥_진흙", Color.white, mudTex, mudN, 0.25f); SaveAsset(mMud, "논바닥_진흙.mat");
        waterMat = Lit("물논_물", new Color(0.05f, 0.075f, 0.065f), null, waterN, 0.96f);
        waterMat.SetFloat("_BumpScale", 0.6f);
        if (waterMat.HasProperty("_BaseMap")) waterMat.SetTextureScale("_BaseMap", new Vector2(1f / 3f, 1f / 3f));
        SaveAsset(waterMat, "물논_물.mat");
    }

    static Material Lit(string name, Color c, Texture2D tex, Texture2D nrm, float smooth)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name, enableInstancing = true };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
        if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    // ==================================================================
    //  도형
    // ==================================================================
    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    static GameObject MeshObject(string name, Mesh mesh, Material m0, Material m1, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { m0, m1 ?? m0 } : new[] { m0 };
        mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
        return go;
    }

    static Mesh BoxMesh(float w, float h, float d, float tileTop, float tileSide)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var top = new List<int>(); var side = new List<int>();
        void Face(Vector3 nn, Vector3 u, Vector3 vv, float hn, float hu, float hv, bool isTop)
        {
            int b = v.Count; float[] A = { -1, 1, 1, -1 }, C = { -1, -1, 1, 1 }; float tile = isTop ? tileTop : tileSide;
            for (int i = 0; i < 4; i++) { v.Add(nn * hn + u * (A[i] * hu) + vv * (C[i] * hv)); n.Add(nn); uv.Add(new Vector2((A[i] + 1) * hu, (C[i] + 1) * hv) / tile); }
            var list = isTop ? top : side;
            bool ok = Vector3.Dot(Vector3.Cross(v[b + 1] - v[b], v[b + 2] - v[b]), nn) > 0;
            if (ok) { list.Add(b); list.Add(b + 1); list.Add(b + 2); list.Add(b); list.Add(b + 2); list.Add(b + 3); }
            else { list.Add(b); list.Add(b + 2); list.Add(b + 1); list.Add(b); list.Add(b + 3); list.Add(b + 2); }
        }
        Face(Vector3.right, Vector3.forward, Vector3.up, w / 2, d / 2, h / 2, false);
        Face(Vector3.left, Vector3.forward, Vector3.up, w / 2, d / 2, h / 2, false);
        Face(Vector3.up, Vector3.right, Vector3.forward, h / 2, w / 2, d / 2, true);
        Face(Vector3.down, Vector3.right, Vector3.forward, h / 2, w / 2, d / 2, false);
        Face(Vector3.forward, Vector3.right, Vector3.up, d / 2, w / 2, h / 2, false);
        Face(Vector3.back, Vector3.right, Vector3.up, d / 2, w / 2, h / 2, false);
        var mesh = new Mesh { name = "상자" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2; mesh.SetTriangles(top, 0); mesh.SetTriangles(side, 1); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh PlaneMesh(float x0, float x1, float z0, float z1, float y, float tile)
    {
        var mesh = new Mesh { name = "땅" };
        mesh.SetVertices(new List<Vector3> { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) });
        mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        mesh.SetUVs(0, new List<Vector2> { new Vector2(x0, z0) / tile, new Vector2(x0, z1) / tile, new Vector2(x1, z1) / tile, new Vector2(x1, z0) / tile });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    // ==================================================================
    //  질감 그리기 (이음새 없이 반복되는 그림)
    // ==================================================================
    const int TS = 512;
    static float Lattice(int x, int y, int seed)
    {
        unchecked { int h = x * 374761393 + y * 668265263 + seed * 1442695041; h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16; return (h & 0xffffff) / 16777215f; }
    }
    static float TileNoise(float u, float v, int freq, int seed)
    {
        float x = u * freq, y = v * freq; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        int x0 = ((xi % freq) + freq) % freq, x1 = (x0 + 1) % freq, y0 = ((yi % freq) + freq) % freq, y1 = (y0 + 1) % freq;
        float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Lattice(x0, y0, seed), Lattice(x1, y0, seed), sx), Mathf.Lerp(Lattice(x0, y1, seed), Lattice(x1, y1, seed), sx), sy);
    }
    static float Fbm(float u, float v, int seed, int baseFreq = 4)
    {
        float s = 0, a = 0.5f, norm = 0; int f = baseFreq;
        for (int i = 0; i < 5; i++) { s += TileNoise(u, v, f, seed + i * 31) * a; norm += a; a *= 0.5f; f *= 2; }
        return s / norm;
    }
    static Texture2D NewTex(string name, bool linear)
    {
        return new Texture2D(TS, TS, TextureFormat.RGBA32, true, linear) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
    }
    static void Blend(Color[] px, int x, int y, Color c, float a)
    {
        x = ((x % TS) + TS) % TS; y = ((y % TS) + TS) % TS; int i = y * TS + x; px[i] = Color.Lerp(px[i], c, a);
    }

    Texture2D MakeGrassTex()
    {
        var px = new Color[TS * TS];
        Color dark = new Color(0.27f, 0.37f, 0.13f), light = new Color(0.42f, 0.53f, 0.2f);
        for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++) px[y * TS + x] = Color.Lerp(dark, light, Fbm(x / (float)TS, y / (float)TS, 11));
        Color[] pal = { new Color(.44f, .56f, .2f), new Color(.31f, .43f, .13f), new Color(.53f, .63f, .26f), new Color(.36f, .48f, .16f), new Color(.6f, .66f, .31f), new Color(.25f, .35f, .11f), new Color(.64f, .64f, .34f) };
        for (int i = 0; i < 16000; i++)
        {
            float x = R(0, TS), y = R(0, TS), a = R(0, 6.283f), len = R(3, 10); var c = pal[rng.Next(pal.Length)]; float al = R(0.5f, 0.9f);
            for (float s = 0; s < len; s += 0.7f) Blend(px, (int)(x + Mathf.Cos(a) * s), (int)(y + Mathf.Sin(a) * s), c, al);
        }
        for (int i = 0; i < px.Length; i++) { float g = R(-0.035f, 0.035f); px[i] += new Color(g, g, g, 0); px[i].a = 1; }
        var t = NewTex("풀_질감", false); t.SetPixels(px); t.Apply(true); return t;
    }

    Texture2D MakeDirtTex()
    {
        var px = new Color[TS * TS];
        Color dark = new Color(0.38f, 0.27f, 0.17f), light = new Color(0.56f, 0.42f, 0.28f);
        for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++) px[y * TS + x] = Color.Lerp(dark, light, Fbm(x / (float)TS, y / (float)TS, 23));
        for (int i = 0; i < 2800; i++)
        {
            int cx = rng.Next(TS), cy = rng.Next(TS); float r = R(0.8f, 3.2f); bool lt = rng.NextDouble() < 0.5;
            var c = lt ? new Color(0.66f, 0.54f, 0.4f) : new Color(0.29f, 0.21f, 0.13f);
            for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) if (dx * dx + dy * dy <= r * r) Blend(px, cx + dx, cy + dy, c, 0.55f);
        }
        for (int i = 0; i < px.Length; i++) { float g = R(-0.05f, 0.05f); px[i] += new Color(g, g, g, 0); px[i].a = 1; }
        var t = NewTex("흙_질감", false); t.SetPixels(px); t.Apply(true); return t;
    }

    Texture2D MakeMudTex()
    {
        var px = new Color[TS * TS];
        Color dark = new Color(0.22f, 0.17f, 0.11f), light = new Color(0.36f, 0.29f, 0.19f);
        for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++)
            {
                float u = x / (float)TS, v = y / (float)TS; var c = Color.Lerp(dark, light, Fbm(u, v, 37));
                c = Color.Lerp(c, new Color(0.27f, 0.27f, 0.17f), Fbm(u, v, 41, 2) * 0.35f);
                float g = R(-0.03f, 0.03f); px[y * TS + x] = new Color(c.r + g, c.g + g, c.b + g, 1);
            }
        var t = NewTex("진흙_질감", false); t.SetPixels(px); t.Apply(true); return t;
    }

    static Texture2D NormalFromHeight(Texture2D src, float strength)
    {
        var s = src.GetPixels(); var o = new Color[TS * TS];
        float H(int x, int y) { x = (x + TS) % TS; y = (y + TS) % TS; var c = s[y * TS + x]; return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; }
        for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++)
            {
                float dx = (H(x + 1, y) - H(x - 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength;
                var n = new Vector3(-dx, -dy, 1).normalized; o[y * TS + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
        var t = NewTex(src.name + "_울퉁불퉁", true); t.SetPixels(o); t.Apply(true); return t;
    }

    Texture2D MakeWaterNormal()
    {
        var h = new Texture2D(TS, TS, TextureFormat.RGBA32, false, true);
        var px = new Color[TS * TS];
        for (int y = 0; y < TS; y++) for (int x = 0; x < TS; x++)
            {
                float u = x / (float)TS, v = y / (float)TS; float f = Fbm(u, v, 53, 8) * 0.7f + Fbm(u, v, 59, 16) * 0.3f; px[y * TS + x] = new Color(f, f, f, 1);
            }
        h.SetPixels(px); h.Apply(false);
        var n = NormalFromHeight(h, 6f); n.name = "물결";
        if (Application.isPlaying) Destroy(h); else DestroyImmediate(h);
        return n;
    }

    // ==================================================================
    //  저장 도우미 (편집 모드에서 Assets/논틀밭틀_생성 에 저장)
    // ==================================================================
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        string path = FOLDER + "/" + file;
        var existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(o, path);
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

    // ==================================================================
    //  Scene 화면 안내선: 노란 선 = 자모가 놓일 자리, 하늘색 = 논 칸
    // ==================================================================
    void OnDrawGizmos()
    {
        if (!showGuides) return;
        if (strokeRects.Count == 0) DefineLayout();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (var r in strokeRects) DrawRect(r.cx, r.cz, r.hw, r.hd, r.yaw, 0.05f);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        foreach (var p in plates) DrawRect((p.x0 + p.x1) / 2, (p.z0 + p.z1) / 2, (p.x1 - p.x0) / 2, (p.z1 - p.z0) / 2, 0, p.top);
    }
    static void DrawRect(float cx, float cz, float hw, float hd, float yaw, float y)
    {
        float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        Vector3 P(float lx, float lz) => new Vector3(cx + c * lx + s * lz, y, cz - s * lx + c * lz);
        Vector3 a = P(-hw, -hd), b = P(hw, -hd), d = P(hw, hd), e = P(-hw, hd);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, d); Gizmos.DrawLine(d, e); Gizmos.DrawLine(e, a);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NonteulFieldMaker))]
public class NonteulFieldMakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulFieldMaker)target;
        EditorGUILayout.HelpBox("②는 자모가 '논틀밭틀' 글자 모양으로 서 있는 상태에서 처음 눌러야 해요.\n자모 찾기 결과는 Console 창에 나와요.\n기존 시스템은 그대로 두고 환경과 질감만 얹어요.\n시작 전에 [별말씀] 논틀밭틀의 '지금 배치 저장 (백업)'을 눌러 두세요.\nScene 화면의 노란 선이 자모 자리, 하늘색 선이 논 칸이에요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("① 환경 만들기 (땅 질감·논·물·벼·풀·하늘·빛·보정)", GUILayout.Height(34))) s.BuildEnvironment();
        if (GUILayout.Button("② 자모 자동 배치 (노란 선에 크기·회전·높이 맞추기)", GUILayout.Height(34))) s.AutoPlaceJamo();
        if (GUILayout.Button("③ 자모에 둑 질감 입히기 (윗면 풀 · 옆면 흙 · ㄹㅏ 흙길)", GUILayout.Height(30))) s.ApplyJamoTextures();
        GUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("자모 질감 되돌리기")) s.RestoreJamoTextures();
        if (GUILayout.Button("자모를 글자 모양으로")) s.ResetJamoToWord();
        if (GUILayout.Button("만든 환경 지우기")) { s.RemoveEnvironment(); EditorSceneManager.MarkSceneDirty(s.gameObject.scene); }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8);
        DrawDefaultInspector();
    }
}
#endif
