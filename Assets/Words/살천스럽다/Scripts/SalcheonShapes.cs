using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [살천스럽다] 소품 모양을 코드로 빚는 도구 (SalcheonSpace가 씀. 직접 붙이는 스크립트 아님)
//  - 눈 쌓인 전나무, 눈 덤불, 눈사람, 눈오리, 이글루, 장독대, 벤치, 가로등, 썰매 …
//  - 모든 소품은 바닥(y=0)에 서고, 앞(+Z)을 바라보게 만들어짐
//  - 한 소품 = 메시 하나 + 재질 여러 칸 (오브젝트 수를 줄여서 가볍게)
public class SalcheonKit
{
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<Vector3> n = new List<Vector3>();
    readonly List<int>[] subs;
    public int seed;

    public SalcheonKit(int subCount, int seed = 0)
    {
        subs = new List<int>[subCount];
        for (int i = 0; i < subCount; i++) subs[i] = new List<int>();
        this.seed = seed;
    }

    // 앞면이 outward 쪽을 보도록 자동으로 감는 방향을 맞춤
    void Tri(int s, int a, int b, int c, Vector3 outward)
    {
        Vector3 f = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (f.sqrMagnitude < 1e-12f) return;
        if (Vector3.Dot(f, outward) < 0) { int t = b; b = c; c = t; }
        subs[s].Add(a); subs[s].Add(b); subs[s].Add(c);
    }

    int Add(Vector3 p, Vector3 nn) { v.Add(p); n.Add(nn); return v.Count - 1; }

    float Noise(Vector3 p)
    {
        float s = seed * 0.731f;
        return Mathf.PerlinNoise(p.x * 1.9f + s, p.z * 1.9f + p.y * 1.3f) * 0.6f
             + Mathf.PerlinNoise(p.y * 2.3f + s * 2f, p.x * 2.1f - p.z) * 0.4f;
    }

    // 타원체 (cutBelow: -1이면 온전한 공, 0이면 위쪽 반구, 0.3이면 윗부분만)
    public void Sphere(int s, Vector3 c, Vector3 r, Quaternion q, int seg = 12, float lump = 0f, float cutBelow = -1f)
    {
        int rings = Mathf.Max(3, seg / 2);
        float latMin = Mathf.Asin(Mathf.Clamp(cutBelow, -1f, 0.95f));
        int start = v.Count;
        for (int j = 0; j <= rings; j++)
        {
            float lat = Mathf.Lerp(latMin, Mathf.PI * 0.5f, (float)j / rings);
            for (int k = 0; k <= seg; k++)
            {
                float lon = k * Mathf.PI * 2f / seg;
                Vector3 u = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                float f = 1f + lump * (Noise(u * 1.3f) - 0.5f) * 2f;
                if (j == rings) f = 1f + lump * (Noise(Vector3.up * 1.3f) - 0.5f) * 2f;
                Vector3 p = new Vector3(u.x * r.x, u.y * r.y, u.z * r.z) * f;
                Vector3 nn = new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized;
                Add(c + q * p, q * nn);
            }
        }
        for (int j = 0; j < rings; j++)
            for (int k = 0; k < seg; k++)
            {
                int a = start + j * (seg + 1) + k, b = a + 1, cc = a + seg + 1, d = cc + 1;
                Vector3 o = (n[a] + n[d]);
                Tri(s, a, cc, b, o);
                Tri(s, b, cc, d, o);
            }
    }

    // 원기둥/원뿔 (a → b, 반지름 ra → rb)
    public void Cylinder(int s, Vector3 a, Vector3 b, float ra, float rb, int seg = 8, bool capA = true, bool capB = true, float lump = 0f)
    {
        Vector3 axis = b - a;
        float h = axis.magnitude;
        if (h < 1e-5f) return;
        Vector3 d = axis / h;
        Vector3 x = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 z = Vector3.Cross(x, d);
        int start = v.Count;
        for (int ring = 0; ring < 2; ring++)
        {
            Vector3 c = ring == 0 ? a : b;
            float r = ring == 0 ? ra : rb;
            for (int k = 0; k <= seg; k++)
            {
                float t = k * Mathf.PI * 2f / seg;
                Vector3 radial = x * Mathf.Cos(t) + z * Mathf.Sin(t);
                float rr = r * (1f + lump * (Noise(c + radial) - 0.5f) * 2f);
                Vector3 nn = (radial * h + d * (ra - rb)).normalized;
                Add(c + radial * rr, nn);
            }
        }
        for (int k = 0; k < seg; k++)
        {
            int p0 = start + k, p1 = p0 + 1, q0 = p0 + seg + 1, q1 = q0 + 1;
            Vector3 o = n[p0] + n[p1];
            Tri(s, p0, q0, p1, o);
            Tri(s, p1, q0, q1, o);
        }
        if (capA && ra > 1e-4f) Cap(s, a, -d, x, z, ra, seg);
        if (capB && rb > 1e-4f) Cap(s, b, d, x, z, rb, seg);
    }

    void Cap(int s, Vector3 c, Vector3 nn, Vector3 x, Vector3 z, float r, int seg)
    {
        int ci = Add(c, nn);
        int start = v.Count;
        for (int k = 0; k <= seg; k++)
        {
            float t = k * Mathf.PI * 2f / seg;
            Add(c + (x * Mathf.Cos(t) + z * Mathf.Sin(t)) * r, nn);
        }
        for (int k = 0; k < seg; k++) Tri(s, ci, start + k, start + k + 1, nn);
    }

    // 돌림판 모양 (profile: (반지름, 높이) 아래→위)
    public void Lathe(int s, Vector3 c, Vector2[] profile, Vector3 scale, int seg = 16, float lump = 0f)
    {
        int start = v.Count;
        int m = profile.Length;
        for (int j = 0; j < m; j++)
        {
            Vector2 prev = profile[Mathf.Max(0, j - 1)], next = profile[Mathf.Min(m - 1, j + 1)];
            Vector2 tan = (next - prev).normalized;
            Vector2 pn = new Vector2(tan.y, -tan.x); // 바깥쪽
            for (int k = 0; k <= seg; k++)
            {
                float t = k * Mathf.PI * 2f / seg;
                Vector3 radial = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                float r = profile[j].x;
                if (r > 1e-4f) r *= 1f + lump * (Noise(radial * 2f + Vector3.up * profile[j].y * 3f) - 0.5f) * 2f;
                Vector3 p = radial * r + Vector3.up * profile[j].y;
                Vector3 nn = (radial * pn.x + Vector3.up * pn.y);
                Add(c + Vector3.Scale(p, scale), new Vector3(nn.x / scale.x, nn.y / scale.y, nn.z / scale.z).normalized);
            }
        }
        for (int j = 0; j < m - 1; j++)
            for (int k = 0; k < seg; k++)
            {
                int a = start + j * (seg + 1) + k, b = a + 1, cc = a + seg + 1, d = cc + 1;
                Vector3 o = n[a] + n[d];
                Tri(s, a, cc, b, o);
                Tri(s, b, cc, d, o);
            }
    }

    public void Box(int s, Vector3 c, Vector3 size, Quaternion q)
    {
        Vector3 h = size * 0.5f;
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var dn in dirs)
        {
            Vector3 u = Mathf.Abs(dn.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(dn, u);
            Vector3 fc = Vector3.Scale(dn, h);
            Vector3 hu = Vector3.Scale(u, h), hw = Vector3.Scale(w, h);
            Vector3 nn = q * dn;
            int i0 = Add(c + q * (fc - hu - hw), nn), i1 = Add(c + q * (fc + hu - hw), nn);
            int i2 = Add(c + q * (fc + hu + hw), nn), i3 = Add(c + q * (fc - hu + hw), nn);
            Tri(s, i0, i1, i2, nn); Tri(s, i0, i2, i3, nn);
        }
    }

    // 눈이 소복이 쌓인 띠 (x: -0.5~0.5 길이, z: -0.5~0.5 폭, y: 0~1 높이)
    public void SnowStrip(int s, int nx = 20, int nz = 8, float lump = 0.12f)
    {
        int start = v.Count;
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = -0.5f + (float)i / nx, z = -0.5f + (float)j / nz;
                float across = Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * z) * (2f * z)));
                float ends = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.5f - Mathf.Abs(x)) / 0.06f));
                float y = Mathf.Pow(across, 0.6f) * ends * (1f + lump * (Noise(new Vector3(x * 3f, 0, z * 1.5f)) - 0.5f) * 2f);
                Add(new Vector3(x, Mathf.Max(0f, y), z), Vector3.up);
            }
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = start + j * (nx + 1) + i, b = a + 1, cc = a + nx + 1, d = cc + 1;
                Tri(s, a, cc, b, Vector3.up);
                Tri(s, b, cc, d, Vector3.up);
            }
        recalcNormals = true;
    }

    bool recalcNormals;

    // 마지막에 메시로. flat이면 면마다 각지게 (얼음·바위)
    public Mesh ToMesh(string name, bool flat = false)
    {
        var m = new Mesh { name = name };
        if (!flat)
        {
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            if (recalcNormals) m.RecalculateNormals();
        }
        else
        {
            var fv = new List<Vector3>(); var fn = new List<Vector3>();
            var fs = new List<int>[subs.Length];
            for (int i = 0; i < subs.Length; i++)
            {
                fs[i] = new List<int>();
                for (int t = 0; t < subs[i].Count; t += 3)
                {
                    Vector3 a = v[subs[i][t]], b = v[subs[i][t + 1]], c = v[subs[i][t + 2]];
                    Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
                    fs[i].Add(fv.Count); fv.Add(a); fn.Add(nn);
                    fs[i].Add(fv.Count); fv.Add(b); fn.Add(nn);
                    fs[i].Add(fv.Count); fv.Add(c); fn.Add(nn);
                }
            }
            if (fv.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(fv); m.SetNormals(fn);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(fs[i], i);
        }
        m.RecalculateBounds();
        return m;
    }
}

public static class SalcheonShapes
{
    static System.Random rng;
    static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static Quaternion Q0 { get { return Quaternion.identity; } }

    // ── 재질 칸 번호 (메시마다 다름 → 주석 참고)

    // 눈 쌓인 전나무 (높이 1 기준) : 0 줄기, 1 잎, 2 눈
    public static Mesh Fir(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(3, seed);
        k.Cylinder(0, new Vector3(0, -0.05f, 0), new Vector3(0, 0.3f, 0), 0.045f, 0.03f, 7);
        int tiers = rng.Next(4, 7);
        float slim = R(0.85f, 1.15f);
        for (int i = 0; i < tiers; i++)
        {
            float t = tiers == 1 ? 0 : (float)i / (tiers - 1);
            float baseY = 0.13f + t * 0.6f;
            float th = 0.34f * (1f - 0.35f * t);
            float r = 0.36f * slim * (1f - 0.72f * t) * R(0.9f, 1.1f);
            k.Cylinder(1, new Vector3(0, baseY, 0), new Vector3(0, baseY + th, 0), r, 0f, 11, true, false, 0.03f);
            // 눈 치마: 층 윗부분을 덮는 들쭉날쭉한 원뿔 (살짝 바깥)
            SnowSkirt(k, 2, baseY, th, r, 11);
        }
        k.Sphere(2, new Vector3(0, 0.13f + 0.6f + 0.34f * 0.65f + 0.01f, 0), new Vector3(0.035f, 0.03f, 0.035f), Q0, 8, 0.2f);
        return k.ToMesh("전나무 " + seed);
    }

    static void SnowSkirt(SalcheonKit k, int s, float baseY, float th, float r, int seg)
    {
        // 원뿔 표면 바로 바깥에, 바닥 테두리 높이가 들쭉날쭉
        var prof = new List<Vector2>();
        float y0 = baseY + th * R(0.12f, 0.3f);
        for (int j = 0; j <= 3; j++)
        {
            float y = Mathf.Lerp(y0, baseY + th, j / 3f);
            float rr = r * (1f - (y - baseY) / th) * 1.1f + 0.006f;
            prof.Add(new Vector2(j == 3 ? 0f : rr, y + 0.012f));
        }
        // 테두리 아래로 살짝 늘어진 눈 (안쪽으로 접힘)
        prof.Insert(0, new Vector2(prof[0].x * 0.93f, y0 - th * 0.06f));
        k.Lathe(s, Vector3.zero, prof.ToArray(), Vector3.one, seg, 0.06f);
    }

    // 앙상한 나무 + 가지 위 눈 : 0 줄기, 1 눈
    public static Mesh BareTree(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(2, seed);
        float w = 0.035f;
        k.Cylinder(0, new Vector3(0, -0.05f, 0), new Vector3(R(-0.04f, 0.04f), 1f, R(-0.04f, 0.04f)), w, w * 0.25f, 7);
        int nb = rng.Next(5, 9);
        for (int b = 0; b < nb; b++)
        {
            float y = R(0.35f, 0.9f);
            float yaw = R(0, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(yaw), R(0.5f, 1.1f), Mathf.Sin(yaw)).normalized;
            float L = R(0.2f, 0.4f) * (1.15f - y);
            Vector3 a = new Vector3(0, y, 0), e = a + dir * L;
            float bw = w * (1f - y) * 0.9f + 0.006f;
            k.Cylinder(0, a, e, bw, bw * 0.3f, 6);
            // 가지 윗면에 쌓인 눈
            Vector3 mid = Vector3.Lerp(a, e, 0.45f) + Vector3.up * bw * 1.1f;
            Quaternion q = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(0, 90f, 0);
            k.Sphere(1, mid, new Vector3(L * 0.38f, bw * 1.4f, bw * 1.8f), q, 8, 0.25f, 0f);
            // 잔가지
            Vector3 tw = Vector3.Lerp(a, e, 0.65f);
            k.Cylinder(0, tw, tw + (dir + new Vector3(R(-0.6f, 0.6f), 0.4f, R(-0.6f, 0.6f))).normalized * L * 0.35f, bw * 0.5f, 0.003f, 5);
        }
        k.Sphere(1, new Vector3(0, 0.36f, 0), new Vector3(0.08f, 0.02f, 0.08f), Q0, 8, 0.3f, 0f);
        return k.ToMesh("앙상한 나무 " + seed);
    }

    // 눈 덤불 : 0 잎, 1 눈
    public static Mesh Bush(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(2, seed);
        int nBlob = rng.Next(3, 6);
        for (int i = 0; i < nBlob; i++)
        {
            float a = R(0, Mathf.PI * 2f), d = i == 0 ? 0 : R(0.25f, 0.5f);
            float r = i == 0 ? 0.5f : R(0.3f, 0.42f);
            Vector3 c = new Vector3(Mathf.Cos(a) * d, r * 0.3f, Mathf.Sin(a) * d);
            k.Sphere(0, c, new Vector3(r, r * 0.85f, r), Q0, 10, 0.18f, -0.4f);
            k.Sphere(1, c + Vector3.up * r * 0.06f, new Vector3(r * 1.04f, r * 0.85f * 1.04f, r * 1.04f), Quaternion.Euler(0, R(0, 360), 0), 10, 0.2f, R(0.15f, 0.4f));
        }
        return k.ToMesh("눈 덤불 " + seed);
    }

    // 눈 덮인 바위 : 0 바위, 1 눈
    public static Mesh Rock(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(2, seed);
        Vector3 r = new Vector3(R(0.8f, 1.2f), R(0.45f, 0.75f), R(0.7f, 1.1f));
        var q = Quaternion.Euler(R(-10, 10), R(0, 360), R(-10, 10));
        k.Sphere(0, Vector3.zero, r, q, 9, 0.28f, -0.5f);
        k.Sphere(1, Vector3.up * r.y * 0.08f, r * 1.05f, q, 9, 0.3f, R(0.2f, 0.5f));
        return k.ToMesh("바위 " + seed, true);
    }

    // 눈 더미 : 0 눈
    public static Mesh Mound(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(1, seed);
        k.Sphere(0, Vector3.down * 0.15f, new Vector3(1f, 1f, R(0.6f, 1f)), Q0, 14, 0.22f, 0.1f);
        if (rng.NextDouble() < 0.6)
            k.Sphere(0, new Vector3(R(-0.6f, 0.6f), -0.1f, R(-0.4f, 0.4f)), new Vector3(0.55f, 0.75f, 0.5f), Q0, 10, 0.25f, 0.1f);
        return k.ToMesh("눈 더미 " + seed);
    }

    // 눈 덮인 산 : 0 눈, 1 바위
    public static Mesh Hill(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(2, seed);
        var prof = new[] { new Vector2(1f, -0.05f), new Vector2(0.78f, 0.25f), new Vector2(0.5f, 0.55f), new Vector2(0.24f, 0.82f), new Vector2(0f, 1f) };
        k.Lathe(0, Vector3.zero, prof, Vector3.one, 12, 0.22f);
        // 눈 사이로 드러난 바위 능선 (살짝 돌린 복사본이 군데군데 튀어나옴)
        var rock = new[] { new Vector2(0.98f, -0.05f), new Vector2(0.8f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.2f, 0.78f), new Vector2(0f, 0.93f) };
        k.seed = seed + 99;
        k.Lathe(1, Vector3.zero, rock, Vector3.one, 9, 0.35f);
        return k.ToMesh("산 " + seed, true);
    }

    // 얼음 결정: 밑동(0) → 육각기둥 → 뾰족 끝(1) : 0 얼음
    public static Mesh Crystal()
    {
        var k = new SalcheonKit(1);
        k.Cylinder(0, Vector3.zero, new Vector3(0, 0.75f, 0), 0.5f, 0.5f, 6, true, false);
        k.Cylinder(0, new Vector3(0, 0.75f, 0), Vector3.up, 0.5f, 0f, 6, false, false);
        return k.ToMesh("얼음 결정", true);
    }

    // 고드름: 위(0)에 붙어 아래(-1)로 뾰족 : 0 고드름
    public static Mesh Icicle()
    {
        var k = new SalcheonKit(1);
        k.Cylinder(0, Vector3.zero, new Vector3(0, -0.3f, 0), 0.5f, 0.32f, 6, true, false);
        k.Cylinder(0, new Vector3(0, -0.3f, 0), Vector3.down, 0.32f, 0f, 6, false, false);
        return k.ToMesh("고드름", true);
    }

    // 자모 위에 쌓인 눈 띠 : 0 눈
    public static Mesh Strip()
    {
        var k = new SalcheonKit(1, 3);
        k.SnowStrip(0);
        return k.ToMesh("자모 위 눈");
    }

    // 눈사람 (키 약 2m) : 0 눈, 1 숯, 2 당근, 3 나뭇가지, 4 목도리, 5 모자
    //  style 0: 한국식 2단 + 털모자   1: 3단 + 양동이   2: 2단 + 목도리만   3: 3단 꼬마 + 털모자
    //  style 4: 길쭉한 3단 + 앞으로 뻗은 긴 팔 (무서운 쪽)
    //  creepy: 웃음 대신 처진 입, 크고 짝짝이인 눈구멍, 아래로 휜 코, 앞으로 뻗은 손가락 가지
    public static Mesh Snowman(int style, int seed, bool creepy = false)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(6, seed);
        bool three = style == 1 || style == 3 || style == 4;
        float[] rs = style == 4 ? new[] { 0.42f, 0.36f, 0.28f } : three ? new[] { 0.55f, 0.42f, 0.3f } : new[] { 0.6f, 0.42f };
        var cs = new List<Vector3>();
        float y = 0f;
        for (int i = 0; i < rs.Length; i++)
        {
            float r = rs[i];
            y += i == 0 ? r * 0.7f : rs[i - 1] * 0.8f + r * 0.8f;
            var c = new Vector3(i == 0 ? 0 : R(-0.03f, 0.03f), y, 0);
            cs.Add(c);
            k.Sphere(0, c, new Vector3(r, r * 0.97f, r), Quaternion.Euler(0, R(0, 360), 0), 16, 0.035f, i == 0 ? -0.75f : -1f);
        }
        Vector3 head = cs[cs.Count - 1]; float hr = rs[rs.Length - 1];
        Vector3 torso = cs[cs.Count - 2]; float tr = rs[rs.Length - 2];

        Face(k, head, hr, creepy);
        // 단추
        for (int i = 0; i < 3; i++)
            k.Sphere(1, torso + new Vector3(0, 0.45f - i * 0.4f, 0.9f).normalized * tr, Vector3.one * tr * 0.08f, Q0, 6);
        // 나뭇가지 팔
        for (int s = -1; s <= 1; s += 2)
        {
            if (creepy || style == 4)
            {
                // 앞으로 뻗은 긴 팔 + 갈퀴 같은 손가락
                Vector3 a0 = torso + new Vector3(s * 0.85f, 0.1f, 0.2f) * tr;
                Vector3 e0 = a0 + new Vector3(s * R(0.15f, 0.35f), R(-0.1f, 0.15f), R(0.7f, 1.1f)) * (style == 4 ? 1.3f : 1f);
                Vector3 elbow = Vector3.Lerp(a0, e0, 0.5f) + new Vector3(s * 0.12f, 0.12f, 0);
                k.Cylinder(3, a0, elbow, 0.024f, 0.018f, 5);
                k.Cylinder(3, elbow, e0, 0.018f, 0.01f, 5);
                for (int fi = 0; fi < 4; fi++)
                    k.Cylinder(3, e0, e0 + new Vector3(s * R(-0.06f, 0.1f) + (fi - 1.5f) * 0.05f, R(-0.12f, 0.05f), R(0.1f, 0.2f)), 0.008f, 0.002f, 4);
                continue;
            }
            Vector3 a = torso + new Vector3(s * 0.88f, 0.15f, 0) * tr;
            Vector3 e = a + new Vector3(s * R(0.55f, 0.75f), R(0.2f, 0.55f), R(-0.15f, 0.15f));
            k.Cylinder(3, a, e, 0.025f, 0.012f, 5);
            Vector3 f = Vector3.Lerp(a, e, 0.7f);
            k.Cylinder(3, f, f + new Vector3(s * 0.12f, 0.2f, R(-0.1f, 0.1f)), 0.013f, 0.006f, 4);
            k.Cylinder(3, e, e + new Vector3(s * 0.13f, R(-0.05f, 0.12f), R(-0.06f, 0.06f)), 0.01f, 0.004f, 4);
        }
        // 목도리
        if (style != 1 && style != 4)
        {
            float neckY = torso.y + tr * 0.82f;
            float nr = hr * 0.88f;
            k.Cylinder(4, new Vector3(0, neckY - 0.07f, 0), new Vector3(0, neckY + 0.08f, 0), nr, nr * 0.95f, 16, true, true, 0.04f);
            Quaternion tq = Quaternion.Euler(R(5f, 15f), R(-25f, -10f), R(-8f, 8f));
            k.Box(4, new Vector3(nr * 0.45f, neckY - 0.22f, nr * 0.85f), new Vector3(0.13f, 0.36f, 0.035f), tq);
        }
        // 모자
        Vector3 top = head + Vector3.up * hr * 0.78f;
        if (style == 1)
        {
            Quaternion tilt = Quaternion.Euler(R(-12f, 12f), 0, R(-15f, 15f));
            k.Cylinder(5, top - tilt * Vector3.up * 0.05f, top + tilt * Vector3.up * hr * 0.75f, hr * 0.62f, hr * 0.5f, 14);
        }
        else if ((style == 0 || style == 3) && !creepy)
        {
            Vector3 hatBase = head + Vector3.up * hr * 0.45f;
            k.Cylinder(4, hatBase, hatBase + new Vector3(R(-0.1f, 0.1f), hr * 1.0f, R(-0.15f, 0f)), hr * 0.92f, hr * 0.1f, 14, true, false);
            k.Cylinder(4, hatBase - Vector3.up * 0.04f, hatBase + Vector3.up * 0.06f, hr * 0.96f, hr * 0.93f, 14);
            k.Sphere(0, hatBase + Vector3.up * hr * 1.02f, Vector3.one * hr * 0.2f, Q0, 8, 0.2f);
        }
        return k.ToMesh("눈사람 " + style + "-" + seed);
    }

    static void Face(SalcheonKit k, Vector3 head, float hr, bool creepy)
    {
        if (!creepy)
        {
            for (int s = -1; s <= 1; s += 2)
                k.Sphere(1, head + new Vector3(s * 0.33f, 0.22f, 0.92f).normalized * hr, Vector3.one * hr * 0.11f, Q0, 8);
            Vector3 noseA = head + new Vector3(0, 0.02f, 0.9f) * hr;
            k.Cylinder(2, noseA, noseA + new Vector3(R(-0.1f, 0.1f), -0.08f, 1f).normalized * hr * 0.75f, hr * 0.14f, 0f, 8, true, false);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.36f + i * 0.18f;
                Vector3 dd = new Vector3(x, -0.34f + 0.55f * x * x, 0.9f).normalized;
                k.Sphere(1, head + dd * hr, Vector3.one * hr * 0.06f, Q0, 6);
            }
            return;
        }
        // 크고 짝짝이인 눈구멍 (반쯤 파묻혀서 움푹 꺼진 듯)
        k.Sphere(1, head + new Vector3(-0.34f, 0.2f, 0.95f).normalized * hr * 0.97f, new Vector3(0.17f, 0.2f, 0.12f) * hr, Q0, 10);
        k.Sphere(1, head + new Vector3(0.33f, 0.15f + R(-0.08f, 0.02f), 0.95f).normalized * hr * 0.97f, new Vector3(0.14f, 0.15f, 0.12f) * hr, Q0, 10);
        // 아래로 처진 코 (색 바랜 가지)
        Vector3 twig = head + new Vector3(0, -0.02f, 0.92f) * hr;
        k.Cylinder(3, twig, twig + new Vector3(R(-0.15f, 0.15f), -0.55f, 0.8f).normalized * hr * 0.5f, hr * 0.07f, 0f, 6, true, false);
        // 처진 입 (길고 삐뚤빼뚤)
        for (int i = 0; i < 9; i++)
        {
            float x = -0.48f + i * 0.12f;
            Vector3 dd = new Vector3(x, -0.3f - 0.5f * x * x + R(-0.04f, 0.04f), 0.88f).normalized;
            k.Sphere(1, head + dd * hr, Vector3.one * hr * R(0.04f, 0.07f), Q0, 6);
        }
    }

    // 굴러떨어진 눈사람 머리 (얼굴은 +Z) : 0 눈, 1 숯, 2 (안 씀), 3 나뭇가지
    public static Mesh FallenHead(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(4, seed);
        float hr = 0.36f;
        Vector3 head = new Vector3(0, hr * 0.8f, 0);
        k.Sphere(0, head, Vector3.one * hr, Q0, 16, 0.05f, -0.8f);
        Face(k, head, hr, true);
        return k.ToMesh("떨어진 머리 " + seed);
    }

    // 까마귀 (길이 약 0.45m, 앞 +Z) : 0 깃털, 1 부리·발
    public static Mesh Crow(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(2, seed);
        Quaternion tilt = Quaternion.Euler(-20f, 0, 0);
        k.Sphere(0, new Vector3(0, 0.12f, 0), new Vector3(0.085f, 0.085f, 0.17f), tilt, 12);
        Vector3 head = new Vector3(0, 0.22f, 0.12f);
        k.Sphere(0, head, Vector3.one * 0.06f, Q0, 10);
        k.Cylinder(1, head + new Vector3(0, -0.005f, 0.05f), head + new Vector3(0, -0.02f, 0.13f), 0.022f, 0f, 6, true, false);
        k.Box(0, new Vector3(0, 0.1f, -0.2f), new Vector3(0.09f, 0.015f, 0.16f), Quaternion.Euler(-25f, 0, 0));
        for (int s = -1; s <= 1; s += 2)
        {
            k.Box(0, new Vector3(s * 0.075f, 0.13f, -0.02f), new Vector3(0.02f, 0.09f, 0.22f), Quaternion.Euler(-15f, s * 6f, 0));
            k.Cylinder(1, new Vector3(s * 0.03f, 0.06f, 0.01f), new Vector3(s * 0.035f, 0f, 0.03f), 0.007f, 0.006f, 4);
        }
        return k.ToMesh("까마귀 " + seed);
    }

    // 발자국 하나 (눈에 꾹 눌린 자국) : 0 자국
    public static Mesh Footprint()
    {
        var k = new SalcheonKit(1, 31);
        k.Sphere(0, new Vector3(0, -0.01f, 0.03f), new Vector3(0.06f, 0.012f, 0.1f), Q0, 10, 0f, 0f);
        k.Sphere(0, new Vector3(0, -0.01f, -0.09f), new Vector3(0.045f, 0.01f, 0.05f), Q0, 8, 0f, 0f);
        return k.ToMesh("발자국");
    }

    // 쓰러진 가로등 (꺼진 채 눈에 반쯤 묻힘) — LampPost와 같은 재질 순서
    public static Mesh FallenLamp()
    {
        var k = new SalcheonKit(3, 37);
        Quaternion q = Quaternion.Euler(0, 0, -78f);
        Vector3 b = new Vector3(0, 0.15f, 0);
        k.Box(0, b, new Vector3(0.32f, 0.3f, 0.32f), Quaternion.Euler(0, 0, -20f));
        k.Cylinder(0, b, b + q * new Vector3(0, 3.4f, 0), 0.07f, 0.05f, 10);
        Vector3 top = b + q * new Vector3(0, 3.6f, 0);
        k.Sphere(1, top, new Vector3(0.18f, 0.22f, 0.18f), q, 12);
        k.Sphere(2, new Vector3(1.8f, 0.05f, 0), new Vector3(2.0f, 0.18f, 0.35f), Q0, 12, 0.3f, 0f);
        return k.ToMesh("쓰러진 가로등");
    }

    // 눈오리 (집게로 찍은 오리 모양 눈뭉치, 길이 약 0.33m) : 0 눈, 1 숯, 2 부리
    public static Mesh Duck(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(3, seed);
        k.Sphere(0, new Vector3(0, 0.06f, 0), new Vector3(0.11f, 0.085f, 0.15f), Q0, 14, 0.02f, -0.6f);
        k.Sphere(0, new Vector3(0, 0.13f, -0.13f), new Vector3(0.045f, 0.035f, 0.05f), Quaternion.Euler(-30f, 0, 0), 8);
        Vector3 head = new Vector3(0, 0.19f, 0.085f);
        k.Sphere(0, head, Vector3.one * 0.068f, Q0, 12, 0.02f);
        k.Sphere(2, head + new Vector3(0, -0.012f, 0.07f), new Vector3(0.038f, 0.014f, 0.04f), Quaternion.Euler(-8f, 0, 0), 8);
        for (int s = -1; s <= 1; s += 2)
            k.Sphere(1, head + new Vector3(s * 0.4f, 0.25f, 0.85f).normalized * 0.066f, Vector3.one * 0.009f, Q0, 6);
        return k.ToMesh("눈오리 " + seed);
    }

    // 이글루 (반지름 3m) : 0 눈, 1 안쪽 불빛
    public static Mesh Igloo()
    {
        var k = new SalcheonKit(2, 5);
        k.Sphere(0, Vector3.zero, new Vector3(3f, 2.6f, 3f), Q0, 22, 0.02f, -0.05f);
        // 얼음 벽돌 줄눈 느낌: 살짝 큰 띠 몇 줄
        for (int i = 1; i <= 3; i++)
        {
            float lat = i * 0.36f;
            float y = Mathf.Sin(lat) * 2.6f, r = Mathf.Cos(lat) * 3f;
            k.Cylinder(0, new Vector3(0, y - 0.04f, 0), new Vector3(0, y + 0.04f, 0), r + 0.05f, r * 0.98f + 0.05f, 22, false, false);
        }
        // 들어가는 굴
        k.Cylinder(0, new Vector3(0, 0, 2.2f), new Vector3(0, 0, 4.1f), 1.25f, 1.15f, 16, false, false, 0.03f);
        k.Cylinder(0, new Vector3(0, 0, 4.0f), new Vector3(0, 0, 4.15f), 1.3f, 1.3f, 16, false, false, 0.05f);
        k.Cylinder(1, new Vector3(0, 0, 3.9f), new Vector3(0, 0, 3.95f), 1.2f, 1.2f, 16, false, true);
        return k.ToMesh("이글루");
    }

    // 장독대 : 0 옹기, 1 눈, 2 돌
    public static Mesh Jangdok(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(3, seed);
        k.Box(2, new Vector3(0, 0.15f, 0), new Vector3(4.2f, 0.45f, 2.6f), Q0);
        k.Box(2, new Vector3(0, 0.42f, 0), new Vector3(4.0f, 0.1f, 2.4f), Q0);
        var jar = new[] { new Vector2(0.28f, 0f), new Vector2(0.42f, 0.14f), new Vector2(0.48f, 0.4f), new Vector2(0.42f, 0.65f), new Vector2(0.28f, 0.78f), new Vector2(0.29f, 0.83f) };
        var lid = new[] { new Vector2(0.34f, 0f), new Vector2(0.34f, 0.04f), new Vector2(0.22f, 0.12f), new Vector2(0.06f, 0.16f), new Vector2(0f, 0.18f) };
        float[] xs = { -1.4f, -0.45f, 0.5f, 1.4f };
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < xs.Length; i++)
            {
                if (row == 1 && rng.NextDouble() < 0.2) continue;
                float sc = row == 0 ? R(0.9f, 1.25f) : R(0.7f, 1.0f);
                Vector3 b = new Vector3(xs[i] + R(-0.1f, 0.1f), 0.47f, row == 0 ? -0.55f : 0.55f);
                k.Lathe(0, b, jar, Vector3.one * sc, 16);
                Vector3 lt = b + Vector3.up * 0.83f * sc;
                k.Lathe(0, lt, lid, Vector3.one * sc, 16);
                k.Sphere(1, lt + Vector3.up * 0.1f * sc, new Vector3(0.36f, 0.12f, 0.36f) * sc, Quaternion.Euler(0, R(0, 360), 0), 12, 0.15f, 0f);
            }
        // 단 위에 쌓인 눈
        k.Sphere(1, new Vector3(0, 0.47f, 0), new Vector3(2.1f, 0.12f, 1.3f), Q0, 16, 0.3f, 0f);
        return k.ToMesh("장독대 " + seed);
    }

    // 벤치 : 0 나무, 1 쇠, 2 눈
    public static Mesh Bench()
    {
        var k = new SalcheonKit(3, 11);
        for (int i = 0; i < 4; i++) k.Box(0, new Vector3(0, 0.48f, -0.18f + i * 0.12f), new Vector3(1.9f, 0.04f, 0.1f), Q0);
        for (int i = 0; i < 3; i++) k.Box(0, new Vector3(0, 0.66f + i * 0.13f, 0.27f), new Vector3(1.9f, 0.1f, 0.035f), Quaternion.Euler(-12f, 0, 0));
        for (int s = -1; s <= 1; s += 2)
        {
            k.Box(1, new Vector3(s * 0.8f, 0.24f, -0.15f), new Vector3(0.05f, 0.48f, 0.05f), Q0);
            k.Box(1, new Vector3(s * 0.8f, 0.45f, 0.24f), new Vector3(0.05f, 0.9f, 0.05f), Quaternion.Euler(-12f, 0, 0));
        }
        k.Sphere(2, new Vector3(0, 0.5f, -0.02f), new Vector3(0.95f, 0.07f, 0.26f), Q0, 14, 0.35f, 0f);
        k.Sphere(2, new Vector3(0, 0.92f, 0.31f), new Vector3(0.95f, 0.04f, 0.04f), Q0, 10, 0.3f, 0f);
        return k.ToMesh("벤치");
    }

    // 가로등 (높이 4m) : 0 쇠, 1 전구, 2 눈
    public static Mesh LampPost()
    {
        var k = new SalcheonKit(3, 13);
        k.Box(0, new Vector3(0, 0.15f, 0), new Vector3(0.32f, 0.3f, 0.32f), Q0);
        k.Cylinder(0, new Vector3(0, 0.3f, 0), new Vector3(0, 3.55f, 0), 0.07f, 0.05f, 10);
        k.Cylinder(0, new Vector3(0, 3.5f, 0), new Vector3(0, 3.58f, 0), 0.2f, 0.2f, 10);
        k.Sphere(1, new Vector3(0, 3.78f, 0), new Vector3(0.18f, 0.22f, 0.18f), Q0, 12);
        k.Cylinder(0, new Vector3(0, 3.98f, 0), new Vector3(0, 4.12f, 0), 0.3f, 0.06f, 10);
        k.Sphere(2, new Vector3(0, 4.05f, 0), new Vector3(0.3f, 0.12f, 0.3f), Q0, 12, 0.25f, 0f);
        k.Sphere(2, new Vector3(0, 0.3f, 0), new Vector3(0.2f, 0.06f, 0.2f), Q0, 8, 0.3f, 0f);
        return k.ToMesh("가로등");
    }

    // 썰매 : 0 빨간 나무, 1 쇠, 2 눈
    public static Mesh Sled()
    {
        var k = new SalcheonKit(3, 17);
        for (int s = -1; s <= 1; s += 2)
        {
            k.Box(1, new Vector3(s * 0.28f, 0.03f, 0), new Vector3(0.04f, 0.04f, 1.3f), Q0);
            k.Box(1, new Vector3(s * 0.28f, 0.13f, 0.72f), new Vector3(0.04f, 0.04f, 0.3f), Quaternion.Euler(-50f, 0, 0));
            for (int i = 0; i < 3; i++) k.Box(1, new Vector3(s * 0.28f, 0.1f, -0.45f + i * 0.45f), new Vector3(0.03f, 0.14f, 0.03f), Q0);
        }
        for (int i = 0; i < 6; i++) k.Box(0, new Vector3(0, 0.19f, -0.55f + i * 0.2f), new Vector3(0.66f, 0.03f, 0.15f), Q0);
        k.Sphere(2, new Vector3(0, 0.21f, -0.1f), new Vector3(0.3f, 0.06f, 0.45f), Q0, 10, 0.35f, 0f);
        return k.ToMesh("썰매");
    }

    // 눈덩이 더미 : 0 눈
    public static Mesh Snowballs(int seed)
    {
        rng = new System.Random(seed);
        var k = new SalcheonKit(1, seed);
        float r = 0.2f;
        for (int layer = 0; layer < 3; layer++)
        {
            int cnt = layer == 0 ? 6 : layer == 1 ? 3 : 1;
            for (int i = 0; i < cnt; i++)
            {
                float a = i * Mathf.PI * 2f / cnt + layer;
                float d = layer == 0 ? r * 1.9f : layer == 1 ? r * 1.0f : 0f;
                k.Sphere(0, new Vector3(Mathf.Cos(a) * d, r * (0.9f + layer * 1.45f), Mathf.Sin(a) * d), Vector3.one * r * R(0.9f, 1.1f), Q0, 10, 0.06f);
            }
        }
        return k.ToMesh("눈덩이 더미 " + seed);
    }

    // 삽 꽂힌 눈더미 : 0 눈, 1 나무, 2 쇠
    public static Mesh ShovelMound()
    {
        var k = new SalcheonKit(3, 19);
        k.Sphere(0, Vector3.zero, new Vector3(1.2f, 0.75f, 0.9f), Q0, 14, 0.2f, 0f);
        Quaternion q = Quaternion.Euler(-18f, 25f, 10f);
        Vector3 blade = new Vector3(0.2f, 0.55f, 0.1f);
        k.Box(2, blade, new Vector3(0.28f, 0.36f, 0.02f), q);
        k.Cylinder(1, blade + q * new Vector3(0, 0.18f, 0), blade + q * new Vector3(0, 1.15f, 0), 0.022f, 0.022f, 6);
        k.Box(1, blade + q * new Vector3(0, 1.17f, 0), new Vector3(0.16f, 0.03f, 0.03f), q);
        return k.ToMesh("삽 꽂힌 눈더미");
    }
}
