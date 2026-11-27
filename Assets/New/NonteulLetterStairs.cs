// =====================================================================
//  NonteulLetterStairs.cs — 마을 길에서 글자 논(ㅂ · ㅌ)으로 올라가는 돌계단 (URP)
//
//  지금까지 만든 것은 하나도 건드리지 않고, 「공간」에 이 스크립트만 더 붙여요.
//   - ㄹ 길 끝 돌계단을 내려와 마을 길을 따라
//       서쪽(왼쪽)으로 가면 → ㅂ 단으로 올라가는 돌계단
//       동쪽(오른쪽)으로 가면 → ㅌ 단으로 올라가는 돌계단
//   - 단 돌벽에 붙여 쌓은 넓은 돌계단 + 바깥쪽 돌 난간 + 계단 위·아래 석등(따뜻한 불빛)
//   - 마을 길에서 계단 아래까지 이어지는 흙길, 계단 위 전망 마당
//   - 플레이 중: 계단 위를 걸으면 높이를 저절로 따라 오르내려요 (Space 안 눌러도 돼요)
//  만든 파일은 Assets/논틀밭틀_생성/글자계단 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(10003)] // 체험 카메라·충돌·길 붙잡기 다음에 계단 높이 맞추기
public class NonteulLetterStairs : MonoBehaviour
{
    [Tooltip("계단 폭 (m)")] [Range(1.2f, 3f)] public float stairWidth = 1.8f;
    [Tooltip("석등 불빛 밝기 (스스로 빛나는 정도 — 조명을 안 써서 가벼워요)")] [Range(0f, 4f)] public float lanternGlow = 1.4f;
    [Tooltip("석등에 진짜 빛(주변을 비추는 조명)도 켜기 — 켜면 조금 무거워져요")] public bool lanternLights = false;
    [Tooltip("계단 둘레 몇 m 안의 나무·덤불을 치울지")] [Range(0f, 6f)] public float clearRadius = 3.5f;
    [Tooltip("계단 위를 걸을 때 높이 따라가기")] public bool followStairs = true;
    [Tooltip("계단 바닥에서 눈까지 높이 (m)")] [Range(0.8f, 3f)] public float eyeHeight = 1.6f;

    // 플레이 중 계단 높이 계산용 (버튼이 채워요): (x, z) 시작 · 끝, 높이 시작 · 끝, 폭
    [HideInInspector] public List<Vector4> stairLines = new List<Vector4>();
    [HideInInspector] public List<Vector3> stairExtra = new List<Vector3>(); // (y0, y1, 반폭)
    [HideInInspector] public List<Rect> landings = new List<Rect>(); [HideInInspector] public List<float> landingY = new List<float>();
    [HideInInspector] public List<MeshFilter> cutFilters = new List<MeshFilter>();
    [HideInInspector] public List<Mesh> cutOriginals = new List<Mesh>();

    const string FOLDER = "Assets/논틀밭틀_생성";
    const string SUB = "Assets/논틀밭틀_생성/고퀄";
    const string OUT = "Assets/논틀밭틀_생성/글자계단";
    const string ROOT_NAME = "논틀밭틀_글자 논 계단 (자동 생성)";
    const string TERRACE_ROOT = "논틀밭틀_계단 다원 (자동 생성)";

    System.Random rng;
    Material mStep, mWall, mPath, mBlade, mFlame, mGranite;
    float lv = 1f; bool raised;

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        if (!LoadMaterials()) return;
        rng = new System.Random(717);
        Remove();
        var tt = GetComponent<NonteulTeaTerrace>();
        raised = tt != null && tt.raiseLetters && transform.Find(TERRACE_ROOT) != null;
        lv = tt != null ? tt.levelScale : 1f;
        if (!raised) { Debug.LogWarning("[논틀밭틀 글자 계단] 글자 단이 높여져 있지 않아요 (계단 다원의 '글자 자리 층층이 높이기'). 계단 없이 끝낼게요."); return; }

        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);
        var road = RoadLoop();

        // 서쪽: ㅂ 단 (서쪽 돌벽에 붙여, 북쪽에서 남쪽으로 올라가요)
        float yB = 6.5f * lv;
        BuildStair(root, "ㅂ 논 계단", new Vector2(-16.1f - stairWidth / 2f - 0.05f, 16.2f), new Vector2(-16.1f - stairWidth / 2f - 0.05f, 4.2f), 0f, yB,
                   new Vector2(-1f, 0f), Rect.MinMaxRect(-16.1f - stairWidth - 0.1f, 2.4f, -15.0f, 4.2f), yB, road);
        // 동쪽: ㅌ 단 (동쪽 돌벽에 붙여, 북쪽에서 남쪽으로 올라가요)
        float yT = 6.0f * lv;
        BuildStair(root, "ㅌ 논 계단", new Vector2(13.6f + stairWidth / 2f + 0.05f, 21.2f), new Vector2(13.6f + stairWidth / 2f + 0.05f, 7.6f), 0f, yT,
                   new Vector2(1f, 0f), Rect.MinMaxRect(12.6f, 5.6f, 13.6f + stairWidth + 0.1f, 7.6f), yT, road);

        ClearTreesAroundStairs();
        SaveAll(); ClearProgress();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 글자 계단] ㅂ · ㅌ 논으로 올라가는 계단을 만들었어요. Ctrl+S로 저장하세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        stairLines.Clear(); stairExtra.Clear(); landings.Clear(); landingY.Clear();
        for (int i = cutFilters.Count - 1; i >= 0; i--)
            if (i < cutOriginals.Count && cutFilters[i] != null && cutOriginals[i] != null) { cutFilters[i].sharedMesh = cutOriginals[i]; MarkDirty(cutFilters[i]); }
        cutFilters.Clear(); cutOriginals.Clear();
        MarkDirty(this); MarkDirty(gameObject);
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
        mStep = L("Assets/논틀밭틀_생성/미로/집_돌.mat"); if (mStep == null) mStep = L("Assets/논틀밭틀_생성/다원/다원_돌담.mat");
        mWall = L("Assets/논틀밭틀_생성/다원/단_돌담.mat"); if (mWall == null) mWall = mStep;
        mPath = L(SUB + "/고퀄_길_흙_1.00.mat"); if (mPath == null) mPath = L(SUB + "/고퀄_흙_1.00.mat");
        mBlade = L(SUB + "/잎_풀.mat");
        if (mStep == null || mPath == null || mBlade == null) { Debug.LogError("[논틀밭틀 글자 계단] 재질을 못 찾았어요. 계단 다원 · ㄹ길·마을 스크립트를 먼저 만들어 주세요."); return false; }
        mFlame = AssetDatabase.LoadAssetAtPath<Material>(OUT + "/석등_불빛.mat");
        if (mFlame == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            mFlame = new Material(sh) { name = "석등_불빛" }; SaveAsset(mFlame, "석등_불빛.mat");
        }
        var glow = new Color(1f, 0.58f, 0.26f);
        if (mFlame.HasProperty("_BaseColor")) mFlame.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.55f));
        if (mFlame.HasProperty("_Smoothness")) mFlame.SetFloat("_Smoothness", 0.1f);
        // 석등 돌: 잔 점박이 화강암
        mGranite = AssetDatabase.LoadAssetAtPath<Material>(OUT + "/석등_화강암.mat");
        if (mGranite == null)
        {
            var sh2 = Shader.Find("Universal Render Pipeline/Lit"); if (sh2 == null) sh2 = Shader.Find("Standard");
            mGranite = new Material(sh2) { name = "석등_화강암" }; SaveAsset(mGranite, "석등_화강암.mat");
        }
        var gt = AssetDatabase.LoadAssetAtPath<Texture2D>(OUT + "/화강암_색.asset");
        if (gt == null) { gt = MakeGranite(); gt.name = "화강암_색"; SaveAsset(gt, "화강암_색.asset"); }
        if (mGranite.HasProperty("_BaseMap")) { mGranite.SetTexture("_BaseMap", gt); mGranite.SetTextureScale("_BaseMap", Vector2.one * 2f); }
        if (mGranite.HasProperty("_BaseColor")) mGranite.SetColor("_BaseColor", Color.white);
        if (mGranite.HasProperty("_Smoothness")) mGranite.SetFloat("_Smoothness", 0.22f);
        EditorUtility.SetDirty(mGranite);
        mFlame.EnableKeyword("_EMISSION"); mFlame.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (mFlame.HasProperty("_EmissionColor")) mFlame.SetColor("_EmissionColor", glow * Mathf.Max(0.01f, lanternGlow));
        EditorUtility.SetDirty(mFlame);
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  계단 하나: 돌계단 + 바깥 난간 + 위 마당 + 석등 + 마을 길에서 오는 흙길
    // ==================================================================
    void BuildStair(Transform root, string name, Vector2 foot, Vector2 top, float y0, float y1, Vector2 outward, Rect landing, float landY, List<Vector2> road)
    {
        var h = new GameObject(name).transform; h.SetParent(root, false);
        var steps = new MB(); var wall = new MB(); var path = new MB(); var grass = new MB(); var flame = new MB(); var lamp = new MB();
        var dir = (top - foot); float len = dir.magnitude; dir /= len;
        int n = Mathf.Max(4, Mathf.CeilToInt((y1 - y0) / 0.3f)); float run = len / n, half = stairWidth / 2f;
        var side = new Vector2(outward.x, outward.y);
        for (int k = 0; k < n; k++)
        {
            var a = foot + dir * (run * k); var b = foot + dir * (run * (k + 1)); var c = (a + b) * 0.5f;
            float y = Mathf.Lerp(y0, y1, (k + 1f) / n);
            // 계단 디딤 (땅부터 쌓은 돌덩이)
            BoxXZ(steps, new Vector3(c.x, y / 2f, c.y), Mathf.Abs(dir.x) > 0.5f ? new Vector3(run + 0.02f, y, stairWidth) : new Vector3(stairWidth, y, run + 0.02f), 0.9f);
            // 바깥쪽 난간
            var wp = c + side * (half + 0.17f);
            BoxXZ(wall, new Vector3(wp.x, (y + 0.75f) / 2f, wp.y), Mathf.Abs(dir.x) > 0.5f ? new Vector3(run + 0.02f, y + 0.75f, 0.34f) : new Vector3(0.34f, y + 0.75f, run + 0.02f), 1.2f);
            // 계단 가장자리 풀
            if (rng.NextDouble() < 0.6) Ribbons(grass, new Vector3(c.x - side.x * (half - 0.05f), y, c.y - side.y * (half - 0.05f)), R(0.12f, 0.3f), 5, 0.1f, 0.7f, 0.015f);
        }
        // 위 마당 (단 위로 넘어가는 돌판)
        BoxXZ(steps, new Vector3(landing.center.x, landY / 2f + 0.02f, landing.center.y), new Vector3(landing.width, landY + 0.04f, landing.height), 0.9f);
        // 석등: 계단 아래 양쪽 + 위 마당
        var down = -dir;
        Lantern(h, lamp, flame, foot + down * 0.7f + side * (half + 0.7f), 0f);
        Lantern(h, lamp, flame, foot + down * 0.7f - side * (half + 0.7f), 0f);
        Lantern(h, lamp, flame, new Vector2(landing.xMin + 0.45f, landing.yMin + 0.45f) + (outward.x > 0 ? new Vector2(landing.width - 0.9f, 0) : Vector2.zero), landY + 0.04f);
        // 마을 길 → 계단 아래 흙길
        var start = foot + down * 0.4f; var near = NearestOn(road, start + down * 4f + side * 2f);
        var pl = new List<Vector2> { start, Vector2.Lerp(start, near, 0.5f) + side * 0.8f, near };
        Strip(path, Smooth(pl, 0.2f), 0.65f, grass);
        Emit(h, "돌계단", steps, mStep, ShadowCastingMode.On, true);
        Emit(h, "돌 난간", wall, mWall, ShadowCastingMode.On, true);
        Emit(h, "석등", lamp, mGranite != null ? mGranite : mStep, ShadowCastingMode.On, true);
        Emit(h, "석등 불빛", flame, mFlame, ShadowCastingMode.Off, false);
        Emit(h, "계단 가는 흙길", path, mPath, ShadowCastingMode.Off, true);
        Emit(h, "계단 풀", grass, mBlade, ShadowCastingMode.Off, false);

        stairLines.Add(new Vector4(foot.x, foot.y, top.x, top.y)); stairExtra.Add(new Vector3(y0, y1, half));
        landings.Add(landing); landingY.Add(landY + 0.04f);
    }

    // 석등 (우리 절·정원에 있는 팔각 석등): 팔각 받침 → 연꽃 받침 → 가는 기둥 → 받침돌
    //  → 불 밝힌 화사석(네 귀퉁이 돌기둥 사이로 불빛) → 끝이 살짝 들린 팔각 지붕돌 → 보주
    void Lantern(Transform parent, MB stone, MB flame, Vector2 p, float baseY)
    {
        var b = new Vector3(p.x, baseY, p.y); float rot = R(0f, 45f);
        Lathe(stone, b, new[] { V(0.34f, 0f), V(0.34f, 0.1f), V(0.3f, 0.14f), V(0.27f, 0.17f) }, rot, true);           // 바닥돌
        Lathe(stone, b, new[] { V(0.27f, 0.17f), V(0.31f, 0.22f), V(0.27f, 0.28f), V(0.17f, 0.32f), V(0.12f, 0.34f) }, rot, false); // 연꽃 받침
        Lathe(stone, b, new[] { V(0.1f, 0.34f), V(0.095f, 0.9f) }, rot, false);                                       // 기둥
        Lathe(stone, b, new[] { V(0.1f, 0.9f), V(0.22f, 0.96f), V(0.25f, 1.0f), V(0.25f, 1.04f), V(0.05f, 1.04f) }, rot, false); // 받침돌
        // 화사석: 안쪽 불빛 + 네 귀퉁이 돌기둥 + 위아래 테
        Lathe(flame, b, new[] { V(0.12f, 1.05f), V(0.12f, 1.33f), V(0.0f, 1.33f) }, rot, false);
        for (int i = 0; i < 4; i++)
        {
            float a = (rot + 45f + i * 90f) * Mathf.Deg2Rad;
            var c = b + new Vector3(Mathf.Cos(a) * 0.15f, 1.19f, Mathf.Sin(a) * 0.15f);
            BoxXZ(stone, c, new Vector3(0.07f, 0.3f, 0.07f), 0.4f);
        }
        Lathe(stone, b, new[] { V(0.2f, 1.04f), V(0.2f, 1.07f), V(0.0f, 1.07f) }, rot, false);
        Lathe(stone, b, new[] { V(0.2f, 1.33f), V(0.21f, 1.36f), V(0.05f, 1.36f) }, rot, false);
        // 지붕돌: 처마 끝이 살짝 들리고 가운데로 둥글게 올라가요
        Lathe(stone, b, new[] { V(0.08f, 1.36f), V(0.34f, 1.39f), V(0.38f, 1.44f), V(0.33f, 1.47f), V(0.22f, 1.55f), V(0.1f, 1.62f), V(0.06f, 1.64f) }, rot, false);
        // 보주
        Lathe(stone, b, new[] { V(0.06f, 1.64f), V(0.08f, 1.68f), V(0.07f, 1.73f), V(0.03f, 1.78f), V(0.0f, 1.8f) }, rot, false);
        if (lanternLights && lanternGlow > 0f)
        {
            var go = new GameObject("석등 빛"); go.transform.SetParent(parent, false); go.transform.localPosition = b + new Vector3(0, 1.2f, 0);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.68f, 0.38f); l.range = 6f; l.intensity = lanternGlow * 0.8f; l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForceVertex;   // 가볍게
        }
    }
    static Vector2 V(float r, float h) => new Vector2(r, h);

    // 팔각으로 돌려 깎은 모양 (r, 높이) 단면을 8면으로 돌려요. 면마다 평평하게 (각진 돌 느낌)
    static void Lathe(MB m, Vector3 b, Vector2[] prof, float rotDeg, bool capBottom)
    {
        const int S = 8;
        for (int j = 0; j < S; j++)
        {
            float a0 = (rotDeg + j * 360f / S) * Mathf.Deg2Rad, a1 = (rotDeg + (j + 1) * 360f / S) * Mathf.Deg2Rad, am = (a0 + a1) * 0.5f;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)); var dm = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
            for (int i = 0; i < prof.Length - 1; i++)
            {
                Vector2 p0 = prof[i], p1 = prof[i + 1];
                float dr = p1.x - p0.x, dh = p1.y - p0.y;
                var nrm = (dm * dh - Vector3.up * dr).normalized; if (nrm.sqrMagnitude < 1e-6f) nrm = dm;
                int k = m.v.Count; float u0 = j / (float)S, u1 = (j + 1f) / S;
                m.Add(b + d0 * p0.x + Vector3.up * p0.y, nrm, new Vector2(u0, p0.y)); m.Add(b + d1 * p0.x + Vector3.up * p0.y, nrm, new Vector2(u1, p0.y));
                m.Add(b + d1 * p1.x + Vector3.up * p1.y, nrm, new Vector2(u1, p1.y)); m.Add(b + d0 * p1.x + Vector3.up * p1.y, nrm, new Vector2(u0, p1.y));
                m.Tri(k, k + 1, k + 2); m.Tri(k, k + 2, k + 3);
            }
            if (capBottom)
            {
                int k = m.v.Count; var p0 = prof[0];
                m.Add(b + Vector3.up * p0.y, Vector3.down, Vector2.zero); m.Add(b + d0 * p0.x + Vector3.up * p0.y, Vector3.down, Vector2.zero); m.Add(b + d1 * p0.x + Vector3.up * p0.y, Vector3.down, Vector2.zero);
                m.Tri(k, k + 1, k + 2);
            }
        }
    }

    // 화강암: 밝은 회색 바탕에 검은·흰 잔점
    static Texture2D MakeGranite()
    {
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat }; var px = new Color[S * S];
        var r = new System.Random(3);
        for (int i = 0; i < px.Length; i++)
        {
            float n = (float)r.NextDouble(); var c = new Color(0.56f, 0.54f, 0.5f) * (0.9f + 0.12f * (float)r.NextDouble());
            if (n < 0.08f) c *= 0.45f; else if (n > 0.95f) c = Color.Lerp(c, Color.white, 0.4f);
            c.a = 1; px[i] = c;
        }
        t.SetPixels(px); t.Apply(true); return t;
    }

    // ==================================================================
    //  계단을 막는 나무·덤불 치우기 (원래 메시는 그대로, 자른 복사본으로 바꿔 끼워요)
    // ==================================================================
    const string VILLAGE_ROOT = "논틀밭틀_ㄹ길·마을 (자동 생성)";
    float StairDist(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < stairLines.Count; i++)
        {
            var a = new Vector2(stairLines[i].x, stairLines[i].y); var b = new Vector2(stairLines[i].z, stairLines[i].w); var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t) - stairExtra[i].z);
            // 계단 아래 흙길 · 석등 자리도 조금
            var dn = (a - b).normalized; best = Mathf.Min(best, Vector2.Distance(p, a + dn * 2.5f) - 1.5f);
        }
        for (int i = 0; i < landings.Count; i++)
        {
            var r = landings[i]; float dx = Mathf.Max(r.xMin - p.x, 0, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0, p.y - r.yMax);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    void ClearTreesAroundStairs()
    {
        var vr = transform.Find(VILLAGE_ROOT); if (vr == null || clearRadius <= 0f) return;
        // 1) 계단 가까운 나무 줄기 자리
        var trunks = new List<Vector2>();
        foreach (var mf in vr.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.name.Contains("줄기") || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var toL = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = toL.MultiplyPoint3x4(v); if (p.y > 0.3f) continue;
                var q = new Vector2(p.x, p.z);
                if (StairDist(q) < clearRadius + 1.5f) { bool dup = false; foreach (var t in trunks) if ((t - q).sqrMagnitude < 1f) { dup = true; break; } if (!dup) trunks.Add(q); }
            }
        }
        bool NearTrunk(Vector2 p, float r) { foreach (var t in trunks) if ((t - p).sqrMagnitude < r * r) return true; return false; }
        // 2) 잘라내기
        foreach (var mf in vr.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.name;
            if (n.Contains("줄기")) CutTris(mf, p => NearTrunk(p, 0.9f));
            else if (n == "활엽수 잎" || n == "활엽수 잎2" || n == "덤불") CutGroups(mf, 88, p => NearTrunk(p, 4.5f) || StairDist(p) < clearRadius);
            else if (n == "소나무 잎") CutGroups(mf, 78, p => NearTrunk(p, 4.5f) || StairDist(p) < clearRadius);
            else if (n.StartsWith("풀숲")) CutGroups(mf, 8, p => StairDist(p) < 0.6f);
        }
    }

    void CutGroups(MeshFilter mf, int group, System.Func<Vector2, bool> remove)
    {
        var src = mf.sharedMesh; if (src == null || !src.isReadable || src.vertexCount % group != 0) return;
        var toL = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var v = src.vertices; int groups = v.Length / group; var kill = new bool[groups]; bool any = false;
        for (int g = 0; g < groups; g++)
        {
            Vector3 c = Vector3.zero; for (int i = 0; i < group; i++) c += v[g * group + i]; c /= group;
            var p = toL.MultiplyPoint3x4(c); if (remove(new Vector2(p.x, p.z))) { kill[g] = true; any = true; }
        }
        if (!any) return;
        var cut = Object.Instantiate(src); cut.name = src.name + "_계단길";
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s); var keep = new List<int>(t.Length);
            for (int i = 0; i < t.Length; i += 3) if (!kill[t[i] / group]) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); }
            cut.SetTriangles(keep, s);
        }
        Swap(mf, src, cut);
    }
    void CutTris(MeshFilter mf, System.Func<Vector2, bool> remove)
    {
        var src = mf.sharedMesh; if (src == null || !src.isReadable) return;
        var toL = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix; var v = src.vertices;
        var cut = Object.Instantiate(src); cut.name = src.name + "_계단길"; bool any = false;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s); var keep = new List<int>(t.Length);
            for (int i = 0; i < t.Length; i += 3)
            {
                var p = toL.MultiplyPoint3x4((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f);
                if (remove(new Vector2(p.x, p.z))) { any = true; continue; }
                keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
            }
            cut.SetTriangles(keep, s);
        }
        if (!any) { DestroyNow(cut); return; }
        Swap(mf, src, cut);
    }
    void Swap(MeshFilter mf, Mesh src, Mesh cut)
    {
        SaveAsset(cut, "메시_글자계단_치움_" + mf.transform.parent.name + "_" + mf.name + ".asset");
        cutFilters.Add(mf); cutOriginals.Add(src);
        mf.sharedMesh = cut;
        var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = cut;
        MarkDirty(mf);
    }

    // ==================================================================
    //  플레이 중: 계단 위에서는 계단 높이를 따라가요
    // ==================================================================
    Byeolmalsseum.ExperienceCamera cam;
    void Start()
    {
        var ws = FindFirstObjectByType<Byeolmalsseum.WordSpace>();
        cam = ws != null ? ws.experienceCamera : FindFirstObjectByType<Byeolmalsseum.ExperienceCamera>();
    }
    void LateUpdate()
    {
        if (!followStairs || cam == null || !cam.MovementEnabled || cam.Paused) return;
        var t = cam.transform; var lp = transform.InverseTransformPoint(t.position); var p = new Vector2(lp.x, lp.z);
        float target = float.NaN;
        for (int i = 0; i < stairLines.Count; i++)
        {
            var a = new Vector2(stairLines[i].x, stairLines[i].y); var b = new Vector2(stairLines[i].z, stairLines[i].w);
            var ab = b - a; float s = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float lat = Mathf.Abs(Vector2.Dot(p - a, new Vector2(-ab.y, ab.x).normalized));
            float along = Vector2.Dot(p - a, ab) / ab.magnitude;
            if (lat <= stairExtra[i].z + 0.3f && along >= -1.0f && along <= ab.magnitude + 0.5f)
                target = Mathf.Lerp(stairExtra[i].x, stairExtra[i].y, s) + eyeHeight;
        }
        for (int i = 0; i < landings.Count && float.IsNaN(target); i++)
            if (landings[i].Contains(p)) target = landingY[i] + eyeHeight;
        if (float.IsNaN(target)) return;
        lp.y = Mathf.Lerp(lp.y, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
        t.position = transform.TransformPoint(lp);
    }

    // ==================================================================
    //  도우미
    // ==================================================================
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
    static Vector2 NearestOn(List<Vector2> line, Vector2 p) { Vector2 best = line[0]; float bd = float.MaxValue; foreach (var q in line) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = q; } } return best; }
    static List<Vector2> Smooth(List<Vector2> c, float step)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < c.Count - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Count - 1)];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
            for (int k = 0; k < n; k++) { float t = k / (float)n, t2 = t * t, t3 = t2 * t; o.Add(0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)); }
        }
        o.Add(c[c.Count - 1]); return o;
    }
    // 흙길 띠 + 가장자리 풀
    void Strip(MB m, List<Vector2> pl, float halfW, MB grass)
    {
        float along = 0;
        for (int i = 0; i < pl.Count - 1; i++)
        {
            var a = pl[i]; var b = pl[i + 1]; var d = (b - a); float seg = d.magnitude; if (seg < 1e-4f) continue; d /= seg; var n = new Vector2(-d.y, d.x);
            int k = m.v.Count; float y = 0.035f;
            m.Add(new Vector3(a.x - n.x * halfW, y, a.y - n.y * halfW), Vector3.up, new Vector2(-halfW, along)); m.Add(new Vector3(b.x - n.x * halfW, y, b.y - n.y * halfW), Vector3.up, new Vector2(-halfW, along + seg));
            m.Add(new Vector3(b.x + n.x * halfW, y, b.y + n.y * halfW), Vector3.up, new Vector2(halfW, along + seg)); m.Add(new Vector3(a.x + n.x * halfW, y, a.y + n.y * halfW), Vector3.up, new Vector2(halfW, along));
            m.Tri(k, k + 1, k + 2); m.Tri(k, k + 2, k + 3);
            if (i % 2 == 0) foreach (float sg in new[] { -1f, 1f }) if (rng.NextDouble() < 0.7) Ribbons(grass, new Vector3(a.x + n.x * sg * (halfW + R(0f, 0.08f)), 0.02f, a.y + n.y * sg * (halfW + R(0f, 0.08f))), R(0.1f, 0.25f), 5, 0.1f, 0.6f, 0.014f);
            along += seg;
        }
    }
    static void BoxXZ(MB m, Vector3 c, Vector3 s, float tile)
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
            m.Tri(k, k + 1, k + 2); m.Tri(k, k + 2, k + 3);
        }
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
            for (int i = 0; i < seg; i++) { int a = k0 + i * 2; m.idx.Add(a); m.idx.Add(a + 1); m.idx.Add(a + 3); m.idx.Add(a); m.idx.Add(a + 3); m.idx.Add(a + 2); }
        }
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
        SaveAsset(mesh, "메시_글자계단_" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = cast; mr.receiveShadows = true;
    }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "글자계단");
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
    static void ClearProgress()
    {
#if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(NonteulLetterStairs))]
public class NonteulLetterStairsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulLetterStairs)target;
        EditorGUILayout.HelpBox("지금까지 만든 것은 건드리지 않고 더해요.\n마을 길에서 ㅂ 논(서쪽) · ㅌ 논(동쪽)으로 올라가는 돌계단 + 난간 + 석등.\n플레이 중 계단 위에서는 높이를 저절로 따라가요.", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🪜 글자 논 계단 만들기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("글자 논 계단 지우기", GUILayout.Height(24))) Run(s.Remove);
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
