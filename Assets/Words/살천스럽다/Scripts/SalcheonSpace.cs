using System.Collections.Generic;
using Byeolmalsseum;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// [살천스럽다] 공간 자동 생성 도구 v2 — 눈 쌓인 숲과 눈 소품 잔뜩
// 쓰는 법: 살천스럽다_공간 씬 → '공간 (소품·조명 등 여기에)' 아래 빈 오브젝트 → 이 스크립트 붙이기 → Inspector 버튼.
//  ① 땅·눈 언덕·산  : 눈이 두툼하게 쌓인 땅(높은 눈 언덕), 눈 더미, 바깥 둘레에 눈 덮인 산 몇 개
//  ② 눈 숲          : 눈 쌓인 전나무 숲(무리 지어), 눈 덤불, 가지에 눈 얹힌 나무, 눈 덮인 바위, 얼음 결정
//  ③ 눈 소품        : 눈사람(2단·3단, 목도리·모자), 큰 눈사람, 눈오리, 이글루, 장독대, 벤치, 가로등, 썰매,
//                      눈덩이 더미, 삽 꽂힌 눈더미, 눈 쌓인 울타리 — 지나가는 길 양옆에 이야기처럼 배치
//  ④ 자모 위 눈·고드름: 자모 윗면에 눈이 소복이, 가로획 아래 고드름, 자모 밑동에 눈더미 (글자 도착 후 자라남)
//  ⑤ 눈·눈보라·후처리·돌풍
//  - 자모 배치는 절대 건드리지 않음. 버튼마다 자기 그룹만 새로 바뀜. Ctrl+Z 가능.
//  - 생성 파일: Assets/Words/살천스럽다/생성 (모양·재질). 재질 색은 거기서 바꾸면 다시 눌러도 유지됨.
public class SalcheonSpace : MonoBehaviour
{
    [Header("분위기")]
    [Tooltip("켜면: 매섭고 으스스한 쪽 (처진 입 눈사람, 까마귀, 끊긴 발자국, 꺼진 가로등, 어두운 안개…)\n끄면: 포근한 겨울 (웃는 눈사람, 눈오리, 따뜻한 이글루)")]
    public bool scary = true;
    [Tooltip("무서운 쪽: 눈사람이 안 볼 때 돌아보고 조금씩 다가옴")] public bool watchingSnowmen = true;
    [Range(0f, 3f)] public float crowAmount = 1f;
    [Range(0, 8)] public int footprintTrails = 4;

    [Header("공통")]
    [Tooltip("숫자를 바꾸고 다시 누르면 다른 배치")] public int seed = 7;
    [Tooltip("자모·시작 지점 둘레로 얼마나 넓게 채울지 (m)")] public float margin = 30f;
    [Tooltip("지나갈 길과 자모 둘레는 이만큼(m) 비워 둠")] public float clearance = 5f;
    [Tooltip("바람이 불어가는 방향 (눈·눈보라)")] public Vector3 windDirection = new Vector3(-1f, 0f, -0.3f);

    [Header("① 땅·눈 언덕·산")]
    [Tooltip("눈 언덕 높이 (0이면 평평). 길과 자모 밑은 늘 평평")] [Range(0f, 1f)] public float snowDunes = 0.7f;
    [Tooltip("땅 높이 조절 (m). 눕혀 둔 자모가 묻히면 조금 내리기")] public float groundOffset = 0f;
    [Range(0f, 3f)] public float moundAmount = 1f;
    [Tooltip("바깥 둘레 산 개수 (0이면 없음)")] [Range(0, 20)] public int hillCount = 6;
    public Vector2 hillHeight = new Vector2(10f, 24f);
    [Tooltip("공간 가장자리에서 산까지 거리 (m)")] public float hillDistance = 25f;

    [Header("② 눈 숲")]
    [Tooltip("전나무 양")] [Range(0f, 3f)] public float forestAmount = 1f;
    public Vector2 treeHeight = new Vector2(5f, 14f);
    [Range(0f, 3f)] public float bushAmount = 1f;
    [Range(0f, 3f)] public float bareTreeAmount = 1f;
    [Range(0f, 3f)] public float rockAmount = 1f;
    [Range(0f, 3f)] public float crystalAmount = 0.6f;

    [Header("③ 눈 소품")]
    [Tooltip("길가 장면(눈사람 가족·벤치와 눈오리·장독대·이글루…) 사이 간격 (m)")] public float sceneSpacing = 20f;
    [Tooltip("길가 가로등 간격 (m, 0이면 없음)")] public float lampSpacing = 16f;
    [Tooltip("길 말고 들판 곳곳 눈사람·눈오리 양")] [Range(0f, 3f)] public float scatterAmount = 1f;
    public bool giantSnowman = true;
    [Range(0, 8)] public int fences = 3;

    [Header("④ 자모 위 눈·고드름")]
    public bool snowOnJamo = true;
    public bool jamoDrifts = true;
    public float icicleSize = 1f;
    [Range(0.25f, 2f)] public float icicleSpacing = 0.55f;
    [Tooltip("이 높이(m)보다 낮은 획엔 고드름 안 담")] public float icicleMinHeight = 1.2f;
    public int icicleMax = 1500;

    [Header("⑤ 눈·눈보라·빛")]
    [Range(0f, 2f)] public float snowAmount = 1f;
    [Range(0f, 2f)] public float blizzardAmount = 1f;
    public bool makeLamps = true;

#if UNITY_EDITOR
    public void Build(int which) { SalcheonBuilder.Run(this, which); }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SalcheonSpace))]
public class SalcheonSpaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (SalcheonSpace)target;
        EditorGUILayout.HelpBox("자모는 건드리지 않고 둘레를 채워요. 버튼마다 자기 그룹만 새로 바뀌고 Ctrl+Z로 되돌릴 수 있어요.\n누른 다음 Ctrl+S로 저장!", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("① 땅·눈 언덕·산", GUILayout.Height(24))) s.Build(1);
            if (GUILayout.Button("② 눈 숲 (전나무·덤불·바위)", GUILayout.Height(24))) s.Build(2);
            if (GUILayout.Button("③ 눈 소품 (눈사람·눈오리·이글루·장독대…)", GUILayout.Height(24))) s.Build(3);
            if (GUILayout.Button("④ 자모 위 눈·고드름", GUILayout.Height(24))) s.Build(4);
            if (GUILayout.Button("⑤ 눈·눈보라·후처리·돌풍", GUILayout.Height(24))) s.Build(5);
            EditorGUILayout.Space(4);
            if (GUILayout.Button("전부 한 번에 (①~⑤)", GUILayout.Height(32))) s.Build(0);
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
    const string SpaceName = "공간 (소품·조명 등 여기에)";

    class Spot { public Vector2 p; public float r; public Spot(Vector2 p, float r) { this.p = p; this.r = r; } }

    class Ctx
    {
        public SalcheonSpace s;
        public WordSpace ws;
        public Transform gen, bg;
        public Rect area;
        public Vector2 center;
        public List<Bounds> jamo = new List<Bounds>();
        public List<Vector3> path = new List<Vector3>();
        public List<Spot> taken = new List<Spot>();
        public Vector3 start, startForward;
        public float baseY, topY;
        public System.Random rng;
        public float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        public int RI(int a, int b) { return rng.Next(a, b); }
        public bool Chance(float p) { return rng.NextDouble() < p; }
    }

    static readonly string[] Names = { "①~⑤ 전부", "① 땅·눈 언덕·산", "② 눈 숲", "③ 눈 소품", "④ 자모 위 눈·고드름", "⑤ 눈·눈보라·후처리·돌풍" };

    public static void Run(SalcheonSpace s, int which)
    {
        if (Application.isPlaying) return;
        var ws = Object.FindAnyObjectByType<WordSpace>();
        if (ws == null) { EditorUtility.DisplayDialog("살천스럽다", "이 씬에 단어 공간이 없어요. 살천스럽다_공간 씬에서 눌러 주세요.", "확인"); return; }

        string what = which >= 0 && which < Names.Length ? Names[which] : "생성물 모두 지우기";
        string msg = which == -1 ? "자동으로 만든 것을 전부 지워요. (자모는 그대로)\nCtrl+Z로 되돌릴 수 있어요."
                                 : what + "을(를) 만들어요.\n이미 만든 게 있으면 그 그룹만 새로 바뀌어요. 자모는 그대로예요.";
        if (!EditorUtility.DisplayDialog("살천스럽다", msg, "실행", "취소")) return;

        Undo.SetCurrentGroupName("살천스럽다 " + what);
        int undoGroup = Undo.GetCurrentGroup();
        var c = Prepare(s, ws);
        try
        {
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
                int total = which == 0 ? 5 : 1, step = 0;
                for (int b = 1; b <= 5; b++)
                {
                    if (which != 0 && which != b) continue;
                    EditorUtility.DisplayProgressBar("살천스럽다", Names[b] + " 만드는 중…", (float)step++ / total);
                    Step(Names[b], c, b);
                }
                EnsureRuntime(c);
                AssetDatabase.SaveAssets();
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
        Debug.Log("[살천스럽다] " + what + " 완료. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    static void Step(string name, Ctx c, int b)
    {
        try
        {
            if (b == 1) BuildGround(c);
            else if (b == 2) BuildForest(c);
            else if (b == 3) BuildProps(c);
            else if (b == 4) BuildJamoSnow(c);
            else if (b == 5) BuildAtmosphere(c);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[살천스럽다] '" + name + "' 만들다 문제가 생겨서 건너뛰었어요: " + e.Message + "\n" + e.StackTrace);
        }
    }

    // ───────────────────────────── 준비
    static Ctx Prepare(SalcheonSpace s, WordSpace ws)
    {
        var c = new Ctx { s = s, ws = ws, rng = new System.Random(s.seed) };
        var spaceRoot = SpaceRoot(ws);
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
        Transform st = ec ? (ec.startPoint ? ec.startPoint : ec.transform) : ws.transform;
        c.start = st.position;
        Vector3 f = st.forward; f.y = 0;
        c.startForward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        if (!any) all = new Bounds(c.start, Vector3.one * 20f);
        all.Encapsulate(c.start);

        // 지나갈 길: 시작 지점 → 가까운 자모 → …
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
        c.topY = Mathf.Max(25f, all.max.y + 5f - c.baseY);
        float m = Mathf.Max(5f, s.margin);
        c.area = Rect.MinMaxRect(all.min.x - m, all.min.z - m, all.max.x + m, all.max.z + m);
        c.center = c.area.center;
        return c;
    }

    static Transform SpaceRoot(WordSpace ws)
    {
        var t = ws.transform.Find(SpaceName);
        return t ? t : ws.transform;
    }

    static void EnsureRoots(Ctx c)
    {
        if (!c.gen)
        {
            var go = new GameObject(GenName);
            Undo.RegisterCreatedObjectUndo(go, "생성물");
            go.transform.SetParent(SpaceRoot(c.ws), false);
            c.gen = go.transform;
        }
        if (!c.bg)
        {
            // 땅·산은 [별말씀] 바깥 → 이동 범위 계산에 안 들어감
            var go = new GameObject(BgName);
            Undo.RegisterCreatedObjectUndo(go, "배경");
            if (go.scene != c.ws.gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, c.ws.gameObject.scene);
            c.bg = go.transform;
        }
    }

    // 그룹을 새로 만들고, 다른 그룹에 이미 있는 것들의 자리를 '찬 자리'로 기록
    static Transform Group(Ctx c, Transform parent, string name)
    {
        var old = parent.Find(name);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        RefreshTaken(c);
        return go.transform;
    }

    static readonly string[] SkipTaken = { "파티클", "후처리", "찬바람 구역", "가로등 빛", "고드름", "자모 위 눈" };

    static void RefreshTaken(Ctx c)
    {
        c.taken.Clear();
        if (!c.gen) return;
        foreach (Transform g in c.gen)
        {
            if (System.Array.IndexOf(SkipTaken, g.name) >= 0) continue;
            foreach (Transform o in g)
            {
                var rs = o.GetComponentsInChildren<Renderer>();
                if (rs == null || rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
                c.taken.Add(new Spot(Flat(b.center), Mathf.Min(Mathf.Max(b.extents.x, b.extents.z), 6f)));
            }
        }
    }

    static Transform FindOldFloor(WordSpace ws) { return ws.transform.Find(SpaceName + "/바닥 (지워도 됨)"); }

    // ───────────────────────────── 땅 높이 · 빈자리
    static float H(Ctx c, float x, float z)
    {
        float amp = 1.6f * c.s.snowDunes;
        if (amp <= 0) return c.baseY;
        float ox = c.s.seed * 13.1f, oz = c.s.seed * 7.7f;
        float h = Mathf.PerlinNoise(x * 0.028f + ox, z * 0.028f + oz) * 0.75f
                + Mathf.PerlinNoise(x * 0.11f + oz, z * 0.11f + ox) * 0.25f;
        Vector3 w = WindFlat(c);
        h += 0.08f * Mathf.Sin((x * w.x + z * w.z) * 1.1f + Mathf.PerlinNoise(x * 0.1f, z * 0.1f) * 4f); // 바람에 깎인 눈결
        h = Mathf.Max(0f, h - 0.15f);
        // 길에서 멀어질수록, 자모에서 멀어질수록 높게 쌓임
        float fPath = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.s.clearance * 0.6f, c.s.clearance + 25f, DistToPath(c, x, z)));
        float fJamo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 7f, DistToJamo(c, x, z)));
        return c.baseY + h * amp * Mathf.Min(fPath, fJamo);
    }

    static Vector3 WindFlat(Ctx c)
    {
        Vector3 w = c.s.windDirection; w.y = 0;
        return w.sqrMagnitude < 1e-4f ? Vector3.left : w.normalized;
    }

    static Vector2 ClosestOnPath(Ctx c, Vector2 p)
    {
        Vector2 best = Flat(c.path[0]); float bd = float.MaxValue;
        for (int i = 0; i + 1 < c.path.Count; i++)
        {
            Vector2 a = Flat(c.path[i]), b = Flat(c.path[i + 1]);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector2 q = a + ab * t;
            float d = (p - q).sqrMagnitude;
            if (d < bd) { bd = d; best = q; }
        }
        return best;
    }

    static float DistToPath(Ctx c, float x, float z)
    {
        var p = new Vector2(x, z);
        return Vector2.Distance(p, ClosestOnPath(c, p));
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

    static bool Free(Ctx c, float x, float z, float extra, float radius)
    {
        float cl = c.s.clearance + extra;
        if (DistToPath(c, x, z) < cl + radius) return false;
        if (DistToJamo(c, x, z) < cl * 0.6f + radius) return false;
        var p = new Vector2(x, z);
        foreach (var t in c.taken) if ((t.p - p).sqrMagnitude < (t.r + radius) * (t.r + radius)) return false;
        return true;
    }

    // 빈자리 하나 찾기 (찾으면 그 자리를 '찬 자리'로 기록)
    static bool TryPoint(Ctx c, float extra, float radius, out Vector3 p, Rect? region = null)
    {
        Rect r = region ?? c.area;
        for (int i = 0; i < 40; i++)
        {
            float x = c.R(r.xMin, r.xMax), z = c.R(r.yMin, r.yMax);
            if (!Free(c, x, z, extra, radius)) continue;
            p = new Vector3(x, H(c, x, z), z);
            c.taken.Add(new Spot(new Vector2(x, z), radius));
            return true;
        }
        p = Vector3.zero;
        return false;
    }

    static bool TryNear(Ctx c, Vector2 around, float spread, float extra, float radius, out Vector3 p)
    {
        return TryPoint(c, extra, radius, out p, Rect.MinMaxRect(around.x - spread, around.y - spread, around.x + spread, around.y + spread));
    }

    static int Count(Ctx c, float per1000, float amount)
    {
        float a = c.area.width * c.area.height / 1000f;
        return Mathf.Clamp(Mathf.RoundToInt(a * per1000 * amount), 0, 600);
    }

    static Quaternion FacePath(Ctx c, Vector3 p, float jitter)
    {
        Vector2 q = ClosestOnPath(c, Flat(p));
        Vector3 d = new Vector3(q.x - p.x, 0, q.y - p.z);
        if (d.sqrMagnitude < 1e-4f) d = -c.startForward;
        return Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0, c.R(-jitter, jitter), 0);
    }

    // ───────────────────────────── 재질 묶음
    class Mats
    {
        public Material snow, ground, ice, icicle, needle, bark, rock, stone, coal, carrot, metal, wood, redWood, onggi, lampBulb, warm, crow, print, deadBulb;
        public bool scary;
        public Material[] scarves, hats;
    }

    static Mats GetMats(Ctx c)
    {
        var m = new Mats();
        m.scary = c.s.scary;
        m.snow = Mat("눈", new Color32(240, 244, 250, 255), 0f, 0.3f);
        m.ground = OwnOrNew("눈바닥", new Color32(223, 230, 237, 255), 0f, 0.05f);
        m.ice = OwnOrNew("얼음", new Color32(168, 216, 240, 204), 0.1f, 0.9f);
        m.icicle = OwnOrNew("고드름", new Color32(216, 240, 255, 217), 0.1f, 0.95f);
        m.needle = m.scary ? Mat("전나무 잎 (어둠)", new Color32(22, 36, 36, 255), 0f, 0.1f) : Mat("전나무 잎", new Color32(38, 66, 58, 255), 0f, 0.15f);
        m.bark = m.scary ? Mat("나무껍질 (어둠)", new Color32(34, 30, 30, 255), 0f, 0.1f) : Mat("나무껍질", new Color32(58, 48, 44, 255), 0f, 0.15f);
        m.crow = Mat("까마귀", new Color32(16, 16, 20, 255), 0f, 0.55f);
        m.print = Mat("발자국", new Color32(150, 162, 178, 255), 0f, 0.05f);
        m.deadBulb = Mat("꺼진 전구", new Color32(58, 66, 76, 255), 0f, 0.6f);
        m.rock = Mat("바위", new Color32(92, 104, 117, 255), 0f, 0.2f);
        m.stone = Mat("장독대 돌", new Color32(128, 128, 124, 255), 0f, 0.15f);
        m.coal = Mat("숯", new Color32(28, 28, 32, 255), 0f, 0.35f);
        m.carrot = Mat("당근", new Color32(232, 118, 40, 255), 0f, 0.3f);
        m.metal = Mat("쇠", new Color32(52, 60, 70, 255), 0.7f, 0.45f);
        m.wood = Mat("나무 판자", new Color32(124, 90, 64, 255), 0f, 0.2f);
        m.redWood = Mat("썰매", new Color32(176, 48, 44, 255), 0f, 0.35f);
        m.onggi = Mat("옹기", new Color32(84, 52, 38, 255), 0f, 0.65f);
        m.lampBulb = OwnOrNew("가로등 빛", new Color32(191, 228, 255, 255), 0f, 0.5f, new Color(0.75f, 0.9f, 1f) * 2f);
        m.warm = Mat("이글루 불빛", new Color32(255, 196, 120, 255), 0f, 0.3f, new Color(1f, 0.62f, 0.3f) * 2.2f);
        m.scarves = m.scary ? new[]
        {
            Mat("목도리 잿빛", new Color32(74, 76, 82, 255), 0f, 0.05f),
            Mat("목도리 먹색", new Color32(36, 38, 44, 255), 0f, 0.05f),
            Mat("목도리 바랜 빨강", new Color32(112, 34, 38, 255), 0f, 0.05f),
        } : new[]
        {
            Mat("목도리 빨강", new Color32(186, 44, 52, 255), 0f, 0.1f),
            Mat("목도리 남색", new Color32(44, 62, 112, 255), 0f, 0.1f),
            Mat("목도리 겨자", new Color32(214, 160, 52, 255), 0f, 0.1f),
            Mat("목도리 초록", new Color32(48, 112, 84, 255), 0f, 0.1f),
        };
        m.hats = new[] { m.metal, Mat("양동이 빨강", new Color32(160, 52, 48, 255), 0.4f, 0.5f) };
        return m;
    }

    // ───────────────────────────── ① 땅·눈 언덕·산
    static void BuildGround(Ctx c)
    {
        var M = GetMats(c);
        var groundG = Group(c, c.bg, "눈 땅");
        var gr = c.area; float pad = 70f;
        var ground = SaveMesh("눈 땅", GroundMesh(c, Rect.MinMaxRect(gr.xMin - pad, gr.yMin - pad, gr.xMax + pad, gr.yMax + pad)));
        Obj(groundG, "땅", ground, M.ground);
        var floor = FindOldFloor(c.ws);
        if (floor && floor.gameObject.activeSelf) { Undo.RecordObject(floor.gameObject, "바닥 끄기"); floor.gameObject.SetActive(false); }

        // 눈 더미
        var mounds = new Mesh[5];
        for (int i = 0; i < mounds.Length; i++) mounds[i] = SaveMesh("눈 더미 " + (i + 1), SalcheonShapes.Mound(100 + i));
        var moundG = Group(c, c.gen, "눈 더미");
        int nm = Count(c, 1.6f, c.s.moundAmount);
        for (int i = 0; i < nm; i++)
        {
            float r = c.R(2f, 6f);
            Vector3 p;
            if (!TryPoint(c, 1f, r * 0.8f, out p)) continue;
            var t = Obj(moundG, "눈 더미", mounds[c.RI(0, mounds.Length)], M.snow).transform;
            t.position = p;
            t.localScale = new Vector3(r, r * c.R(0.25f, 0.45f), r);
            t.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        }

        // 바깥 둘레 눈 덮인 산 (배경)
        var hillG = Group(c, c.bg, "눈 덮인 산");
        if (c.s.hillCount <= 0) return;
        var hills = new Mesh[3];
        for (int v = 0; v < 3; v++) hills[v] = SaveMesh("산 " + (v + 1), SalcheonShapes.Hill(1000 + v * 77 + c.s.seed));
        Rect ring = Rect.MinMaxRect(c.area.xMin - c.s.hillDistance, c.area.yMin - c.s.hillDistance, c.area.xMax + c.s.hillDistance, c.area.yMax + c.s.hillDistance);
        float per = 2f * (ring.width + ring.height);
        for (int i = 0; i < c.s.hillCount; i++)
        {
            float u = Mathf.Repeat((i + c.R(-0.3f, 0.3f)) / c.s.hillCount * per, per);
            Vector2 q, outward;
            if (u < ring.width) { q = new Vector2(ring.xMin + u, ring.yMin); outward = new Vector2(0, -1); }
            else if (u < ring.width + ring.height) { q = new Vector2(ring.xMax, ring.yMin + u - ring.width); outward = new Vector2(1, 0); }
            else if (u < 2f * ring.width + ring.height) { q = new Vector2(ring.xMax - (u - ring.width - ring.height), ring.yMax); outward = new Vector2(0, 1); }
            else { q = new Vector2(ring.xMin, ring.yMax - (u - 2f * ring.width - ring.height)); outward = new Vector2(-1, 0); }
            q += outward * c.R(0f, 25f);
            float h = c.R(c.s.hillHeight.x, c.s.hillHeight.y), rad = h * c.R(1.4f, 2.2f);
            var hill = Obj(hillG, "산", hills[c.RI(0, 3)], M.snow, M.rock).transform;
            hill.position = new Vector3(q.x, c.baseY - 0.5f, q.y);
            hill.localScale = new Vector3(rad, h, rad * c.R(0.7f, 1.1f));
            hill.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        }
    }

    // ───────────────────────────── ② 눈 숲
    static void BuildForest(Ctx c)
    {
        var M = GetMats(c);
        var firs = new Mesh[6];
        for (int i = 0; i < firs.Length; i++) firs[i] = SaveMesh("전나무 " + (i + 1), SalcheonShapes.Fir(200 + i));
        var bushes = new Mesh[4];
        for (int i = 0; i < bushes.Length; i++) bushes[i] = SaveMesh("눈 덤불 " + (i + 1), SalcheonShapes.Bush(300 + i));
        var bare = new Mesh[3];
        for (int i = 0; i < bare.Length; i++) bare[i] = SaveMesh("앙상한 나무 " + (i + 1), SalcheonShapes.BareTree(400 + i));
        var rocks = new Mesh[4];
        for (int i = 0; i < rocks.Length; i++) rocks[i] = SaveMesh("바위 " + (i + 1), SalcheonShapes.Rock(500 + i));
        var crystal = SaveMesh("얼음 결정", SalcheonShapes.Crystal());

        // 전나무 숲: 숲 덩어리 여러 개 + 띄엄띄엄
        var firG = Group(c, c.gen, "눈 쌓인 전나무");
        int nTrees = Count(c, 5.5f, c.s.forestAmount);
        int groves = Mathf.Max(1, nTrees / 12);
        int placed = 0;
        for (int g = 0; g < groves && placed < nTrees; g++)
        {
            Vector3 gc;
            if (!TryPoint(c, 4f, 0.5f, out gc)) continue;
            c.taken.RemoveAt(c.taken.Count - 1);
            int k = c.RI(7, 17);
            for (int i = 0; i < k && placed < nTrees; i++)
            {
                float h = c.R(c.s.treeHeight.x, c.s.treeHeight.y);
                Vector3 p;
                if (!TryNear(c, Flat(gc), c.R(6f, 16f), 1f, h * 0.17f, out p)) continue;
                PlaceTree(firG, firs[c.RI(0, firs.Length)], p, h, c, M);
                placed++;
            }
        }
        for (; placed < nTrees; placed++)
        {
            float h = c.R(c.s.treeHeight.x, c.s.treeHeight.y);
            Vector3 p;
            if (!TryPoint(c, 1.5f, h * 0.17f, out p)) continue;
            PlaceTree(firG, firs[c.RI(0, firs.Length)], p, h, c, M);
        }

        // 눈 덤불 (숲 가장자리·길가에 많이)
        var bushG = Group(c, c.gen, "눈 덤불");
        int nb = Count(c, c.s.scary ? 2.5f : 4f, c.s.bushAmount);
        for (int i = 0; i < nb; i++)
        {
            float s = c.R(0.8f, 2.2f);
            Vector3 p;
            if (!TryPoint(c, c.Chance(0.4f) ? -c.s.clearance * 0.5f : 0.5f, s * 0.6f, out p)) continue;
            var t = Obj(bushG, "눈 덤불", bushes[c.RI(0, bushes.Length)], M.needle, M.snow).transform;
            t.position = p + Vector3.down * 0.1f;
            t.localScale = new Vector3(s, s * c.R(0.8f, 1.2f), s);
            t.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        }

        // 가지에 눈 얹힌 앙상한 나무
        var bareG = Group(c, c.gen, "앙상한 나무");
        int nbt = Count(c, c.s.scary ? 2.6f : 0.9f, c.s.bareTreeAmount);
        for (int i = 0; i < nbt; i++)
        {
            float h = c.R(4f, 10f);
            Vector3 p;
            if (!TryPoint(c, 1f, 1.2f, out p)) continue;
            var t = Obj(bareG, "앙상한 나무", bare[c.RI(0, bare.Length)], M.bark, M.snow).transform;
            t.position = p + Vector3.down * 0.2f;
            t.localScale = Vector3.one * h;
            t.rotation = Quaternion.Euler(c.R(-4f, 4f), c.R(0, 360), c.R(-4f, 4f));
        }

        // 눈 덮인 바위
        var rockG = Group(c, c.gen, "눈 덮인 바위");
        int nr = Count(c, 1.4f, c.s.rockAmount);
        for (int i = 0; i < nr; i++)
        {
            float s = c.R(0.6f, 3f);
            Vector3 p;
            if (!TryPoint(c, 0.5f, s * 0.9f, out p)) continue;
            var t = Obj(rockG, "바위", rocks[c.RI(0, rocks.Length)], M.rock, M.snow).transform;
            t.position = p;
            t.localScale = Vector3.one * s;
            t.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        }

        // 얼음 결정 무리 (글자 도착 후 자라남)
        var crysG = Group(c, c.gen, "얼음 결정 무리");
        int nc = Count(c, 0.7f, c.s.crystalAmount);
        for (int i = 0; i < nc; i++)
        {
            float size = c.R(0.6f, 1.6f);
            Vector3 p;
            if (!TryPoint(c, 0.5f, size * 1.2f, out p)) continue;
            var cluster = new GameObject("결정 무리").transform;
            cluster.SetParent(crysG, false);
            cluster.position = p;
            cluster.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
            int k = c.RI(3, 8);
            for (int j = 0; j < k; j++)
            {
                var t = Obj(cluster, "결정", crystal, M.ice).transform;
                float a = c.R(0, 360) * Mathf.Deg2Rad, d = c.R(0f, 1.1f) * size;
                t.localPosition = new Vector3(Mathf.Cos(a) * d, -0.15f, Mathf.Sin(a) * d);
                float h = c.R(0.6f, 2.8f) * size, w = c.R(0.22f, 0.55f) * size;
                t.localScale = new Vector3(w, h, w);
                t.localRotation = Quaternion.Euler(c.R(-28f, 28f), c.R(0, 360), c.R(-28f, 28f));
            }
        }
    }

    static void PlaceTree(Transform parent, Mesh mesh, Vector3 p, float h, Ctx c, Mats M)
    {
        var t = Obj(parent, "전나무", mesh, M.bark, M.needle, M.snow).transform;
        t.position = p + Vector3.down * 0.15f;
        float w = h * c.R(0.85f, 1.15f);
        t.localScale = new Vector3(w, h, w);
        t.rotation = Quaternion.Euler(c.R(-2f, 2f), c.R(0, 360), c.R(-2f, 2f));
    }

    // ───────────────────────────── ③ 눈 소품
    class PropKit
    {
        public Mesh[] snowmen = new Mesh[5];
        public Mesh duck, igloo, bench, lamp, sled, shovel, head, crow, fallenLamp, print;
        public bool scary;
        public Mesh[] jangdok = new Mesh[2], balls = new Mesh[2];
    }

    static void BuildProps(Ctx c)
    {
        var M = GetMats(c);
        var K = new PropKit();
        K.scary = c.s.scary;
        for (int i = 0; i < 5; i++) K.snowmen[i] = SaveMesh("눈사람 " + (i + 1) + (K.scary ? " (으스스)" : ""), SalcheonShapes.Snowman(i, 600 + i, K.scary));
        K.head = SaveMesh("떨어진 머리", SalcheonShapes.FallenHead(650));
        K.crow = SaveMesh("까마귀", SalcheonShapes.Crow(660));
        K.fallenLamp = SaveMesh("쓰러진 가로등", SalcheonShapes.FallenLamp());
        K.print = SaveMesh("발자국", SalcheonShapes.Footprint());
        K.duck = SaveMesh("눈오리", SalcheonShapes.Duck(700));
        K.igloo = SaveMesh("이글루", SalcheonShapes.Igloo());
        K.bench = SaveMesh("벤치", SalcheonShapes.Bench());
        K.lamp = SaveMesh("가로등", SalcheonShapes.LampPost());
        K.sled = SaveMesh("썰매", SalcheonShapes.Sled());
        K.shovel = SaveMesh("삽 꽂힌 눈더미", SalcheonShapes.ShovelMound());
        for (int i = 0; i < 2; i++) K.jangdok[i] = SaveMesh("장독대 " + (i + 1), SalcheonShapes.Jangdok(800 + i));
        for (int i = 0; i < 2; i++) K.balls[i] = SaveMesh("눈덩이 더미 " + (i + 1), SalcheonShapes.Snowballs(900 + i));

        var root = Group(c, c.gen, "눈 소품");
        var lights = new List<Light>();

        // 큰 눈사람: 시작 지점에서 앞쪽에 보이게
        if (c.s.giantSnowman)
        {
            Vector3 right = Vector3.Cross(Vector3.up, c.startForward);
            Vector3 p = Vector3.zero; bool ok = false;
            for (int i = 0; i < 30 && !ok; i++)
            {
                Vector3 guess = c.start + c.startForward * c.R(22f, 40f) + right * c.R(-1f, 1f) * c.R(8f, 20f);
                if (Free(c, guess.x, guess.z, 0f, 3.5f)) { p = new Vector3(guess.x, H(c, guess.x, guess.z), guess.z); ok = true; }
            }
            if (ok)
            {
                c.taken.Add(new Spot(Flat(p), 3.5f));
                var g = K.scary ? Snowman(root, K, M, p, 4, 3.6f, c) : Snowman(root, K, M, p, 0, 4.2f, c);
                g.name = "큰 눈사람";
                g.transform.rotation = Quaternion.LookRotation(Flat3(c.start - p), Vector3.up);
                MoundAt(root, p, 4.5f, c, M);
            }
        }

        // 길가 장면들 (번갈아 양옆)
        int sceneIdx = 0;
        foreach (var spot in PathSpots(c, c.s.sceneSpacing, c.s.clearance + 2.5f, 3.5f, 0))
        {
            var sceneRoot = new GameObject("장면").transform;
            sceneRoot.SetParent(root, false);
            sceneRoot.position = spot;
            sceneRoot.rotation = FacePath(c, spot, 15f);
            int type = sceneIdx++ % 8;
            if (K.scary) BuildScaryScene(sceneRoot, type, K, M, c, lights); else BuildScene(sceneRoot, type, K, M, c, lights);
        }

        // 길가 가로등
        if (c.s.lampSpacing > 0.5f)
        {
            int li = 0;
            foreach (var spot in PathSpots(c, c.s.lampSpacing, c.s.clearance * 0.6f, 0.4f, 1))
            {
                bool lit = K.scary ? li % 4 == 0 : li % 3 == 0;
                li++;
                if (K.scary && c.Chance(0.15f))
                {
                    var f = Obj(root, "쓰러진 가로등", K.fallenLamp, M.metal, M.deadBulb, M.snow).transform;
                    f.position = spot + Vector3.down * 0.1f;
                    f.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
                    continue;
                }
                var t = Obj(root, "가로등", K.lamp, M.metal, (K.scary && !lit) ? M.deadBulb : M.lampBulb, M.snow).transform;
                t.position = spot + Vector3.down * 0.1f;
                float lean = K.scary ? 9f : 2f;
                t.rotation = Quaternion.Euler(c.R(-lean, lean), c.R(0, 360), c.R(-lean, lean));
                if (lit && lights.Count < 10)
                    lights.Add(PointLight(t, new Vector3(0, 3.75f, 0), 9f, K.scary ? 1.1f : 1.6f, new Color32(191, 228, 255, 255)));
            }
        }

        // 들판 곳곳: 혼자 선 눈사람, 눈오리 떼, 눈덩이
        int ns = Count(c, 0.35f, c.s.scatterAmount);
        for (int i = 0; i < ns; i++)
        {
            Vector3 p;
            if (!TryPoint(c, 2f, 1f, out p)) continue;
            var g = Snowman(root, K, M, p, K.scary ? c.RI(0, 5) : c.RI(0, 4), c.R(0.8f, 1.3f), c);
            g.transform.rotation = FacePath(c, p, 40f) * Quaternion.Euler(c.R(-6f, 6f), 0, c.R(-8f, 8f));
        }
        int nd = K.scary ? 0 : Count(c, 0.18f, c.s.scatterAmount);
        for (int i = 0; i < nd; i++)
        {
            Vector3 p;
            if (!TryPoint(c, 1f, 2f, out p)) continue;
            DuckFlock(root, K, M, p, c.RI(5, 12), c);
        }

        // 눈 쌓인 울타리
        for (int i = 0; i < c.s.fences; i++)
        {
            Vector3 p;
            if (!TryPoint(c, 3f, 5f, out p)) continue;
            Fence(root, p, c, M, i);
        }

        if (K.scary)
        {
            // 끊긴 발자국: 길에서 숲 쪽으로 걸어가다 뚝 끊김
            for (int i = 0; i < c.s.footprintTrails; i++) FootTrail(root, K, M, c, i);
            // 까마귀
            Crows(c, K, M, root);
            // 안 볼 때 돌아보는 눈사람들
            if (c.s.watchingSnowmen)
            {
                var wch = Undo.AddComponent<SalcheonWatchers>(root.gameObject);
                foreach (var tr in root.GetComponentsInChildren<Transform>())
                    if (tr.name == "눈사람" || tr.name == "큰 눈사람") wch.watchers.Add(tr);
            }
        }

        // 가로등 깜빡임에 넣기
        var amb = c.gen.GetComponent<SalcheonAmbience>();
        if (amb == null) amb = Undo.AddComponent<SalcheonAmbience>(c.gen.gameObject);
        Undo.RecordObject(amb, "분위기");
        var all = new List<Light>();
        if (amb.flickerLights != null) foreach (var l in amb.flickerLights) if (l && !l.transform.IsChildOf(root)) all.Add(l);
        all.AddRange(lights);
        amb.flickerLights = all.ToArray();
    }

    // 길을 따라 일정 간격으로, 양옆 번갈아 빈자리 찾기
    static List<Vector3> PathSpots(Ctx c, float spacing, float side, float radius, int phase)
    {
        var res = new List<Vector3>();
        float acc = spacing * 0.5f + phase * spacing * 0.5f;
        int flip = phase;
        for (int i = 0; i + 1 < c.path.Count; i++)
        {
            Vector2 a = Flat(c.path[i]), b = Flat(c.path[i + 1]);
            float len = Vector2.Distance(a, b);
            if (len < 1e-3f) continue;
            Vector2 dir = (b - a) / len, nrm = new Vector2(-dir.y, dir.x);
            for (; acc < len; acc += spacing)
            {
                Vector2 q = a + dir * acc;
                float sgn = (flip++ % 2 == 0) ? 1f : -1f;
                for (int tries = 0; tries < 8; tries++)
                {
                    float off = side + radius + tries * 3f;
                    Vector2 cand = q + nrm * sgn * off + dir * c.R(-2f, 2f);
                    if (!Free(c, cand.x, cand.y, -c.s.clearance + 0.01f, radius)) { if (tries == 3) sgn = -sgn; continue; }
                    if (DistToPath(c, cand.x, cand.y) < side) continue;
                    c.taken.Add(new Spot(cand, radius));
                    res.Add(new Vector3(cand.x, H(c, cand.x, cand.y), cand.y));
                    break;
                }
            }
            acc -= len;
        }
        return res;
    }

    // 길가 장면 8가지 (장면 오브젝트의 +Z가 길 쪽)
    static void BuildScene(Transform s, int type, PropKit K, Mats M, Ctx c, List<Light> lights)
    {
        switch (type)
        {
            case 0: // 눈사람 가족
                SnowmanLocal(s, K, M, new Vector3(-0.9f, 0, 0), 1, 1.15f, c);
                SnowmanLocal(s, K, M, new Vector3(0.6f, 0, 0.3f), 0, 1.0f, c);
                SnowmanLocal(s, K, M, new Vector3(1.7f, 0, 0.6f), 3, 0.6f, c);
                Local(s, K.balls[0], new Vector3(-2.2f, 0, 0.8f), 0, 1f, M.snow);
                break;
            case 1: // 벤치 + 눈오리들
                Local(s, K.bench, Vector3.zero, 0, 1f, M.wood, M.metal, M.snow);
                for (int i = 0; i < 5; i++)
                    Local(s, K.duck, new Vector3(-0.7f + i * 0.35f, 0.56f, -0.05f), c.R(-25f, 25f), 1.1f, M.snow, M.coal, M.carrot);
                for (int i = 0; i < 4; i++)
                    Local(s, K.duck, new Vector3(-0.5f + i * 0.4f, 0.02f, 0.6f), c.R(-30f, 30f), 1.2f, M.snow, M.coal, M.carrot);
                break;
            case 2: // 장독대
                Local(s, K.jangdok[c.RI(0, 2)], Vector3.zero, c.R(-8f, 8f), 1f, M.onggi, M.snow, M.stone);
                Local(s, K.shovel, new Vector3(2.8f, 0, 0.3f), c.R(0, 360), 0.9f, M.snow, M.wood, M.metal);
                break;
            case 3: // 이글루 (안에 불빛)
                Local(s, K.igloo, new Vector3(0, -0.05f, -2.5f), 0, 1f, M.snow, M.warm);
                if (lights.Count < 10) lights.Add(PointLight(s, new Vector3(0, 0.6f, 2.2f), 7f, 1.4f, new Color32(255, 176, 96, 255)));
                SnowmanLocal(s, K, M, new Vector3(2.6f, 0, 1.6f), 2, 0.8f, c);
                break;
            case 4: // 썰매 + 눈덩이 + 꼬마 눈사람
                Local(s, K.sled, new Vector3(-0.8f, 0.02f, 0.4f), c.R(20f, 60f), 1f, M.redWood, M.metal, M.snow);
                Local(s, K.balls[1], new Vector3(1.2f, 0, -0.2f), 0, 1f, M.snow);
                SnowmanLocal(s, K, M, new Vector3(0.2f, 0, -1.2f), 3, 0.7f, c);
                break;
            case 5: // 눈오리 행렬
                for (int i = 0; i < 12; i++)
                    Local(s, K.duck, new Vector3(-2.4f + i * 0.42f, 0.02f, Mathf.Sin(i * 0.7f) * 0.25f), 90f + c.R(-10f, 10f), 1.3f, M.snow, M.coal, M.carrot);
                break;
            case 6: // 삽 꽂힌 눈더미 + 눈사람 만들다 만 것
                Local(s, K.shovel, Vector3.zero, c.R(0, 360), 1.2f, M.snow, M.wood, M.metal);
                Local(s, K.balls[0], new Vector3(1.8f, 0, 0.5f), 0, 1.2f, M.snow);
                SnowmanLocal(s, K, M, new Vector3(-1.8f, 0, 0.4f), 1, 1.0f, c);
                break;
            default: // 눈사람 둘이 마주 보기 + 벤치
                var a = SnowmanLocal(s, K, M, new Vector3(-0.9f, 0, 0), 0, 1.1f, c);
                var b = SnowmanLocal(s, K, M, new Vector3(0.9f, 0, 0), 2, 1.0f, c);
                a.transform.localRotation = Quaternion.Euler(0, 70f, 0);
                b.transform.localRotation = Quaternion.Euler(0, -70f, 0);
                Local(s, K.bench, new Vector3(0, 0, -1.6f), 0, 1f, M.wood, M.metal, M.snow);
                break;
        }
    }

    // 으스스한 길가 장면 8가지 (+Z가 길 쪽)
    static void BuildScaryScene(Transform s, int type, PropKit K, Mats M, Ctx c, List<Light> lights)
    {
        switch (type)
        {
            case 0: // 길 쪽을 보는 셋 — 하나만 등을 돌림
                SnowmanLocal(s, K, M, new Vector3(-1.2f, 0, 0), 1, 1.15f, c);
                SnowmanLocal(s, K, M, new Vector3(0.3f, 0, 0.4f), 0, 1.0f, c);
                SnowmanLocal(s, K, M, new Vector3(1.6f, 0, -0.2f), 2, 0.95f, c).transform.localRotation = Quaternion.Euler(0, 180f + c.R(-10f, 10f), 0);
                break;
            case 1: // 빈 벤치 위에 머리 하나
                Local(s, K.bench, Vector3.zero, 0, 1f, M.wood, M.metal, M.snow);
                Local(s, K.head, new Vector3(c.R(-0.5f, 0.5f), 0.5f, -0.05f), c.R(-20f, 20f), 0.9f, M.snow, M.coal, M.carrot, M.bark);
                break;
            case 2: // 장독대 + 까마귀 자리
                Local(s, K.jangdok[c.RI(0, 2)], Vector3.zero, c.R(-8f, 8f), 1f, M.onggi, M.snow, M.stone);
                break;
            case 3: // 불 꺼진 이글루 + 안으로 들어간 발자국
                Local(s, K.igloo, new Vector3(0, -0.05f, -2.5f), 0, 1f, M.snow, M.coal);
                for (int i = 0; i < 9; i++)
                {
                    float z = 6f - i * 0.7f;
                    Local(s, K.print, new Vector3((i % 2 == 0 ? -0.13f : 0.13f) + Mathf.Sin(i * 0.5f) * 0.2f, 0.02f, z), 180f + c.R(-8f, 8f), 1.1f, M.print);
                }
                break;
            case 4: // 뒤집힌 썰매 + 떨어진 머리
                var sled = Local(s, K.sled, new Vector3(-0.8f, 0.3f, 0.4f), c.R(20f, 60f), 1f, M.redWood, M.metal, M.snow);
                sled.transform.localRotation *= Quaternion.Euler(0, 0, 165f);
                Local(s, K.head, new Vector3(1.0f, 0, 0.6f), c.R(-30f, 30f), 1f, M.snow, M.coal, M.carrot, M.bark);
                break;
            case 5: // 안쪽을 보고 둘러선 눈사람들 (가운데는 비어 있음)
                int n = 5;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var g = SnowmanLocal(s, K, M, new Vector3(Mathf.Cos(a) * 2.6f, 0, Mathf.Sin(a) * 2.6f), c.RI(0, 3), c.R(0.8f, 1.05f), c);
                    g.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a)), Vector3.up);
                }
                break;
            case 6: // 쓰러진 가로등 + 삽 꽂힌 눈더미
                Local(s, K.fallenLamp, Vector3.zero, c.R(0, 360), 1f, M.metal, M.deadBulb, M.snow);
                Local(s, K.shovel, new Vector3(2.4f, 0, 0.8f), c.R(0, 360), 1.1f, M.snow, M.wood, M.metal);
                break;
            default: // 길쭉한 눈사람 혼자, 팔을 길 쪽으로 뻗음
                SnowmanLocal(s, K, M, Vector3.zero, 4, c.R(1.1f, 1.35f), c).transform.localRotation = Quaternion.Euler(c.R(-4f, 4f), c.R(-10f, 10f), c.R(-6f, 6f));
                break;
        }
    }

    // 끊긴 발자국 한 줄 (메시 하나로)
    static void FootTrail(Transform parent, PropKit K, Mats M, Ctx c, int index)
    {
        Vector3 p0;
        if (!TryPoint(c, -c.s.clearance * 0.6f, 0.5f, out p0)) return;
        c.taken.RemoveAt(c.taken.Count - 1);
        Vector2 q = ClosestOnPath(c, Flat(p0));
        Vector3 dir = Flat3(p0 - new Vector3(q.x, 0, q.y));
        dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward; // 길에서 멀어지는 쪽
        var root = new GameObject("끊긴 발자국").transform;
        root.SetParent(parent, false);
        root.position = p0;
        var k = new SalcheonKit(1, index);
        Vector3 cur = p0;
        int steps = c.RI(18, 34);
        for (int i = 0; i < steps; i++)
        {
            dir = Quaternion.Euler(0, c.R(-7f, 7f), 0) * dir;
            cur += dir * 0.72f;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (i % 2 == 0 ? 0.13f : -0.13f);
            Vector3 w = cur + side;
            Vector3 lp = w - p0; lp.y = H(c, w.x, w.z) - p0.y + 0.015f;
            Quaternion rq = Quaternion.LookRotation(dir, Vector3.up);
            k.Sphere(0, lp + rq * new Vector3(0, 0, 0.03f), new Vector3(0.065f, 0.012f, 0.11f), rq, 10, 0f, 0f);
            k.Sphere(0, lp + rq * new Vector3(0, 0, -0.1f), new Vector3(0.05f, 0.01f, 0.055f), rq, 8, 0f, 0f);
        }
        var mesh = SaveMesh("발자국 줄 " + (index + 1), k.ToMesh("발자국 줄"));
        Obj(root, "발자국", mesh, M.print);
    }

    // 까마귀: 앙상한 나무 꼭대기, 자모 윗면, 울타리 말뚝, 장독대, 땅
    static void Crows(Ctx c, PropKit K, Mats M, Transform propsRoot)
    {
        var g = new GameObject("까마귀").transform;
        g.SetParent(propsRoot, false);
        int budget = Mathf.RoundToInt(40 * c.s.crowAmount);
        if (budget <= 0) return;
        var spots = new List<KeyValuePair<Vector3, float>>(); // (자리, 크기)

        var bare = c.gen.Find("앙상한 나무");
        if (bare) foreach (Transform t in bare) if (c.Chance(0.3f)) spots.Add(new KeyValuePair<Vector3, float>(t.position + t.up * t.localScale.y * 0.97f, 2.2f));

        ForEachSegment(c, (p, A, B, thick, depth) =>
        {
            if (!c.Chance(0.12f)) return;
            Vector3[] cand = { thick, -thick, depth, -depth };
            int best = 0;
            for (int i = 1; i < 4; i++) if (cand[i].normalized.y > cand[best].normalized.y) best = i;
            if (cand[best].normalized.y < 0.7f) return;
            Vector3 top = Vector3.Lerp(A, B, c.R(0.2f, 0.8f)) + cand[best];
            if (top.y < c.baseY + 3f) return;
            float extra = Mathf.Clamp((best < 2 ? depth : thick).magnitude * 0.44f, 0.12f, 1.2f); // 자모 위 눈 두께만큼
            spots.Add(new KeyValuePair<Vector3, float>(top + Vector3.up * extra, 3.2f));
        });

        foreach (var tr in propsRoot.GetComponentsInChildren<Transform>())
        {
            if (tr.name.StartsWith("장독대") && c.Chance(0.8f))
                for (int i = 0; i < 2; i++) spots.Add(new KeyValuePair<Vector3, float>(tr.TransformPoint(new Vector3(c.R(-1.5f, 1.5f), 1.45f, -0.55f)), 1.6f));
            if (tr.name == "울타리" && c.Chance(0.6f))
            {
                var mr = tr.GetComponent<Renderer>();
                if (mr) spots.Add(new KeyValuePair<Vector3, float>(new Vector3(mr.bounds.center.x, mr.bounds.max.y, mr.bounds.center.z), 1.8f));
            }
        }

        for (int i = 0; i < 4; i++) // 땅 위 무리
        {
            Vector3 p;
            if (!TryPoint(c, 0f, 1.5f, out p)) continue;
            int n = c.RI(3, 7);
            for (int j = 0; j < n; j++) spots.Add(new KeyValuePair<Vector3, float>(p + new Vector3(c.R(-1.5f, 1.5f), 0, c.R(-1.5f, 1.5f)), 1.8f));
        }

        // 섞어서 budget만큼
        for (int i = spots.Count - 1; i > 0; i--) { int j = c.RI(0, i + 1); var t = spots[i]; spots[i] = spots[j]; spots[j] = t; }
        for (int i = 0; i < spots.Count && i < budget; i++)
        {
            var t = Obj(g, "까마귀", K.crow, M.crow, M.coal).transform;
            t.position = spots[i].Key;
            t.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
            t.localScale = Vector3.one * spots[i].Value * c.R(0.9f, 1.1f);
        }
        Undo.AddComponent<SalcheonCrows>(g.gameObject);
    }

    static GameObject Local(Transform parent, Mesh mesh, Vector3 lp, float yaw, float scale, params Material[] mats)
    {
        var go = Obj(parent, mesh.name, mesh, mats);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static Material[] SnowmanMats(PropKit K, Mats M, int style, Ctx c)
    {
        return new[] { M.snow, M.coal, M.carrot, M.bark, M.scarves[c.RI(0, M.scarves.Length)], M.hats[c.RI(0, M.hats.Length)] };
    }

    static GameObject SnowmanLocal(Transform parent, PropKit K, Mats M, Vector3 lp, int style, float scale, Ctx c)
    {
        var go = Local(parent, K.snowmen[style], lp, c.R(-20f, 20f), scale, SnowmanMats(K, M, style, c));
        go.name = "눈사람";
        return go;
    }

    static GameObject Snowman(Transform parent, PropKit K, Mats M, Vector3 p, int style, float scale, Ctx c)
    {
        var go = Obj(parent, "눈사람", K.snowmen[style], SnowmanMats(K, M, style, c));
        go.transform.position = p + Vector3.down * 0.05f;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static void MoundAt(Transform parent, Vector3 p, float r, Ctx c, Mats M)
    {
        var t = Obj(parent, "눈 더미", SaveMesh("눈 더미 1", SalcheonShapes.Mound(100)), M.snow).transform;
        t.position = p + Vector3.down * 0.1f;
        t.localScale = new Vector3(r, r * 0.18f, r);
    }

    static void DuckFlock(Transform parent, PropKit K, Mats M, Vector3 p, int n, Ctx c)
    {
        var g = new GameObject("눈오리 떼").transform;
        g.SetParent(parent, false);
        g.position = p;
        g.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        for (int i = 0; i < n; i++)
        {
            Vector3 lp = new Vector3(c.R(-1.6f, 1.6f), 0, c.R(-1.2f, 1.2f));
            Vector3 wp = g.TransformPoint(lp);
            lp.y = H(c, wp.x, wp.z) - p.y;
            Local(g, K.duck, lp, c.R(-40f, 40f), c.R(1.1f, 1.6f), M.snow, M.coal, M.carrot);
        }
    }

    static void Fence(Transform parent, Vector3 p, Ctx c, Mats M, int index)
    {
        var line = new GameObject("눈 쌓인 울타리").transform;
        line.SetParent(parent, false);
        line.position = p;
        line.rotation = Quaternion.Euler(0, c.R(0, 360), 0);
        var k = new SalcheonKit(2, index);
        int posts = c.RI(6, 11);
        Vector3 prevTop = Vector3.zero; bool hasPrev = false;
        for (int i = 0; i < posts; i++)
        {
            float lx = i * c.R(1.6f, 2.1f);
            Vector3 wp = line.TransformPoint(new Vector3(lx, 0, 0));
            float gy = H(c, wp.x, wp.z) - p.y;
            float ph = c.R(1.0f, 1.5f), sink = c.R(0.1f, 0.5f);
            Quaternion lean = Quaternion.Euler(c.R(-8f, 8f), 0, c.R(-8f, 8f));
            Vector3 bottom = new Vector3(lx, gy - sink, 0), top = bottom + lean * Vector3.up * ph;
            k.Box(0, (bottom + top) * 0.5f, new Vector3(0.14f, ph, 0.14f), lean);
            k.Sphere(1, top + Vector3.up * 0.01f, new Vector3(0.12f, 0.08f, 0.12f), Quaternion.identity, 8, 0.25f, 0f);
            Vector3 railTop = top - Vector3.up * 0.25f;
            if (hasPrev)
            {
                Vector3 d = railTop - prevTop;
                Quaternion rq = Quaternion.FromToRotation(Vector3.right, d.normalized);
                Vector3 mid = (prevTop + railTop) * 0.5f;
                k.Box(0, mid, new Vector3(d.magnitude, 0.08f, 0.06f), rq);
                k.Sphere(1, mid + Vector3.up * 0.05f, new Vector3(d.magnitude * 0.5f, 0.05f, 0.06f), rq, 10, 0.3f, 0f);
                Vector3 mid2 = mid - Vector3.up * 0.45f;
                k.Box(0, mid2, new Vector3(d.magnitude, 0.08f, 0.06f), rq);
            }
            prevTop = railTop; hasPrev = true;
        }
        var mesh = SaveMesh("울타리 " + (index + 1), k.ToMesh("울타리"));
        var go = Obj(line, "울타리", mesh, M.wood, M.snow);
        go.transform.localPosition = Vector3.zero;
    }

    static Light PointLight(Transform parent, Vector3 lp, float range, float intensity, Color color)
    {
        var go = new GameObject("빛");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
        return l;
    }

    // ───────────────────────────── ④ 자모 위 눈·고드름·밑동 눈더미
    // 자모 획 하나하나를 월드 좌표로 돌면서 콜백
    delegate void SegFn(JamoPiece p, Vector3 A, Vector3 B, Vector3 thick, Vector3 depth);

    static void ForEachSegment(Ctx c, SegFn fn)
    {
        float th = c.ws.strokeThickness, dp = c.ws.strokeDepth;
        foreach (var p in c.ws.Pieces)
        {
            if (!p || p.points == null) continue;
            var tr = p.transform;
            foreach (var stroke in p.Strokes)
                for (int i = 0; i + 1 < stroke.Length; i++)
                {
                    Vector2 a2 = stroke[i] - p.pivot, b2 = stroke[i + 1] - p.pivot;
                    Vector3 A = tr.TransformPoint(new Vector3(a2.x, a2.y, 0));
                    Vector3 B = tr.TransformPoint(new Vector3(b2.x, b2.y, 0));
                    Vector2 ld = (b2 - a2).normalized;
                    Vector3 thick = tr.TransformVector(new Vector3(-ld.y, ld.x, 0) * (th * 0.5f));
                    Vector3 depth = tr.TransformVector(new Vector3(0, 0, dp * 0.5f));
                    fn(p, A, B, thick, depth);
                }
        }
    }

    static void BuildJamoSnow(Ctx c)
    {
        var M = GetMats(c);

        // 자모 윗면에 소복이 쌓인 눈
        var stripG = Group(c, c.gen, "자모 위 눈");
        if (c.s.snowOnJamo)
        {
            var strip = SaveMesh("자모 위 눈", SalcheonShapes.Strip());
            int n = 0;
            ForEachSegment(c, (p, A, B, thick, depth) =>
            {
                Vector3 d = B - A; float len = d.magnitude;
                if (len < 0.3f) return;
                // 위를 향하는 면 고르기
                Vector3[] cand = { thick, -thick, depth, -depth };
                int best = 0;
                for (int i = 1; i < 4; i++) if (cand[i].normalized.y > cand[best].normalized.y) best = i;
                Vector3 up = cand[best];
                if (up.normalized.y < 0.45f) return;
                Vector3 side = best < 2 ? depth : thick;
                float width = side.magnitude * 2f;
                float capH = Mathf.Clamp(width * 0.22f, 0.12f, 1.2f);
                Vector3 upN = up.normalized, dirN = d / len;
                Vector3 fwd = Vector3.Cross(dirN, upN);
                if (fwd.sqrMagnitude < 1e-6f) return;
                var t = Obj(stripG, "눈", strip, M.snow).transform;
                t.position = (A + B) * 0.5f + up - upN * 0.02f;
                t.rotation = Quaternion.LookRotation(fwd.normalized, upN);
                t.localScale = new Vector3(len + thick.magnitude * 1.8f, capH, width * 1.04f);
                n++;
            });
            Debug.Log("[살천스럽다] 자모 위 눈 " + n + "줄");
        }

        // 고드름
        var icG = Group(c, c.gen, "고드름");
        var icicle = SaveMesh("고드름", SalcheonShapes.Icicle());
        int total = 0;
        ForEachSegment(c, (p, A, B, thick, depth) =>
        {
            Vector3 d = B - A; float len = d.magnitude;
            if (len < 0.8f || Mathf.Abs(d.normalized.y) > 0.3f) return;
            Vector3 n = thick.y > 0 ? -thick : thick;
            Vector3 dv = depth.y > 0 ? -depth : depth;
            if (dv.normalized.y < n.normalized.y) { var tmp = n; n = dv; dv = tmp; }
            if (n.normalized.y > -0.45f) return; // 아랫면이 아래를 안 보면 패스
            float step = Mathf.Max(0.2f, c.s.icicleSpacing);
            int cnt = Mathf.Min(120, Mathf.FloorToInt(len / step));
            for (int k = 0; k < cnt && total < c.s.icicleMax; k++)
            {
                float t = (k + c.R(0.2f, 0.8f)) / Mathf.Max(1, cnt);
                Vector3 pos = A + d * t + n + dv * c.R(-0.8f, 0.8f);
                if (pos.y < c.baseY + c.s.icicleMinHeight) continue;
                if (c.Chance(0.15f)) continue;
                float L = Mathf.Min(c.R(0.3f, 1.9f) * c.s.icicleSize, pos.y - c.baseY - 0.3f);
                if (L < 0.15f) continue;
                var ic = Obj(icG, "고드름", icicle, M.icicle).transform;
                ic.position = pos;
                float r = Mathf.Clamp(L * 0.09f, 0.04f, 0.22f);
                ic.localScale = new Vector3(r * 2f, L, r * 2f);
                ic.rotation = Quaternion.Euler(c.R(-4f, 4f), c.R(0, 360), c.R(-4f, 4f));
                total++;
            }
        });

        // 자모 밑동에 쌓인 눈더미 (땅에 닿은 획 둘레)
        var driftG = Group(c, c.gen, "자모 밑 눈더미");
        if (c.s.jamoDrifts)
        {
            var mounds = new Mesh[3];
            for (int i = 0; i < 3; i++) mounds[i] = SaveMesh("눈 더미 " + (i + 1), SalcheonShapes.Mound(100 + i));
            int nd = 0;
            ForEachSegment(c, (p, A, B, thick, depth) =>
            {
                Vector3 d = B - A; float len = d.magnitude;
                float halfDown = Mathf.Abs(thick.y) + Mathf.Abs(depth.y);           // 획 상자의 위아래 절반
                float halfSide = Mathf.Min(Flat3(thick).magnitude + Flat3(depth).magnitude, 12f); // 옆 절반
                Vector3 dirFlat = Flat3(d);
                Vector3 perp = Vector3.Cross(Vector3.up, dirFlat.sqrMagnitude > 1e-4f ? dirFlat.normalized : Vector3.forward);
                int cnt = Mathf.Max(1, Mathf.FloorToInt(len / 4f));
                for (int k = 0; k <= cnt && nd < 300; k++)
                {
                    Vector3 q = Vector3.Lerp(A, B, (float)k / cnt);
                    if (q.y - halfDown > c.baseY + 1.2f) continue; // 땅에 닿은 부분만
                    if (c.Chance(0.35f)) continue;
                    float r = c.R(1.2f, 3.2f);
                    float sideSign = c.Chance(0.5f) ? 1f : -1f;
                    Vector3 pos = q + perp * sideSign * (halfSide + r * 0.25f);
                    pos.y = c.baseY;
                    var t = Obj(driftG, "눈더미", mounds[c.RI(0, 3)], M.snow).transform;
                    t.position = pos;
                    t.rotation = Quaternion.LookRotation(dirFlat.sqrMagnitude > 1e-4f ? dirFlat.normalized : Vector3.forward, Vector3.up) * Quaternion.Euler(0, 90f, 0);
                    t.localScale = new Vector3(r * c.R(1.2f, 1.8f), r * c.R(0.35f, 0.6f), r);
                    nd++;
                }
            });
        }
        Debug.Log("[살천스럽다] 고드름 " + total + "개");
    }

    // ───────────────────────────── ⑤ 눈·눈보라·후처리·돌풍·가로등 빛
    static void BuildAtmosphere(Ctx c)
    {
        var M = GetMats(c);
        var particles = Group(c, c.gen, "파티클");
        Vector3 w = WindFlat(c);
        float areaK = Mathf.Clamp(c.area.width * c.area.height / 9000f, 0.3f, 8f);
        var snowMat = ParticleMat("눈입자", new Color(0.96f, 0.98f, 1f, 1f), Color.black);
        var glintMat = ParticleMat("반짝임", new Color(0.8f, 0.93f, 1f, 1f), new Color(0.8f, 0.93f, 1f) * 2.5f);
        Vector3 ctr = new Vector3(c.center.x, c.baseY, c.center.y);

        float hK = c.topY / 25f;
        var snow = Particles(particles, "눈", ctr + Vector3.up * (c.topY * 0.5f), new Vector3(c.area.width, c.topY, c.area.height),
            12f, 160f * areaK * hK * c.s.snowAmount, new Vector2(0.05f, 0.13f), new Vector3(w.x * 2f, -1.2f, w.z * 2f), snowMat, false, true, 1f, false, true);
        var drift = Particles(particles, "땅 눈보라", ctr + Vector3.up * 0.8f, new Vector3(c.area.width, 1.5f, c.area.height),
            4f, 250f * areaK * c.s.snowAmount, new Vector2(0.02f, 0.04f), new Vector3(w.x * 6.3f, 0f, w.z * 6.3f), snowMat, true, false, 0.35f, false, true);
        Particles(particles, "얼음 반짝임", ctr + Vector3.up * 4.5f, new Vector3(c.area.width * 0.8f, 9f, c.area.height * 0.8f),
            1.5f, 120f * areaK, new Vector2(0.05f, 0.25f), Vector3.zero, glintMat, false, false, 1f, true, false);
        var bliz = Particles(particles, "카메라 눈보라", c.start, new Vector3(30f, 8f, 30f),
            2.5f, 350f * c.s.blizzardAmount * (c.s.scary ? 1.4f : 1f), new Vector2(0.02f, 0.06f), new Vector3(w.x * 9f, -1f, w.z * 9f), snowMat, true, false, 0.6f, false, true);

        // 후처리 2개
        var post = Group(c, c.gen, "후처리");
        var mainProfile = c.s.scary ? Profile("살천 후처리 (으스스)", p =>
        {
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var b = p.Add<Bloom>(true); b.threshold.Override(0.9f); b.intensity.Override(0.35f); b.scatter.Override(0.6f);
            var v = p.Add<Vignette>(true); v.color.Override(new Color32(6, 10, 18, 255)); v.intensity.Override(0.5f); v.smoothness.Override(0.45f);
            var ca = p.Add<ColorAdjustments>(true); ca.saturation.Override(-45f); ca.postExposure.Override(-0.35f); ca.contrast.Override(18f);
            p.Add<WhiteBalance>(true).temperature.Override(-35f);
            var fg = p.Add<FilmGrain>(true); fg.intensity.Override(0.35f);
            p.Add<ChromaticAberration>(true).intensity.Override(0.15f);
        }) : Profile("살천 후처리", p =>
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

        // 찬바람 구역: ㄹ · ㄹ · ㄷ
        var zonesG = Group(c, c.gen, "찬바람 구역");
        var picks = new List<JamoPiece>();
        foreach (var p in c.ws.Pieces) if (p && (p.jamo == "ㄹ" || p.jamo == "ㄷ")) picks.Add(p);
        if (picks.Count == 0) foreach (var p in c.ws.Pieces) { if (p && picks.Count < 3) picks.Add(p); }
        int zi = 1;
        foreach (var jp in picks)
        {
            var r = jp.visual ? jp.visual.GetComponent<Renderer>() : null;
            if (!r) continue;
            var b = r.bounds;
            var z = new GameObject("찬바람 구역 " + zi++ + " (" + jp.syllable + " " + jp.jamo + ")").AddComponent<ColdGustZone>();
            z.transform.SetParent(zonesG, false);
            z.transform.position = new Vector3(b.center.x, c.baseY, b.center.z);
            z.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.6f, 4f, 20f);
            z.snow = snow; z.groundDrift = drift; z.blizzard = bliz; z.gustVolume = gustVol;
        }

        // 자모 가로등 빛: ㅏ 팔 끝 / ㅂ 안쪽
        var lights = new List<Light>();
        var lampG = Group(c, c.gen, "가로등 빛");
        if (c.s.makeLamps)
        {
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
                    lights.Add(Lamp(lampG, "가로등 (" + p.syllable + " ㅏ)", wp + down, Mathf.Clamp(size * 0.012f, 0.3f, 1.4f), Mathf.Clamp(size * 0.25f, 8f, 30f), 3f, new Color32(191, 228, 255, 255), sphere, M.lampBulb));
                }
                else if (p.jamo == "ㅂ")
                {
                    lights.Add(Lamp(lampG, "갇힌 빛 (ㅂ)", r.bounds.center, Mathf.Clamp(size * 0.015f, 0.4f, 1.6f), Mathf.Clamp(size * 0.18f, 6f, 20f), 1.5f, new Color32(159, 216, 255, 255), sphere, M.lampBulb));
                }
            }
        }

        var amb = c.gen.GetComponent<SalcheonAmbience>();
        if (amb == null) amb = Undo.AddComponent<SalcheonAmbience>(c.gen.gameObject);
        Undo.RecordObject(amb, "분위기");
        amb.blizzard = bliz.transform;
        amb.blizzardOffset = new Vector3(-w.x * 6f, 1f, -w.z * 6f);
        amb.flickerAmount = c.s.scary ? 0.4f : 0.15f;

        // 무서운 분위기: 어두운 안개·낮은 빛·가끔 숨 멎는 순간
        var mood = c.gen.GetComponent<SalcheonMood>();
        if (c.s.scary)
        {
            if (mood == null) mood = Undo.AddComponent<SalcheonMood>(c.gen.gameObject);
            Undo.RecordObject(mood, "분위기");
            mood.stopDuringHush = new[] { snow, drift, bliz };
        }
        else if (mood != null) Undo.DestroyObjectImmediate(mood);
        var all = new List<Light>(lights);
        if (amb.flickerLights != null) foreach (var l in amb.flickerLights) if (l && !l.transform.IsChildOf(lampG)) all.Add(l);
        amb.flickerLights = all.ToArray();
    }

    static Light Lamp(Transform parent, string name, Vector3 pos, float bulb, float range, float intensity, Color color, Mesh sphere, Material bulbMat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var b = Obj(go.transform, "전구", sphere, bulbMat);
        b.transform.localScale = Vector3.one * bulb;
        b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
        return l;
    }

    // 글자 도착 후 자라날 것들 연결
    static void EnsureRuntime(Ctx c)
    {
        if (!c.gen) return;
        var f = c.gen.GetComponent<SalcheonFrost>();
        if (f == null) f = Undo.AddComponent<SalcheonFrost>(c.gen.gameObject);
        Undo.RecordObject(f, "서리 연출");
        var list = new List<Transform>();
        foreach (var n in new[] { "자모 위 눈", "고드름", "자모 밑 눈더미", "얼음 결정 무리" })
        {
            var t = c.gen.Find(n);
            if (t) list.Add(t);
        }
        f.growGroups = list.ToArray();
    }

    // ───────────────────────────── 파티클 · 후처리
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
        return ps;
    }

    static Material ParticleMat(string name, Color color, Color emission)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        m = new Material(sh ? sh : Shader.Find("Sprites/Default"));
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        MakeTransparent(m);
        if (emission.maxColorComponent > 0)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
    {
        string path = Dir + "/" + name + ".asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p) return p; // 손으로 조절한 값 유지
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

    // ───────────────────────────── 재질 (이미 있으면 그대로 → 직접 바꾼 색 유지)
    static Material OwnOrNew(string name, Color color, float metallic, float smooth, Color? emission = null)
    {
        var own = AssetDatabase.LoadAssetAtPath<Material>(OwnMatDir + "/" + name + ".mat");
        return own ? own : Mat(name, color, metallic, smooth, emission);
    }

    static Material Mat(string name, Color color, float metallic, float smooth, Color? emission = null)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(sh ? sh : Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (color.a < 0.99f) MakeTransparent(m);
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

    // ───────────────────────────── 메시 · 오브젝트
    static GameObject Obj(Transform parent, string name, Mesh mesh, params Material[] mats)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    static Mesh BuiltinMesh(PrimitiveType t)
    {
        var tmp = GameObject.CreatePrimitive(t);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return m;
    }

    // 같은 이름 파일이 있으면 내용만 바꿔 넣음 (씬의 참조 유지)
    static Mesh SaveMesh(string name, Mesh fresh)
    {
        string path = MeshDir + "/" + name + ".asset";
        fresh.name = name;
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old)
        {
            EditorUtility.CopySerialized(fresh, old);
            old.name = name;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(old);
            return old;
        }
        AssetDatabase.CreateAsset(fresh, path);
        return fresh;
    }

    static Mesh GroundMesh(Ctx c, Rect r)
    {
        float step = Mathf.Max(1.5f, Mathf.Max(r.width, r.height) / 180f);
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

    static Vector2 Flat(Vector3 v) { return new Vector2(v.x, v.z); }
    static Vector3 Flat3(Vector3 v) { return new Vector3(v.x, 0, v.z); }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
#endif
