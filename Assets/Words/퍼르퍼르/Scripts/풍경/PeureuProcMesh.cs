using System.Collections.Generic;
using UnityEngine;

// [퍼르퍼르 풍경] 모양을 코드로 만드는 메시 (언덕·오솔길·풀꽃·바람개비·연·새·구름·기둥·고리)
//  - 씬에는 '어떤 모양인지'만 저장되고, 열 때마다 새로 만들어요 → 씬 파일이 가벼움.
//  - 오브젝트를 복사·이동해도 그대로 따라와요. 크기는 Scale로 바꾸면 됨.
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PeureuProcMesh : MonoBehaviour
{
    public enum Kind { 언덕, 오솔길, 풀꽃, 바람개비날개, 방패연, 가오리연, 새, 구름, 기둥, 고리 }

    public Kind kind = Kind.기둥;
    [Tooltip("언덕·오솔길·풀꽃이 따르는 땅")] public PeureuGround ground;
    [Tooltip("모양 무작위 번호 (구름·풀꽃)")] public int seed = 1;

    [Header("풀꽃 (이 오브젝트가 맡는 네모 범위, 땅 기준 x·z)")]
    public Rect area = new Rect(-20, -20, 40, 40);
    [Tooltip("풀 포기 수 (가장 빽빽한 곳 기준)")] public int tufts = 1500;
    [Tooltip("꽃 수")] public int flowers = 220;
    public Color[] flowerColors =
    {
        new Color32(0xff, 0xff, 0xff, 255), new Color32(0xf9, 0xd2, 0xdc, 255), new Color32(0xff, 0xc9, 0xa8, 255),
        new Color32(0xbf, 0xe3, 0xf7, 255), new Color32(0xff, 0xe7, 0xa0, 255), new Color32(0xd9, 0xcc, 0xf2, 255),
    };
    public Color grassTip = new Color32(0xe6, 0xec, 0xb4, 255);
    public Color grassTip2 = new Color32(0xc6, 0xdc, 0xa0, 255);

    [Header("고리 (바람 정원 윗테두리)")]
    public float ringRadius = 11f;
    public float ringThickness = .22f;

    Mesh mesh;
    bool shared;
    static readonly Dictionary<Kind, Mesh> cache = new Dictionary<Kind, Mesh>();

    void OnEnable()
    {
        if (ground) ground.Changed += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (ground) ground.Changed -= Rebuild;
        Release();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
    }
#endif

    void Release()
    {
        if (mesh != null && !shared)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        mesh = null;
    }

    public void Rebuild()
    {
        if (this == null) return;
        Release();
        bool cacheable = kind == Kind.바람개비날개 || kind == Kind.방패연 || kind == Kind.가오리연 || kind == Kind.새 || kind == Kind.기둥;
        if (cacheable && cache.TryGetValue(kind, out var c) && c != null)
        {
            mesh = c; shared = true;
        }
        else
        {
            var mb = new MB();
            switch (kind)
            {
                case Kind.언덕: BuildHills(mb); break;
                case Kind.오솔길: BuildPath(mb); break;
                case Kind.풀꽃: BuildMeadow(mb); break;
                case Kind.바람개비날개: BuildPinwheel(mb); break;
                case Kind.방패연: BuildShieldKite(mb); break;
                case Kind.가오리연: BuildRayKite(mb); break;
                case Kind.새: BuildBird(mb); break;
                case Kind.구름: BuildCloud(mb); break;
                case Kind.기둥: BuildPole(mb); break;
                case Kind.고리: BuildRing(mb); break;
            }
            mesh = mb.ToMesh(kind.ToString(), kind != Kind.언덕 && kind != Kind.오솔길 && kind != Kind.풀꽃 && kind != Kind.구름 && kind != Kind.고리);
            mesh.hideFlags = HideFlags.DontSave;
            shared = cacheable;
            if (cacheable) cache[kind] = mesh;
        }
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // ───────────────────────────── 메시 도우미
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> nrm = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color> col = new List<Color>();
        public readonly List<List<int>> subs = new List<List<int>> { new List<int>() };
        public bool hasNormals, hasColors;

        public List<int> Sub(int i) { while (subs.Count <= i) subs.Add(new List<int>()); return subs[i]; }

        public int Add(Vector3 p, Vector2 t, Color c)
        {
            v.Add(p); uv.Add(t); col.Add(c);
            return v.Count - 1;
        }
        public int Add(Vector3 p) => Add(p, Vector2.zero, Color.white);

        // 각진 삼각형 (양면이면 뒷면도)
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, bool twoSided = false)
        {
            var s = Sub(sub);
            s.Add(Add(a)); s.Add(Add(b)); s.Add(Add(c));
            if (twoSided) { s.Add(Add(a)); s.Add(Add(c)); s.Add(Add(b)); }
        }

        public void Idx(int sub, int a, int b, int c) { var s = Sub(sub); s.Add(a); s.Add(b); s.Add(c); }

        public Mesh ToMesh(string name, bool flat)
        {
            var m = new Mesh { name = "퍼르퍼르 " + name + " (실행 중 생성)" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            if (hasColors) m.SetColors(col);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
            if (hasNormals) m.SetNormals(nrm); else m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    static Vector3[] icoV;
    static int[] icoF;
    static void IcoBase()
    {
        if (icoV != null) return;
        float t = (1 + Mathf.Sqrt(5)) / 2;
        icoV = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < icoV.Length; i++) icoV[i] = icoV[i].normalized;
        icoF = new[]
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
    }

    // 둥근 덩어리 (부드러운 법선), m으로 위치·크기 변환
    static void Blob(MB mb, int sub, Matrix4x4 m, int level, float bumpy, float flattenBelow = -9f)
    {
        IcoBase();
        var tris = new List<Vector3>();
        for (int i = 0; i < icoF.Length; i += 3) { tris.Add(icoV[icoF[i]]); tris.Add(icoV[icoF[i + 1]]); tris.Add(icoV[icoF[i + 2]]); }
        for (int l = 0; l < level; l++)
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
        var map = new Dictionary<Vector3Int, int>();
        var s = mb.Sub(sub);
        Matrix4x4 nm = m.inverse.transpose;
        foreach (var p0 in tris)
        {
            var key = new Vector3Int(Mathf.RoundToInt(p0.x * 1000), Mathf.RoundToInt(p0.y * 1000), Mathf.RoundToInt(p0.z * 1000));
            if (!map.TryGetValue(key, out int idx))
            {
                float bump = 1 + bumpy * Mathf.Sin(p0.x * 5.1f + p0.y * 3.3f) * Mathf.Cos(p0.z * 4.7f - p0.y * 2.1f);
                Vector3 p = p0 * bump;
                Vector3 n = p0;
                if (p.y < flattenBelow) { p.y = flattenBelow; n = Vector3.Lerp(n, Vector3.down, .5f).normalized; }
                idx = mb.Add(m.MultiplyPoint3x4(p));
                mb.nrm.Add(nm.MultiplyVector(n).normalized);
                map[key] = idx;
            }
            s.Add(idx);
        }
        mb.hasNormals = true;
    }

    // ───────────────────────────── 언덕 (부드러운 땅)
    void BuildHills(MB mb)
    {
        if (!ground) return;
        const int N = 110;
        Vector2 c = ground.CoreCenter;
        float E = ground.extent;
        for (int i = 0; i <= N; i++)
            for (int j = 0; j <= N; j++)
            {
                float x = c.x - E + 2 * E * i / N, z = c.y - E + 2 * E * j / N;
                mb.Add(new Vector3(x, ground.Height(x, z), z), new Vector2((float)i / N, (float)j / N), Color.white);
            }
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                int a = i * (N + 1) + j, b = a + 1, d = a + N + 1, e = d + 1;
                mb.Idx(0, a, b, e); mb.Idx(0, a, e, d);
            }
    }

    // ───────────────────────────── 오솔길 (흙길 띠)
    void BuildPath(MB mb)
    {
        if (!ground) return;
        float z0 = ground.PathStartZ, z1 = ground.PathEndZ;
        float w = ground.pathWidth * .5f;
        int n = Mathf.Max(2, Mathf.CeilToInt((z1 - z0) / .8f));
        const int across = 6;
        for (int i = 0; i <= n; i++)
        {
            float z = Mathf.Lerp(z0, z1, (float)i / n);
            float px = ground.PathX(z);
            // 끝은 둥글게 좁아짐
            float endK = Mathf.Clamp01(Mathf.Min(z - z0, z1 - z) / 3f);
            float ww = w * Mathf.Lerp(.35f, 1f, Mathf.Sqrt(endK)) * (1f + .12f * Mathf.Sin(z * .7f + ground.seed));
            for (int j = 0; j <= across; j++)
            {
                float x = px + Mathf.Lerp(-ww, ww, (float)j / across);
                mb.Add(new Vector3(x, ground.Height(x, z) + .035f, z));
            }
        }
        for (int i = 0; i < n; i++)
            for (int j = 0; j < across; j++)
            {
                int a = i * (across + 1) + j, b = a + 1, d = a + across + 1, e = d + 1;
                mb.Idx(0, a, d, e); mb.Idx(0, a, e, b);
            }
    }

    // ───────────────────────────── 풀꽃 (셰이더가 바람에 흔들어 줌)
    void BuildMeadow(MB mb)
    {
        if (!ground) return;
        var rng = new System.Random(seed * 7919 + 13);
        System.Func<float> R = () => (float)rng.NextDouble();
        System.Func<float, float, float> Rr = (a, b) => a + R() * (b - a);
        mb.hasColors = true;
        Color baseC = Color.white;

        int placed = 0, tries = 0;
        while (placed < tufts && tries < tufts * 6)
        {
            tries++;
            float x = area.xMin + R() * area.width, z = area.yMin + R() * area.height;
            float dens = Density(x, z);
            if (R() > dens) continue;
            placed++;
            float y = ground.Height(x, z);
            int blades = 3;
            Color tip = Color.Lerp(grassTip, grassTip2, R());
            float h0 = Rr(.35f, .85f) * (.8f + .5f * dens);
            for (int k = 0; k < blades; k++)
            {
                float a = R() * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Rr(.035f, .06f);
                Vector3 lean = new Vector3(Rr(-.18f, .18f), 0, Rr(-.18f, .18f));
                Vector3 root = new Vector3(x + Rr(-.12f, .12f), y - .03f, z + Rr(-.12f, .12f));
                float h = h0 * Rr(.7f, 1.2f);
                int i0 = mb.Add(root - side, new Vector2(0, 0), baseC);
                int i1 = mb.Add(root + side, new Vector2(0, 0), baseC);
                int i2 = mb.Add(root + Vector3.up * h + lean, new Vector2(0, 1), tip);
                mb.Idx(0, i0, i2, i1);
            }
        }

        for (int f = 0, t2 = 0; f < flowers && t2 < flowers * 6; t2++)
        {
            float x = area.xMin + R() * area.width, z = area.yMin + R() * area.height;
            float dens = Density(x, z);
            if (R() > dens) continue;
            f++;
            float y = ground.Height(x, z);
            float h = Rr(.3f, .62f);
            Vector3 lean = new Vector3(Rr(-.08f, .08f), 0, Rr(-.08f, .08f));
            Vector3 root = new Vector3(x, y - .02f, z), top = root + Vector3.up * h + lean;
            Color stemTip = grassTip2;
            int s0 = mb.Add(root + new Vector3(-.015f, 0, 0), Vector2.zero, baseC);
            int s1 = mb.Add(root + new Vector3(.015f, 0, 0), Vector2.zero, baseC);
            int s2 = mb.Add(top, new Vector2(0, 1), stemTip);
            mb.Idx(0, s0, s2, s1);
            Color pc = flowerColors[rng.Next(flowerColors.Length)];
            float r = Rr(.07f, .13f);
            float rot = R() * Mathf.PI;
            for (int q = 0; q < 2; q++)
            {
                float a = rot + q * Mathf.PI * .5f;
                Vector3 ax = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                Vector3 up = Vector3.up * r;
                int a0 = mb.Add(top - ax - up * .3f, new Vector2(0, 1), pc);
                int a1 = mb.Add(top + ax - up * .3f, new Vector2(0, 1), pc);
                int a2 = mb.Add(top + ax + up, new Vector2(0, 1), pc);
                int a3 = mb.Add(top - ax + up, new Vector2(0, 1), pc);
                mb.Idx(0, a0, a3, a2); mb.Idx(0, a0, a2, a1);
            }
            // 위에서 보이는 꽃잎 판
            Vector3 fx = new Vector3(r * 1.1f, 0, 0), fz = new Vector3(0, 0, r * 1.1f);
            int b0 = mb.Add(top - fx, new Vector2(0, 1), pc), b1 = mb.Add(top + fz, new Vector2(0, 1), pc);
            int b2 = mb.Add(top + fx, new Vector2(0, 1), pc), b3 = mb.Add(top - fz, new Vector2(0, 1), pc);
            mb.Idx(0, b0, b1, b2); mb.Idx(0, b0, b2, b3);
        }
        // 법선은 위쪽 (셰이더가 빛 계산을 거의 안 함)
        for (int i = 0; i < mb.v.Count; i++) mb.nrm.Add(Vector3.up);
        mb.hasNormals = true;
    }

    // 풀이 얼마나 빽빽한지 (0~1): 길 가장자리·글자 근처는 빽빽, 멀어지면 성기게, 길 위·정원 단 위는 없음
    float Density(float x, float z)
    {
        float pd = ground.PathDistance(x, z);
        if (pd < ground.pathWidth * .5f + .15f) return 0f;
        if (ground.noGrass != null)
            foreach (var r in ground.noGrass) if (r.Contains(new Vector2(x, z))) return 0f;
        if (ground.clearRadius > 0f)
        {
            float dc = Vector2.Distance(new Vector2(x, z), new Vector2(ground.clearCenter.x, ground.clearCenter.z));
            if (dc < ground.clearRadius) return 0f;
        }
        float near = pd < 6f ? 1f : Mathf.Lerp(1f, .55f, Mathf.Clamp01((pd - 6f) / 20f));
        float o = ground.OutsideCore(x, z);
        float far = Mathf.Lerp(1f, .25f, Mathf.Clamp01(o / 45f));
        float patch = .55f + .45f * Mathf.PerlinNoise(x * .06f + seed, z * .06f - seed);
        return Mathf.Clamp01(near * far * patch);
    }

    // ───────────────────────────── 바람개비 날개 (반지름 1, 앞 = +Z, 두 가지 색 번갈아)
    static void BuildPinwheel(MB mb)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            System.Func<float, float, float, Vector3> P = (ang, r, z) => new Vector3(Mathf.Cos(a + ang) * r, Mathf.Sin(a + ang) * r, z);
            Vector3 c = Vector3.zero;
            Vector3 tip = P(0, 1f, 0);
            Vector3 mid = P(.75f, .72f, 0);
            Vector3 curl = P(.32f, .62f, .32f);
            int sub = i % 2;
            mb.Tri(sub, c, tip, mid, true);
            mb.Tri(sub, c, curl, tip, true);
        }
        // 가운데 핀
        Blob(mb, 2, Matrix4x4.TRS(new Vector3(0, 0, .05f), Quaternion.identity, Vector3.one * .09f), 1, 0f);
        mb.hasNormals = false;
        mb.nrm.Clear();
    }

    // ───────────────────────────── 방패연 (가로 1, 세로 1.4, 가운데 방구멍, 위에 꼭지)
    static void BuildShieldKite(MB mb)
    {
        const float hx = .5f, hy = .7f, holeR = .19f;
        Vector2 hc = new Vector2(0, -.04f);
        var angles = new List<float>();
        const int N = 40;
        for (int i = 0; i < N; i++) angles.Add(Mathf.PI * 2f * i / N);
        foreach (var corner in new[] { new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy), new Vector2(hx, -hy) })
        {
            float a = Mathf.Atan2(corner.y - hc.y, corner.x - hc.x);
            if (a < 0) a += Mathf.PI * 2f;
            angles.Add(a);
        }
        angles.Sort();
        System.Func<float, Vector3> Outer = a =>
        {
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float tx = Mathf.Abs(d.x) < 1e-5f ? 1e9f : ((d.x > 0 ? hx : -hx) - hc.x) / d.x;
            float ty = Mathf.Abs(d.y) < 1e-5f ? 1e9f : ((d.y > 0 ? hy : -hy) - hc.y) / d.y;
            Vector2 p = hc + d * Mathf.Min(tx, ty);
            return new Vector3(p.x, p.y, 0);
        };
        System.Func<float, Vector3> Inner = a => new Vector3(hc.x + Mathf.Cos(a) * holeR, hc.y + Mathf.Sin(a) * holeR, 0);
        for (int i = 0; i < angles.Count; i++)
        {
            float a0 = angles[i], a1 = angles[(i + 1) % angles.Count];
            if (i == angles.Count - 1) a1 += Mathf.PI * 2f;
            Vector3 o0 = Outer(a0), o1 = Outer(a1), i0 = Inner(a0), i1 = Inner(a1);
            mb.Tri(0, i0, o1, o0, true);
            mb.Tri(0, i0, i1, o1, true);
        }
        // 꼭지 (위쪽 동그라미) — 연 앞면(-Z)에서 보이게 살짝 앞으로
        const int M = 20;
        Vector3 kc = new Vector3(0, .42f, -.008f);
        for (int i = 0; i < M; i++)
        {
            float a0 = Mathf.PI * 2f * i / M, a1 = Mathf.PI * 2f * (i + 1) / M;
            mb.Tri(1, kc, kc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * .16f, kc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * .16f, true);
        }
        // 머리 띠 (윗변 색 띠)
        Vector3 t0 = new Vector3(-hx, hy, -.006f), t1 = new Vector3(hx, hy, -.006f), t2 = new Vector3(hx, hy - .12f, -.006f), t3 = new Vector3(-hx, hy - .12f, -.006f);
        mb.Tri(1, t0, t1, t2, true); mb.Tri(1, t0, t2, t3, true);
    }

    // ───────────────────────────── 가오리연 (마름모, 위·아래 두 색)
    static void BuildRayKite(MB mb)
    {
        Vector3 top = new Vector3(0, .75f, 0), right = new Vector3(.62f, .12f, 0), bottom = new Vector3(0, -.8f, 0), left = new Vector3(-.62f, .12f, 0);
        Vector3 c = new Vector3(0, .12f, -.06f);
        mb.Tri(0, top, right, c, true); mb.Tri(0, top, c, left, true);
        mb.Tri(1, bottom, c, right, true); mb.Tri(1, bottom, left, c, true);
    }

    // ───────────────────────────── 솟대 새 (앞 = +Z)
    static void BuildBird(MB mb)
    {
        Blob(mb, 0, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-8, 0, 0), new Vector3(.26f, .24f, .62f)), 1, .02f);
        Blob(mb, 0, Matrix4x4.TRS(new Vector3(0, .22f, .42f), Quaternion.identity, Vector3.one * .17f), 1, 0f);
        // 부리
        mb.Tri(0, new Vector3(-.05f, .22f, .55f), new Vector3(.05f, .22f, .55f), new Vector3(0, .2f, .78f), true);
        mb.Tri(0, new Vector3(0, .27f, .55f), new Vector3(0, .2f, .78f), new Vector3(.05f, .22f, .55f), true);
        mb.Tri(0, new Vector3(0, .27f, .55f), new Vector3(-.05f, .22f, .55f), new Vector3(0, .2f, .78f), true);
        // 꼬리
        mb.Tri(0, new Vector3(-.12f, .02f, -.45f), new Vector3(.12f, .02f, -.45f), new Vector3(0, .16f, -.85f), true);
        // 날개 (살짝 펼침)
        mb.Tri(0, new Vector3(.18f, .06f, .2f), new Vector3(.18f, .06f, -.3f), new Vector3(.62f, .22f, -.12f), true);
        mb.Tri(0, new Vector3(-.18f, .06f, .2f), new Vector3(-.62f, .22f, -.12f), new Vector3(-.18f, .06f, -.3f), true);
        // Blob은 부드러운 법선을 넣었으므로 나머지(삼각형)도 법선이 필요 → 다시 계산
        mb.hasNormals = false; mb.nrm.Clear();
    }

    // ───────────────────────────── 구름 (몽글몽글, 바닥은 평평)
    void BuildCloud(MB mb)
    {
        var rng = new System.Random(seed * 31 + 5);
        System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
        int n = 6 + rng.Next(4);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1) - .5f;
            float s = R(.45f, .8f) * (1f - Mathf.Abs(t) * .9f);
            var pos = new Vector3(t * 2.6f + R(-.15f, .15f), R(-.05f, .25f) + s * .3f, R(-.35f, .35f));
            Blob(mb, 0, Matrix4x4.TRS(pos, Quaternion.Euler(0, R(0, 360), 0), new Vector3(s * 1.25f, s, s * 1.1f)), 2, .05f, -.25f / s);
        }
    }

    // ───────────────────────────── 기둥 (반지름 1, 높이 1, 바닥이 0)
    static void BuildPole(MB mb)
    {
        const int S = 14;
        for (int s = 0; s < S; s++)
        {
            float a0 = Mathf.PI * 2 * s / S, a1 = Mathf.PI * 2 * (s + 1) / S;
            Vector3 b0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), b1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 t0 = b0 + Vector3.up, t1 = b1 + Vector3.up;
            int i0 = mb.Add(b0), i1 = mb.Add(b1), i2 = mb.Add(t0), i3 = mb.Add(t1);
            mb.nrm.Add(b0); mb.nrm.Add(b1); mb.nrm.Add(b0); mb.nrm.Add(b1);
            mb.Idx(0, i0, i2, i3); mb.Idx(0, i0, i3, i1);
            int c0 = mb.Add(Vector3.up), c1 = mb.Add(t1), c2 = mb.Add(t0);
            mb.nrm.Add(Vector3.up); mb.nrm.Add(Vector3.up); mb.nrm.Add(Vector3.up);
            mb.Idx(0, c0, c1, c2);
            int d0 = mb.Add(Vector3.zero), d1 = mb.Add(b0), d2 = mb.Add(b1);
            mb.nrm.Add(Vector3.down); mb.nrm.Add(Vector3.down); mb.nrm.Add(Vector3.down);
            mb.Idx(0, d0, d1, d2);
        }
        mb.hasNormals = true;
    }

    // ───────────────────────────── 고리 (바람 정원 윗테두리, 이 오브젝트 위치가 가운데)
    void BuildRing(MB mb)
    {
        const int S = 72, T = 8;
        for (int i = 0; i <= S; i++)
        {
            float a = Mathf.PI * 2 * i / S;
            Vector3 c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (int j = 0; j <= T; j++)
            {
                float b = Mathf.PI * 2 * j / T;
                Vector3 n = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                mb.Add(c * ringRadius + n * ringThickness);
                mb.nrm.Add(n);
            }
        }
        for (int i = 0; i < S; i++)
            for (int j = 0; j < T; j++)
            {
                int a = i * (T + 1) + j, b = a + 1, c = a + T + 1, d = c + 1;
                mb.Idx(0, a, d, c); mb.Idx(0, a, b, d);
            }
        mb.hasNormals = true;
    }
}
