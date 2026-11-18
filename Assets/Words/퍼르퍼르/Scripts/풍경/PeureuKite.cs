using UnityEngine;

// [퍼르퍼르 풍경] 하늘에 떠서 바람 따라 일렁이는 연 + 땅까지 늘어진 연줄
//  - 이 오브젝트 위치 = 연이 떠 있는 기본 자리. 'anchor' = 연줄이 땅에 묶인 자리(월드).
//  - 자식 '몸'이 실제로 움직이고, 그 아래 '꼬리'(PeureuFlutter)가 따라 나부껴요.
[ExecuteAlways]
public class PeureuKite : MonoBehaviour
{
    [Tooltip("연줄이 땅에 묶인 자리 (월드 좌표)")] public Vector3 anchor;
    [Tooltip("실제로 움직이는 연 몸통")] public Transform body;
    [Tooltip("연줄")] public LineRenderer line;

    [Header("움직임")]
    [Tooltip("좌우로 떠도는 폭 (m)")] public float sway = 2.5f;
    [Tooltip("위아래로 떠도는 폭 (m)")] public float bob = 1.4f;
    [Tooltip("연이 뒤로 젖혀진 각도 (도)")] public float tilt = 28f;
    [Tooltip("좌우로 기우뚱하는 정도 (도)")] public float roll = 14f;
    [Tooltip("연줄 처짐")] public float sag = .06f;
    public float seed;

    const int LinePoints = 18;

    void OnEnable()
    {
        if (line) line.positionCount = LinePoints;
        Pose(0f, 1f);
    }

    /// <summary>코드에서 값을 바꾼 뒤 자세 다시 잡기</summary>
    public void Refresh()
    {
        if (line) line.positionCount = LinePoints;
        Pose(0f, 1f);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        Pose(Time.time, Mathf.Min(PeureuAir.Amount, 3f));
    }

    void Pose(float t, float amt)
    {
        if (body == null) return;
        Vector3 fd = PeureuAir.FlatDir;
        Vector3 side = Vector3.Cross(Vector3.up, fd);

        float nx = Mathf.PerlinNoise(seed, t * .23f) - .5f;
        float ny = Mathf.PerlinNoise(seed + 10f, t * .31f) - .5f;
        float nr = Mathf.PerlinNoise(seed + 20f, t * .55f) - .5f;
        float gustK = .7f + .5f * Mathf.Clamp(amt, 0f, 2f);

        Vector3 offset = side * (nx * sway * 2f * gustK)
                       + Vector3.up * (ny * bob * 2f + Mathf.Sin(t * .9f + seed) * bob * .3f)
                       + fd * ((amt - 1f) * 1.5f);
        body.position = transform.position + offset;

        Quaternion look = Quaternion.LookRotation(fd, Vector3.up); // +Z = 바람 가는 쪽, 앞면(-Z)은 바람 오는 쪽
        body.rotation = look * Quaternion.Euler(tilt + ny * 10f, 0f, nr * 2f * roll + nx * 18f);

        if (line == null) return;
        if (line.positionCount != LinePoints) line.positionCount = LinePoints;
        Vector3 a = anchor;
        Vector3 b = body.TransformPoint(new Vector3(0, -.1f, -.05f));
        float dist = Vector3.Distance(a, b);
        Vector3 ctrl = (a + b) * .5f + Vector3.down * (dist * sag) - fd * (dist * .05f);
        for (int i = 0; i < LinePoints; i++)
        {
            float u = (float)i / (LinePoints - 1);
            Vector3 p = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * ctrl + u * u * b;
            line.SetPosition(i, p);
        }
    }
}
