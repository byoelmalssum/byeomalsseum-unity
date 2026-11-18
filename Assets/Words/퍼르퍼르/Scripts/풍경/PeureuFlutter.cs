using UnityEngine;

// [퍼르퍼르 풍경] 가벼운 천이 바람에 '퍼르퍼르' 나부끼는 모양 (물리 Cloth 없이 가볍게)
//  - 리본: 한쪽 끝이 묶여서 바람 방향으로 길게 날림 (솟대 리본, 연 꼬리)
//  - 휘장: 윗변이 묶여서 아래로 늘어지고 주름이 일렁임 (바람 정원 커튼)
//  - 삼각깃발: 윗변이 묶인 세모 깃발
//  모양은 플레이할 때마다 이 값들로 새로 만들어요 (씬 파일이 무거워지지 않음).
//  묶인 자리 = 이 오브젝트의 위치. 빨간 화살표(X축) = 묶인 변의 방향. Scale은 (1,1,1)로 둘 것.
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PeureuFlutter : MonoBehaviour
{
    public enum Kind { 리본, 휘장, 삼각깃발 }

    public Kind kind = Kind.리본;

    [Header("크기 (m)")]
    public float width = .5f;
    public float length = 10f;
    [Tooltip("끝으로 갈수록 가늘어짐 (0~1)")] [Range(0, 1)] public float taper = .3f;
    [Tooltip("가로 · 세로 칸 수 (많을수록 부드럽지만 무거움)")] public int cols = 2;
    public int rows = 28;

    [Header("나부끼는 모양")]
    [Tooltip("0: 아래로 축 처짐 ~ 1: 바람 방향으로 쭉")] [Range(0, 1)] public float stream = .85f;
    [Tooltip("물결 크기")] public float waveAmp = .45f;
    [Tooltip("길이 안에 들어가는 물결 수")] public float waveCount = 1.6f;
    [Tooltip("물결 빠르기")] public float waveSpeed = 4f;
    [Tooltip("옆으로 흔들림")] public float sideAmp = .2f;
    [Tooltip("비틀림 (도)")] public float twist = 35f;
    [Tooltip("휘장 가로 주름 크기 (m)")] public float foldAmp = .25f;
    [Tooltip("휘장 가로 주름 개수")] public float foldCount = 3f;
    [Tooltip("휘장이 바람을 안고 부푸는 정도 (m)")] public float billow = .9f;
    [Tooltip("바람을 얼마나 받는지")] public float windGain = 1f;
    [Tooltip("카메라가 이 거리 안에 오면 더 세게 나부낌 (m)")] public float nearRadius = 12f;
    public float seed;

    Mesh mesh;
    Vector3[] verts;
    MeshRenderer rend;
    float clock, smoothAmt = 1f;
    int vCount;

    void OnEnable()
    {
        rend = GetComponent<MeshRenderer>();
        Build();
        clock = seed * 3.1f;
        Pose(Application.isPlaying ? PeureuAir.Amount : 1f);
    }

    void OnDisable()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        mesh = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled) return;
            Build();
            Pose(1f);
        };
    }
#endif

    /// <summary>코드에서 값을 바꾼 뒤 모양 다시 만들기</summary>
    public void Refresh()
    {
        rend = GetComponent<MeshRenderer>();
        Build();
        Pose(Application.isPlaying ? PeureuAir.Amount : 1f);
    }

    void Build()
    {
        cols = Mathf.Clamp(cols, 1, 40);
        rows = Mathf.Clamp(rows, 2, 80);
        int perSide = (cols + 1) * (rows + 1);
        vCount = perSide;
        verts = new Vector3[perSide * 2];
        var uv = new Vector2[perSide * 2];
        var tris = new int[cols * rows * 12];
        int t = 0;
        for (int i = 0; i <= rows; i++)
            for (int j = 0; j <= cols; j++)
            {
                int k = i * (cols + 1) + j;
                uv[k] = uv[k + perSide] = new Vector2((float)j / cols, (float)i / rows);
            }
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                int a = i * (cols + 1) + j, b = a + 1, c = a + cols + 1, d = c + 1;
                // 앞면
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
                // 뒷면 (꼭짓점을 따로 둬서 양면 모두 빛을 제대로 받음)
                a += perSide; b += perSide; c += perSide; d += perSide;
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = b; tris[t++] = d; tris[t++] = c;
            }

        if (mesh == null)
        {
            mesh = new Mesh { name = "퍼르퍼르 천 (실행 중 생성)", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        float r = length * 1.15f + width;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * r * 2f);
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void Update()
    {
        if (!Application.isPlaying || mesh == null) return;
        float amt = PeureuAir.Amount * windGain + PeureuAir.Near(transform.position, nearRadius) * 1.4f;
        smoothAmt = Mathf.Lerp(smoothAmt, amt, 1f - Mathf.Exp(-Time.deltaTime * 3f));
        clock += Time.deltaTime * waveSpeed * (.55f + .55f * Mathf.Min(smoothAmt, 2.5f));
        if (rend != null && !rend.isVisible) return; // 안 보이면 계산 생략
        Pose(smoothAmt);
    }

    void Pose(float amt)
    {
        if (mesh == null || verts == null) return;
        amt = Mathf.Clamp(amt, 0f, 3f);
        float a01 = Mathf.Clamp01(amt);

        // 바람 · 중력 방향을 이 오브젝트 기준으로
        Vector3 down = transform.InverseTransformDirection(Vector3.down);
        Vector3 wind = transform.InverseTransformDirection(PeureuAir.Dir);
        Vector3 W = Vector3.right;
        // 휘장은 윗변 전체가 묶여 있어서 옆으로 쏠리지 않고 앞뒤로만 부풀어요
        if (kind == Kind.휘장) wind -= W * Vector3.Dot(wind, W);

        float s;
        switch (kind)
        {
            case Kind.리본: s = stream * (.45f + .55f * a01); break;
            case Kind.휘장: s = stream * .35f * amt; break;
            default: s = stream * (.25f + .45f * a01); break;
        }
        s = Mathf.Clamp01(s);
        Vector3 b = (down * (1f - s) + wind * s);
        if (b.sqrMagnitude < 1e-4f) b = down;
        b.Normalize();

        Vector3 Wp = W - b * Vector3.Dot(W, b);
        if (Wp.sqrMagnitude < 1e-4f) Wp = Vector3.Cross(b, Vector3.forward);
        Wp.Normalize();
        Vector3 n = Vector3.Cross(Wp, b).normalized;

        float seg = length / rows;
        float twoPi = Mathf.PI * 2f;
        float tw = clock;
        float waveK = kind == Kind.휘장 ? .8f : 1f;
        float billowAmt = Mathf.Min(amt, 2f) * (.55f + .45f * Mathf.Sin(tw * .5f + seed));
        Vector3 p = Vector3.zero;

        for (int i = 0; i <= rows; i++)
        {
            float v = (float)i / rows;
            float grow = Mathf.Pow(v, .8f);

            float main = Mathf.Sin(twoPi * waveCount * v - tw + seed);
            float side = Mathf.Sin(twoPi * waveCount * .6f * v - tw * .73f + seed * 1.7f);
            Vector3 d = b + n * (waveAmp * waveK * (.35f + .65f * amt) * grow * main)
                          + Wp * (sideAmp * amt * grow * side);
            d.Normalize();

            // 폭 방향 (진행 방향에 수직 + 비틀림)
            Vector3 wv = Wp - d * Vector3.Dot(Wp, d);
            if (wv.sqrMagnitude < 1e-5f) wv = Wp;
            wv.Normalize();
            if (kind == Kind.리본)
            {
                float ang = twist * v * Mathf.Sin(twoPi * .5f * v - tw * .6f + seed * 2.3f) * (.4f + .6f * a01);
                wv = Quaternion.AngleAxis(ang, d) * wv;
            }
            Vector3 nv = Vector3.Cross(wv, d);

            float wHere;
            switch (kind)
            {
                case Kind.삼각깃발: wHere = width * (1f - v); break;
                case Kind.휘장: wHere = width; break;
                default: wHere = width * (1f - taper * v); break;
            }

            for (int j = 0; j <= cols; j++)
            {
                float u = (float)j / cols - .5f;
                Vector3 q = p + wv * (u * wHere);
                if (kind == Kind.휘장)
                {
                    float fold = Mathf.Sin(twoPi * foldCount * (u + .5f) + tw * .45f + seed + v * 1.3f);
                    float fold2 = Mathf.Sin(twoPi * (foldCount * .5f) * (u + .5f) - tw * .3f + seed * 2.1f);
                    q += nv * (foldAmp * (.35f + .65f * v) * (fold * (.6f + .4f * a01) + fold2 * .5f * amt));
                    // 가운데가 바람을 안고 볼록하게 부풂
                    q += nv * (billow * billowAmt * v * Mathf.Sin(Mathf.PI * (u + .5f)));
                }
                int k = i * (cols + 1) + j;
                verts[k] = q;
                verts[k + vCount] = q;
            }
            p += d * seg;
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
