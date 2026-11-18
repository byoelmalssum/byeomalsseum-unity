using System.Collections.Generic;
using System.Linq;
using Byeolmalsseum;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 길차다 숲
// 쓰는 법: 길차다_공간 씬에 빈 오브젝트 → 이 스크립트 붙이기 → Inspector의 [숲 만들기] 버튼.
// 만들어진 나무·세계수는 씬에 그대로 남아서 직접 옮기고, 지우고, 복사할 수 있어요.
// 색은 Assets/Words/길차다/숲/Materials 에서 바꾸면 돼요.
public class GilchadaForest : MonoBehaviour
{
    [Tooltip("체험 카메라 빠르기 (m/초)")] public float cameraSpeed = 1.6f;
    [Tooltip("켜면 부드러운 모양 + 빛 번짐(후처리). 끄면 각진 로우폴리")] public bool highQuality = true;

    public static readonly Vector3 TREE = new Vector3(0, 0, 106);
    public static readonly float[] JamoZ = { -4, 10, 25, 42, 54, 65, 76 };

    // 숲길: 살짝 구불구불하다가 세계수 앞에서 곧게
    public static float PathX(float z) => 3.2f * Mathf.Sin(-z * .045f) * (z < 80 ? 1 : Mathf.Max(0, (100 - z) / 20f));

    static float Hash(float x, float z)
    {
        float h = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // 땅 높이
    public static float H(float x, float z)
    {
        float y = (Mathf.Sin(x * .21f) + Mathf.Cos(z * .17f)) * .35f + Hash(x, z) * .25f;
        if (Mathf.Abs(x - PathX(z)) < 3.5f && z < 90) y *= .25f;
        float dT = Vector2.Distance(new Vector2(x, z), new Vector2(TREE.x, TREE.z));
        if (dT < 22) y = y * .3f + (dT < 11 ? (11 - dT) * .12f : 0);
        return y;
    }

#if UNITY_EDITOR
    const string Word = "길차다";
    const string GroupName = "숲 (다시 만들면 이 그룹이 새로 바뀜)";
    const string AssetDir = "Assets/Words/길차다/숲";

    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ───────────────────────────── 버튼
    public void ButtonBuild()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        bool exists = transform.Find(GroupName) != null;
        if (!EditorUtility.DisplayDialog("길차다 숲",
            exists ? "숲을 새로 만들어요.\n지금 '숲' 그룹 안에서 직접 옮기거나 지운 건 처음 상태로 돌아가요.\n(Ctrl+Z로 되돌리기 가능)"
                   : "숲(땅·숲길·나무·세계수·안개·빛)을 만들어요.\n자모와 다른 소품은 건드리지 않아요.", "만들기", "취소")) return;
        Build(ws, this);
    }

    public void ButtonRebuildWorldTree()
    {
        if (FindWordSpace() == null) return;
        if (transform.Find(GroupName) == null) { EditorUtility.DisplayDialog("길차다 숲", "먼저 ① 숲 만들기를 눌러 주세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("길차다 숲",
            "세계수 공간만 새로 만들어요.\n(세계수 · 둘레 고목 · 이끼 바닥 · 노란 햇살 · 입구 앞 길가 나무)\n\n숲길 쪽에서 직접 고친 건 그대로 남아요. (Ctrl+Z로 되돌리기 가능)", "다시 만들기", "취소")) return;
        RebuildWorldTreeArea(this);
    }

    public void ButtonHighQuality()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        if (!EditorUtility.DisplayDialog("길차다 숲",
            highQuality ? "나무·땅 모양을 부드럽게 바꾸고, 빛 번짐·색감 후처리와 아침 숲 분위기를 적용해요.\n배치는 그대로예요. 직접 고친 것도 그대로 남아요."
                        : "나무·땅 모양을 각진 로우폴리로 되돌려요.\n배치는 그대로예요.", "바꾸기", "취소")) return;
        HQ = highQuality;
        meshes.Clear();
        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Meshes"); EnsureFolder(AssetDir + "/Materials");
        // 같은 이름의 모양 파일을 새로 고치면 씬의 모든 나무가 한 번에 바뀜
        foreach (var (n, sides, r0, r1) in new[] { ("기둥5", 5, 1f, .6f), ("원뿔6", 6, 1f, 0f), ("기둥6", 6, 1f, .5f), ("기둥7", 7, 1f, .47f),
                                                   ("원뿔5", 5, 1f, 0f), ("가지7", 7, 1f, .7f), ("원뿔4", 4, 1f, 0f), ("기둥4", 4, 1f, 1f), ("둔덕11", 11, 1f, .6f) })
            Frustum(n, sides, r0, r1);
        Ico();
        SaveMesh("세계수몸통", TwistedTrunk());
        SaveMesh("땅", GroundMesh());
        if (HQ) SetupPostProcessing(ws, transform);
        else { var v = transform.Find(PostName); if (v) Undo.DestroyObjectImmediate(v.gameObject); }
        ApplyMorningLook(transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log(HQ ? "[길차다 숲] 고퀄 모드로 바꿨어요." : "[길차다 숲] 로우폴리로 되돌렸어요.");
    }

    public void ButtonDetail()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        var f = transform.Find(GroupName);
        if (f == null) { EditorUtility.DisplayDialog("길차다 숲", "먼저 ① 숲 만들기를 눌러 주세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("길차다 숲",
            "① 숲길 양옆을 덤불·나무로 빽빽하게 채우고\n② 나무 잎을 찰흙 덩어리 대신 나뭇잎이 겹친 모양으로 바꾸고\n③ 숲길(깊은 그늘) ↔ 세계수 공터(밝은 안개빛) 대비를 크게 해요.\n\n직접 고친 배치는 그대로예요.", "적용", "취소")) return;
        Undo.SetCurrentGroupName("숲 디테일");
        int ug = Undo.GetCurrentGroup();
        forest = f; HQ = highQuality; rng = new System.Random(23);
        mats.Clear(); meshes.Clear();
        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Meshes"); EnsureFolder(AssetDir + "/Materials"); EnsureFolder(AssetDir + "/Textures");
        if (f.Find(WallName) == null) BuildPathWalls();
        AddLeafCards(f);
        ApplyMorningLook(transform);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(ug);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[길차다 숲] 숲길 벽 · 나뭇잎 디테일 · 분위기 대비를 적용했어요.");
    }

    public void ButtonCameraPath()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        if (!EditorUtility.DisplayDialog("길차다 숲", "체험 카메라 경로를 숲길 → 세계수 둘레로 바꿔요.\n(Ctrl+Z로 되돌리기 가능)", "바꾸기", "취소")) return;
        forest = transform;
        SetupCameraPath(ws, cameraSpeed);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    public void ButtonPlaceJamo()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        if (!EditorUtility.DisplayDialog("길차다 숲", "ㄱ ㅣ ㄹ ㅊ ㅏ ㄷ ㅏ 를 숲길을 따라 순서대로 놓아요.\n지금 옮겨둔 자리는 바뀌어요. (Ctrl+Z로 되돌리기 가능)", "놓기", "취소")) return;
        PlaceJamo(ws, transform);
    }

    static WordSpace FindWordSpace()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("길차다 숲", "플레이를 끄고 다시 눌러 주세요.", "확인"); return null; }
        var ws = Object.FindAnyObjectByType<WordSpace>();
        if (ws == null) EditorUtility.DisplayDialog("길차다 숲", "이 씬에 단어 공간이 없어요. 길차다_공간 씬에서 눌러 주세요.", "확인");
        return ws;
    }

    // ───────────────────────────── 만들기
    static Transform forest;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    static bool HQ = true;
    const string PostName = "후처리 (빛 번짐 · 색감)";

    static void Build(WordSpace ws, GilchadaForest owner)
    {
        HQ = owner.highQuality;
        rng = new System.Random(7);
        mats.Clear(); meshes.Clear();
        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Meshes"); EnsureFolder(AssetDir + "/Materials");
        Undo.SetCurrentGroupName("길차다 숲 만들기");
        int undoGroup = Undo.GetCurrentGroup();

        // 숲은 이 스크립트가 붙은 오브젝트 아래에 만듦
        Undo.RecordObject(owner.transform, "위치");
        owner.transform.SetPositionAndRotation(ws.transform.position, Quaternion.identity);
        owner.transform.localScale = Vector3.one;

        var oldGround = ws.transform.Find("공간 (소품·조명 등 여기에)/바닥 (지워도 됨)");
        if (oldGround && oldGround.gameObject.activeSelf) { Undo.RecordObject(oldGround.gameObject, "바닥 끄기"); oldGround.gameObject.SetActive(false); }

        var old = owner.transform.Find(GroupName);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
        forest = new GameObject(GroupName).transform;
        Undo.RegisterCreatedObjectUndo(forest.gameObject, "길차다 숲");
        forest.SetParent(owner.transform, false);

        BuildGround();
        BuildPathTrees();
        BuildOuterPines();
        BuildTallForest();
        BuildUndergrowth();
        BuildWorldTree();
        var warm = BuildWarmLight();
        BuildFireflies();
        var atmo = BuildAtmosphere(ws);
        atmo.warmLights = warm;
        SetupCameraPath(ws, owner.cameraSpeed);

        if (HQ) SetupPostProcessing(ws, owner.transform);
        ApplyMorningLook(owner.transform);
        atmo.Apply(0, 0);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
        Selection.activeGameObject = forest.gameObject;
        Debug.Log("[길차다 숲] 숲을 만들었어요. 저장(Ctrl+S)하고 플레이해 보세요.");
    }

    static void RebuildWorldTreeArea(GilchadaForest owner)
    {
        forest = owner.transform.Find(GroupName);
        HQ = owner.highQuality;
        rng = new System.Random(11);
        mats.Clear(); meshes.Clear();
        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Meshes"); EnsureFolder(AssetDir + "/Materials");
        Undo.SetCurrentGroupName("세계수 공간 다시 만들기");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var n in new[] { "세계수", "세계수 둘레 고목 숲", "땅/빛나는 풀밭", "땅/이끼 바닥", "세계수 햇살 (노란 빛)" })
        {
            var t = forest.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        // 세계수 입구를 가리던 길가 나무 치우기
        var pathTrees = forest.Find("길가 나무 (길 위를 덮어 초반을 어둡게)");
        if (pathTrees)
        {
            var del = new List<GameObject>();
            foreach (Transform c in pathTrees) if (c.localPosition.z > 74) del.Add(c.gameObject);
            foreach (var go in del) Undo.DestroyObjectImmediate(go);
        }

        var ground = forest.Find("땅");
        if (ground == null) ground = Group("땅");
        BuildMossFloor(ground);
        BuildTallForest();
        BuildWorldTree();
        var warm = BuildWarmLight();

        var atmo = forest.GetComponentInChildren<ForestAtmosphere>();
        if (atmo)
        {
            Undo.RecordObject(atmo, "분위기");
            atmo.warmLights = warm;
            if (atmo.treeLight)
            {
                Undo.RecordObject(atmo.treeLight, "빛"); Undo.RecordObject(atmo.treeLight.transform, "빛");
                atmo.treeLight.color = Hex("#ffd9a0"); atmo.treeLight.name = "세계수 아래 햇빛";
                atmo.treeLight.transform.localPosition = new Vector3(0, 6, 90);
            }
            if (atmo.spiritLight)
            {
                Undo.RecordObject(atmo.spiritLight, "빛"); Undo.RecordObject(atmo.spiritLight.transform, "빛");
                atmo.spiritLight.color = Hex("#e8f6ff");
                atmo.spiritLight.transform.localPosition = new Vector3(0, 18, 106);
            }
        }

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
        Debug.Log("[길차다 숲] 세계수 공간을 새로 만들었어요.");
    }

    // ───────────────────────────── 땅 · 숲길 · 빛나는 풀밭
    static void BuildGround()
    {
        var g = Group("땅");
        Part(g, "땅", SaveMesh("땅", GroundMesh()), Mat("땅", "#183026"), Vector3.zero, Quaternion.identity, Vector3.one, false);

        var tris = new List<Vector3>();
        // 숲길 (흙길 띠)
        for (float z = -24; z < 94; z += 1.5f)
        {
            float z1 = z + 1.5f, w = 2.3f;
            Vector3 a = V(PathX(z) - w, z, .04f), b = V(PathX(z) + w, z, .04f), c = V(PathX(z1) + w, z1, .04f), d = V(PathX(z1) - w, z1, .04f);
            tris.AddRange(new[] { a, c, b, a, d, c });
        }
        Part(g, "숲길", SaveMesh("숲길", Flat(tris)), Mat("숲길", "#3a3a2c"), Vector3.zero, Quaternion.identity, Vector3.one, false);

        BuildMossFloor(g);
    }

    // 세계수 둘레 이끼 바닥 (땅과 자연스럽게 이어지는 차분한 초록)
    static void BuildMossFloor(Transform g)
    {
        var tris = new List<Vector3>();
        const int seg = 40, rings = 8; const float RAD = 22f;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                float r0 = RAD * r / rings, r1 = RAD * (r + 1) / rings;
                float a0 = Mathf.PI * 2 * s / seg, a1 = Mathf.PI * 2 * (s + 1) / seg;
                Vector3 p00 = VP(r0, a0), p01 = VP(r0, a1), p10 = VP(r1, a0), p11 = VP(r1, a1);
                tris.AddRange(new[] { p00, p01, p11, p00, p11, p10 });
            }
        Part(g, "이끼 바닥", SaveMesh("이끼바닥", Flat(tris)), Mat("이끼 바닥", "#24402d"), Vector3.zero, Quaternion.identity, Vector3.one, false);
    }
    static Mesh GroundMesh()
    {
        const int N = 80; const float S = 260f;
        var tris = new List<Vector3>();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                float x0 = -S / 2 + S * i / N, x1 = -S / 2 + S * (i + 1) / N;
                float z0 = -60 + S * j / N, z1 = -60 + S * (j + 1) / N;
                Vector3 a = V(x0, z0), b = V(x1, z0), c = V(x1, z1), d = V(x0, z1);
                tris.AddRange(new[] { a, c, b, a, d, c });
            }
        return HQ ? Weld(tris, -1) : Flat(tris);
    }

    static Vector3 V(float x, float z, float up = 0) => new Vector3(x, H(x, z) + up, z);
    static Vector3 VP(float r, float a) => V(TREE.x + Mathf.Cos(a) * r, TREE.z + Mathf.Sin(a) * r, .05f);

    // ───────────────────────────── 나무들
    static Material Bark => Mat("나무 껍질", "#1d2629");
    static Material Leaf(int i) => i switch
    {
        0 => Mat("잎 짙은", "#16332e"),
        1 => Mat("잎 청록", "#2a5c50"),
        2 => Mat("잎 깊은", "#1f4a40"),
        _ => Mat("잎 그늘", "#12302a"),
    };

    static void Pine(Transform parent, float x, float z, float s)
    {
        var t = Child(parent, "침엽수", new Vector3(x, H(x, z), z));
        float h = R(10, 17) * s;
        Part(t, "줄기", Frustum("기둥5", 5, 1, .6f), Bark, new Vector3(0, 0, 0), Quaternion.identity, new Vector3(.38f * s, h * .55f, .38f * s));
        var top = Child(t, "잎 (흔들림)", new Vector3(0, h * .35f, 0));
        var m = Leaf(rng.Next(4));
        for (int k = 0; k < 3; k++)
        {
            float r = (2.6f - k * .7f) * s * R(.9f, 1.15f), hh = h * .36f;
            Part(top, "잎", Frustum("원뿔6", 6, 1, 0), m, new Vector3(0, k * h * .17f, 0), Quaternion.Euler(0, R(0, 180), 0), new Vector3(r, hh, r));
        }
        Sway(top, 2f, R(.9f, 1.3f));
    }

    static void Broadleaf(Transform parent, float x, float z, float s, float lean)
    {
        var t = Child(parent, "활엽수", new Vector3(x, H(x, z), z));
        float h = R(8, 12) * s;
        Part(t, "줄기", Frustum("기둥6", 6, 1, .5f), Bark, Vector3.zero, Quaternion.Euler(0, 0, -lean * 7f), new Vector3(.6f * s, h, .6f * s));
        var crown = Child(t, "잎 (흔들림)", new Vector3(Mathf.Sin(lean * .12f) * h * .9f, h * .95f, 0));
        var m = Leaf(rng.Next(4));
        int n = 4 + rng.Next(3);
        for (int k = 0; k < n; k++)
        {
            float r = R(2.2f, 3.6f) * s;
            Part(crown, "잎", Ico(), m, new Vector3(R(-3, 3) * s + lean * 2.5f * s, R(-1, 1.5f) * s, R(-3, 3) * s),
                 Quaternion.Euler(R(0, 60), R(0, 360), 0), new Vector3(r, r * R(.6f, .85f), r));
        }
        Sway(crown, 2.6f, R(.8f, 1.2f));
    }

    static void TallTree(Transform parent, float x, float z, float s)
    {
        var t = Child(parent, "고목", new Vector3(x, H(x, z), z));
        float h = R(24, 34) * s;
        var lean = Quaternion.Euler(R(-4.5f, 4.5f), 0, R(-4.5f, 4.5f));
        Part(t, "줄기", Frustum("기둥7", 7, 1, .47f), Bark, Vector3.zero, lean, new Vector3(1.5f * s, h, 1.5f * s));
        for (int k = 0; k < 4; k++)
        {
            float a = k / 4f * 360f + R(-17, 17);
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Vector3 rootDir = (dir * 3.3f + Vector3.down * 1.4f).normalized;
            Part(t, "뿌리", Frustum("원뿔5", 5, 1, 0), Bark, dir * 1.0f * s + Vector3.up * 1.4f * s,
                 Quaternion.FromToRotation(Vector3.up, rootDir), new Vector3(.9f * s, 3.6f * s, .9f * s));
        }
        for (int k = 0; k < 2; k++)
        {
            float y = h * R(.45f, .75f);
            var dir = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
            Limb(t, Vector3.up * y, Vector3.up * (y + 3.5f * s) + dir * 5f * s, .35f * s, Bark, false);
        }
        var crown = Child(t, "잎 (흔들림)", lean * Vector3.up * h * .98f);
        for (int k = 0; k < 6; k++)
        {
            float r = R(3, 5.5f) * s;
            Part(crown, "잎", Ico(), Leaf(rng.Next(4)), new Vector3(R(-4, 4) * s, R(-2, 2.5f) * s, R(-4, 4) * s),
                 Quaternion.Euler(R(0, 60), R(0, 360), 0), new Vector3(r, r * .6f, r));
        }
        Sway(crown, 1.4f, R(.6f, .9f));
    }

    // 두 점 사이 통나무 (끝이 가늘어짐) + 위쪽에 이끼
    static Transform Limb(Transform parent, Vector3 a, Vector3 b, float r, Material m, bool moss = true, Material mossMat = null)
    {
        Vector3 d = b - a; float L = d.magnitude;
        var t = Part(parent, "가지", Frustum("가지7", 7, 1, .7f), m, a, Quaternion.FromToRotation(Vector3.up, d), new Vector3(r, L, r));
        if (moss)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(L / 1.6f));
            for (int i = 0; i < n; i++)
            {
                float k = (i + .5f) / n, rr = Mathf.Lerp(r, r * .7f, k);
                float sz = rr * R(.8f, 1.15f);
                Part(parent, "이끼", Ico(), mossMat ? mossMat : Mat("이끼", "#4f7d3a"), a + d * k + Vector3.up * rr * .55f,
                     Quaternion.Euler(0, R(0, 360), 0), new Vector3(sz, sz * .45f, sz));
            }
        }
        return t;
    }

    // ───────────────────────────── 숲길 양옆 벽 (덤불 + 나무 두세 겹)
    const string WallName = "숲길 양옆 숲 (길로 이끄는 벽)";

    static void Bush(Transform parent, float x, float z, float s)
    {
        var t = Child(parent, "덤불", new Vector3(x, H(x, z), z));
        int n = 3 + rng.Next(3);
        for (int k = 0; k < n; k++)
        {
            float r = R(1, 1.8f) * s;
            Part(t, "잎", Ico(), Leaf(rng.Next(4)), new Vector3(R(-1.2f, 1.2f) * s, R(.5f, 1.8f) * s, R(-1.2f, 1.2f) * s),
                 Quaternion.Euler(R(0, 40), R(0, 360), 0), new Vector3(r, r * R(.7f, .9f), r));
        }
        Sway(t, 2f, R(1, 1.5f));
    }

    static void BuildPathWalls()
    {
        var g = Group(WallName);
        for (float z = -14; z < 80; z += R(1.6f, 2.6f))
            foreach (int side in new[] { -1, 1 })
            {
                bool nearJamo = JamoZ.Any(j => Mathf.Abs(z - j) < 6);
                float px = PathX(z);
                if (!nearJamo) Bush(g, px + side * R(3.6f, 6f), z, R(.8f, 1.3f));          // 길 바로 옆 덤불
                if (rng.NextDouble() < .55) Broadleaf(g, px + side * R(8, 13), z, R(.9f, 1.3f), -side); // 두 번째 줄 나무
                if (rng.NextDouble() < .3) TallTree(g, px + side * R(13, 20), z, R(.5f, .7f));          // 뒤쪽 큰 나무
            }
    }

    // ───────────────────────────── 나뭇잎 카드 (찰흙 덩어리 → 잎이 겹친 모양)
    static void AddLeafCards(Transform root)
    {
        var sphereCards = SphereCards(); var coneCards = ConeCards();
        var list = new List<MeshRenderer>(root.GetComponentsInChildren<MeshRenderer>(true));
        foreach (var r in list)
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var src = r.sharedMaterial; if (src == null) continue;
            bool leafy = src.name.StartsWith("잎") || src.name.StartsWith("수관");
            if (!leafy || src.name.StartsWith("잎카드") || src.name.StartsWith("잎속")) continue;
            if (r.transform.Find("잎 카드")) continue;
            Mesh cards = mf.sharedMesh.name == "이십면체" ? sphereCards : mf.sharedMesh.name == "원뿔6" ? coneCards : null;
            if (cards == null) continue;
            var go = new GameObject("잎 카드", typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "잎 카드");
            go.transform.SetParent(r.transform, false);
            go.transform.localScale = Vector3.one * 1.06f;
            go.GetComponent<MeshFilter>().sharedMesh = cards;
            var cr = go.GetComponent<MeshRenderer>();
            cr.sharedMaterial = LeafCardMat(src);
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // 잎 사이로 얼룩진 그림자
            // 안쪽 덩어리는 조금 작고 어둡게 → 잎 사이 틈이 깊어 보임
            Undo.RecordObject(r.transform, "잎");
            r.transform.localScale *= .88f;
            Undo.RecordObject(r, "잎");
            r.sharedMaterial = DarkInner(src);
        }
    }

    static Material DarkInner(Material src)
    {
        string path = $"{AssetDir}/Materials/잎속 {src.name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(src);
        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.green;
        Color dark = c * .55f; dark.a = 1;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", dark);
        if (m.HasProperty("_Color")) m.SetColor("_Color", dark);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material LeafCardMat(Material src)
    {
        string path = $"{AssetDir}/Materials/잎카드 {src.name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(src);
        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.green;
        Color tint = Color.Lerp(c, new Color(.55f, .8f, .45f), .25f) * 1.35f; tint.a = 1;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        var tex = LeafTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", .45f);
        m.EnableKeyword("_ALPHATEST_ON");
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);   // 앞뒤 모두 보이게
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f);
        if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
        m.renderQueue = 2450;
        m.enableInstancing = true;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 잎 무늬 텍스처 (여러 장의 잎이 겹친 모양, 투명 배경)
    static Texture2D LeafTexture()
    {
        string path = $"{AssetDir}/Textures/잎.png";
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t) return t;
        const int S = 512;
        var px = new Color32[S * S];
        var r = new System.Random(5);
        float F(double a, double b) => (float)(a + r.NextDouble() * (b - a));
        for (int n = 0; n < 230; n++)
        {
            float cx = F(40, S - 40), cy = F(40, S - 40);
            // 가운데로 모일수록 촘촘 (가장자리는 성글게 → 잎 덩어리 윤곽이 들쭉날쭉)
            float dc = Vector2.Distance(new Vector2(cx, cy), new Vector2(S / 2, S / 2)) / (S / 2);
            if (dc > .95f || r.NextDouble() < dc * .6f) continue;
            float len = F(26, 52), wid = len * F(.38f, .5f), ang = F(0, Mathf.PI * 2);
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float shade = F(.62f, 1f);
            int x0 = Mathf.Max(0, (int)(cx - len)), x1 = Mathf.Min(S - 1, (int)(cx + len));
            int y0 = Mathf.Max(0, (int)(cy - len)), y1 = Mathf.Min(S - 1, (int)(cy + len));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float u = (dx * ca + dy * sa) / len;      // 잎 길이 방향 -1..1
                    float v = (-dx * sa + dy * ca) / wid;     // 잎 폭 방향
                    float w = 1 - u * u;                      // 끝이 뾰족한 잎 모양
                    if (w <= 0 || Mathf.Abs(v) > w * (1 - .35f * u)) continue;
                    float vein = Mathf.Abs(v) < .06f ? .82f : 1f;
                    float light = shade * vein * (1 - .25f * (u + 1) * .5f) * (1 - .15f * Mathf.Abs(v));
                    byte g = (byte)(Mathf.Clamp01(light) * 255);
                    px[y * S + x] = new Color32(g, g, g, 255);
                }
        }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 구 표면을 덮는 잎 카드들 (법선은 구 방향 → 덩어리 전체가 부드럽게 빛을 받음)
    static Mesh SphereCards()
    {
        const string name = "잎카드구";
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var r = new System.Random(3);
        int N = 64;
        for (int i = 0; i < N; i++)
        {
            float y = 1 - 2f * (i + .5f) / N, rad = Mathf.Sqrt(1 - y * y), phi = i * 2.39996f;
            Vector3 dir = new Vector3(Mathf.Cos(phi) * rad, y, Mathf.Sin(phi) * rad);
            Card(verts, norms, uvs, tris, dir * (float)(.82 + r.NextDouble() * .2), dir, (float)(.55 + r.NextDouble() * .3), r);
        }
        return SaveCards(name, verts, norms, uvs, tris);
    }

    // 원뿔(침엽수) 표면 잎 카드
    static Mesh ConeCards()
    {
        const string name = "잎카드원뿔";
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var r = new System.Random(4);
        for (int i = 0; i < 46; i++)
        {
            float h = Mathf.Pow((float)r.NextDouble(), .8f) * .92f, a = (float)(r.NextDouble() * Mathf.PI * 2);
            float rad = (1 - h) * (float)(.8 + r.NextDouble() * .2);
            Vector3 out1 = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 nrm = (out1 + Vector3.up * .5f).normalized;
            Card(verts, norms, uvs, tris, out1 * rad + Vector3.up * h, nrm, (float)(.35 + r.NextDouble() * .25) * (1.2f - h), r);
        }
        return SaveCards(name, verts, norms, uvs, tris);
    }

    static void Card(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 nrm, float size, System.Random r)
    {
        Vector3 tilt = (nrm + new Vector3((float)r.NextDouble() - .5f, (float)r.NextDouble() - .5f, (float)r.NextDouble() - .5f) * .9f).normalized;
        Quaternion q = Quaternion.LookRotation(tilt) * Quaternion.Euler(0, 0, (float)r.NextDouble() * 360);
        Vector3 ax = q * Vector3.right * size, ay = q * Vector3.up * size;
        int b = v.Count;
        v.Add(c - ax - ay); v.Add(c + ax - ay); v.Add(c + ax + ay); v.Add(c - ax + ay);
        for (int k = 0; k < 4; k++) n.Add(nrm);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
        t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
    }

    static Mesh SaveCards(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        string path = $"{AssetDir}/Meshes/{name}.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = m == null;
        if (isNew) m = new Mesh { name = name }; else m.Clear();
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); m.RecalculateTangents();
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildPathTrees()
    {
        var g = Group("길가 나무 (길 위를 덮어 초반을 어둡게)");
        for (float z = -12; z < 74; z += R(4, 6.5f))
            foreach (int side in new[] { -1, 1 })
            {
                float x = PathX(z) + side * R(4.5f, 7.5f);
                if (JamoZ.Any(j => Mathf.Abs(z - j) < 5)) continue; // 자모 자리 비워두기
                Broadleaf(g, x, z + R(-1.5f, 1.5f), R(1, 1.35f), -side);
            }
    }

    static void BuildOuterPines()
    {
        var g = Group("바깥 침엽수");
        int placed = 0, tries = 0;
        while (placed < 320 && tries < 6000)
        {
            tries++;
            float x = R(-90, 90), z = R(-30, 175);
            float dPath = Mathf.Abs(x - PathX(z)), dTree = Vector2.Distance(new Vector2(x, z), new Vector2(TREE.x, TREE.z));
            if (z < 92 && dPath < 9) continue;
            if (dTree < 44) continue;
            Pine(g, x, z, R(.9f, 1.5f)); placed++;
        }
    }

    static void BuildTallForest()
    {
        var g = Group("세계수 둘레 고목 숲");
        for (int i = 0; i < 34; i++)
        {
            float a = R(0, Mathf.PI * 2), r = rng.NextDouble() < .35 ? R(21, 24) : R(33, 48);
            float x = TREE.x + Mathf.Cos(a) * r, z = TREE.z + Mathf.Sin(a) * r;
            if (z < TREE.z - 3 && Mathf.Abs(x - TREE.x) < (TREE.z - z) * 1.2f + 4) continue; // 입구에서 세계수를 가리지 않게 앞쪽 비우기
            TallTree(g, x, z, R(.85f, 1.25f));
        }
    }

    static void BuildUndergrowth()
    {
        var g = Group("고사리 · 돌 · 버섯");
        var f1 = Mat("고사리", "#2f6b4c"); var f2 = Mat("고사리 밝은", "#3f8a52");
        void Fern(float x, float z, float s)
        {
            var t = Child(g, "고사리 (흔들림)", new Vector3(x, H(x, z), z));
            for (int k = 0; k < 5; k++)
                Part(t, "잎", Frustum("원뿔4", 4, 1, 0), k % 2 == 1 ? f1 : f2, Vector3.zero,
                     Quaternion.Euler(R(-40, 40), R(0, 360), R(-40, 40)), new Vector3(.22f * s, R(.8f, 1.6f) * s, .22f * s), false);
            Sway(t, 7f, R(1.4f, 2f));
        }
        for (int i = 0; i < 150; i++) { float z = R(-12, 88); int side = rng.NextDouble() < .5 ? -1 : 1; Fern(PathX(z) + side * R(2.2f, 6), z, 1); }
        for (int i = 0; i < 90; i++) { float a = R(0, Mathf.PI * 2), r = R(9, 30); Fern(TREE.x + Mathf.Cos(a) * r, TREE.z + Mathf.Sin(a) * r, R(.9f, 1.4f)); }

        var stone = Mat("돌", "#6f8a86"); var stoneMoss = Mat("이끼 돌", "#5f8f4a");
        for (int i = 0; i < 40; i++)
        {
            float a = R(0, Mathf.PI * 2), r = R(10, 22), x = TREE.x + Mathf.Cos(a) * r, z = TREE.z + Mathf.Sin(a) * r, s = R(.4f, 1.1f);
            Part(g, "돌", Ico(), i % 3 == 0 ? stoneMoss : stone, new Vector3(x, H(x, z) + .1f, z), Random3(), new Vector3(s, s * .6f, s));
        }
        var fungus = Mat("빛나는 버섯", "#bdf3ff", "#7fe0f0", 1.5f);
        for (int i = 0; i < 70; i++)
        {
            float z = R(-10, 86); int side = rng.NextDouble() < .5 ? -1 : 1; float x = PathX(z) + side * R(1.8f, 3.2f);
            Part(g, "버섯", Frustum("원뿔5", 5, 1, 0), fungus, new Vector3(x, H(x, z) + .2f, z), Quaternion.Euler(180, 0, 0), new Vector3(.12f, .18f, .12f), false);
        }
    }

    // ───────────────────────────── 세계수
    const float WT_H = 30f;        // 몸통 높이
    const float WT_R0 = 8f, WT_R1 = 4f; // 아래 · 위 반지름

    static void BuildWorldTree()
    {
        var g = Child(forest, "세계수", new Vector3(TREE.x, H(TREE.x, TREE.z), TREE.z));
        var bark = Mat("세계수 껍질", "#26332f");
        var moss = Mat("세계수 이끼", "#35593a");
        var floorMoss = Mat("이끼 바닥", "#24402d");

        Part(g, "몸통", SaveMesh("세계수몸통", TwistedTrunk()), bark, Vector3.zero, Quaternion.identity, Vector3.one);
        // 밑동 둔덕: 땅에서 몸통으로 완만하게 이어짐
        Part(g, "밑동 둔덕", Frustum("둔덕11", 11, 1, .6f), Mat("뿌리 둔덕", "#2b3d33"), Vector3.down * .8f, Quaternion.Euler(0, 15, 0), new Vector3(13, 3.6f, 13));

        var roots = Child(g, "판뿌리", Vector3.zero);
        var fungus = Mat("빛나는 버섯", "#bdf3ff", "#7fe0f0", 1.5f);
        for (int i = 0; i < 11; i++)
        {
            float a = i / 11f * Mathf.PI * 2 + R(-.15f, .15f), L = R(11, 16);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 A = dir * 5 + Vector3.up * 4.5f, B = dir * L + Vector3.down * 1.2f; // 끝은 땅속으로 묻힘
            Limb(roots, A, B, 1.8f, bark, true, moss);
            // 뿌리 끝을 덮는 이끼 더미 → 땅과 이어짐
            float m = R(1.8f, 2.6f);
            Part(roots, "뿌리 끝 이끼", Ico(), floorMoss, dir * (L - 1) + Vector3.up * .1f, Quaternion.Euler(0, R(0, 360), 0), new Vector3(m, m * .35f, m));
            for (int k = 0; k < 3; k++)
                Part(roots, "버섯", Ico(), fungus, Vector3.Lerp(A, B, R(.3f, .9f)) + Random.insideUnitSphere * 1.2f, Quaternion.identity, Vector3.one * R(.05f, .09f), false);
        }
        // 밑동 둘레 이끼 · 고사리 느낌 덩어리
        for (int i = 0; i < 24; i++)
        {
            float a = R(0, Mathf.PI * 2), r = R(9, 15), m = R(1, 2.2f);
            Part(roots, "이끼 덩어리", Ico(), i % 3 == 0 ? moss : floorMoss, new Vector3(Mathf.Cos(a) * r, .1f, Mathf.Sin(a) * r), Quaternion.Euler(0, R(0, 360), 0), new Vector3(m, m * .4f, m));
        }

        var deco = Child(g, "몸통 이끼", Vector3.zero);
        for (int i = 0; i < 22; i++)
        {
            float y = R(2, WT_H * .55f), a = R(0, Mathf.PI * 2) + y * .05f, r = Mathf.Lerp(WT_R0, WT_R1, y / WT_H) - .2f, sz = R(.9f, 1.6f);
            Part(deco, "이끼", Ico(), moss, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Quaternion.Euler(0, R(0, 360), 0), new Vector3(sz, sz * .3f, sz));
        }

        var branches = Child(g, "큰 가지", Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2 + R(-.2f, .2f);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Limb(branches, dir * 2.5f + Vector3.up * (WT_H - 6), dir * 16 + Vector3.up * R(WT_H, WT_H + 5), 1.6f, bark, true, moss);
        }

        var crown = Child(g, "높은 수관 (흔들림)", Vector3.up * (WT_H + 4));
        var cm = new[] { Mat("수관 깊은", "#1f4a40", "#0f2e2a", .4f), Mat("수관 청록", "#2a5c50", "#1b4a44", .4f), Mat("수관 밝은", "#3f7a68", "#2f6a60", .5f) };
        for (int i = 0; i < 30; i++)
        {
            float a = R(0, Mathf.PI * 2), r = R(0, 22), y = R(-4, 6) * (1 - r / 30), sz = R(5, 8.5f);
            Part(crown, "잎", Ico(), cm[rng.Next(3)], new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Random3(), new Vector3(sz, sz * R(.5f, .7f), sz));
        }
        Sway(crown, .6f, .4f);
    }

    static Mesh TwistedTrunk()
    {
        int sides = HQ ? 24 : 11, rings = HQ ? 20 : 10;
        float mid = WT_H / 2;
        var grid = new Vector3[rings + 1, sides + 1];
        for (int r = 0; r <= rings; r++)
            for (int s = 0; s <= sides; s++)
            {
                float y = WT_H * r / rings, a = Mathf.PI * 2 * s / sides;
                float rad = Mathf.Lerp(WT_R0, WT_R1, y / WT_H) * (1 + .12f * Mathf.Sin(a * 5 + (y - mid) * .3f));
                float tw = (y - mid) * .05f;
                grid[r, s] = new Vector3(Mathf.Cos(a + tw) * rad, y, Mathf.Sin(a + tw) * rad);
            }
        var tris = new List<Vector3>();
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                Vector3 a = grid[r, s], b = grid[r, s + 1], c = grid[r + 1, s + 1], d = grid[r + 1, s];
                tris.AddRange(new[] { a, c, b, a, d, c });
            }
        return HQ ? Weld(tris, -1) : Flat(tris);
    }

    // ───────────────────────────── 노란 햇살 (나뭇잎 사이로 새어 들어오는 빛)
    static Light[] BuildWarmLight()
    {
        var g = Group("세계수 햇살 (노란 빛)");
        var lights = new List<Light>();
        // 수관 사이로 비스듬히 내리쬐는 스포트라이트
        Vector3[] aims = { new Vector3(-6, 0, 92), new Vector3(7, 0, 96), new Vector3(-2, 0, 118) };
        for (int i = 0; i < aims.Length; i++)
        {
            var go = new GameObject($"햇살 {i + 1}");
            Undo.RegisterCreatedObjectUndo(go, "햇살");
            go.transform.SetParent(g, false);
            Vector3 from = aims[i] + new Vector3(-12, 55, -18);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(aims[i] - from);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.color = Hex("#ffd27a"); l.range = 110; l.spotAngle = 24; l.innerSpotAngle = 8;
            l.intensity = 0; l.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
            lights.Add(l);
        }
        // 눈에 보이는 빛줄기
        var shaft = ShaftMaterial();
        for (int i = 0; i < 6; i++)
        {
            float a = R(0, Mathf.PI * 2), r = R(10, 19);
            var pos = new Vector3(TREE.x + Mathf.Cos(a) * r, 0, TREE.z + Mathf.Sin(a) * r);
            float w = R(2.2f, 3.6f);
            Part(g, "빛줄기", OpenCone(), shaft, pos, Quaternion.Euler(-14, 33, 0), new Vector3(w, R(36, 46), w), false);
        }
        return lights.ToArray();
    }

    // 뚜껑 없는 원뿔대 (아래 넓고 위 좁음) — 빛줄기용
    static Mesh OpenCone()
    {
        const string name = "빛줄기원뿔";
        if (meshes.TryGetValue(name, out var cached)) return cached;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        const int sides = 16;
        for (int k = 0; k <= sides; k++)
        {
            float a = Mathf.PI * 2 * k / sides;
            v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); uv.Add(new Vector2((float)k / sides, 0));
            v.Add(new Vector3(Mathf.Cos(a) * .25f, 1, Mathf.Sin(a) * .25f)); uv.Add(new Vector2((float)k / sides, 1));
        }
        for (int k = 0; k < sides; k++)
        {
            int b = k * 2;
            t.AddRange(new[] { b, b + 3, b + 2, b, b + 1, b + 3 });
        }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        m = SaveMesh(name, m);
        meshes[name] = m;
        return m;
    }

    // 빛줄기 무늬: 아래·위로 사라지고, 결이 있는 옅은 빛
    static Texture2D ShaftTexture()
    {
        string path = $"{AssetDir}/Textures/빛줄기.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex) return tex;
        EnsureFolder(AssetDir + "/Textures");
        const int W = 128, Hh = 128;
        var px = new Color32[W * Hh];
        for (int y = 0; y < Hh; y++)
            for (int x = 0; x < W; x++)
            {
                float vv = y / (Hh - 1f), uu = x / (W - 1f);
                float fadeV = Mathf.SmoothStep(0, 1, vv / .25f) * Mathf.SmoothStep(0, 1, (1 - vv) / .45f);
                float streak = .55f + .45f * Mathf.Sin(uu * Mathf.PI * 2 * 5 + Mathf.Sin(uu * 23) * 1.3f);
                byte a = (byte)(Mathf.Clamp01(fadeV * streak) * 255);
                px[y * W + x] = new Color32(255, 255, 255, a);
            }
        var t2 = new Texture2D(W, Hh, TextureFormat.RGBA32, false);
        t2.SetPixels32(px); t2.Apply();
        System.IO.File.WriteAllBytes(path, t2.EncodeToPNG());
        Object.DestroyImmediate(t2);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ShaftMaterial()
    {
        string path = $"{AssetDir}/Materials/햇살 빛줄기.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            m = new Material(sh);
        }
        else Undo.RecordObject(m, "빛줄기");
        var tex = ShaftTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        var c = Hex("#ffe6b0"); c.a = .035f;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); // 투명 + 더하기
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0); // 안쪽에서도 보이게
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────────── 다듬기 (빛줄기 줄이기 · 공터 나무 채우기 · 시작 부분 비우기)
    public void ButtonPolish()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        var f = transform.Find(GroupName);
        if (f == null) { EditorUtility.DisplayDialog("길차다 숲", "먼저 ① 숲 만들기를 눌러 주세요.", "확인"); return; }
        Undo.SetCurrentGroupName("숲 다듬기");
        int ug = Undo.GetCurrentGroup();
        forest = f; HQ = highQuality; rng = new System.Random(31);
        mats.Clear(); meshes.Clear();

        // ① 빛줄기: 개수 줄이고 훨씬 투명하게, 위아래로 사라지게
        meshes.Remove("빛줄기원뿔"); OpenCone(); ShaftMaterial();
        void Thin(string group, int keep, float lightMul)
        {
            var g = f.Find(group); if (g == null) return;
            int seen = 0; var del = new List<GameObject>();
            foreach (Transform c in g)
            {
                if (c.name == "빛줄기") { if (++seen > keep) del.Add(c.gameObject); }
                var l = c.GetComponent<Light>();
                if (l && l.type == LightType.Spot && lightMul != 1) { Undo.RecordObject(l, "햇살"); l.intensity *= lightMul; }
            }
            foreach (var go in del) Undo.DestroyObjectImmediate(go);
        }
        Thin("숲길 햇살", 2, .7f);
        Thin("세계수 햇살 (노란 빛)", 3, 1f);
        var atmo = f.GetComponentInChildren<ForestAtmosphere>(true);
        if (atmo) { Undo.RecordObject(atmo, "햇살"); atmo.warmLightMax = 5.5f; }

        // ② 세계수 공터 둘레에서 비어 보이는 곳에 고목 채우기 (입구 쪽은 계속 비움)
        var tall = f.Find("세계수 둘레 고목 숲");
        if (tall == null) tall = Group("세계수 둘레 고목 숲");
        var have = new List<Vector2>();
        foreach (Transform c in tall) have.Add(new Vector2(c.localPosition.x, c.localPosition.z));
        for (int deg = 0; deg < 360; deg += 12)
        {
            float a = deg * Mathf.Deg2Rad;
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? R(23, 27) : R(30, 36);
                float x = TREE.x + Mathf.Cos(a) * r, z = TREE.z + Mathf.Sin(a) * r;
                if (z < TREE.z - 3 && Mathf.Abs(x - TREE.x) < (TREE.z - z) * 1.2f + 4) continue; // 입구 앞 시야
                var p = new Vector2(x, z);
                if (have.Any(h => (h - p).sqrMagnitude < 8.5f * 8.5f)) continue;
                int before = tall.childCount;
                TallTree(tall, x, z, R(.85f, 1.2f));
                for (int k = before; k < tall.childCount; k++) Undo.RegisterCreatedObjectUndo(tall.GetChild(k).gameObject, "고목");
                have.Add(p);
            }
        }

        // ③ 시작 부분: 글자가 나타나는 앞쪽 덤불·나무 치우기
        foreach (var n in new[] { WallName, "길가 나무 (길 위를 덮어 초반을 어둡게)", "고사리 · 돌 · 버섯" })
        {
            var g = f.Find(n); if (g == null) continue;
            var del = new List<GameObject>();
            foreach (Transform c in g)
            {
                Vector3 lp = c.localPosition;
                if (lp.z < 4 && Mathf.Abs(lp.x - PathX(lp.z)) < 10) del.Add(c.gameObject);
            }
            foreach (var go in del) Undo.DestroyObjectImmediate(go);
        }

        // 새로 만든 고목에도 잎 카드
        AddLeafCards(tall);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(ug);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[길차다 숲] 다듬기 완료: 빛줄기 줄임 · 공터 고목 채움 · 시작 부분 비움");
    }

    // ───────────────────────────── 시작 숲길 다시 채우기 + 하늘 열림 타이밍
    public void ButtonStartForest()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        var f = transform.Find(GroupName);
        if (f == null) { EditorUtility.DisplayDialog("길차다 숲", "먼저 ① 숲 만들기를 눌러 주세요.", "확인"); return; }
        Undo.SetCurrentGroupName("시작 숲길");
        int ug = Undo.GetCurrentGroup();
        forest = f; HQ = highQuality; rng = new System.Random(41);
        mats.Clear(); meshes.Clear();

        var old = f.Find("시작 숲길");
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var g = Group("시작 숲길");
        Undo.RegisterCreatedObjectUndo(g.gameObject, "시작 숲길");
        // 글자가 뜨는 자리(z -16 ~ -6, 길 가운데 ±4m)는 비우고, 그 바깥은 숲길처럼 촘촘하게
        for (float z = -22; z < 6; z += R(2.6f, 3.6f))
            foreach (int side in new[] { -1, 1 })
            {
                float px = PathX(z);
                bool glyphZone = z > -17 && z < -5;
                // 길가 나무: 줄기는 가늘고 잎은 높아서 글자를 가리지 않음
                Broadleaf(g, px + side * R(glyphZone ? 5.2f : 4.5f, glyphZone ? 6.5f : 6f), z + R(-.8f, .8f), R(1.05f, 1.35f), -side);
                // 덤불: 글자 자리 옆은 조금 바깥으로
                Bush(g, px + side * R(glyphZone ? 6.8f : 3.8f, glyphZone ? 8.5f : 5.8f), z + R(-1, 1), R(.8f, 1.2f));
                if (rng.NextDouble() < .5) TallTree(g, px + side * R(10, 16), z, R(.5f, .7f));
            }
        // 고사리는 낮아서 길 가까이도 괜찮음
        var f1 = Mat("고사리", "#2f6b4c"); var f2 = Mat("고사리 밝은", "#3f8a52");
        for (int i = 0; i < 40; i++)
        {
            float z = R(-22, 6); int side = rng.NextDouble() < .5 ? -1 : 1; float x = PathX(z) + side * R(2.4f, 5f);
            var t = Child(g, "고사리 (흔들림)", new Vector3(x, H(x, z), z));
            for (int k = 0; k < 5; k++)
                Part(t, "잎", Frustum("원뿔4", 4, 1, 0), k % 2 == 1 ? f1 : f2, Vector3.zero,
                     Quaternion.Euler(R(-40, 40), R(0, 360), R(-40, 40)), new Vector3(.22f, R(.8f, 1.6f), .22f), false);
            Sway(t, 7f, R(1.4f, 2f));
        }
        AddLeafCards(g);

        // 하늘이 열리는 타이밍: 입구 연출이 시작될 때 (걸어서 더 들어가야 밝아지던 것 수정)
        var atmo = f.GetComponentInChildren<ForestAtmosphere>(true);
        if (atmo)
        {
            Undo.RecordObject(atmo, "분위기");
            atmo.darkUntilZ = 84f; atmo.lightFromZ = 96f;   // 연출 없이 걸어 들어와도 결국 밝아짐
            atmo.revealSeconds = 4f; atmo.revealResetZ = 70f;
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(ug);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[길차다 숲] 시작 숲길을 다시 채우고, 하늘이 열리는 타이밍을 입구 연출에 맞췄어요.");
    }

    // ───────────────────────────── 반딧불
    static void BuildFireflies()
    {
        var g = Group("반딧불");
        MakeFireflies(g, "숲길 반딧불", new Vector3(0, 2.5f, 38), new Vector3(16, 4.5f, 104), 260);
        MakeFireflies(g, "세계수 반딧불", new Vector3(TREE.x, 3, TREE.z), new Vector3(50, 6, 50), 200);
    }

    static void MakeFireflies(Transform parent, string name, Vector3 pos, Vector3 size, int max)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.maxParticles = max;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6, 10);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.06f, .14f);
        main.startColor = (Color)new Color32(0xf5, 0xe0, 0x8c, 255);
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = max / 8f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
        var noise = ps.noise; noise.enabled = true; noise.strength = .4f; noise.frequency = .3f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                     new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.3f, .5f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = FireflyMaterial();
    }

    static Material FireflyMaterial()
    {
        string path = $"{AssetDir}/Materials/반딧불.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh);
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
        if (m.HasProperty("_Surface"))
        {
            // URP 투명 + 더하기 (빛나는 느낌)
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ───────────────────────────── 분위기 (안개 · 빛)
    static ForestAtmosphere BuildAtmosphere(WordSpace ws)
    {
        var g = Group("분위기 (안개 · 빛)");
        var atmo = Undo.AddComponent<ForestAtmosphere>(g.gameObject);
        atmo.target = ws.experienceCamera ? ws.experienceCamera.transform : null;
        atmo.cameras = new[] { ws.experienceCamera ? ws.experienceCamera.Cam : null, ws.workCamera }.Where(c => c).ToArray();

        // 해 (씬에 있는 Directional Light 사용)
        var sun = RenderSettings.sun ? RenderSettings.sun : Object.FindAnyObjectByType<Light>();
        if (sun)
        {
            Undo.RecordObject(sun, "해"); Undo.RecordObject(sun.transform, "해");
            sun.color = new Color32(0xd8, 0xf0, 0xff, 255);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, 160, 0);
        }
        atmo.sun = sun;

        atmo.treeLight = PointLight(g, "세계수 아래 햇빛", new Vector3(0, 6, 90), "#ffd9a0", 60);
        atmo.spiritLight = PointLight(g, "세계수 정령빛", new Vector3(0, 18, 106), "#e8f6ff", 80);

        foreach (var c in atmo.cameras) { Undo.RecordObject(c, "카메라"); c.farClipPlane = 400; }
        return atmo;
    }

    static Light PointLight(Transform parent, string name, Vector3 pos, string hex, float range)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = Hex(hex); l.range = range; l.intensity = 0; l.shadows = LightShadows.None;
        return l;
    }

    // ───────────────────────────── 체험 카메라 경로: 숲길 → 세계수 둘레
    static void SetupCameraPath(WordSpace ws, float speed)
    {
        var ec = ws.experienceCamera;
        if (ec == null || ec.path == null) return;
        var path = ec.path;
        Undo.RecordObject(path, "카메라 경로");
        path.closed = false;
        for (int i = path.transform.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(path.transform.GetChild(i).gameObject);

        var pts = new List<Vector3>();
        for (float z = -16; z <= 72; z += 8) pts.Add(new Vector3(PathX(z), 1.75f, z));
        for (int i = 0; i <= 8; i++)
        {
            float a = -Mathf.PI / 2 - i / 8f * Mathf.PI * 1.1f;
            pts.Add(new Vector3(TREE.x + Mathf.Cos(a) * 28, 2 + i * .35f, TREE.z + Mathf.Sin(a) * 28));
        }
        for (int i = 0; i < pts.Count; i++)
        {
            var p = new GameObject($"경로점 {i + 1}");
            Undo.RegisterCreatedObjectUndo(p, "경로점");
            p.transform.SetParent(path.transform, false);
            p.transform.position = forest.TransformPoint(pts[i]);
        }
        Undo.RecordObject(ec, "체험 카메라");
        foreach (var f in ec.GetType().GetFields())   // 팀 도구 버전마다 속도 칸 이름이 달라서 찾아서 바꿈
            if (f.FieldType == typeof(float) && f.Name.ToLower().Contains("speed")) { f.SetValue(ec, speed); break; }
        path.Rebuild();
        ec.PlaceAtStart();
    }

    // ───────────────────────────── 자모 배치
    struct Spot { public Vector3 pos; public Vector3 euler; public float size; }

    static void PlaceJamo(WordSpace ws, Transform origin)
    {
        var spots = new[]
        {
            new Spot { pos = new Vector3(PathX(-4), 5f, -4),       size = 8  },                 // ㄱ 길 위로 걸친 고목
            new Spot { pos = new Vector3(PathX(10) - 4.5f, 6.5f, 10), size = 12 },              // ㅣ 곧은 고목
            new Spot { pos = new Vector3(PathX(25), .3f, 25), euler = new Vector3(90, 0, 0), size = 7 }, // ㄹ 길을 가로지른 뿌리
            new Spot { pos = new Vector3(PathX(42), 5.5f, 42),     size = 10 },                 // ㅊ 기대 선 두 나무
            new Spot { pos = new Vector3(PathX(54) + 4.5f, 6, 54), size = 11 },                 // ㅏ 가지 뻗은 나무
            new Spot { pos = new Vector3(PathX(65) - 6, 4.5f, 65), size = 8  },                 // ㄷ 통나무 틀
            new Spot { pos = new Vector3(PathX(76) + 4.5f, 6, 76), size = 10 },                 // ㅏ 공터 직전
        };
        var pieces = ws.Pieces.OrderBy(p => p.syllableIndex).ThenBy(p => (int)p.role).ToArray();
        if (pieces.Length == 0) { EditorUtility.DisplayDialog("길차다 숲", "자모가 없어요. 단어 공간 도구로 자모를 먼저 만들어 주세요.", "확인"); return; }

        Undo.SetCurrentGroupName("자모를 숲길 자리에 놓기");
        for (int i = 0; i < pieces.Length && i < spots.Length; i++)
        {
            var p = pieces[i]; var s = spots[i];
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var v in p.points ?? new Vector2[0]) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            float ext = Mathf.Max(max.x - min.x, max.y - min.y, .05f);
            float parentScale = p.transform.parent ? p.transform.parent.lossyScale.x : 1f;

            Undo.RecordObject(p.transform, "자모 배치");
            p.transform.position = origin.TransformPoint(s.pos);
            p.transform.rotation = Quaternion.Euler(s.euler);
            p.transform.localScale = Vector3.one * (s.size / ext / parentScale);
        }
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
        Debug.Log($"[길차다 숲] 자모 {Mathf.Min(pieces.Length, spots.Length)}개를 숲길 자리에 놓았어요.");
    }

    // ───────────────────────────── 도우미
    static Transform Group(string name) => Child(forest, name, Vector3.zero);

    static Transform Child(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot, Vector3 scale, bool shadow = true)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static void Sway(Transform t, float deg, float speed)
    {
        var s = t.gameObject.AddComponent<ForestSway>();
        s.amount = deg; s.speed = speed; s.phase = R(0, 6.28f);
    }

    static Quaternion Random3() => Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360));

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    // 머티리얼: 이미 있으면 그대로 사용 (직접 바꾼 색 유지)
    static Material Mat(string name, string hex, string emissionHex = null, float emission = 0)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            Object.DestroyImmediate(tmp);
            Color c = Hex(hex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .05f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .05f);
            if (emissionHex != null && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Hex(emissionHex) * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    // 메시: 각진 로우폴리 (면마다 법선 따로)
    static Mesh Flat(List<Vector3> tris)
    {
        var m = new Mesh();
        if (tris.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var idx = new int[tris.Count];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        m.SetVertices(tris);
        m.SetTriangles(idx, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // 메시: 부드러운 모양 (같은 자리 꼭짓점을 합쳐 법선을 고르게). flatFrom 이후 삼각형은 각지게 둠 (뚜껑 등)
    static Mesh Weld(List<Vector3> tris, int flatFrom)
    {
        var verts = new List<Vector3>();
        var idx = new int[tris.Count];
        var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < tris.Count; i++)
        {
            Vector3 p = tris[i];
            if (flatFrom >= 0 && i >= flatFrom) { idx[i] = verts.Count; verts.Add(p); continue; }
            var key = new Vector3Int(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.y * 1000), Mathf.RoundToInt(p.z * 1000));
            if (!map.TryGetValue(key, out int v)) { v = verts.Count; verts.Add(p); map[key] = v; }
            idx[i] = v;
        }
        var m = new Mesh();
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetTriangles(idx, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // 빛 번짐(Bloom) · 영화 같은 색감(ACES) · 살짝 어두운 가장자리 — 프로필은 숲 폴더에 저장돼서 직접 조절 가능
    static void SetupPostProcessing(WordSpace ws, Transform owner)
    {
        string path = $"{AssetDir}/길차다 후처리.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet(out T c)) return c;
            c = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
        // 아침 숲: 어둡게 누르지 않고, 은은한 빛 번짐 + 살짝 따뜻한 색
        var bloom = Get<Bloom>();
        bloom.threshold.Override(1.05f); bloom.intensity.Override(.7f); bloom.scatter.Override(.75f); bloom.tint.Override(Color.white);
        var tone = Get<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);
        var color = Get<ColorAdjustments>();
        color.postExposure.Override(.25f); color.contrast.Override(6f); color.saturation.Override(4f);
        var wb = Get<WhiteBalance>();
        wb.temperature.Override(6f); wb.tint.Override(-4f);
        var vig = Get<Vignette>();
        vig.intensity.Override(.32f); vig.smoothness.Override(.5f); vig.color.Override(new Color(.03f, .07f, .05f));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var t = owner.Find(PostName);
        Volume vol;
        if (t == null)
        {
            var go = new GameObject(PostName);
            Undo.RegisterCreatedObjectUndo(go, "후처리");
            go.transform.SetParent(owner, false);
            vol = go.AddComponent<Volume>();
        }
        else
        {
            vol = t.GetComponent<Volume>();
            if (vol == null) vol = Undo.AddComponent<Volume>(t.gameObject);
        }
        Undo.RecordObject(vol, "후처리");
        vol.isGlobal = true; vol.priority = 5; vol.sharedProfile = profile;

        var cams = new List<Camera>();
        if (ws.experienceCamera) cams.Add(ws.experienceCamera.Cam);
        if (ws.workCamera) cams.Add(ws.workCamera);
        foreach (var cam in cams)
        {
            Undo.RecordObject(cam, "카메라"); cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data) { Undo.RecordObject(data, "카메라"); data.renderPostProcessing = true; }
        }
    }

    // 아침 숲: 무성한 잎 그늘 + 아주 옅은 안개 + 청록 발광
    static void ApplyMorningLook(Transform owner)
    {
        var atmo = owner.GetComponentInChildren<ForestAtmosphere>(true);
        if (atmo)
        {
            Undo.RecordObject(atmo, "아침 숲");
            // 숲길: 잎 그늘로 주변은 어둡게 가라앉고, 길에 떨어지는 햇살로 시선이 모이게
            // 공터로 나서는 순간 확 바뀌도록 바뀌는 구간을 짧게 (공터 가장자리 z≈86)
            atmo.darkUntilZ = 84f; atmo.lightFromZ = 96f;
            atmo.fogDark = Hex("#1b2b24"); atmo.fogLight = Hex("#bcd8d0");
            atmo.densityDark = .026f; atmo.densityLight = .011f;
            atmo.ambientSky = Hex("#9cc2ae"); atmo.ambientEquator = Hex("#3f5d4a"); atmo.ambientGround = Hex("#1f2a1c");
            atmo.ambientDark = .4f; atmo.ambientLight = 1.3f;
            atmo.sunDark = 1.2f; atmo.sunLight = 1.9f;
            atmo.treeLightMax = 2f; atmo.spiritLightMax = 1.6f; atmo.warmLightMax = 5.5f;
            if (atmo.sun)
            {
                Undo.RecordObject(atmo.sun, "해"); Undo.RecordObject(atmo.sun.transform, "해");
                atmo.sun.color = Hex("#fff0cc");
                atmo.sun.shadows = LightShadows.Soft; atmo.sun.shadowStrength = .85f;
                atmo.sun.transform.rotation = Quaternion.Euler(50, 150, 0);
            }
            atmo.Apply(0, 0);
            EditorUtility.SetDirty(atmo);
        }
        // 땅은 어둡게, 숲길은 밝은 흙색 → 길이 눈에 들어옴
        void Recolor(string name, string hex)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{AssetDir}/Materials/{name}.mat");
            if (m == null) return;
            Undo.RecordObject(m, "색");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Hex(hex));
            if (m.HasProperty("_Color")) m.SetColor("_Color", Hex(hex));
            EditorUtility.SetDirty(m);
        }
        Recolor("땅", "#142219");
        Recolor("숲길", "#6a5f45");

        // 숲길에 떨어지는 햇살 (잎 사이로 내려오는 빛 웅덩이 + 옅은 빛줄기)
        var f = owner.Find(GroupName);
        if (f)
        {
            var old = f.Find("숲길 햇살");
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            var g = new GameObject("숲길 햇살").transform;
            Undo.RegisterCreatedObjectUndo(g.gameObject, "숲길 햇살");
            g.SetParent(f, false);
            var shaft = ShaftMaterial();
            foreach (float z in new[] { -8f, 17f, 33f, 48f, 60f, 71f })
            {
                var aim = new Vector3(PathX(z) + Random.Range(-.8f, .8f), H(PathX(z), z), z);
                var from = aim + new Vector3(-7, 30, -9);
                var lgo = new GameObject("햇살");
                lgo.transform.SetParent(g, false);
                lgo.transform.localPosition = from;
                lgo.transform.localRotation = Quaternion.LookRotation(aim - from);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot; l.color = Hex("#ffe2a6"); l.intensity = 2.2f; l.range = 45;
                l.spotAngle = 14; l.innerSpotAngle = 4; l.shadows = LightShadows.None;
                if (z != 17f && z != 60f) continue; // 빛줄기는 두 군데만 (빛 웅덩이는 모두)
                var cone = OpenCone();
                Part(g, "빛줄기", cone, shaft, aim, Quaternion.FromToRotation(Vector3.up, (from - aim).normalized), new Vector3(2.4f, 30, 2.4f), false);
            }
        }

        // 길가 버섯: 하얀 반짝이 → 차분한 청록빛
        var fungus = AssetDatabase.LoadAssetAtPath<Material>($"{AssetDir}/Materials/빛나는 버섯.mat");
        if (fungus)
        {
            Undo.RecordObject(fungus, "버섯");
            if (fungus.HasProperty("_BaseColor")) fungus.SetColor("_BaseColor", Hex("#2f524c"));
            if (fungus.HasProperty("_EmissionColor")) fungus.SetColor("_EmissionColor", Hex("#2ad4c4") * .7f);
            EditorUtility.SetDirty(fungus);
        }
    }

    static Mesh SaveMesh(string name, Mesh m)
    {
        m.name = name;
        string path = $"{AssetDir}/Meshes/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing)
        {
            existing.Clear();
            existing.indexFormat = m.indexFormat;
            existing.SetVertices(m.vertices);
            existing.SetTriangles(m.triangles, 0);
            existing.SetNormals(m.normals);
            if (m.uv != null && m.uv.Length == m.vertexCount) existing.SetUVs(0, m.uv);
            existing.RecalculateBounds();
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 바닥이 (0,0,0), 높이 1, 아래 반지름 r0, 위 반지름 r1 인 각기둥 / 각뿔
    static Mesh Frustum(string name, int sides, float r0, float r1)
    {
        if (meshes.TryGetValue(name, out var cached)) return cached;
        if (HQ) sides = Mathf.Max(14, sides * 3);
        var side = new List<Vector3>(); var caps = new List<Vector3>();
        for (int s = 0; s < sides; s++)
        {
            float a0 = Mathf.PI * 2 * s / sides, a1 = Mathf.PI * 2 * (s + 1) / sides;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0), b1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
            Vector3 t0 = new Vector3(Mathf.Cos(a0) * r1, 1, Mathf.Sin(a0) * r1), t1 = new Vector3(Mathf.Cos(a1) * r1, 1, Mathf.Sin(a1) * r1);
            side.AddRange(new[] { b0, t1, b1 });
            if (r1 > 0) { side.AddRange(new[] { b0, t0, t1 }); caps.AddRange(new[] { Vector3.up, t1, t0 }); }
            caps.AddRange(new[] { Vector3.zero, b0, b1 });
        }
        var tris = new List<Vector3>(side); tris.AddRange(caps);
        var m = SaveMesh(name, HQ ? Weld(tris, side.Count) : Flat(tris));
        meshes[name] = m;
        return m;
    }

    // 반지름 1 정이십면체
    static Mesh Ico()
    {
        const string name = "이십면체";
        if (meshes.TryGetValue(name, out var cached)) return cached;
        float t = (1 + Mathf.Sqrt(5)) / 2;
        var v = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        }.Select(x => x.normalized).ToArray();
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        var tris = new List<Vector3>();
        for (int i = 0; i < f.Length; i += 3) tris.AddRange(new[] { v[f[i]], v[f[i + 1]], v[f[i + 2]] });
        if (HQ)
        {
            for (int level = 0; level < 2; level++)
            {
                var next = new List<Vector3>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    Vector3 ab = ((a + b) * .5f).normalized, bc = ((b + c) * .5f).normalized, ca = ((c + a) * .5f).normalized;
                    next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                tris = next;
            }
            for (int i = 0; i < tris.Count; i++)
            {
                Vector3 p = tris[i];
                float bump = 1 + .07f * Mathf.Sin(p.x * 5.1f + p.y * 3.3f) * Mathf.Cos(p.z * 4.7f - p.y * 2.1f);
                tris[i] = p * bump;
            }
        }
        var m = SaveMesh(name, HQ ? Weld(tris, -1) : Flat(tris));
        meshes[name] = m;
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(GilchadaForest))]
class GilchadaForestInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var f = (GilchadaForest)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("① 숲 만들기 → ② 자모 놓기 → 저장(Ctrl+S) → 플레이\n만들어진 나무는 직접 옮기고 지우고 복사해도 돼요.", MessageType.Info);
        if (GUILayout.Button("① 숲 만들기 (카메라 경로 포함)", GUILayout.Height(32))) f.ButtonBuild();
        if (GUILayout.Button("② 자모를 숲길 자리에 놓기", GUILayout.Height(28))) f.ButtonPlaceJamo();
        EditorGUILayout.Space(4);
        if (GUILayout.Button("③ 세계수 공간만 다시 만들기", GUILayout.Height(28))) f.ButtonRebuildWorldTree();
        if (GUILayout.Button(f.highQuality ? "④ 고퀄 모드 적용 (부드러운 모양 + 빛 번짐)" : "④ 로우폴리로 되돌리기", GUILayout.Height(28))) f.ButtonHighQuality();
        if (GUILayout.Button("⑤ 숲길 빽빽하게 + 나뭇잎 디테일 + 공터 대비", GUILayout.Height(28))) f.ButtonDetail();
        if (GUILayout.Button("⑥ 다듬기 (빛줄기 줄이기 · 공터 나무 채우기 · 시작 부분 비우기)", GUILayout.Height(28))) f.ButtonPolish();
        if (GUILayout.Button("⑦ 시작 숲길 채우기 + 하늘 열림 타이밍", GUILayout.Height(28))) f.ButtonStartForest();
        if (GUILayout.Button("카메라 경로만 다시 숲길로", GUILayout.Height(24))) f.ButtonCameraPath();
    }
}
#endif
