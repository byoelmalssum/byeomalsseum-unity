using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// [살천스럽다] 공간 자동 생성 도구
// 쓰는 법: 살천스럽다_공간 씬 → '공간 (소품·조명 등 여기에)' 아래에 빈 오브젝트 → 이 스크립트 붙이기
//          → Inspector의 버튼을 누름. (플레이 중엔 안 눌림)
//  ① 빙원·설산   : 울퉁불퉁한 눈 땅, 눈 더미, 얼음 결정 무리, 둘레의 설산 언덕
//  ② 주변 소품   : 얼어붙은 나무, 서리 바위, 반쯤 묻힌 울타리, 얼음 기둥
//  ③ 고드름      : 자모의 가로획 아래에 고드름을 줄줄이
//  ④ 눈·빛·돌풍  : 눈 / 땅 눈보라 / 카메라 눈보라 / 반짝임 파티클, 후처리 2개, 찬바람 구역(ㄹ·ㄹ·ㄷ), 가로등 빛(ㅏ·ㅏ·ㅂ)
//  - 자모 배치는 절대 건드리지 않음. 자모가 놓인 자리를 읽어서 그 둘레를 채움.
//  - 버튼마다 자기 그룹만 새로 바꿈. 생성된 것도 손으로 옮기고 지울 수 있음. Ctrl+Z 가능.
//  - 생성 파일은 Assets/Words/살천스럽다/생성 폴더에 저장됨.
public class SalcheonSpace : MonoBehaviour
{
    [Header("공통")]
    [Tooltip("숫자를 바꾸고 다시 누르면 다른 배치가 나와요")] public int seed = 7;
    [Tooltip("자모·시작 지점 둘레로 얼마나 넓게 채울지 (m)")] public float margin = 30f;
    [Tooltip("자모 둘레와 지나갈 길은 이 거리(m)만큼 비워 둠")] public float clearance = 6f;
    [Tooltip("바람이 불어가는 방향 (눈·눈보라)")] public Vector3 windDirection = new Vector3(-1f, 0f, -0.3f);

    [Header("① 빙원·설산")]
    [Tooltip("땅의 울퉁불퉁함 (0이면 평평). 자모 밑과 지나갈 길은 늘 평평")] [Range(0f, 1f)] public float groundBumpiness = 0.6f;
    [Tooltip("땅 높이 조절 (m). 눕혀 둔 자모가 눈에 묻히면 조금 내리기 (예: -0.2)")] public float groundOffset = 0f;
    [Range(0f, 3f)] public float moundAmount = 1f;
    [Tooltip("얼음 결정 무리 양")] [Range(0f, 3f)] public float crystalAmount = 1f;
    [Tooltip("둘레 설산 언덕 개수")] [Range(0, 40)] public int hillCount = 18;
    public Vector2 hillHeight = new Vector2(15f, 45f);
    [Tooltip("공간 가장자리에서 언덕까지 거리 (m). 안개가 짙으면 가까이")] public float hillDistance = 20f;

    [Header("② 주변 소품")]
    [Range(0f, 3f)] public float treeAmount = 1f;
    [Range(0f, 3f)] public float rockAmount = 1f;
    [Range(0f, 3f)] public float fenceAmount = 1f;
    [Range(0f, 3f)] public float pillarAmount = 1f;

    [Header("③ 고드름")]
    [Tooltip("고드름 크기 배수")] public float icicleSize = 1f;
    [Tooltip("고드름 사이 간격 (m)")] [Range(0.25f, 2f)] public float icicleSpacing = 0.55f;
    [Tooltip("이 높이(m)보다 낮은 획 아래엔 안 달림 (바닥에 눕힌 자모 제외)")] public float icicleMinHeight = 1.2f;
    public int icicleMax = 1500;

    [Header("④ 눈·눈보라·빛")]
    [Range(0f, 2f)] public float snowAmount = 1f;
    [Range(0f, 2f)] public float blizzardAmount = 1f;
    public bool makeLamps = true;

#if UNITY_EDITOR
    public void Build(int which) => SalcheonBuilder.Run(this, which);
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SalcheonSpace))]
public class SalcheonSpaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (SalcheonSpace)target;
        EditorGUILayout.HelpBox("자모 배치는 건드리지 않고, 자모가 놓인 자리 둘레를 채워요.\n버튼마다 자기 그룹만 새로 바뀌고, Ctrl+Z로 되돌릴 수 있어요.\n누른 다음 Ctrl+S로 저장하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("① 빙원·설산 만들기", GUILayout.Height(26))) s.Build(1);
            if (GUILayout.Button("② 주변 소품 뿌리기", GUILayout.Height(26))) s.Build(2);
            if (GUILayout.Button("③ 고드름 달기", GUILayout.Height(26))) s.Build(3);
            if (GUILayout.Button("④ 눈·눈보라·후처리·돌풍·가로등", GUILayout.Height(26))) s.Build(4);
            EditorGUILayout.Space(4);
            if (GUILayout.Button("전부 한 번에 (①~④)", GUILayout.Height(30))) s.Build(0);
            if (GUILayout.Button("생성물 모두 지우기")) s.Build(-1);
        }
        EditorGUILayout.Space(8);
        DrawDefaultInspector();
    }
}

public static class SalcheonBuilder
{
    const string Dir = "Assets/Words/살천스럽다/생성";
    const string MatDir = Dir + "/Materials";
    const string MeshDir = Dir + "/Meshes";
    const string OwnMatDir = "Assets/Words/살천스럽다/Materials";
    const string GenName = "살천 생성물";
    const string BgName = "살천스럽다 배경 (이동 범위 밖)";

    // ───────────────────────────── 준비
    class Ctx
    {
        public SalcheonSpace s;
        public WordSpace ws;
        public Transform gen, bg;
        public Rect area;            // 채울 범위 (XZ)
        public Vector2 center;
        public List<Bounds> jamo = new List<Bounds>();
        public List<Vector3> path = new List<Vector3>();
        public Vector3 start;
        public float baseY, topY;
        public System.Random rng;
        public float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public int RI(int a, int b) => rng.Next(a, b);
    }

    public static void Run(SalcheonSpace s, int which)
    {
        if (Application.isPlaying) return;
        var ws = Object.FindAnyObjectByType<WordSpace>();
        if (ws == null) { EditorUtility.DisplayDialog("살천스럽다", "이 씬에 단어 공간이 없어요. 살천스럽다_공간 씬에서 눌러 주세요.", "확인"); return; }

        string[] names = { "①~④ 전부", "① 빙원·설산", "② 주변 소품", "③ 고드름", "④ 눈·눈보라·후처리·돌풍·가로등" };
        string what = which >= 0 && which < names.Length ? names[which] : "생성물 모두 지우기";
        string msg = which == -1 ? "자동으로 만든 것을 전부 지워요. (자모는 그대로)\nCtrl+Z로 되돌릴 수 있어요."
                                 : $"{what}을(를) 만들어요.\n이미 만든 게 있으면 그 그룹만 새로 바뀌어요. 자모는 그대로예요.\n(Ctrl+Z로 되돌리기 가능)";
        if (!EditorUtility.DisplayDialog("살천스럽다", msg, "실행", "취소")) return;

        Undo.SetCurrentGroupName("살천스럽다 " + what);
        int undoGroup = Undo.GetCurrentGroup();

        var c = Prepare(s, ws);
        if (which == -1)
        {
            if (c.gen) Undo.DestroyObjectImmediate(c.gen.gameObject);
            if (c.bg) Undo.DestroyObjectImmediate(c.bg.gameObject);
            var floor = FindOldFloor(ws);
            if (floor && !floor.gameObject.activeSelf) { Undo.RecordObject(floor.gameObject, "바닥"); floor.gameObject.SetActive(true); }
        }
        else
        {
            EnsureFolder(Dir); EnsureFolder(MatDir); EnsureFolder(MeshDir);
            EnsureRoots(c);
            if (which == 1 || which == 0) BuildField(c);
            if (which == 2 || which == 0) BuildProps(c);
            if (which == 3 || which == 0) BuildIcicles(c);
            if (which == 4 || which == 0) BuildAtmosphere(c);
            EnsureRuntime(c);
            AssetDatabase.SaveAssets();
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
        Debug.Log($"[살천스럽다] {what} 완료. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    static Ctx Prepare(SalcheonSpace s, WordSpace ws)
    {
        var c = new Ctx { s = s, ws = ws, rng = new System.Random(s.seed) };
        var spaceRoot = ws.transform.Find("공간 (소품·조명 등 여기에)") ?? ws.transform;
        c.gen = spaceRoot.Find(GenName);
        var bgGo = GameObject.Find(BgName);
        c.bg = bgGo ? bgGo.transform : null;

        var all = new Bounds();
        bool any = false;
        var centers = new List<Vector3>();
        foreach (var p in ws.Pieces)
        {
            if (!p || !p.visual) continue;
            var r = p.visual.GetComponent<Renderer>();
            if (!r) continue;
            c.jamo.Add(r.bounds);
            centers.Add(r.bounds.center);
            if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
        }
        var ec = ws.experienceCamera;
        c.start = ec ? (ec.startPoint ? ec.startPoint.position : ec.transform.position) : ws.transform.position;
        if (!any) all = new Bounds(c.start, Vector3.one * 20f);
        all.Encapsulate(c.start);

        // 지나갈 길: 시작 지점 → 가장 가까운 자모 → … (가까운 순서로 잇기)
        c.path.Add(c.start);
        var left = new List<Vector3>(centers);
        Vector3 cur = c.start;
        while (left.Count > 0)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < left.Count; i++)
            {
                float d = (Flat(left[i]) - Flat(cur)).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            cur = left[best]; left.RemoveAt(best);
            c.path.Add(cur);
        }

        var oldFloor = FindOldFloor(ws);
        c.baseY = (oldFloor ? oldFloor.position.y : ws.floorHeight) + s.groundOffset;
        c.topY = Mathf.Max(25f, all.max.y + 5f);

        float m = Mathf.Max(5f, s.margin);
        c.area = Rect.MinMaxRect(all.min.x - m, all.min.z - m, all.max.x + m, all.max.z + m);
        c.center = c.area.center;
        return c;
    }

    static void EnsureRoots(Ctx c)
    {
        if (!c.gen)
        {
            var spaceRoot = c.ws.transform.Find("공간 (소품·조명 등 여기에)") ?? c.ws.transform;
            var go = new GameObject(GenName);
            Undo.RegisterCreatedObjectUndo(go, "생성물");
            go.transform.SetParent(spaceRoot, false);
            go.transform.localPosition = Vector3.zero;
            c.gen = go.transform;
        }
        if (!c.bg)
        {
            // 땅·언덕은 [별말씀] 바깥에 둬서 이동 범위 계산에 안 들어가게 함 (언덕까지 날아가지 않게)
            var go = new GameObject(BgName);
            Undo.RegisterCreatedObjectUndo(go, "배경");
            SceneManager_MoveToScene(go, c.ws.gameObject.scene);
            c.bg = go.transform;
        }
    }

    static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    static Transform Group(Transform parent, string name)
    {
        var old = parent.Find(name);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform FindOldFloor(WordSpace ws) => ws.transform.Find("공간 (소품·조명 등 여기에)/바닥 (지워도 됨)");

    // ───────────────────────────── 땅 높이 · 빈자리
    static float H(Ctx c, float x, float z)
    {
        float amp = 0.4f * c.s.groundBumpiness;
        if (amp <= 0) return c.baseY;
        float ox = c.s.seed * 13.1f, oz = c.s.seed * 7.7f;
        float h = Mathf.PerlinNoise(x * 0.045f + ox, z * 0.045f + oz) * 0.75f
                + Mathf.PerlinNoise(x * 0.17f + oz, z * 0.17f + ox) * 0.25f;
        // 바람에 깎인 눈결 (바람 방향과 수직인 잔물결)
        Vector3 w = c.s.windDirection; w.y = 0; if (w.sqrMagnitude < 1e-4f) w = Vector3.left; w.Normalize();
        h += 0.18f * (0.5f + 0.5f * Mathf.Sin((x * w.x + z * w.z) * 1.3f + Mathf.PerlinNoise(x * 0.1f, z * 0.1f) * 4f));
        // 지나가는 길은 평평하게
        float dPath = DistToPath(c, x, z);
        float flat = Mathf.SmoothStep(0.3f, 1f, Mathf.InverseLerp(c.s.clearance * 0.5f, c.s.clearance * 1.5f, dPath));
        // 자모 밑(눕혀 둔 ㄹ 같은 것)은 완전히 평평하게 → 땅에 묻히지 않음
        float dJ = DistToJamo(c, x, z);
        flat = Mathf.Min(flat, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 4f, dJ)));
        return c.baseY + h / 1.18f * amp * flat;
    }

    static float DistToPath(Ctx c, float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < c.path.Count; i++)
        {
            Vector2 a = Flat(c.path[i]), b = Flat(c.path[i + 1]);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        if (c.path.Count == 1) best = Vector2.Distance(p, Flat(c.path[0]));
        return best;
    }

    static float DistToJamo(Ctx c, float x, float z)
    {
        float best = float.MaxValue;
        foreach (var b in c.jamo)
        {
            float dx = Mathf.Max(b.min.x - x, 0f, x - b.max.x);
            float dz = Mathf.Max(b.min.z - z, 0f, z - b.max.z);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    static bool Blocked(Ctx c, float x, float z, float extra)
    {
        float cl = c.s.clearance + extra;
        if (DistToPath(c, x, z) < cl) return true;
        foreach (var b in c.jamo)
        {
            if (x > b.min.x - cl && x < b.max.x + cl && z > b.min.z - cl && z < b.max.z + cl) return true;
        }
        return false;
    }

    static bool TryPoint(Ctx c, float extra, out Vector3 p, Rect? region = null)
    {
        Rect r = region ?? c.area;
        for (int i = 0; i < 40; i++)
        {
            float x = c.R(r.xMin, r.xMax), z = c.R(r.yMin, r.yMax);
            if (Blocked(c, x, z, extra)) continue;
            p = new Vector3(x, H(c, x, z), z);
            return true;
        }
        p = Vector3.zero;
        return false;
    }

    static int Count(Ctx c, float per1000, float amount)
    {
        float a = c.area.width * c.area.height / 1000f;
        return Mathf.Clamp(Mathf.RoundToInt(a * per1000 * amount), 0, 400);
    }

    // ───────────────────────────── ① 빙원·설산
    static void BuildField(Ctx c)
    {
        var snowMat = OwnOrNew("눈바닥", new Color32(223, 230, 237, 255), 0f, 0.05f);
        var iceMat = OwnOrNew("얼음", new Color32(168, 216, 240, 204), 0.1f, 0.9f);
        var rockMat = Mat("산 바위", new Color32(74, 90, 107, 255), 0f, 0.2f);
        var capMat = Mat("눈 덮개", new Color32(232, 238, 244, 255), 0f, 0.15f);

        // 땅
        var groundG = Group(c.bg, "빙원 땅");
        var gr = c.area; float pad = 60f;
        var ground = GroundMesh(c, Rect.MinMaxRect(gr.xMin - pad, gr.yMin - pad, gr.xMax + pad, gr.yMax + pad));
        ground = SaveMesh("빙원 땅", ground, true);
        var gGo = Obj(groundG, "땅", ground, snowMat);
        gGo.GetComponent<MeshRenderer>().receiveShadows = true;
        var floor = FindOldFloor(c.ws);
        if (floor && floor.gameObject.activeSelf) { Undo.RecordObject(floor.gameObject, "바닥 끄기"); floor.gameObject.SetActive(false); }

        // 눈 더미
        var sphere = BuiltinMesh(PrimitiveType.Sphere);
        var moundG = Group(c.gen, "눈 더미");
        int nm = Count(c, 0.6f, c.s.moundAmount);
        for (int i = 0; i < nm; i++)
        {
            if (!TryPoint(c, 4f, out var p)) continue;
            float r = c.R(3f, 9f);
            var t = Obj(moundG, "눈 더미", sphere, snowMat).transform;
            t.position = p + Vector3.down * r * 0.04f;
            t.localScale = new Vector3(r, r * c.R(0.12f, 0.22f), r * c.R(0.6f, 0.95f));
            t.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        }

        // 얼음 결정 무리 (밑동 기준으로 자라남)
        var crystal = SaveMesh("얼음 결정", CrystalMesh(), false);
        var crysG = Group(c.gen, "얼음 결정 무리");
        int nc = Count(c, 1.2f, c.s.crystalAmount);
        for (int i = 0; i < nc; i++)
        {
            if (!TryPoint(c, 1.5f, out var p)) continue;
            var cluster = new GameObject("결정 무리").transform;
            cluster.SetParent(crysG, false);
            cluster.position = p;
            cluster.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
            float size = c.R(0.6f, 1.6f);
            int k = c.RI(3, 8);
            for (int j = 0; j < k; j++)
            {
                var t = Obj(cluster, "결정", crystal, iceMat).transform;
                float a = c.R(0, 360) * Mathf.Deg2Rad, d = c.R(0f, 1.1f) * size;
                t.localPosition = new Vector3(Mathf.Cos(a) * d, -0.15f, Mathf.Sin(a) * d);
                float h = c.R(0.6f, 2.8f) * size, w = c.R(0.22f, 0.55f) * size;
                t.localScale = new Vector3(w, h, w);
                t.localRotation = Quaternion.Euler(c.R(-28f, 28f), c.R(0, 360), c.R(-28f, 28f));
            }
        }

        // 둘레 설산 언덕 (배경, 이동 범위 밖)
        var hillG = Group(c.bg, "설산 언덕");
        var hills = new Mesh[3];
        for (int v = 0; v < 3; v++) hills[v] = SaveMesh("언덕" + (v + 1), HillMesh(1000 + v * 77 + c.s.seed), false);
        // 공간 가장자리(사각형)를 한 바퀴 돌며 바깥쪽에 배치
        Rect ring = Rect.MinMaxRect(c.area.xMin - c.s.hillDistance, c.area.yMin - c.s.hillDistance, c.area.xMax + c.s.hillDistance, c.area.yMax + c.s.hillDistance);
        float per = 2f * (ring.width + ring.height);
        for (int i = 0; i < c.s.hillCount; i++)
        {
            float u = (i + c.R(-0.35f, 0.35f)) / Mathf.Max(1, c.s.hillCount) * per;
            u = Mathf.Repeat(u, per);
            Vector2 q, outward;
            if (u < ring.width) { q = new Vector2(ring.xMin + u, ring.yMin); outward = new Vector2(0, -1); }
            else if (u < ring.width + ring.height) { q = new Vector2(ring.xMax, ring.yMin + u - ring.width); outward = new Vector2(1, 0); }
            else if (u < 2f * ring.width + ring.height) { q = new Vector2(ring.xMax - (u - ring.width - ring.height), ring.yMax); outward = new Vector2(0, 1); }
            else { q = new Vector2(ring.xMin, ring.yMax - (u - 2f * ring.width - ring.height)); outward = new Vector2(-1, 0); }
            q += outward * c.R(0f, 30f);
            Vector3 pos = new Vector3(q.x, c.baseY, q.y);
            float h = c.R(c.s.hillHeight.x, c.s.hillHeight.y), rad = h * c.R(1.1f, 1.8f);
            var mesh = hills[c.RI(0, 3)];
            var hill = Obj(hillG, "언덕", mesh, rockMat).transform;
            hill.position = pos + Vector3.down * 0.5f;
            hill.localScale = new Vector3(rad, h, rad * c.R(0.7f, 1.1f));
            hill.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
            var cap = Obj(hill, "눈 덮개", mesh, capMat).transform;
            cap.localPosition = new Vector3(0, 0.56f, 0);
            cap.localScale = new Vector3(0.5f, 0.46f, 0.5f);
            cap.localRotation = Quaternion.identity;
        }
    }

    // ───────────────────────────── ② 주변 소품
    static void BuildProps(Ctx c)
    {
        var barkMat = Mat("앙상한 나무", new Color32(43, 49, 56, 255), 0f, 0.25f);
        var rockMat = Mat("서리 바위", new Color32(94, 107, 120, 255), 0f, 0.25f);
        var iceMat = OwnOrNew("얼음", new Color32(168, 216, 240, 204), 0.1f, 0.9f);
        var cyl = BuiltinMesh(PrimitiveType.Cylinder);
        var cube = BuiltinMesh(PrimitiveType.Cube);
        var sphere = BuiltinMesh(PrimitiveType.Sphere);
        var crystal = SaveMesh("얼음 결정", CrystalMesh(), false);

        // 얼어붙은 나무
        var treeG = Group(c.gen, "얼어붙은 나무");
        int nt = Count(c, 0.35f, c.s.treeAmount);
        for (int i = 0; i < nt; i++)
        {
            if (!TryPoint(c, 3f, out var p)) continue;
            var tree = new GameObject("얼어붙은 나무").transform;
            tree.SetParent(treeG, false);
            tree.position = p;
            tree.rotation = Quaternion.Euler(c.R(-6f, 6f), c.R(0, 360), c.R(-6f, 6f));
            float h = c.R(4f, 9f), w = c.R(0.18f, 0.35f);
            var trunk = Obj(tree, "줄기", cyl, barkMat).transform;
            trunk.localPosition = new Vector3(0, h * 0.5f, 0);
            trunk.localScale = new Vector3(w, h * 0.5f, w);
            int nb = c.RI(4, 8);
            for (int b = 0; b < nb; b++)
            {
                float y = h * c.R(0.4f, 0.92f), bl = h * c.R(0.18f, 0.38f) * (1.1f - y / h);
                var dir = Quaternion.Euler(c.R(-60f, -30f), c.R(0, 360), 0) * Vector3.forward;
                dir = Quaternion.AngleAxis(-90f, Vector3.Cross(dir, Vector3.up).normalized) * dir; // 위쪽으로 비스듬히
                var br = Obj(tree, "가지", cyl, barkMat).transform;
                Vector3 basePos = new Vector3(0, y, 0);
                br.localPosition = basePos + dir.normalized * bl * 0.5f;
                br.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                br.localScale = new Vector3(w * 0.4f, bl * 0.5f, w * 0.4f);
            }
        }

        // 서리 바위
        var rockG = Group(c.gen, "서리 바위");
        int nr = Count(c, 0.8f, c.s.rockAmount);
        for (int i = 0; i < nr; i++)
        {
            if (!TryPoint(c, 1f, out var p)) continue;
            float s = c.R(0.6f, 2.6f);
            var t = Obj(rockG, "바위", sphere, rockMat).transform;
            float ry = s * c.R(0.35f, 0.7f);
            t.position = p + Vector3.down * ry * 0.3f;
            t.localScale = new Vector3(s * c.R(0.8f, 1.3f), ry, s * c.R(0.8f, 1.3f));
            t.rotation = Quaternion.Euler(c.R(-15f, 15f), c.R(0, 360), c.R(-15f, 15f));
        }

        // 반쯤 묻힌 울타리
        var fenceG = Group(c.gen, "묻힌 울타리");
        int nf = Mathf.Max(c.s.fenceAmount > 0 ? 1 : 0, Count(c, 0.08f, c.s.fenceAmount));
        for (int i = 0; i < nf; i++)
        {
            if (!TryPoint(c, 6f, out var p)) continue;
            var line = new GameObject("울타리").transform;
            line.SetParent(fenceG, false);
            line.position = p;
            line.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
            int posts = c.RI(5, 10);
            Vector3 prev = Vector3.zero; bool hasPrev = false;
            for (int k = 0; k < posts; k++)
            {
                float lx = k * c.R(1.6f, 2.1f);
                Vector3 wp = line.TransformPoint(new Vector3(lx, 0, 0));
                float gy = H(c, wp.x, wp.z) - line.position.y;
                float ph = c.R(1.1f, 1.7f), sink = c.R(0.2f, 0.7f);
                var post = Obj(line, "말뚝", cube, barkMat).transform;
                post.localPosition = new Vector3(lx, gy + ph * 0.5f - sink, 0);
                post.localScale = new Vector3(0.16f, ph, 0.16f);
                post.localRotation = Quaternion.Euler(c.R(-12f, 12f), 0, c.R(-12f, 12f));
                Vector3 top = post.localPosition + Vector3.up * (ph * 0.5f - 0.25f);
                if (hasPrev && c.R(0, 1) > 0.3f)
                {
                    var rail = Obj(line, "가로대", cube, barkMat).transform;
                    Vector3 mid = (prev + top) * 0.5f, d = top - prev;
                    rail.localPosition = mid;
                    rail.localRotation = Quaternion.FromToRotation(Vector3.right, d.normalized);
                    rail.localScale = new Vector3(d.magnitude, 0.09f, 0.07f);
                }
                prev = top; hasPrev = true;
            }
        }

        // 얼음 기둥 (밑동 기준으로 자라남)
        var pillarG = Group(c.gen, "얼음 기둥");
        int np = Count(c, 0.15f, c.s.pillarAmount);
        for (int i = 0; i < np; i++)
        {
            if (!TryPoint(c, 3f, out var p)) continue;
            var t = Obj(pillarG, "얼음 기둥", crystal, iceMat).transform;
            t.position = p + Vector3.down * 0.3f;
            float h = c.R(5f, 12f), w = c.R(1.2f, 2.6f);
            t.localScale = new Vector3(w, h, w);
            t.rotation = Quaternion.Euler(c.R(-12f, 12f), c.R(0, 360), c.R(-12f, 12f));
        }
    }

    // ───────────────────────────── ③ 고드름
    static void BuildIcicles(Ctx c)
    {
        var icicleMat = OwnOrNew("고드름", new Color32(216, 240, 255, 217), 0.1f, 0.95f);
        var mesh = SaveMesh("고드름", IcicleMesh(), false);
        var g = Group(c.gen, "고드름");
        float th = c.ws.strokeThickness, dp = c.ws.strokeDepth;
        int total = 0;

        foreach (var p in c.ws.Pieces)
        {
            if (!p || p.points == null) continue;
            var tr = p.transform;
            foreach (var stroke in p.Strokes)
            {
                for (int i = 0; i + 1 < stroke.Length; i++)
                {
                    Vector2 a2 = stroke[i] - p.pivot, b2 = stroke[i + 1] - p.pivot;
                    Vector3 A = tr.TransformPoint(new Vector3(a2.x, a2.y, 0));
                    Vector3 B = tr.TransformPoint(new Vector3(b2.x, b2.y, 0));
                    Vector3 d = B - A;
                    float len = d.magnitude;
                    if (len < 0.8f || Mathf.Abs(d.normalized.y) > 0.3f) continue; // 가로에 가까운 획만

                    // 획의 아랫면: 두께 방향·깊이 방향 중 더 아래를 향하는 쪽
                    Vector2 ld = (b2 - a2).normalized;
                    Vector3 n = tr.TransformVector(new Vector3(-ld.y, ld.x, 0) * (th * 0.5f));
                    Vector3 depthV = tr.TransformVector(new Vector3(0, 0, dp * 0.5f));
                    if (n.y > 0) n = -n;
                    if (depthV.y > 0) depthV = -depthV;
                    if (depthV.normalized.y < n.normalized.y) { var tmp = n; n = depthV; depthV = tmp; } // 아래쪽 = n, 옆으로 펼칠 쪽 = depthV

                    float step = Mathf.Max(0.2f, c.s.icicleSpacing);
                    int cnt = Mathf.Min(120, Mathf.FloorToInt(len / step));
                    for (int k = 0; k < cnt && total < c.s.icicleMax; k++)
                    {
                        float t = (k + c.R(0.2f, 0.8f)) / Mathf.Max(1, cnt);
                        Vector3 pos = A + d * t + n + depthV * c.R(-0.8f, 0.8f);
                        if (pos.y < c.baseY + c.s.icicleMinHeight) continue;
                        if (c.R(0, 1) < 0.15f) continue; // 군데군데 빈 곳
                        float L = c.R(0.3f, 1.9f) * c.s.icicleSize;
                        L = Mathf.Min(L, pos.y - c.baseY - 0.3f);
                        if (L < 0.15f) continue;
                        var ic = Obj(g, "고드름", mesh, icicleMat).transform;
                        ic.position = pos;
                        float r = Mathf.Clamp(L * 0.09f, 0.04f, 0.22f);
                        ic.localScale = new Vector3(r * 2f, L, r * 2f);
                        ic.rotation = Quaternion.Euler(c.R(-4f, 4f), c.R(0, 360), c.R(-4f, 4f));
                        total++;
                    }
                }
            }
        }
        if (total == 0) Debug.Log("[살천스럽다] 고드름을 달 만한 가로획이 없었어요. (높이가 Icicle Min Height보다 낮거나 세로획뿐)");
        else Debug.Log($"[살천스럽다] 고드름 {total}개");
    }

    // ───────────────────────────── ④ 눈·눈보라·후처리·돌풍·가로등
    static void BuildAtmosphere(Ctx c)
    {
        var particles = Group(c.gen, "파티클");
        Vector3 w = c.s.windDirection; w.y = 0; if (w.sqrMagnitude < 1e-4f) w = Vector3.left; w.Normalize();
        float areaK = Mathf.Clamp(c.area.width * c.area.height / 9000f, 0.3f, 8f);
        var snowMat = ParticleMat("눈입자", new Color(0.96f, 0.98f, 1f, 1f), Color.black);
        var glintMat = ParticleMat("반짝임", new Color(0.8f, 0.93f, 1f, 1f), new Color(0.8f, 0.93f, 1f) * 2.5f);
        Vector3 ctr = new Vector3(c.center.x, 0, c.center.y);

        // 눈: 자모 꼭대기 높이까지 공간 전체에 (중간에서 생겨나 부드럽게 나타남)
        float hK = c.topY / 25f;
        var snow = Particles(particles, "눈", ctr + Vector3.up * (c.baseY + c.topY * 0.5f), new Vector3(c.area.width, c.topY, c.area.height),
            life: 12f, rate: 160f * areaK * hK * c.s.snowAmount, size: new Vector2(0.05f, 0.13f),
            vel: new Vector3(w.x * 2f, -1.2f, w.z * 2f), mat: snowMat, stretch: false, noise: true, alpha: 1f, sizeCurve: false, fade: true);
        var drift = Particles(particles, "땅 눈보라", ctr + Vector3.up * (c.baseY + 0.8f), new Vector3(c.area.width, 1.5f, c.area.height),
            life: 4f, rate: 250f * areaK * c.s.snowAmount, size: new Vector2(0.02f, 0.04f),
            vel: new Vector3(w.x * 6.3f, 0f, w.z * 6.3f), mat: snowMat, stretch: true, noise: false, alpha: 0.35f, sizeCurve: false, fade: true);
        Particles(particles, "얼음 반짝임", ctr + Vector3.up * (c.baseY + 4.5f), new Vector3(c.area.width * 0.8f, 9f, c.area.height * 0.8f),
            life: 1.5f, rate: 120f * areaK, size: new Vector2(0.05f, 0.25f),
            vel: Vector3.zero, mat: glintMat, stretch: false, noise: false, alpha: 1f, sizeCurve: true, fade: false);
        var bliz = Particles(particles, "카메라 눈보라", c.start, new Vector3(30f, 8f, 30f),
            life: 2.5f, rate: 350f * c.s.blizzardAmount, size: new Vector2(0.02f, 0.06f),
            vel: new Vector3(w.x * 9f, -1f, w.z * 9f), mat: snowMat, stretch: true, noise: false, alpha: 0.6f, sizeCurve: false, fade: true);

        // 후처리 2개
        var post = Group(c.gen, "후처리");
        var mainProfile = Profile("살천 후처리", p =>
        {
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var b = p.Add<Bloom>(true); b.threshold.Override(0.85f); b.intensity.Override(0.5f); b.scatter.Override(0.55f);
            var v = p.Add<Vignette>(true); v.color.Override(new Color32(15, 25, 40, 255)); v.intensity.Override(0.38f); v.smoothness.Override(0.4f);
            p.Add<ColorAdjustments>(true).saturation.Override(-20f);
            p.Add<WhiteBalance>(true).temperature.Override(-30f);
        });
        var gustProfile = Profile("살천 돌풍 후처리", p =>
        {
            var v = p.Add<Vignette>(true); v.color.Override(new Color32(230, 242, 255, 255)); v.intensity.Override(0.55f); v.smoothness.Override(1f);
        });
        var mainVol = new GameObject("후처리 (차갑게)").AddComponent<Volume>();
        mainVol.transform.SetParent(post, false); mainVol.isGlobal = true; mainVol.sharedProfile = mainProfile; mainVol.priority = 0; mainVol.weight = 1f;
        var gustVol = new GameObject("돌풍 후처리 (Weight 0)").AddComponent<Volume>();
        gustVol.transform.SetParent(post, false); gustVol.isGlobal = true; gustVol.sharedProfile = gustProfile; gustVol.priority = 1; gustVol.weight = 0f;
        foreach (var cam in new[] { c.ws.experienceCamera ? c.ws.experienceCamera.Cam : null, c.ws.workCamera })
        {
            if (!cam) continue;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data) { Undo.RecordObject(data, "후처리 켜기"); data.renderPostProcessing = true; }
        }

        // 찬바람 구역: ㄹ · ㄹ · ㄷ 자리 (없으면 자모 3개를 고름)
        var zonesG = Group(c.gen, "찬바람 구역");
        var picks = new List<JamoPiece>();
        foreach (var p in c.ws.Pieces) if (p && (p.jamo == "ㄹ" || p.jamo == "ㄷ")) picks.Add(p);
        if (picks.Count == 0) foreach (var p in c.ws.Pieces) { if (p && picks.Count < 3) picks.Add(p); }
        int zi = 1;
        foreach (var jp in picks)
        {
            var r = jp.visual ? jp.visual.GetComponent<Renderer>() : null;
            if (!r) continue;
            var b = r.bounds;
            var z = new GameObject($"찬바람 구역 {zi++} ({jp.syllable} {jp.jamo})").AddComponent<ColdGustZone>();
            z.transform.SetParent(zonesG, false);
            z.transform.position = new Vector3(b.center.x, c.baseY, b.center.z);
            z.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.6f, 4f, 20f);
            z.snow = snow; z.groundDrift = drift; z.blizzard = bliz; z.gustVolume = gustVol;
        }

        // 가로등 빛: ㅏ 짧은 획 끝 / ㅂ 안쪽
        var lights = new List<Light>();
        var lampG = Group(c.gen, "가로등 빛");
        if (c.s.makeLamps)
        {
            var bulbMat = OwnOrNew("가로등 빛", new Color32(191, 228, 255, 255), 0f, 0.5f, new Color(0.75f, 0.9f, 1f) * 2f);
            var sphere = BuiltinMesh(PrimitiveType.Sphere);
            foreach (var p in c.ws.Pieces)
            {
                if (!p || !p.visual) continue;
                var r = p.visual.GetComponent<Renderer>();
                if (!r) continue;
                float size = r.bounds.size.magnitude;
                if (p.jamo == "ㅏ")
                {
                    var strokes = p.Strokes;
                    if (strokes.Count < 2) continue;
                    // 짧은 획 = 오른쪽으로 뻗은 팔. 기둥에서 먼 쪽 끝
                    int shortI = 0; float shortL = float.MaxValue;
                    for (int i = 0; i < strokes.Count; i++)
                    {
                        float L = (strokes[i][strokes[i].Length - 1] - strokes[i][0]).magnitude;
                        if (L < shortL) { shortL = L; shortI = i; }
                    }
                    var s = strokes[shortI];
                    Vector2 tip = s[0].x > s[s.Length - 1].x ? s[0] : s[s.Length - 1];
                    Vector3 wp = p.transform.TransformPoint(new Vector3(tip.x - p.pivot.x, tip.y - p.pivot.y, 0));
                    Vector3 down = -p.transform.up * (c.ws.strokeThickness * p.transform.lossyScale.y * 0.5f + 0.4f);
                    lights.Add(Lamp(lampG, $"가로등 ({p.syllable} ㅏ)", wp + down, Mathf.Clamp(size * 0.012f, 0.3f, 1.4f), Mathf.Clamp(size * 0.25f, 8f, 30f), 3f, new Color32(191, 228, 255, 255), sphere, bulbMat));
                }
                else if (p.jamo == "ㅂ")
                {
                    lights.Add(Lamp(lampG, "갇힌 빛 (ㅂ)", r.bounds.center, Mathf.Clamp(size * 0.015f, 0.4f, 1.6f), Mathf.Clamp(size * 0.18f, 6f, 20f), 1.5f, new Color32(159, 216, 255, 255), sphere, bulbMat));
                }
            }
        }

        var amb = c.gen.GetComponent<SalcheonAmbience>() ?? Undo.AddComponent<SalcheonAmbience>(c.gen.gameObject);
        Undo.RecordObject(amb, "분위기");
        amb.blizzard = bliz.transform;
        amb.flickerLights = lights.ToArray();
        amb.blizzardOffset = new Vector3(-w.x * 6f, 1f, -w.z * 6f);
    }

    static Light Lamp(Transform parent, string name, Vector3 pos, float bulb, float range, float intensity, Color color, Mesh sphere, Material bulbMat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var b = Obj(go.transform, "전구", sphere, bulbMat);
        b.transform.localPosition = Vector3.zero;
        b.transform.localScale = Vector3.one * bulb;
        b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
        return l;
    }

    // 자라날 것들을 SalcheonFrost에 연결 (처음엔 숨겼다가 글자 도착 후 자라남)
    static void EnsureRuntime(Ctx c)
    {
        if (!c.gen) return;
        var f = c.gen.GetComponent<SalcheonFrost>() ?? Undo.AddComponent<SalcheonFrost>(c.gen.gameObject);
        Undo.RecordObject(f, "서리 연출");
        var list = new List<Transform>();
        foreach (var n in new[] { "고드름", "얼음 결정 무리", "얼음 기둥" })
        {
            var t = c.gen.Find(n);
            if (t) list.Add(t);
        }
        f.growGroups = list.ToArray();
    }

    // ───────────────────────────── 파티클 · 후처리 만들기
    static ParticleSystem Particles(Transform parent, string name, Vector3 pos, Vector3 box, float life, float rate, Vector2 size,
                                    Vector3 vel, Material mat, bool stretch, bool noise, float alpha, bool sizeCurve, bool fade)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new Color(1f, 1f, 1f, alpha);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * life * 1.15f), 50, 30000);

        var em = ps.emission; em.enabled = true; em.rateOverTime = rate;

        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.position = Vector3.zero; sh.scale = box;

        var v = ps.velocityOverLifetime;
        v.enabled = vel != Vector3.zero;
        v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(vel.x); v.y = new ParticleSystem.MinMaxCurve(vel.y); v.z = new ParticleSystem.MinMaxCurve(vel.z);

        var nz = ps.noise; nz.enabled = noise; nz.strength = 0.4f; nz.frequency = 0.3f;

        if (fade)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
        if (sizeCurve)
        {
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 1f; }
        else r.renderMode = ParticleSystemRenderMode.Billboard;

        Undo.RegisterCreatedObjectUndo(go, name);
        return ps;
    }

    static Material ParticleMat(string name, Color color, Color emission)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        m = new Material(sh ? sh : Shader.Find("Sprites/Default"));
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        // 반투명 (Transparent, Alpha 블렌드)
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (emission.maxColorComponent > 0)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
    {
        string path = $"{Dir}/{name}.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p) return p; // 이미 있으면 그대로 (손으로 조절한 값 유지)
        p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        fill(p);
        foreach (var comp in p.components)
        {
            comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(comp, p);
        }
        EditorUtility.SetDirty(p);
        return p;
    }

    // ───────────────────────────── 재질
    static Material OwnOrNew(string name, Color color, float metallic, float smooth, Color? emission = null)
    {
        var own = AssetDatabase.LoadAssetAtPath<Material>($"{OwnMatDir}/{name}.mat");
        return own ? own : Mat(name, color, metallic, smooth, emission);
    }

    static Material Mat(string name, Color color, float metallic, float smooth, Color? emission = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
        Object.DestroyImmediate(tmp);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (color.a < 0.99f)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
        }
        m.enableInstancing = true;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ───────────────────────────── 메시
    static GameObject Obj(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = false;
        return go;
    }

    static Mesh BuiltinMesh(PrimitiveType t)
    {
        var tmp = GameObject.CreatePrimitive(t);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return m;
    }

    // 같은 이름 파일이 있으면: overwrite면 내용을 바꿔 넣고(참조 유지), 아니면 기존 것 사용
    static Mesh SaveMesh(string name, Mesh fresh, bool overwrite)
    {
        string path = $"{MeshDir}/{name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old && !overwrite) { Object.DestroyImmediate(fresh); return old; }
        if (old)
        {
            old.Clear();
            old.indexFormat = fresh.indexFormat;
            old.SetVertices(new List<Vector3>(fresh.vertices));
            old.SetNormals(new List<Vector3>(fresh.normals));
            old.SetUVs(0, new List<Vector2>(fresh.uv));
            old.SetTriangles(fresh.triangles, 0);
            old.RecalculateBounds();
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(old);
            return old;
        }
        fresh.name = name;
        AssetDatabase.CreateAsset(fresh, path);
        return fresh;
    }

    static Mesh GroundMesh(Ctx c, Rect r)
    {
        float step = Mathf.Max(1.5f, Mathf.Max(r.width, r.height) / 160f);
        int nx = Mathf.CeilToInt(r.width / step) + 1, nz = Mathf.CeilToInt(r.height / step) + 1;
        var v = new List<Vector3>(nx * nz); var uv = new List<Vector2>(nx * nz);
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = r.xMin + i * step, z = r.yMin + j * step;
                v.Add(new Vector3(x, H(c, x, z), z));
                uv.Add(new Vector2(x / 10f, z / 10f));
            }
        var t = new List<int>((nx - 1) * (nz - 1) * 6);
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, cc = a + nx, d = cc + 1;
                t.Add(a); t.Add(cc); t.Add(b);
                t.Add(b); t.Add(cc); t.Add(d);
            }
        var m = new Mesh();
        if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 면마다 꼭짓점을 따로 둬서 각진(로우폴리) 느낌
    static Mesh Faceted(List<Vector3> tris)
    {
        var n = new List<Vector3>(tris.Count); var idx = new List<int>(tris.Count); var uv = new List<Vector2>(tris.Count);
        for (int i = 0; i < tris.Count; i += 3)
        {
            Vector3 nn = Vector3.Cross(tris[i + 1] - tris[i], tris[i + 2] - tris[i]).normalized;
            for (int k = 0; k < 3; k++) { n.Add(nn); idx.Add(i + k); uv.Add(new Vector2(tris[i + k].x, tris[i + k].y)); }
        }
        var m = new Mesh();
        m.SetVertices(tris); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(idx, 0);
        m.RecalculateBounds();
        return m;
    }

    static Vector3 RingAt(int i, float y, float r, int n)
    {
        float a = i * Mathf.PI * 2f / n;
        return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
    }

    static void Tri(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c) { t.Add(a); t.Add(b); t.Add(c); }

    // 얼음 결정: 밑동(0) → 육각기둥(0.75) → 뾰족한 끝(1)
    static Mesh CrystalMesh()
    {
        var t = new List<Vector3>();
        const int N = 6;
        Vector3 tip = new Vector3(0, 1f, 0), bottom = new Vector3(0, 0, 0);
        for (int i = 0; i < N; i++)
        {
            Vector3 a0 = RingAt(i, 0, 0.5f, N), a1 = RingAt(i + 1, 0, 0.5f, N), b0 = RingAt(i, 0.75f, 0.5f, N), b1 = RingAt(i + 1, 0.75f, 0.5f, N);
            Tri(t, a0, b0, b1); Tri(t, a0, b1, a1);   // 옆면
            Tri(t, b0, tip, b1);                      // 끝
            Tri(t, a0, a1, bottom);                   // 밑면
        }
        return Faceted(t);
    }

    // 고드름: 위(0)에 붙어서 아래(-1)로 뾰족 → 위에서 아래로 자라남
    static Mesh IcicleMesh()
    {
        var t = new List<Vector3>();
        const int N = 6;
        Vector3 tip = new Vector3(0, -1f, 0), top = Vector3.zero;
        for (int i = 0; i < N; i++)
        {
            Vector3 a0 = RingAt(i, 0, 0.5f, N), a1 = RingAt(i + 1, 0, 0.5f, N), b0 = RingAt(i, -0.3f, 0.32f, N), b1 = RingAt(i + 1, -0.3f, 0.32f, N);
            Tri(t, a0, a1, b1); Tri(t, a0, b1, b0);
            Tri(t, b0, b1, tip);
            Tri(t, a1, a0, top);
        }
        return Faceted(t);
    }

    // 설산 언덕: 울퉁불퉁한 원뿔 (밑지름 2, 높이 1)
    static Mesh HillMesh(int seed)
    {
        var rng = new System.Random(seed);
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        const int N = 10;
        float[] ys = { 0f, 0.4f, 0.72f };
        float[] rs = { 1f, 0.62f, 0.3f };
        var rings = new Vector3[ys.Length, N];
        for (int k = 0; k < ys.Length; k++)
            for (int i = 0; i < N; i++)
            {
                float a = (i + R(-0.2f, 0.2f)) * Mathf.PI * 2f / N, rr = rs[k] * R(0.78f, 1.12f);
                rings[k, i] = new Vector3(Mathf.Cos(a) * rr, ys[k] + (k == 0 ? 0 : R(-0.05f, 0.05f)), Mathf.Sin(a) * rr);
            }
        Vector3 apex = new Vector3(R(-0.1f, 0.1f), 1f, R(-0.1f, 0.1f));
        var t = new List<Vector3>();
        for (int k = 0; k + 1 < ys.Length; k++)
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                Tri(t, rings[k, i], rings[k + 1, i], rings[k + 1, j]);
                Tri(t, rings[k, i], rings[k + 1, j], rings[k, j]);
            }
        int last = ys.Length - 1;
        for (int i = 0; i < N; i++) Tri(t, rings[last, i], apex, rings[last, (i + 1) % N]);
        return Faceted(t);
    }

    // ───────────────────────────── 기타
    static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
#endif
