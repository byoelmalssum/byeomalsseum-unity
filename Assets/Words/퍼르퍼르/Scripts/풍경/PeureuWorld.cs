using System.Collections.Generic;
using System.Linq;
using Byeolmalsseum;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 퍼르퍼르 · 바람 언덕
// 쓰는 법: 메뉴 Tools > 별말씀 > 퍼르퍼르 풍경 도구 놓기 → Inspector의 [① 풍경 만들기] 버튼.
//  - 주리가 놓은 자모·천줄·바람(PeureuWind)은 전혀 건드리지 않고, 그 둘레에 세계를 지어요.
//  - 오솔길은 체험 시작 지점 → ㅍ 문 → ㅍ 문 → 바람 정원 순서로 자동으로 이어져요. (자모를 옮기고 다시 누르면 길도 따라감)
//  - 만들어진 솟대·바람개비·연·구름은 씬에서 직접 옮기고, 지우고, 복사해도 돼요.
//  - 색은 Assets/Words/퍼르퍼르/풍경/Materials 에서 바꾸면 돼요.
//
//  ▸ 언덕·오솔길·풀꽃 들판      : 가운데는 평평하고 바깥은 부드러운 언덕, 풀꽃이 바람결 따라 물결침
//  ▸ 솟대 리본 장대 + 깃발줄    : 긴 오색 리본이 바람 방향으로 '퍼르퍼르'
//  ▸ 바람개비 (작은 것 + 큰 것)  : 바람이 세지면 빨리, 관객이 옆을 지나가면 휘리릭
//  ▸ 방패연 · 가오리연 · 구름   : 하늘에서 일렁이고 흘러감
//  ▸ 바람결 · 종잇조각 파티클   : 눈에 보이는 바람
//  ▸ 바람 정원 (끝 공간)        : 마지막 ㅍ 문 너머, 하늘하늘한 휘장이 둘러싼 원형 정원.
//                                 가운데에 들어서면 큰 돌풍이 불고 꽃잎이 소용돌이쳐 올라가며 공간 전체가 한꺼번에 나부낌
public class PeureuWorld : MonoBehaviour
{
    [Header("무작위 번호 (바꾸고 다시 만들면 다른 배치)")]
    public int seed = 7;

    [Header("양")]
    [Range(0, 220)] public int pinwheels = 120;
    [Range(0, 8)] public int giantPinwheels = 4;
    [Range(0, 24)] public int ribbonPoles = 12;
    [Range(0, 14)] public int kites = 7;
    [Range(0, 30)] public int clouds = 16;
    [Tooltip("풀꽃 양 (무거우면 줄이기)")] [Range(0, 2)] public float grassAmount = 1f;

    [Header("바람 정원 (끝 공간)")]
    public bool makeGarden = true;
    [Tooltip("정원 반지름 (m)")] public float gardenRadius = 11f;
    [Tooltip("마지막 글자에서 정원 입구까지 거리 (m)")] public float gardenGap = 12f;

#if UNITY_EDITOR
    const string GroupName = "풍경 (다시 만들면 이 그룹이 새로 바뀜)";
    const string GardenName = "바람 정원 (끝 공간 · 다시 만들면 새로 바뀜)";
    const string AssetDir = "Assets/Words/퍼르퍼르/풍경";
    const string SpaceName = "공간 (소품·조명 등 여기에)";

    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static bool Chance(float p) => rng.NextDouble() < p;

    // ───────────────────────────── 메뉴
    [MenuItem("Tools/별말씀/퍼르퍼르 풍경 도구 놓기")]
    static void PlaceTool()
    {
        var ws = Object.FindAnyObjectByType<WordSpace>();
        if (ws == null || ws.word != "퍼르퍼르")
        {
            EditorUtility.DisplayDialog("퍼르퍼르 풍경", "퍼르퍼르_공간 씬을 먼저 열어 주세요.\n(Tools > 별말씀 > 공간 씬 만들기 · 열기 > 퍼르퍼르)", "확인");
            return;
        }
        var w = Object.FindAnyObjectByType<PeureuWorld>();
        if (w == null)
        {
            var go = new GameObject("퍼르퍼르 풍경");
            Undo.RegisterCreatedObjectUndo(go, "퍼르퍼르 풍경 도구");
            SceneManager_Move(go, ws.gameObject.scene);
            w = Undo.AddComponent<PeureuWorld>(go);
        }
        Selection.activeGameObject = w.gameObject;
        EditorGUIUtility.PingObject(w.gameObject);
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    // ───────────────────────────── 버튼
    public void ButtonBuild()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        bool exists = transform.Find(GroupName) != null;
        if (!EditorUtility.DisplayDialog("퍼르퍼르 풍경",
            (exists ? "풍경을 새로 만들어요.\n'풍경' 그룹·'바람 정원'에서 직접 옮기거나 지운 건 처음 상태로 돌아가요.\n\n"
                    : "언덕·오솔길·풀꽃·솟대 리본·바람개비·연·구름·바람결과\n마지막 ㅍ 문 너머 '바람 정원'을 만들어요.\n\n") +
            "자모·천줄·바람 설정은 건드리지 않아요. (Ctrl+Z로 되돌리기 가능)", "만들기", "취소")) return;
        Build(ws, this, true);
    }

    public void ButtonGardenOnly()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        if (transform.Find(GroupName) == null) { EditorUtility.DisplayDialog("퍼르퍼르 풍경", "먼저 ① 풍경 만들기를 눌러 주세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("퍼르퍼르 풍경", "바람 정원(끝 공간)만 새로 만들어요.\n들판 쪽에서 직접 고친 건 그대로 남아요.", "다시 만들기", "취소")) return;
        Build(ws, this, false);
    }

    public void ButtonClear()
    {
        var ws = FindWordSpace(); if (ws == null) return;
        if (!EditorUtility.DisplayDialog("퍼르퍼르 풍경", "만든 풍경과 바람 정원을 지우고, 원래 바닥을 다시 켜요.\n(Ctrl+Z로 되돌리기 가능)", "지우기", "취소")) return;
        Undo.SetCurrentGroupName("퍼르퍼르 풍경 지우기");
        var g = transform.Find(GroupName); if (g) Undo.DestroyObjectImmediate(g.gameObject);
        var space = SpaceOf(ws);
        var gd = space.Find(GardenName); if (gd) Undo.DestroyObjectImmediate(gd.gameObject);
        var floor = OldFloor(ws);
        if (floor && !floor.activeSelf) { Undo.RecordObject(floor, "바닥"); floor.SetActive(true); }
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
    }

    static WordSpace FindWordSpace()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("퍼르퍼르 풍경", "플레이를 끄고 다시 눌러 주세요.", "확인"); return null; }
        var ws = Object.FindAnyObjectByType<WordSpace>();
        if (ws == null) EditorUtility.DisplayDialog("퍼르퍼르 풍경", "이 씬에 단어 공간이 없어요. 퍼르퍼르_공간 씬에서 눌러 주세요.", "확인");
        return ws;
    }

    static Transform SpaceOf(WordSpace ws)
    {
        var t = ws.transform.Find(SpaceName);
        return t ? t : ws.transform;
    }

    // 씬 맨 위에 있는 원래 '바닥' (풍경 언덕과 겹치지 않게 끔)
    static GameObject OldFloor(WordSpace ws)
    {
        foreach (var root in ws.gameObject.scene.GetRootGameObjects())
            if (root.name == "바닥" && root.GetComponent<MeshRenderer>()) return root;
        var inSpace = SpaceOf(ws).Find("바닥 (지워도 됨)");
        return inSpace ? inSpace.gameObject : null;
    }

    // ───────────────────────────── 배치 읽기
    struct Layout
    {
        public Vector3 start;
        public List<Vector2> gates;      // ㅍ 문 가운데 (x, z)
        public List<Rect> blocked;       // 땅에 닿은 자모 자리 (x, z)
        public List<Rect> flat;          // 바닥에 눕힌 자모 자리 (풀이 안 남)
        public List<Bounds> pieces;      // 자모 하나하나의 크기 (장대·깃발줄이 뚫고 지나가지 않게)
        public Bounds jamo;
        public Vector3 garden;
        public float pathEndZ;
        public Vector2 coreMin, coreMax;
    }

    Layout Read(WordSpace ws)
    {
        var L = new Layout { gates = new List<Vector2>(), blocked = new List<Rect>(), flat = new List<Rect>(), pieces = new List<Bounds>() };
        L.start = ws.experienceCamera && ws.experienceCamera.startPoint ? ws.experienceCamera.startPoint.position
                : ws.experienceCamera ? ws.experienceCamera.transform.position : new Vector3(0, 1.6f, -1);

        bool any = false;
        var gates = new List<Vector3>();
        foreach (var p in ws.Pieces)
        {
            if (p == null) continue;
            var rs = p.GetComponentsInChildren<Renderer>(true);
            Bounds b;
            if (rs.Length > 0 && rs[0].bounds.size.sqrMagnitude > 1e-4f)
            {
                b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
            }
            else b = new Bounds(p.transform.position, p.transform.lossyScale * .6f);

            if (!any) { L.jamo = b; any = true; } else L.jamo.Encapsulate(b);
            L.pieces.Add(b);
            if (b.min.y < 2.5f) L.blocked.Add(Rect.MinMaxRect(b.min.x - .6f, b.min.z - .6f, b.max.x + .6f, b.max.z + .6f));
            if (b.max.y < 3f) L.flat.Add(Rect.MinMaxRect(b.min.x - .3f, b.min.z - .3f, b.max.x + .3f, b.max.z + .3f));
            if (p.jamo == "ㅍ" && b.min.y < 3f && b.max.y > 2f) gates.Add(b.center);
        }
        if (!any) L.jamo = new Bounds(new Vector3(0, 5, 35), new Vector3(30, 10, 70));

        foreach (var g in gates.OrderBy(g => g.z)) L.gates.Add(new Vector2(g.x, g.z));

        float lastX = L.gates.Count > 0 ? L.gates[L.gates.Count - 1].x : L.jamo.center.x;
        float gz = L.jamo.max.z + gardenGap + gardenRadius;
        L.garden = new Vector3(lastX, 0, gz);
        L.pathEndZ = makeGarden ? gz - gardenRadius * .55f : L.jamo.max.z + 6f;

        float minX = Mathf.Min(L.jamo.min.x, L.start.x, makeGarden ? lastX - gardenRadius : L.jamo.min.x);
        float maxX = Mathf.Max(L.jamo.max.x, L.start.x, makeGarden ? lastX + gardenRadius : L.jamo.max.x);
        L.coreMin = new Vector2(minX - 14f, L.start.z - 16f);
        L.coreMax = new Vector2(maxX + 14f, (makeGarden ? gz + gardenRadius : L.jamo.max.z) + 16f);
        return L;
    }

    // ───────────────────────────── 만들기
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Transform root;
    static PeureuGround ground;
    static Layout lay;
    static readonly List<Vector2> usedSpots = new List<Vector2>();

    static void Build(WordSpace ws, PeureuWorld owner, bool everything)
    {
        mats.Clear(); usedSpots.Clear();
        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Materials");
        Undo.SetCurrentGroupName(everything ? "퍼르퍼르 풍경 만들기" : "바람 정원 다시 만들기");
        int undoGroup = Undo.GetCurrentGroup();

        // 이 도구는 씬 맨 위에 있어야 함 (단어 공간 안에 있으면 이동 범위가 언덕 끝까지 넓어져 버림)
        if (owner.transform.parent != null) Undo.SetTransformParent(owner.transform, null, "위치");
        Undo.RecordObject(owner.transform, "위치");
        owner.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        owner.transform.localScale = Vector3.one;

        lay = owner.Read(ws);
        Vector3 wind = WindDir(ws);
        Vector3 windFlat = new Vector3(wind.x, 0, wind.z); windFlat = windFlat.sqrMagnitude > 1e-4f ? windFlat.normalized : Vector3.right;

        if (everything)
        {
            rng = new System.Random(owner.seed);
            var floor = OldFloor(ws);
            if (floor && floor.activeSelf) { Undo.RecordObject(floor, "바닥 끄기"); floor.SetActive(false); }

            var old = owner.transform.Find(GroupName);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            root = new GameObject(GroupName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "퍼르퍼르 풍경");
            root.SetParent(owner.transform, false);

            Step("언덕 · 오솔길", () => BuildGround(owner));
            Step("풀꽃 들판", () => BuildMeadow(owner));
            Step("솟대 리본", () => BuildRibbonPoles(owner, windFlat));
            Step("바람개비", () => BuildPinwheels(owner, windFlat));
            Step("연", () => BuildKites(owner, windFlat));
            Step("구름", () => BuildClouds(owner, windFlat));
            Step("바람결", () => BuildWindParticles(windFlat));
        }
        else
        {
            root = owner.transform.Find(GroupName);
            ground = root ? root.GetComponentInChildren<PeureuGround>() : null;
            rng = new System.Random(owner.seed * 17 + 3);
        }

        var space = SpaceOf(ws);
        var oldGarden = space.Find(GardenName);
        if (oldGarden) Undo.DestroyObjectImmediate(oldGarden.gameObject);
        if (owner.makeGarden) Step("바람 정원", () => BuildGarden(owner, space, windFlat));

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
        Selection.activeGameObject = owner.gameObject;
        Debug.Log("[퍼르퍼르 풍경] 다 만들었어요. 저장(Ctrl+S)하고 플레이해 보세요. 바람 정원 가운데에 들어서면 큰 돌풍이 불어요.");
    }

    static void Step(string name, System.Action a)
    {
        try { a(); }
        catch (System.Exception e) { Debug.LogWarning($"[퍼르퍼르 풍경] '{name}' 만들다 문제가 생겨서 건너뛰었어요: {e.Message}\n{e.StackTrace}"); }
    }

    static Vector3 WindDir(WordSpace ws)
    {
        var w = ws.GetComponentInChildren<PeureuWind>(true);
        if (w == null) w = Object.FindAnyObjectByType<PeureuWind>();
        return w ? w.Direction : new Vector3(1, .15f, .4f).normalized;
    }

    // ───────────────────────────── 언덕 · 오솔길
    static void BuildGround(PeureuWorld owner)
    {
        var go = new GameObject("언덕", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        ground = go.AddComponent<PeureuGround>();
        ground.coreMin = lay.coreMin;
        ground.coreMax = lay.coreMax;
        ground.seed = owner.seed;

        var pts = new List<Vector2> { new Vector2(lay.start.x, lay.start.z - 9f), new Vector2(lay.start.x, lay.start.z + 4f) };
        foreach (var g in lay.gates) if (g.y > lay.start.z + 8f) pts.Add(g);
        if (owner.makeGarden) pts.Add(new Vector2(lay.garden.x, lay.pathEndZ));
        else pts.Add(new Vector2(pts[pts.Count - 1].x, lay.pathEndZ));
        // z 순서 보장
        pts = pts.OrderBy(p => p.y).ToList();
        for (int i = 1; i < pts.Count; i++) if (pts[i].y - pts[i - 1].y < 1f) pts[i] = new Vector2(pts[i].x, pts[i - 1].y + 1f);
        ground.pathPoints = pts.ToArray();
        ground.noGrass = lay.flat.ToArray();
        if (owner.makeGarden)
        {
            ground.clearCenter = lay.garden;
            ground.clearRadius = owner.gardenRadius + 2.2f;
        }

        var pm = go.AddComponent<PeureuProcMesh>();
        pm.ground = ground; pm.kind = PeureuProcMesh.Kind.언덕;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Mat("언덕", "#cbd59f");
        pm.Rebuild();

        var path = new GameObject("오솔길", typeof(MeshFilter), typeof(MeshRenderer));
        path.transform.SetParent(go.transform, false);
        var pp = path.AddComponent<PeureuProcMesh>();
        pp.ground = ground; pp.kind = PeureuProcMesh.Kind.오솔길;
        var pr = path.GetComponent<MeshRenderer>();
        pr.sharedMaterial = Mat("오솔길", "#f4e6cf");
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pp.Rebuild();

        // 정원 높이를 땅에 맞춤
        lay.garden.y = ground.Height(lay.garden.x, lay.garden.z);
    }

    // ───────────────────────────── 풀꽃 들판 (40m 조각으로 나눠서)
    static void BuildMeadow(PeureuWorld owner)
    {
        if (ground == null || owner.grassAmount <= 0f) return;
        var g = Group("풀꽃 들판 (바람결 따라 물결침)");
        var mat = GrassMat();
        float x0 = ground.coreMin.x - 42f, x1 = ground.coreMax.x + 42f;
        float z0 = ground.coreMin.y - 30f, z1 = ground.coreMax.y + 36f;
        const float S = 40f;
        int n = 0;
        for (float x = x0; x < x1; x += S)
            for (float z = z0; z < z1; z += S)
            {
                var go = new GameObject($"풀꽃 {++n}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(g, false);
                var pm = go.AddComponent<PeureuProcMesh>();
                pm.kind = PeureuProcMesh.Kind.풀꽃;
                pm.ground = ground;
                pm.seed = owner.seed * 100 + n;
                pm.area = new Rect(x, z, Mathf.Min(S, x1 - x), Mathf.Min(S, z1 - z));
                // 가운데에서 먼 조각은 성기게 (안개 속이라 잘 안 보임)
                float o = ground.OutsideCore(x + S * .5f, z + S * .5f);
                float k = Mathf.Lerp(1f, .35f, Mathf.Clamp01(o / 50f));
                pm.tufts = Mathf.RoundToInt(1700 * owner.grassAmount * k);
                pm.flowers = Mathf.RoundToInt(260 * owner.grassAmount * k);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pm.Rebuild();
            }
    }

    // ───────────────────────────── 솟대 리본 장대 + 깃발줄
    static readonly string[] RibbonHex = { "#ffb08a", "#f7a8c0", "#8fc3e6", "#ffe08a", "#bfa8e8", "#a8dcc4", "#ffffff", "#ffc9a8" };

    static void BuildRibbonPoles(PeureuWorld owner, Vector3 windFlat)
    {
        if (ground == null || owner.ribbonPoles <= 0) return;
        var g = Group("솟대 리본 장대");
        var tops = new List<(Vector3 top, int side)>();
        float zA = ground.PathStartZ + 6f, zB = (owner.makeGarden ? lay.garden.z - owner.gardenRadius - 3f : lay.jamo.max.z);
        int count = owner.ribbonPoles;
        int onHills = Mathf.Min(3, count / 4);
        int inField = count - onHills;

        for (int i = 0; i < inField; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float zt = Mathf.Lerp(zA, zB, (i / 2 + .5f) / Mathf.Ceil(inField / 2f));
            for (int tries = 0; tries < 40; tries++)
            {
                float z = zt + R(-4f, 4f);
                float x = ground.PathX(z) + side * R(14f, 26f);
                if (!Free(x, z, 3.5f, 9f) || UnderJamo(x, z, 2f)) continue;
                tops.Add((MakePole(g, x, z, R(12f, 18f), windFlat), side));
                break;
            }
        }
        for (int i = 0; i < onHills; i++)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int side = Chance(.5f) ? -1 : 1;
                float z = R(zA, zB);
                float x = (side < 0 ? ground.coreMin.x - R(4f, 18f) : ground.coreMax.x + R(4f, 18f));
                if (!Free(x, z, 3f, 12f) || UnderJamo(x, z, 2f)) continue;
                MakePole(g, x, z, R(16f, 22f), windFlat);
                break;
            }
        }

        // 같은 쪽 장대끼리 깃발줄
        var flagMats = new[] { Mat("깃발 분홍", "#f7a8c0"), Mat("깃발 하늘", "#8fc3e6"), Mat("깃발 노랑", "#ffe08a"), Mat("깃발 살구", "#ffb08a"), Mat("깃발 흰", "#fffaf2"), Mat("깃발 보라", "#bfa8e8") };
        foreach (int side in new[] { -1, 1 })
        {
            var line = tops.Where(t => t.side == side).Select(t => t.top).OrderBy(t => t.z).ToList();
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector3 a = line[i] + Vector3.down * R(2.2f, 3.2f), b = line[i + 1] + Vector3.down * R(2.2f, 3.2f);
                float d = Vector3.Distance(a, b);
                if (d > 34f || HitsJamo(a, b, d * .09f)) continue;
                Bunting(g, "깃발줄", a, b, d * .09f, flagMats);
            }
        }
    }

    static Vector3 MakePole(Transform parent, float x, float z, float h, Vector3 windFlat)
    {
        float y = ground.Height(x, z);
        var t = Child(parent, "솟대", new Vector3(x, y - .3f, z));
        Part(t, "장대", PeureuProcMesh.Kind.기둥, Mat("장대", "#cdb49a"), Vector3.zero, Quaternion.identity, new Vector3(.16f, h, .16f));
        // 솟대 새: 바람 오는 쪽을 보고 앉아 있음
        Part(t, "솟대 새", PeureuProcMesh.Kind.새, Mat("솟대 새", "#b89c82"), new Vector3(0, h + .25f, 0),
             Quaternion.LookRotation(-windFlat) * Quaternion.Euler(0, R(-35f, 35f), 0), Vector3.one * R(1.1f, 1.5f));

        int n = 3 + rng.Next(4);
        for (int k = 0; k < n; k++)
        {
            var go = new GameObject($"리본 {k + 1}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0, h - .35f - k * R(.25f, .5f), 0);
            go.transform.rotation = Quaternion.LookRotation(windFlat) * Quaternion.Euler(0, R(-25f, 25f), 0);
            var f = go.AddComponent<PeureuFlutter>();
            f.kind = PeureuFlutter.Kind.리본;
            // 리본이 글자 쪽(길 쪽)으로 날리는 장대는 짧게, 바깥으로 날리는 장대는 길게
            Vector2 c2 = ground.CoreCenter;
            bool towardCenter = Vector3.Dot(windFlat, new Vector3(c2.x - x, 0, c2.y - z)) > 0f && ground.OutsideCore(x, z) < 1f;
            f.length = towardCenter ? R(6f, 10f) : R(9f, 19f);
            f.width = R(.3f, .75f);
            f.taper = R(.1f, .5f);
            f.rows = Mathf.Clamp(Mathf.RoundToInt(f.length * 2.2f), 16, 48);
            f.cols = 2;
            f.stream = R(.75f, .95f);
            f.waveAmp = R(.35f, .6f);
            f.waveCount = R(1.1f, 2.2f);
            f.waveSpeed = R(3f, 5f);
            f.twist = R(20f, 60f);
            f.seed = R(0f, 100f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mat("리본 " + (rng.Next(RibbonHex.Length) + 1), null);
            f.Refresh();
        }
        usedSpots.Add(new Vector2(x, z));
        return new Vector3(x, y - .3f + h, z);
    }

    static void Bunting(Transform parent, string name, Vector3 a, Vector3 b, float sag, Material[] flagMats)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = a;
        var bt = go.AddComponent<PeureuBunting>();
        bt.from = Vector3.zero;
        bt.to = b - a;
        bt.sag = sag;
        bt.spacing = R(.85f, 1.05f);
        bt.flagWidth = .62f; bt.flagLength = .9f;
        bt.seed = R(0, 100);
        var mr = go.GetComponent<MeshRenderer>();
        var list = flagMats.OrderBy(_ => rng.Next()).ToArray();
        mr.sharedMaterials = list;

        var rope = new GameObject("줄", typeof(LineRenderer));
        rope.transform.SetParent(go.transform, false);
        var lr = rope.GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.widthMultiplier = .035f;
        lr.sharedMaterial = UnlitMat("줄", "#9c8672");
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.numCapVertices = 0;
        bt.rope = lr;
        bt.Refresh();
    }

    // ───────────────────────────── 바람개비
    static readonly string[][] PinPairs =
    {
        new[] { "#f7a8c0", "#fffaf2" }, new[] { "#8fc3e6", "#ffe08a" }, new[] { "#ffb08a", "#bfe3f7" },
        new[] { "#bfa8e8", "#fffaf2" }, new[] { "#a8dcc4", "#f9d2dc" }, new[] { "#ffe08a", "#f7a8c0" },
    };

    static void BuildPinwheels(PeureuWorld owner, Vector3 windFlat)
    {
        if (ground == null) return;
        var g = Group("바람개비 (가까이 지나가면 휘리릭)");
        Undo.AddComponent<PeureuSpinners>(g.gameObject);
        var pin = Mat("바람개비 핀", "#fffaf2");
        float zA = ground.PathStartZ + 2f, zB = owner.makeGarden ? lay.garden.z - owner.gardenRadius - 2f : lay.jamo.max.z + 4f;

        int n = owner.pinwheels, placed = 0;
        // ① 오솔길 가장자리에 줄지어
        int edge = Mathf.RoundToInt(n * .45f);
        for (int tries = 0; placed < edge && tries < edge * 30; tries++)
        {
            float z = R(zA, zB);
            int side = Chance(.5f) ? -1 : 1;
            float x = ground.PathX(z) + side * R(ground.pathWidth * .5f + .6f, ground.pathWidth * .5f + 3.2f);
            if (!Free(x, z, .9f, 0f)) continue;
            Pinwheel(g, x, z, R(.95f, 1.45f), R(.28f, .4f), windFlat, pin);
            placed++;
        }
        // ② 들판에 무리 지어
        int clusters = Mathf.Max(1, (n - placed) / 10);
        for (int c = 0; c < clusters && placed < n; c++)
        {
            float cz = R(zA - 4f, zB + 6f);
            int side = Chance(.5f) ? -1 : 1;
            float cx = ground.PathX(cz) + side * R(7f, 32f);
            int m = 7 + rng.Next(8);
            for (int k = 0, tries = 0; k < m && placed < n && tries < m * 20; tries++)
            {
                float x = cx + R(-4.5f, 4.5f), z = cz + R(-4.5f, 4.5f);
                if (!Free(x, z, .8f, 0f)) continue;
                Pinwheel(g, x, z, R(.9f, 1.7f), R(.3f, .45f), windFlat, pin);
                placed++; k++;
            }
        }
        // ③ 언덕 위 큰 바람개비
        for (int i = 0, tries = 0; i < owner.giantPinwheels && tries < 200; tries++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float z = R(zA, zB + 10f);
            float x = side < 0 ? ground.coreMin.x - R(2f, 16f) : ground.coreMax.x + R(2f, 16f);
            if (!Free(x, z, 4f, 10f)) continue;
            Pinwheel(g, x, z, R(9f, 14f), R(2.4f, 3.8f), windFlat, pin, true);
            usedSpots.Add(new Vector2(x, z));
            i++;
        }
    }

    static void Pinwheel(Transform parent, float x, float z, float h, float r, Vector3 windFlat, Material pin, bool giant = false)
    {
        float y = ground.Height(x, z);
        var t = Child(parent, giant ? "큰 바람개비" : "바람개비", new Vector3(x, y - .05f, z));
        float lean = giant ? 0f : 4f;
        var stick = Part(t, "막대", PeureuProcMesh.Kind.기둥, Mat("바람개비 막대", "#e9dccb"), Vector3.zero,
                         Quaternion.Euler(R(-lean, lean), 0, R(-lean, lean)), new Vector3(giant ? .14f : .018f, h, giant ? .14f : .018f), giant);
        Vector3 top = stick.localRotation * (Vector3.up * h);
        var pair = PinPairs[rng.Next(PinPairs.Length)];
        var rotor = Part(t, "날개 (돌아감)", PeureuProcMesh.Kind.바람개비날개, Mat("바람개비 " + pair[0], pair[0]),
                         top + Vector3.up * .02f, Quaternion.LookRotation(-windFlat) * Quaternion.Euler(R(-8f, 8f), R(-30f, 30f), 0), Vector3.one * r, giant);
        rotor.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat("바람개비 " + pair[0], pair[0]), Mat("바람개비 " + pair[1], pair[1]), pin };
    }

    // ───────────────────────────── 연
    static void BuildKites(PeureuWorld owner, Vector3 windFlat)
    {
        if (ground == null || owner.kites <= 0) return;
        var g = Group("연 (하늘에서 일렁임)");
        Vector2 cc = ground.CoreCenter;
        Vector3 center = new Vector3(cc.x, 0, cc.y);
        float baseAng = Mathf.Atan2(windFlat.z, windFlat.x);
        var faceMat = Mat("연 바탕", "#fffdf7");
        var marks = new[] { Mat("연 꼭지 분홍", "#f28aa8"), Mat("연 꼭지 하늘", "#6fb0e0"), Mat("연 꼭지 살구", "#ff9f72"), Mat("연 꼭지 노랑", "#ffd166") };
        var lineMat = UnlitMat("연줄", "#8c7a6b");

        for (int i = 0; i < owner.kites; i++)
        {
            float a = baseAng + Mathf.Lerp(-2.1f, 2.1f, owner.kites == 1 ? .5f : (float)i / (owner.kites - 1)) + R(-.2f, .2f);
            float dist = R(34f, 62f);
            float hgt = R(20f, 36f);
            Vector3 home = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * dist;
            home.z = Mathf.Clamp(home.z, ground.coreMin.y - 25f, ground.coreMax.y + 30f);
            home.y = ground.Height(home.x, home.z) + hgt;
            Vector3 anchor = home - windFlat * (hgt * R(.8f, 1.1f)) + new Vector3(R(-6f, 6f), 0, R(-6f, 6f));
            // 연줄이 글자 사이 길 한가운데 묶이지 않도록 바깥 언덕으로 밀어냄
            Vector3 outDir = new Vector3(anchor.x - center.x, 0, anchor.z - center.z);
            outDir = outDir.sqrMagnitude > .01f ? outDir.normalized : -windFlat;
            for (int s2 = 0; s2 < 60 && ground.OutsideCore(anchor.x, anchor.z) < 4f; s2++) anchor += outDir * 2f;
            anchor.y = ground.Height(anchor.x, anchor.z) + .3f;

            var k = Child(g, "연", home);
            var kite = k.gameObject.AddComponent<PeureuKite>();
            kite.anchor = anchor;
            kite.seed = R(0, 100);
            kite.sway = R(1.8f, 3.2f);
            kite.bob = R(1f, 1.8f);
            var body = Child(k, "몸", Vector3.zero);
            kite.body = body;

            bool shield = i % 2 == 0;
            float s = R(2.2f, 3.2f);
            var shape = Part(body, shield ? "방패연" : "가오리연", shield ? PeureuProcMesh.Kind.방패연 : PeureuProcMesh.Kind.가오리연,
                             faceMat, Vector3.zero, Quaternion.identity, Vector3.one * s);
            var mark = marks[rng.Next(marks.Length)];
            shape.GetComponent<MeshRenderer>().sharedMaterials = shield ? new[] { faceMat, mark } : new[] { mark, faceMat };
            if (!shield)
            {
                // 가오리연 긴 꼬리
                Tail(body, new Vector3(0, -.8f * s, 0), R(10f, 15f), R(.28f, .4f), mark);
                Tail(body, new Vector3(-.45f * s, .12f * s, 0), R(2.5f, 4f), .16f, faceMat);
                Tail(body, new Vector3(.45f * s, .12f * s, 0), R(2.5f, 4f), .16f, faceMat);
            }

            var line = new GameObject("연줄", typeof(LineRenderer));
            line.transform.SetParent(k, false);
            var lr = line.GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = .04f;
            lr.sharedMaterial = lineMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            kite.line = lr;
            kite.Refresh();
            foreach (var tf in body.GetComponentsInChildren<PeureuFlutter>()) tf.Refresh();

            // 연 날리는 자리 표시: 작은 얼레(실패)
            Part(g, "얼레", PeureuProcMesh.Kind.기둥, Mat("장대", "#cdb49a"), anchor + Vector3.down * .3f, Quaternion.identity, new Vector3(.18f, .5f, .18f));
        }
    }

    static void Tail(Transform body, Vector3 at, float length, float width, Material m)
    {
        var go = new GameObject("꼬리", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(body, false);
        go.transform.localPosition = at;
        var f = go.AddComponent<PeureuFlutter>();
        f.kind = PeureuFlutter.Kind.리본;
        f.length = length; f.width = width; f.taper = .2f;
        f.rows = Mathf.Clamp(Mathf.RoundToInt(length * 2.4f), 10, 40); f.cols = 1;
        f.stream = .55f; f.waveAmp = .55f; f.waveCount = 1.4f; f.waveSpeed = 4.5f; f.twist = 40f;
        f.nearRadius = 0f;
        f.seed = R(0, 100);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        f.Refresh();
    }

    // ───────────────────────────── 구름
    static void BuildClouds(PeureuWorld owner, Vector3 windFlat)
    {
        if (ground == null || owner.clouds <= 0) return;
        var g = Group("구름 (바람 따라 흘러감)");
        var m = Mat("구름", "#ffffff", "#ffe2cf", .35f);
        Vector2 cc = ground.CoreCenter;
        for (int i = 0; i < owner.clouds; i++)
        {
            float a = R(0, Mathf.PI * 2), d = R(30f, 85f);
            var pos = new Vector3(cc.x + Mathf.Cos(a) * d, R(30f, 55f), cc.y + Mathf.Sin(a) * d);
            float s = R(5f, 11f);
            var t = Part(g, "구름", PeureuProcMesh.Kind.구름, m, pos, Quaternion.Euler(0, R(0, 360), 0), new Vector3(s, s * R(.7f, 1f), s), false);
            t.GetComponent<PeureuProcMesh>().seed = owner.seed * 50 + i;
            t.GetComponent<PeureuProcMesh>().Rebuild();
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var dr = t.gameObject.AddComponent<Drift>();
            dr.velocity = windFlat * R(.5f, 1.2f);
            dr.bob = R(.3f, .8f);
            dr.bobSpeed = R(.1f, .25f);
            dr.spin = 0f;
            dr.areaCenter = new Vector3(cc.x, 0, cc.y);
            dr.areaSize = new Vector3(200f, 0, 200f);
        }
    }

    // ───────────────────────────── 바람결 (눈에 보이는 바람) + 날리는 종잇조각
    static void BuildWindParticles(Vector3 windFlat)
    {
        if (ground == null) return;
        var g = Group("바람결 (파티클)");
        Vector2 cc = ground.CoreCenter;
        Vector2 size = ground.coreMax - ground.coreMin;
        // 바람이 불어오는 쪽에서 시작하도록 상자를 바람 반대쪽으로 조금 밀어 둠
        Vector3 center = new Vector3(cc.x, 6f, cc.y) - windFlat * 12f;

        // ① 바람결: 가늘고 긴 빛줄기가 바람 방향으로 '쉬익'
        {
            var ps = NewPS(g, "바람결", center);
            var main = ps.main;
            main.maxParticles = 320;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .1f);
            main.startColor = new Color(1f, 1f, 1f, .55f);
            var em = ps.emission; em.rateOverTime = 55f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(size.x + 50f, 13f, size.y + 30f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector3 v = windFlat * 10f + Vector3.up * .4f;
            vel.x = new ParticleSystem.MinMaxCurve(v.x); vel.y = new ParticleSystem.MinMaxCurve(v.y); vel.z = new ParticleSystem.MinMaxCurve(v.z);
            var nz = ps.noise; nz.enabled = true; nz.strength = .8f; nz.frequency = .15f; nz.scrollSpeed = .25f;
            Fade(ps, .55f);
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = .55f; pr.lengthScale = 3f;
            pr.sharedMaterial = ParticleMat("바람결", "#ffffff", true, false);
        }
        // ② 종잇조각 · 꽃잎: 파스텔 조각들이 뒤집히며 날아감
        {
            var ps = NewPS(g, "날리는 종잇조각", center);
            var main = ps.main;
            main.maxParticles = 420;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.1f, .22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = PastelRandom();
            var em = ps.emission; em.rateOverTime = 34f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(size.x + 40f, 12f, size.y + 20f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector3 v = windFlat * 3.2f;
            vel.x = new ParticleSystem.MinMaxCurve(v.x); vel.y = new ParticleSystem.MinMaxCurve(-.15f); vel.z = new ParticleSystem.MinMaxCurve(v.z);
            var nz = ps.noise; nz.enabled = true; nz.strength = 1.4f; nz.frequency = .25f; nz.scrollSpeed = .4f;
            Tumble(ps);
            Fade(ps, 1f);
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            pr.alignment = ParticleSystemRenderSpace.World;
            pr.sharedMaterial = ParticleMat("종잇조각", "#ffffff", false, true);
        }
    }

    // ───────────────────────────── 바람 정원 (끝 공간)
    static void BuildGarden(PeureuWorld owner, Transform space, Vector3 windFlat)
    {
        float Rg = owner.gardenRadius;
        Vector3 c = lay.garden;
        if (ground != null) c.y = ground.Height(c.x, c.z);

        var g = new GameObject(GardenName).transform;
        Undo.RegisterCreatedObjectUndo(g.gameObject, "바람 정원");
        g.SetParent(space, false);
        g.position = c;
        g.rotation = Quaternion.identity;

        var wood = Mat("장대", "#cdb49a");
        // 단
        Part(g, "단", PeureuProcMesh.Kind.기둥, Mat("정원 단", "#fff4e0"), Vector3.down * .1f, Quaternion.identity, new Vector3(Rg + 1.8f, .32f, Rg + 1.8f));
        Part(g, "가운데 단", PeureuProcMesh.Kind.기둥, Mat("정원 가운데", "#ffd9c2"), Vector3.down * .1f, Quaternion.identity, new Vector3(Rg * .42f, .36f, Rg * .42f));

        // 기둥 + 윗테두리
        const int N = 12;
        const float H = 9f;
        Vector3 toStart = new Vector3(lay.start.x - c.x, 0, lay.start.z - c.z).normalized;
        var posts = new List<Vector3>();
        for (int i = 0; i < N; i++)
        {
            float a = Mathf.PI * 2f * (i + .5f) / N + Mathf.Atan2(toStart.z, toStart.x);
            var p = new Vector3(Mathf.Cos(a) * Rg, 0, Mathf.Sin(a) * Rg);
            posts.Add(p);
            Part(g, "기둥", PeureuProcMesh.Kind.기둥, wood, p, Quaternion.identity, new Vector3(.13f, H, .13f));
        }
        var ring = Part(g, "윗테두리", PeureuProcMesh.Kind.고리, wood, Vector3.up * H, Quaternion.identity, Vector3.one, false);
        var rpm = ring.GetComponent<PeureuProcMesh>(); rpm.ringRadius = Rg; rpm.ringThickness = .14f; rpm.Rebuild();

        // 휘장: 입구(시작 지점 쪽) 한 칸은 비움
        var sheer = new[] { SheerMat("휘장 흰", "#ffffff", .58f), SheerMat("휘장 분홍", "#f9d2dc", .6f), SheerMat("휘장 하늘", "#bfe3f7", .6f), SheerMat("휘장 살구", "#ffc9a8", .6f) };
        var curtains = Child(g, "휘장 (하늘하늘)", Vector3.zero);
        for (int i = 0; i < N; i++)
        {
            Vector3 a = posts[i], b = posts[(i + 1) % N];
            Vector3 mid = (a + b) * .5f;
            if (Vector3.Dot(mid.normalized, toStart) > .93f) continue; // 입구
            var go = new GameObject("휘장", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(curtains, false);
            go.transform.localPosition = mid + Vector3.up * (H - .12f);
            // 빨간 화살표(X축)가 a→b 방향이 되도록
            go.transform.rotation = Quaternion.LookRotation(Vector3.Cross((b - a).normalized, Vector3.up), Vector3.up);
            var f = go.AddComponent<PeureuFlutter>();
            f.kind = PeureuFlutter.Kind.휘장;
            f.width = Vector3.Distance(a, b) * .97f;
            f.length = H - .5f;
            f.cols = 10; f.rows = 16;
            f.stream = .3f; f.waveAmp = .35f; f.waveCount = .8f; f.waveSpeed = 2.2f;
            f.sideAmp = .1f; f.foldAmp = .42f; f.foldCount = 2.5f; f.billow = R(.8f, 1.2f);
            f.nearRadius = 9f;
            f.seed = R(0, 100);
            var cr = go.GetComponent<MeshRenderer>();
            cr.sharedMaterial = sheer[i % sheer.Length];
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 비치는 천이라 그림자 없이
            f.Refresh();
        }

        // 가운데 바람 기둥 + 큰 바람개비 + 리본
        const float MH = 13f;
        Part(g, "바람 기둥", PeureuProcMesh.Kind.기둥, wood, Vector3.zero, Quaternion.identity, new Vector3(.2f, MH, .2f));
        var spin = Child(g, "가운데 바람개비", Vector3.zero);
        Undo.AddComponent<PeureuSpinners>(spin.gameObject).baseSpeed = 120f;
        var pair = PinPairs[rng.Next(PinPairs.Length)];
        var rotor = Part(spin, "날개 (돌아감)", PeureuProcMesh.Kind.바람개비날개, Mat("바람개비 " + pair[0], pair[0]),
                         new Vector3(0, MH + .3f, 0) - windFlat * .25f, Quaternion.LookRotation(-windFlat), Vector3.one * 2.4f);
        rotor.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat("바람개비 " + pair[0], pair[0]), Mat("바람개비 " + pair[1], pair[1]), Mat("바람개비 핀", "#fffaf2") };

        var ribbons = Child(g, "기둥 리본", Vector3.zero);
        for (int k = 0; k < 10; k++)
        {
            var go = new GameObject($"리본 {k + 1}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(ribbons, false);
            go.transform.localPosition = new Vector3(0, MH - .6f - (k % 3) * .35f, 0);
            go.transform.rotation = Quaternion.Euler(0, k * 36f, 0);
            var f = go.AddComponent<PeureuFlutter>();
            f.kind = PeureuFlutter.Kind.리본;
            f.length = R(9f, 13f); f.width = R(.35f, .6f); f.taper = .25f;
            f.rows = 30; f.cols = 2;
            f.stream = R(.6f, .85f); f.waveAmp = .55f; f.waveCount = R(1.2f, 2f); f.waveSpeed = R(3.5f, 5f); f.twist = 50f;
            f.nearRadius = 14f;
            f.seed = R(0, 100);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat("리본 " + (k % RibbonHex.Length + 1), null);
            f.Refresh();
        }

        // 기둥 꼭대기 → 윗테두리로 깃발줄 (회전목마 지붕처럼)
        var flagMats = new[] { Mat("깃발 분홍", "#f7a8c0"), Mat("깃발 하늘", "#8fc3e6"), Mat("깃발 노랑", "#ffe08a"), Mat("깃발 살구", "#ffb08a"), Mat("깃발 흰", "#fffaf2"), Mat("깃발 보라", "#bfa8e8") };
        var canopy = Child(g, "깃발 지붕", Vector3.zero);
        for (int i = 0; i < N; i++)
        {
            Vector3 top = c + Vector3.up * (MH - .9f);
            Vector3 edge = c + posts[i] + Vector3.up * (H + .1f);
            Bunting(canopy, "깃발줄", top, edge, .7f, flagMats);
        }

        // 따뜻한 빛
        var lightGo = new GameObject("정원 햇빛");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0, 5f, 0);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = Hex("#ffd9b0"); l.range = Rg * 2.2f; l.intensity = 2.2f; l.shadows = LightShadows.None;

        // 위로 감겨 올라가는 꽃잎 소용돌이 (늘)
        {
            var ps = NewPS(g, "꽃잎 소용돌이", new Vector3(0, .4f, 0), false);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 300;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.1f, .2f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = PastelRandom();
            var em = ps.emission; em.rateOverTime = 28f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Rg * .75f; sh.rotation = new Vector3(-90f, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(1.3f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(.55f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            vel.radial = new ParticleSystem.MinMaxCurve(-.12f);
            var nz = ps.noise; nz.enabled = true; nz.strength = .5f; nz.frequency = .4f;
            Tumble(ps);
            Fade(ps, 1f);
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            pr.alignment = ParticleSystemRenderSpace.World;
            pr.sharedMaterial = ParticleMat("종잇조각", "#ffffff", false, true);
        }

        // 가운데에 들어서면: 큰 돌풍 + 꽃잎이 한꺼번에 솟구침 (주리의 BreezeZone 사용)
        {
            var zoneGo = new GameObject("가운데 돌풍 (들어서면 발동)");
            zoneGo.transform.SetParent(g, false);
            var burst = NewPS(zoneGo.transform, "솟구치는 꽃잎", new Vector3(0, .3f, 0), false);
            var main = burst.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .24f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(.08f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = PastelRandom();
            var em = burst.emission; em.rateOverTime = 0f;
            var sh = burst.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 3f; sh.rotation = new Vector3(-90f, 0, 0);
            var vel = burst.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(1.1f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            var drag = burst.limitVelocityOverLifetime; drag.enabled = true; drag.limit = new ParticleSystem.MinMaxCurve(3f); drag.dampen = .08f;
            var nz = burst.noise; nz.enabled = true; nz.strength = 1.2f; nz.frequency = .3f;
            Tumble(burst);
            Fade(burst, 1f);
            var pr = burst.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            pr.alignment = ParticleSystemRenderSpace.World;
            pr.sharedMaterial = ParticleMat("종잇조각", "#ffffff", false, true);

            var zone = zoneGo.AddComponent<BreezeZone>();
            zone.radius = 5f;
            zone.maxHeight = 9f;
            zone.cooldown = 9f;
            zone.burst = burst;
            zone.burstCount = 380;
            zone.windBoost = 24f;
            zone.boostSeconds = 4.5f;
        }
        usedSpots.Add(new Vector2(c.x, c.z));
    }

    // ───────────────────────────── 파티클 도우미
    static ParticleSystem NewPS(Transform parent, string name, Vector3 localPos, bool world = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 재생 중에는 duration을 못 바꿔서
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = world;
        main.playOnAwake = true;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var sh = ps.shape; sh.enabled = true;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    static void Fade(ParticleSystem ps, float peak)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                     new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, .15f), new GradientAlphaKey(peak, .8f), new GradientAlphaKey(0, 1) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    static void Tumble(ParticleSystem ps)
    {
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true;
        rol.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rol.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
        rol.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
    }

    static ParticleSystem.MinMaxGradient PastelRandom()
    {
        var grad = new Gradient();
        grad.SetKeys(new[]
        {
            new GradientColorKey(Hex("#ffffff"), 0f), new GradientColorKey(Hex("#f9c4d2"), .2f), new GradientColorKey(Hex("#ffd2b8"), .4f),
            new GradientColorKey(Hex("#bfe3f7"), .6f), new GradientColorKey(Hex("#ffe7a0"), .8f), new GradientColorKey(Hex("#d9ccf2"), 1f),
        }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        return new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
    }

    // ───────────────────────────── 자리 확인
    // (x, z)가 길·자모·정원·다른 큰 물체와 겹치지 않는지
    static bool Free(float x, float z, float clearance, float spotSpacing)
    {
        if (ground != null && ground.PathDistance(x, z) < ground.pathWidth * .5f + clearance) return false;
        foreach (var r in lay.blocked)
            if (x > r.xMin - clearance && x < r.xMax + clearance && z > r.yMin - clearance && z < r.yMax + clearance) return false;
        if (ground != null && ground.clearRadius > 0f &&
            Vector2.Distance(new Vector2(x, z), new Vector2(ground.clearCenter.x, ground.clearCenter.z)) < ground.clearRadius + clearance) return false;
        if (spotSpacing > 0f)
            foreach (var s in usedSpots) if (Vector2.Distance(s, new Vector2(x, z)) < spotSpacing) return false;
        return true;
    }

    // (x, z) 위로 자모가 지나가는지 (높이 상관없이)
    static bool UnderJamo(float x, float z, float margin)
    {
        foreach (var b in lay.pieces)
            if (x > b.min.x - margin && x < b.max.x + margin && z > b.min.z - margin && z < b.max.z + margin) return true;
        return false;
    }

    // a→b 깃발줄이 자모를 뚫고 지나가는지
    static bool HitsJamo(Vector3 a, Vector3 b, float sag)
    {
        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f;
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t) + .5f);
            foreach (var bb in lay.pieces)
            {
                var e = bb; e.Expand(1.2f);
                if (e.Contains(p)) return true;
            }
        }
        return false;
    }

    // ───────────────────────────── 오브젝트 도우미
    static Transform Group(string name) => Child(root, name, Vector3.zero);

    static Transform Child(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static Transform Part(Transform parent, string name, PeureuProcMesh.Kind kind, Material mat, Vector3 localPos, Quaternion localRot, Vector3 scale, bool shadow = true)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        var pm = go.AddComponent<PeureuProcMesh>();
        pm.kind = kind;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        pm.Rebuild();
        return go.transform;
    }

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    // ───────────────────────────── 재질 (이미 있으면 그대로 사용 → 직접 바꾼 색 유지)
    static Material Mat(string name, string hex, string emissionHex = null, float emission = 0)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        if (hex == null && name.StartsWith("리본 "))
        {
            int idx; if (int.TryParse(name.Substring(3), out idx)) hex = RibbonHex[Mathf.Clamp(idx - 1, 0, RibbonHex.Length - 1)];
        }
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (hex == null) return null;
            m = NewLit();
            Color c = Hex(hex);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f);
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

    static Material NewLit()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh != null) return new Material(sh);
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
        Object.DestroyImmediate(tmp);
        return m;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void MakeTransparent(Material m, bool twoSided)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0);
        if (twoSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // 하늘하늘 비치는 휘장
    static Material SheerMat(string name, string hex, float alpha)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = NewLit();
            Color c = Hex(hex); c.a = alpha;
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .2f);
            MakeTransparent(m, false); // 천 메시가 이미 양면
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Hex(hex) * .18f);
            }
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    static Material UnlitMat(string name, string hex)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            m = new Material(sh);
            SetColor(m, Hex(hex));
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    static Material ParticleMat(string name, string hex, bool soft, bool twoSided)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            SetColor(m, Hex(hex));
            if (soft)
            {
                var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
            }
            MakeTransparent(m, twoSided);
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    static Material GrassMat()
    {
        const string name = "풀꽃";
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{AssetDir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Peureu/Grass");
            if (sh == null)
            {
                Debug.LogWarning("[퍼르퍼르 풍경] 풀 셰이더(Peureu/Grass)를 못 찾아서 기본 재질로 만들었어요. Settings/PeureuGrass.shader가 있는지 확인해 주세요.");
                m = NewLit();
                SetColor(m, Hex("#c6d6a0"));
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
            }
            else m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
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
[CustomEditor(typeof(PeureuWorld))]
class PeureuWorldInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var w = (PeureuWorld)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("① 풍경 만들기 → 저장(Ctrl+S) → 플레이\n" +
                                "자모를 옮겼으면 ①을 다시 누르면 오솔길·정원 자리가 따라가요.\n" +
                                "만들어진 솟대·바람개비·연·구름은 직접 옮기고 지우고 복사해도 돼요.\n" +
                                "바람 세기·방향은 '공간'의 PeureuWind 값을 같이 써요.", MessageType.Info);
        if (GUILayout.Button("① 풍경 만들기 (언덕·들판·솟대·바람개비·연·구름·바람 정원)", GUILayout.Height(34))) w.ButtonBuild();
        if (GUILayout.Button("② 바람 정원(끝 공간)만 다시 만들기", GUILayout.Height(26))) w.ButtonGardenOnly();
        EditorGUILayout.Space(4);
        if (GUILayout.Button("풍경 지우기 (원래대로)", GUILayout.Height(22))) w.ButtonClear();
    }
}
#endif
