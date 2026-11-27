// =====================================================================
//  NonteulStairPathFix.cs — 계단으로 가는 흙길을 자연스럽게 다듬기 (URP)
//
//  지금까지 만든 것은 하나도 건드리지 않고, 「공간」에 이 스크립트만 더 붙여요.
//   - 글자 논 계단 스크립트가 만든 「계단 가는 흙길」은 지우지 않고 숨기기만 해요.
//   - 같은 자리에 새 흙길을 깔아요:
//       · 조각조각 끊긴 판이 아니라 한 장으로 이어진 길 (이음새 줄이 안 보여요)
//       · 가운데가 살짝 볼록하고 가장자리는 땅속으로 스며들어요
//       · 길 폭이 들쭉날쭉해서 사람이 밟아 다진 길처럼 보여요
//       · 계단 밑과 마을 길에 닿는 곳은 나팔처럼 넓어지며 자연스럽게 이어져요
//       · 길섶 풀과 작은 자갈
//       · 길 밑 땅 높이를 직접 재서, 다른 바닥판 밑에 묻혀 끊겨 보이지 않게 그 위에 깔아요
//   - 「되돌리기」를 누르면 새 길을 지우고 원래 길을 다시 보여 줘요.
//  만든 파일은 Assets/논틀밭틀_생성/계단흙길 폴더에 저장돼요.
// =====================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class NonteulStairPathFix : MonoBehaviour
{
    [Tooltip("오른쪽(ㅌ) 계단 가는 길 다듬기")] public bool fixRight = true;
    [Tooltip("왼쪽(ㅂ) 계단 가는 길도 다듬기")] public bool fixLeft = true;
    [Tooltip("길 반폭 (m)")] [Range(0.4f, 1.0f)] public float halfWidth = 0.62f;
    [Tooltip("길섶 풀 양")] [Range(0f, 2f)] public float edgeGrass = 1f;
    [Tooltip("길 위 작은 자갈 양")] [Range(0f, 2f)] public float pebbleAmount = 1f;

    [HideInInspector] public List<Renderer> hidden = new List<Renderer>();

    const string FOLDER = "Assets/논틀밭틀_생성";
    const string OUT = "Assets/논틀밭틀_생성/계단흙길";
    const string STAIR_ROOT = "논틀밭틀_글자 논 계단 (자동 생성)";
    const string ROOT_NAME = "논틀밭틀_계단 흙길 다듬기 (자동 생성)";
    const string OLD_PATH = "계단 가는 흙길";

    System.Random rng;
    Material mPath, mBlade, mStone;

    // ==================================================================
    //  버튼
    // ==================================================================
    public void Build()
    {
        Remove();
        if (!LoadMaterials()) return;
        var stairs = transform.Find(STAIR_ROOT);
        if (stairs == null) { Debug.LogError("[논틀밭틀 계단 흙길] 글자 논 계단이 아직 없어요. 🪜 글자 논 계단 만들기를 먼저 눌러 주세요."); return; }
        rng = new System.Random(2718);
        var root = new GameObject(ROOT_NAME).transform; root.SetParent(transform, false);
        int done = 0;
        if (fixRight && FixOne(stairs, root, "ㅌ 논 계단")) done++;
        if (fixLeft && FixOne(stairs, root, "ㅂ 논 계단")) done++;
        SaveAll();
        MarkDirty(this); MarkDirty(gameObject);
        Debug.Log("[논틀밭틀 계단 흙길] 흙길 " + done + "개를 다듬었어요. Ctrl+S로 저장하고 플레이해 보세요.");
    }

    public void Remove()
    {
        var old = transform.Find(ROOT_NAME); if (old) DestroyNow(old.gameObject);
        foreach (var r in hidden) if (r != null) { r.enabled = true; MarkDirty(r); }
        hidden.Clear();
        MarkDirty(this); MarkDirty(gameObject);
    }

    bool LoadMaterials()
    {
#if UNITY_EDITOR
        Material L(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
        mPath = L(FOLDER + "/고퀄/고퀄_길_흙_1.00.mat"); if (mPath == null) mPath = L(FOLDER + "/고퀄/고퀄_흙_1.00.mat");
        mBlade = L(FOLDER + "/고퀄/잎_풀.mat");
        mStone = L(FOLDER + "/글자계단/석등_화강암.mat"); if (mStone == null) mStone = L(FOLDER + "/미로/집_돌.mat");
        if (mPath == null) { Debug.LogError("[논틀밭틀 계단 흙길] 흙길 재질을 못 찾았어요."); return false; }
        return true;
#else
        return false;
#endif
    }

    // ==================================================================
    //  계단 하나의 흙길 다시 깔기
    // ==================================================================
    bool FixOne(Transform stairs, Transform root, string stairName)
    {
        var st = stairs.Find(stairName); if (st == null) { Debug.LogWarning("[논틀밭틀 계단 흙길] '" + stairName + "'을 못 찾았어요."); return false; }
        var ls = GetComponent<NonteulLetterStairs>();
        if (ls == null || ls.stairLines.Count < 2) { Debug.LogError("[논틀밭틀 계단 흙길] 글자 논 계단 정보가 없어요. 🪜 글자 논 계단 만들기를 다시 눌러 주세요."); return false; }

        // 계단 정보는 원래 길 모양이 아니라 계단 스크립트가 저장해 둔 값에서 직접 읽어요
        //  (ㅂ 계단 = 서쪽(x < 0), ㅌ 계단 = 동쪽(x > 0))
        int idx = -1; bool east = stairName.Length > 0 && stairName[0] == 'ㅌ';   // 글자 비교는 한 글자씩 정확하게 (StartsWith는 자모를 무시할 때가 있어요)
        for (int i = 0; i < ls.stairLines.Count; i++) if ((ls.stairLines[i].x > 0f) == east) { idx = i; break; }
        if (idx < 0) return false;
        var foot = new Vector2(ls.stairLines[idx].x, ls.stairLines[idx].y); var top = new Vector2(ls.stairLines[idx].z, ls.stairLines[idx].w);
        float half = ls.stairExtra[idx].z;
        var up = (top - foot).normalized; var down = -up; var outward = new Vector2(east ? 1f : -1f, 0f);

        // 길 줄기: 첫 계단 밑 → 석등 두 개 사이로 곧게 → 부드럽게 꺾여 마을 길로
        var road = RoadLoop();
        var p0 = foot + up * 0.35f;                         // 첫 디딤돌 밑으로 쏙 들어가요 (끊김 없게)
        var p1 = foot + down * 1.4f;                        // 석등 사이를 곧게 지나요
        var target = NearestOn(road, foot + down * 3.2f + outward * 2.2f);
        if (Vector2.Distance(target, p1) < 1.2f) target = NearestOn(road, foot + down * 4.5f + outward * 2.2f);
        var toT = target - p1;
        var p2 = p1 + down * Mathf.Min(1.2f, toT.magnitude * 0.35f) + toT * 0.25f;
        var dirIn = (target - p2).normalized;
        var p4 = target + dirIn * 0.7f;                     // 마을 길 안쪽까지 들어가서 마을 길 밑으로 스며들어요
        var pl = Relax(Resample(Smooth(new List<Vector2> { p0, foot, p1, p2, target, p4 }, 0.1f), 0.1f), 4);
        // 다듬을 때 시작·끝이 움직이지 않게 다시 고정
        pl[0] = p0; pl[pl.Count - 1] = p4;

        // 길이 지나는 자리의 실제 바닥 높이를 재요 (다른 바닥판에 묻혀서 끊겨 보이던 문제)
        var ground = new Ground(this, pl, 2.5f, out string report);
        Debug.Log("[논틀밭틀 계단 흙길 v5] " + stairName + ": 계단 밑 " + foot.ToString("F2") + " → 마을 길 " + target.ToString("F2") + ", 길이 " + Length(pl).ToString("F1") + "m" + (report.Length > 0 ? "\n   길 밑 바닥: " + report : ""));
        var path = new MB(); var grass = new MB(); var stones = new MB();
        BuildPath(path, grass, stones, pl, ground, half);
        var holder = new GameObject(stairName + " 흙길").transform; holder.SetParent(root, false);
        Emit(holder, "흙길", path, mPath, ShadowCastingMode.Off, true, stairName);
        if (mBlade != null) Emit(holder, "길섶 풀", grass, mBlade, ShadowCastingMode.Off, false, stairName);
        if (mStone != null) Emit(holder, "자갈", stones, mStone, ShadowCastingMode.On, false, stairName);

        // 원래 흙길은 숨기기만 해요
        var oldGo = st.Find(OLD_PATH);
        if (oldGo != null) { var mr = oldGo.GetComponent<MeshRenderer>(); if (mr != null && mr.enabled) { mr.enabled = false; hidden.Add(mr); MarkDirty(mr); } }
        return true;
    }

    static float Length(List<Vector2> p) { float L = 0; for (int i = 1; i < p.Count; i++) L += Vector2.Distance(p[i - 1], p[i]); return L; }
    // 마을 길 (ㄹ길·마을 스크립트와 같은 식)
    static List<Vector2> RoadLoop()
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
            Vector2 q0 = c[Mathf.Max(i - 1, 0)], q1 = c[i], q2 = c[i + 1], q3 = c[Mathf.Min(i + 2, c.Count - 1)];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(q1, q2) / step));
            for (int k = 0; k < n; k++) { float t = k / (float)n, t2 = t * t, t3 = t2 * t; o.Add(0.5f * ((2 * q1) + (-q0 + q2) * t + (2 * q0 - 5 * q1 + 4 * q2 - q3) * t2 + (-q0 + 3 * q1 - 3 * q2 + q3) * t3)); }
        }
        o.Add(c[c.Count - 1]); return o;
    }

    void BuildPath(MB m, MB grass, MB stones, List<Vector2> pl, Ground G, float stairHalf)
    {
        int n = pl.Count; var A = new float[n]; float total = 0;
        for (int i = 1; i < n; i++) { total += Vector2.Distance(pl[i - 1], pl[i]); A[i] = total; }
        // 가로 단면: 바깥 테두리(땅속) · 가장자리 · 반 · 가운데 · 반 · 가장자리 · 바깥 테두리
        float[] fr = { -1.22f, -1f, -0.5f, 0f, 0.5f, 1f, 1.22f };
        float[] hy = { -0.015f, 0.018f, 0.03f, 0.036f, 0.03f, 0.018f, -0.015f };
        int C = fr.Length; var Lw = new float[n]; var Rw = new float[n]; var N = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = A[i];
            var t = (pl[Mathf.Min(i + 1, n - 1)] - pl[Mathf.Max(i - 1, 0)]).normalized; N[i] = new Vector2(-t.y, t.x);
            // 계단 밑(시작)과 마을 길(끝)에서 나팔처럼 넓어져요
            float atStair = Smooth01(1f - (a - 0.35f) / 1.6f);   // 계단 밑에서는 계단 폭만큼 넓게
            float baseW = Mathf.Lerp(halfWidth, stairHalf * 0.95f, atStair) * (1f + 0.55f * Smooth01(1f - (total - a) / 1.8f));
            float wob = 1f - atStair * 0.8f;
            Lw[i] = baseW * (1f + wob * 0.17f * S(Noise(a * 1.2f, 3.1f)) + 0.07f * S(Noise(a * 4.7f, 8.3f)));
            Rw[i] = baseW * (1f + wob * 0.17f * S(Noise(a * 1.2f, 13.7f)) + 0.07f * S(Noise(a * 4.7f, 21.9f)));
        }
        for (int i = 0; i < n; i++)
        {
            float a = A[i];
            float sink = 0.022f * Smooth01(1f - (total - a) / 1.1f);   // 마을 길 쪽 끝은 마을 길 밑으로 스며들어요
            for (int c = 0; c < C; c++)
            {
                float w = fr[c] < 0 ? Lw[i] : Rw[i]; float off = fr[c] * w;
                if (Mathf.Abs(fr[c]) > 1.1f) off += Mathf.Sign(fr[c]) * 0.05f * S(Noise(a * 6f, c * 5.3f));   // 땅으로 스미는 들쭉날쭉한 테두리
                var p = pl[i] + N[i] * off;
                float y = G.H(p) + hy[c] - sink + 0.004f * S(Noise(p.x * 3.1f, p.y * 3.1f));       // 실제 바닥 위로, 살짝 울퉁불퉁
                m.Add(new Vector3(p.x, y, p.y), Vector3.up, new Vector2(off, a));
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int c = 0; c < C - 1; c++)
            {
                int k = i * C + c;
                m.Tri(k, k + C, k + C + 1); m.Tri(k, k + C + 1, k + 1);
            }

        // 길섶 풀: 가장자리 따라 촘촘히, 가장자리 안쪽(밟혀서 듬성한 곳)에도 조금
        for (float a = 0.15f; a < total; a += 0.12f / Mathf.Max(0.05f, edgeGrass))
        {
            int i = IndexAt(A, a);
            foreach (float sg in new[] { -1f, 1f })
            {
                if (rng.NextDouble() > 0.75) continue;
                float w = sg < 0 ? Lw[i] : Rw[i]; float o = sg * w * R(0.88f, 1.25f);
                var p = pl[i] + N[i] * o;
                Ribbons(grass, new Vector3(p.x, G.H(p) + 0.005f, p.y), R(0.08f, 0.24f), rng.Next(3, 7), 0.12f, 0.6f, 0.013f);
            }
            if (rng.NextDouble() < 0.12 * edgeGrass)
            {
                float sg = rng.NextDouble() < 0.5 ? -1f : 1f; float w = sg < 0 ? Lw[i] : Rw[i];
                var p = pl[i] + N[i] * (sg * w * R(0.55f, 0.8f));
                Ribbons(grass, new Vector3(p.x, G.H(p) + 0.02f, p.y), R(0.05f, 0.12f), 3, 0.06f, 0.5f, 0.01f);
            }
        }
        // 작은 자갈
        int count = Mathf.RoundToInt(total * 7f * pebbleAmount);
        for (int k = 0; k < count; k++)
        {
            int i = IndexAt(A, R(0f, total)); float f = R(-0.95f, 0.95f); float w = f < 0 ? Lw[i] : Rw[i];
            var p = pl[i] + N[i] * (f * w);
            Pebble(stones, new Vector3(p.x, G.H(p) + 0.022f, p.y), R(0.01f, 0.03f));
        }
    }

    // ==================================================================
    //  바닥 높이 재기: 길 둘레에 있는 낮은 바닥 메시(풀밭·땅판 등)의 윗면 높이
    //  (마을 흙길·계단·석등·풀잎·이 스크립트가 만든 것은 빼요 — 마을 길 밑으로는 스며들어야 하니까)
    // ==================================================================
    class Ground
    {
        const float CELL = 0.5f, MAXY = 0.45f;
        readonly Dictionary<long, List<Vector3>> grid = new Dictionary<long, List<Vector3>>();
        readonly List<Terrain> terrains = new List<Terrain>(); readonly Transform self;
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public Ground(NonteulStairPathFix owner, List<Vector2> pl, float margin, out string report)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in pl) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y); }
            x0 -= margin; x1 += margin; z0 -= margin; z1 += margin;
            self = owner.transform; var w2l = self.worldToLocalMatrix;
            var area = new Bounds(); bool first = true;
            foreach (var c in new[] { new Vector3(x0, -1f, z0), new Vector3(x1, -1f, z0), new Vector3(x0, 1f, z1), new Vector3(x1, 1f, z1), new Vector3(x0, 1f, z0), new Vector3(x1, -1f, z1) })
            { var w = self.TransformPoint(c); if (first) { area = new Bounds(w, Vector3.zero); first = false; } else area.Encapsulate(w); }
            var found = new Dictionary<string, float>();
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<MeshRenderer>();
#endif
            foreach (var r in all)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !r.bounds.Intersects(area)) continue;
                if (Skip(r, self)) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var toL = w2l * r.transform.localToWorldMatrix;
                if (!mf.sharedMesh.isReadable)
                {
                    // 읽을 수 없는 메시(가져온 모델 등): 얇은 판이면 상자 윗면으로 대신해요
                    var bb = mf.sharedMesh.bounds; var lo = toL.MultiplyPoint3x4(bb.min); var hi = toL.MultiplyPoint3x4(bb.max);
                    float bx0 = Mathf.Min(lo.x, hi.x), bx1 = Mathf.Max(lo.x, hi.x), bz0 = Mathf.Min(lo.z, hi.z), bz1 = Mathf.Max(lo.z, hi.z), by = Mathf.Max(lo.y, hi.y);
                    bool thinPlate = Mathf.Abs(hi.y - lo.y) <= 0.15f, bigSlab = (bx1 - bx0) > 3f && (bz1 - bz0) > 3f;
                    if (!(thinPlate || bigSlab) || by > MAXY || by < -0.3f) continue;
                    AddTri(new Vector3(bx0, by, bz0), new Vector3(bx1, by, bz0), new Vector3(bx1, by, bz1), x0, x1, z0, z1);
                    AddTri(new Vector3(bx0, by, bz0), new Vector3(bx1, by, bz1), new Vector3(bx0, by, bz1), x0, x1, z0, z1);
                    found[r.name + "(판)"] = by; continue;
                }
                var m = mf.sharedMesh; var v = m.vertices; var t = m.triangles;
                var lv = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) lv[i] = toL.MultiplyPoint3x4(v[i]);
                float usedTop = float.MinValue;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = lv[t[i]], b = lv[t[i + 1]], c = lv[t[i + 2]];
                    float ymax = Mathf.Max(a.y, Mathf.Max(b.y, c.y)), ymin = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                    if (ymin > MAXY || ymax < -0.3f) continue;
                    var nrm = Vector3.Cross(b - a, c - a); if (Mathf.Abs(nrm.normalized.y) < 0.6f) continue;   // 벽처럼 선 면은 빼요
                    float tx0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), tx1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), tz0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), tz1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    if (tx1 < x0 || tx0 > x1 || tz1 < z0 || tz0 > z1) continue;
                    AddTri(a, b, c, x0, x1, z0, z1);
                    usedTop = Mathf.Max(usedTop, Mathf.Min(ymax, MAXY));
                }
                if (usedTop > -0.05f) { string key = r.name; found[key] = found.ContainsKey(key) ? Mathf.Max(found[key], usedTop) : usedTop; }
            }
            foreach (var tr in Terrain.activeTerrains) if (tr != null && tr.enabled) terrains.Add(tr);
            // 충돌체가 있는 바닥(큐브·평면·ProBuilder로 만든 땅 등)도 위에서 아래로 쏘아서 재요
            Physics.SyncTransforms();
            for (float gx = x0; gx <= x1; gx += 0.25f)
                for (float gz = z0; gz <= z1; gz += 0.25f)
                {
                    var from = self.TransformPoint(new Vector3(gx, 3f, gz)); var dn = -self.up;
                    foreach (var h in Physics.RaycastAll(from, dn, 4f * self.lossyScale.y, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var r = h.collider.GetComponent<Renderer>();
                        if (r != null && Skip(r, self)) continue;
                        if (Skip(h.collider.transform)) continue;
                        var lp = self.InverseTransformPoint(h.point); var ln = self.InverseTransformDirection(h.normal);
                        if (lp.y > MAXY || lp.y < -0.3f || ln.y < 0.6f) continue;
                        float e = 0.13f;
                        AddTri(new Vector3(gx - e, lp.y, gz - e), new Vector3(gx + e, lp.y, gz - e), new Vector3(gx + e, lp.y, gz + e), x0, x1, z0, z1);
                        AddTri(new Vector3(gx - e, lp.y, gz - e), new Vector3(gx + e, lp.y, gz + e), new Vector3(gx - e, lp.y, gz + e), x0, x1, z0, z1);
                        string key = h.collider.name + "(충돌체)"; found[key] = found.ContainsKey(key) ? Mathf.Max(found[key], lp.y) : lp.y;
                    }
                }
            var parts = new List<string>(); foreach (var kv in found) parts.Add(kv.Key + " " + (kv.Value * 100f).ToString("F0") + "cm");
            report = string.Join(", ", parts);
        }

        void AddTri(Vector3 a, Vector3 b, Vector3 c, float x0, float x1, float z0, float z1)
        {
            float tx0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), tx1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), tz0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), tz1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            for (int gx = Mathf.FloorToInt(Mathf.Max(tx0, x0) / CELL); gx <= Mathf.FloorToInt(Mathf.Min(tx1, x1) / CELL); gx++)
                for (int gz = Mathf.FloorToInt(Mathf.Max(tz0, z0) / CELL); gz <= Mathf.FloorToInt(Mathf.Min(tz1, z1) / CELL); gz++)
                {
                    long k = Key(gx, gz); if (!grid.TryGetValue(k, out var list)) { list = new List<Vector3>(); grid[k] = list; }
                    list.Add(a); list.Add(b); list.Add(c);
                }
        }

        static bool Skip(Transform tt)
        {
            for (var t = tt; t != null; t = t.parent)
                if (t.name == ROOT_NAME || t.name.StartsWith("석등", System.StringComparison.Ordinal) || t.name == "돌계단" || t.name == "돌 난간" || t.name == "흙길" || t.name == OLD_PATH || t.name.Contains("농로") || t.name.Contains("카메라") || t.name.Contains("충돌")) return true;
            return false;
        }
        static bool Skip(Renderer r, Transform self)
        {
            for (var t = r.transform; t != null; t = t.parent)
                if (t.name == ROOT_NAME || t.name.StartsWith("석등", System.StringComparison.Ordinal) || t.name == "돌계단" || t.name == "돌 난간" || t.name == "흙길" || t.name == OLD_PATH || t.name.Contains("농로")) return true;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (m.name.Contains("잎") || m.name.Contains("줄기") || m.name.Contains("불빛") || m.renderQueue >= 2900) return true;   // 풀잎·나뭇잎·물·투명한 것은 빼요
            }
            return false;
        }

        // (x, z) 자리 바닥 윗면 높이 (없으면 0)
        public float H(Vector2 p)
        {
            float best = 0f; bool any = false;
            foreach (var tr in terrains)
            {
                var w = self.TransformPoint(new Vector3(p.x, 0f, p.y)); var tp = tr.transform.position; var size = tr.terrainData.size;
                if (w.x < tp.x || w.z < tp.z || w.x > tp.x + size.x || w.z > tp.z + size.z) continue;
                float y = self.InverseTransformPoint(new Vector3(w.x, tr.SampleHeight(w) + tp.y, w.z)).y;
                if (y <= MAXY && (!any || y > best)) { best = y; any = true; }
            }
            if (!grid.TryGetValue(Key(Mathf.FloorToInt(p.x / CELL), Mathf.FloorToInt(p.y / CELL)), out var list)) return any ? Mathf.Max(best, 0f) : 0f;
            for (int i = 0; i + 2 < list.Count; i += 3)
            {
                Vector3 a = list[i], b = list[i + 1], c = list[i + 2];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z); if (Mathf.Abs(d) < 1e-8f) continue;
                float w0 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / d, w1 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / d, w2 = 1f - w0 - w1;
                if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                float y = w0 * a.y + w1 * b.y + w2 * c.y; if (y > MAXY) continue;
                if (!any || y > best) { best = y; any = true; }
            }
            return any ? Mathf.Max(best, 0f) : 0f;
        }
    }

    // ==================================================================
    //  도우미
    // ==================================================================
    static List<Vector2> Resample(List<Vector2> line, float step)
    {
        var o = new List<Vector2> { line[0] }; float carry = 0;
        for (int i = 0; i < line.Count - 1; i++)
        {
            var a = line[i]; var b = line[i + 1]; float seg = Vector2.Distance(a, b); float d = step - carry;
            while (d <= seg) { o.Add(Vector2.Lerp(a, b, d / seg)); d += step; }
            carry = seg - (d - step);
        }
        if (Vector2.Distance(o[o.Count - 1], line[line.Count - 1]) > step * 0.3f) o.Add(line[line.Count - 1]);
        return o;
    }
    static List<Vector2> Relax(List<Vector2> p, int iters)
    {
        for (int it = 0; it < iters; it++)
        {
            var q = new List<Vector2>(p);
            for (int i = 1; i < p.Count - 1; i++) q[i] = p[i] * 0.5f + (p[i - 1] + p[i + 1]) * 0.25f;
            p = q;
        }
        return p;
    }
    static int IndexAt(float[] A, float d) { int i = System.Array.BinarySearch(A, d); if (i < 0) i = ~i; return Mathf.Clamp(i, 0, A.Length - 1); }

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
            for (int j = 0; j < LO; j++)
            {
                int a = k0 + i * (LO + 1) + j;
                m.Tri(a, a + LO + 1, a + LO + 2); m.Tri(a, a + LO + 2, a + 1);
            }
    }

    void Emit(Transform parent, string name, MB mb, Material mat, ShadowCastingMode cast, bool tangents, string tag)
    {
        if (mb.v.Count == 0) return;
        var mesh = mb.ToMesh(name, tangents);
        SaveAsset(mesh, "메시_계단흙길_" + tag + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = cast; r.receiveShadows = true;
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
            if (tangents) { m.RecalculateNormals(); m.RecalculateTangents(); }
            return m;
        }
    }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float S(float x) => (x - 0.5f) * 2f;
    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f; } }
    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
    }
    static void DestroyNow(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    static void SaveAsset(Object o, string file)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || o == null) return;
        if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder("Assets", "논틀밭틀_생성");
        if (!AssetDatabase.IsValidFolder(OUT)) AssetDatabase.CreateFolder(FOLDER, "계단흙길");
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
}

#if UNITY_EDITOR
[CustomEditor(typeof(NonteulStairPathFix))]
public class NonteulStairPathFixEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var s = (NonteulStairPathFix)target;
        EditorGUILayout.HelpBox("[v5] 지금까지 만든 것은 건드리지 않고 더해요.\n계단으로 가는 흙길을 한 장으로 이어진 자연스러운 길로 다시 깔아요.\n(원래 길은 지우지 않고 숨기기만 해요)", MessageType.Info);
        GUILayout.Space(4);
        if (GUILayout.Button("🟫 계단 흙길 다듬기", GUILayout.Height(40))) Run(s.Build);
        if (GUILayout.Button("되돌리기 (원래 길 다시 보이기)", GUILayout.Height(24))) Run(s.Remove);
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
