using UnityEngine;

// [퍼르퍼르 풍경] 줄에 줄줄이 달린 세모 깃발 (만국기처럼) — 한 줄이 메시 하나라 가벼워요
//  - from / to = 줄 양 끝 (이 오브젝트 기준). 이 오브젝트 Scale은 (1,1,1)로 둘 것.
//  - 재질은 MeshRenderer의 Materials 칸 순서대로 깃발에 번갈아 칠해져요.
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PeureuBunting : MonoBehaviour
{
    public Vector3 from = new Vector3(-5, 0, 0);
    public Vector3 to = new Vector3(5, 0, 0);
    [Tooltip("줄이 가운데에서 처지는 길이 (m)")] public float sag = 1.2f;
    [Tooltip("깃발 사이 간격 (m)")] public float spacing = .9f;
    [Tooltip("깃발 폭 (m)")] public float flagWidth = .6f;
    [Tooltip("깃발 길이 (m)")] public float flagLength = .85f;
    [Tooltip("나부끼는 정도 (도)")] public float swing = 28f;
    public float speed = 3.2f;
    [Tooltip("카메라가 이 거리 안이면 더 세게 (m)")] public float nearRadius = 14f;
    [Tooltip("줄 (선택)")] public LineRenderer rope;
    public float seed;

    Mesh mesh;
    Vector3[] verts;
    int flags, colors = 1, perSide;
    float clock, smoothAmt = 1f;
    MeshRenderer rend;

    public Vector3 StringPoint(float t) => Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));

    void OnEnable()
    {
        rend = GetComponent<MeshRenderer>();
        Build();
        Pose(1f);
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
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) { Build(); Pose(1f); } };
    }
#endif

    /// <summary>코드에서 값을 바꾼 뒤 모양 다시 만들기</summary>
    public void Refresh()
    {
        rend = GetComponent<MeshRenderer>();
        Build();
        Pose(1f);
    }

    void Build()
    {
        float len = Vector3.Distance(from, to);
        flags = Mathf.Max(1, Mathf.FloorToInt(len / Mathf.Max(.2f, spacing)));
        colors = Mathf.Max(1, rend ? rend.sharedMaterials.Length : 1);
        perSide = flags * 5;
        verts = new Vector3[perSide * 2];
        var subs = new System.Collections.Generic.List<int>[colors];
        for (int c = 0; c < colors; c++) subs[c] = new System.Collections.Generic.List<int>();
        for (int f = 0; f < flags; f++)
        {
            int o = f * 5; // TL TR ML MR TIP
            var s = subs[f % colors];
            s.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3, o + 2, o + 4, o + 3 });
            int b = o + perSide;
            s.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2, b + 2, b + 3, b + 4 });
        }
        if (mesh == null)
        {
            mesh = new Mesh { name = "퍼르퍼르 깃발줄 (실행 중 생성)", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.subMeshCount = colors;
        for (int c = 0; c < colors; c++) mesh.SetTriangles(subs[c], c);
        Vector3 mid = (from + to) * .5f;
        mesh.bounds = new Bounds(mid, new Vector3(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y), Mathf.Abs(to.z - from.z)) + Vector3.one * (sag + flagLength * 2f + 1f));
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (rope)
        {
            rope.useWorldSpace = false;
            rope.positionCount = 20;
            for (int i = 0; i < 20; i++) rope.SetPosition(i, StringPoint(i / 19f));
        }
    }

    void Update()
    {
        if (!Application.isPlaying || mesh == null) return;
        Vector3 mid = transform.TransformPoint(StringPoint(.5f));
        float amt = PeureuAir.Amount + PeureuAir.Near(mid, nearRadius) * 1.2f;
        smoothAmt = Mathf.Lerp(smoothAmt, amt, 1f - Mathf.Exp(-Time.deltaTime * 3f));
        clock += Time.deltaTime * speed * (.6f + .5f * Mathf.Min(smoothAmt, 2.5f));
        if (rend != null && !rend.isVisible) return;
        Pose(smoothAmt);
    }

    void Pose(float amt)
    {
        if (mesh == null || verts == null) return;
        amt = Mathf.Clamp(amt, 0f, 3f);
        Vector3 down = transform.InverseTransformDirection(Vector3.down);
        Vector3 wind = transform.InverseTransformDirection(PeureuAir.Dir);
        float half = flagWidth * .5f;

        for (int f = 0; f < flags; f++)
        {
            float t = (f + .5f) / flags;
            float dt = (half / Mathf.Max(.01f, Vector3.Distance(from, to)));
            Vector3 L = StringPoint(Mathf.Clamp01(t - dt)), R = StringPoint(Mathf.Clamp01(t + dt));
            Vector3 axis = (R - L).normalized;
            Vector3 hang = (down - axis * Vector3.Dot(down, axis)).normalized;
            Vector3 n = Vector3.Cross(axis, hang);
            float push = Vector3.Dot(wind, n); // 바람이 깃발 면을 미는 정도 (-1~1)

            float wob = Mathf.Sin(clock + f * .75f + seed) + .5f * Mathf.Sin(clock * 1.7f + f * 1.3f + seed * 2f);
            float ang = (push * 38f * Mathf.Clamp01(amt)) + wob * swing * (.35f + .45f * amt);
            Vector3 h1 = Quaternion.AngleAxis(ang * .55f, axis) * hang;
            Vector3 h2 = Quaternion.AngleAxis(ang, axis) * hang;
            float twist = Mathf.Sin(clock * 1.3f + f * 2.1f + seed) * 10f * amt;
            h2 = Quaternion.AngleAxis(twist, h1) * h2;

            Vector3 midTop = (L + R) * .5f;
            Vector3 ML = L + h1 * flagLength * .5f + (midTop - L) * .25f;
            Vector3 MR = R + h1 * flagLength * .5f + (midTop - R) * .25f;
            Vector3 tip = midTop + h1 * flagLength * .5f + h2 * flagLength * .5f;

            int o = f * 5;
            verts[o] = L; verts[o + 1] = R; verts[o + 2] = ML; verts[o + 3] = MR; verts[o + 4] = tip;
            for (int k = 0; k < 5; k++) verts[o + perSide + k] = verts[o + k];
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
