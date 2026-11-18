using UnityEngine;

// [퍼르퍼르 풍경] 언덕 모양 설정 (PeureuWorld의 [풍경 만들기]가 값을 채워 줌)
//  - 가운데(글자들이 있는 곳)는 거의 평평하고, 바깥으로 갈수록 부드러운 언덕이 솟아요.
//  - 오솔길은 시작 지점 → ㅍ 문들 → 바람 정원 순서로 이어져요.
//  - 값을 바꾸면 언덕·길·풀밭이 바로 다시 만들어져요.
[ExecuteAlways, DisallowMultipleComponent]
public class PeureuGround : MonoBehaviour
{
    [Header("평평한 가운데 (x, z 범위, m)")]
    public Vector2 coreMin = new Vector2(-30, -15);
    public Vector2 coreMax = new Vector2(30, 115);

    [Header("언덕")]
    [Tooltip("가장 높은 언덕 높이 (m)")] public float hillHeight = 12f;
    [Tooltip("언덕이 솟는 거리 (클수록 완만)")] public float hillReach = 28f;
    [Tooltip("언덕의 굽이 크기 (m)")] public float rolling = 3.5f;
    [Tooltip("땅이 펼쳐진 크기 (가운데에서 한쪽으로, m)")] public float extent = 170f;
    public int seed = 3;

    [Header("오솔길 (x, z 점들 — z 순서대로)")]
    public Vector2[] pathPoints = { new Vector2(0, -8), new Vector2(0, 100) };
    public float pathWidth = 3.4f;

    [Header("풀이 안 나는 곳")]
    [Tooltip("바닥에 눕힌 자모 자리 (x, z)")] public Rect[] noGrass = new Rect[0];
    [Tooltip("바람 정원 단 가운데")] public Vector3 clearCenter = new Vector3(0, 0, 92);
    public float clearRadius = 0f;

    public System.Action Changed;

    void Update() => PeureuAir.Tick(); // 풀 셰이더에 바람 값 넣기

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Changed?.Invoke(); };
    }
#endif

    public Vector2 CoreCenter => (coreMin + coreMax) * .5f;

    /// <summary>오솔길 가운데 x (z 위치에서)</summary>
    public float PathX(float z)
    {
        if (pathPoints == null || pathPoints.Length == 0) return 0f;
        if (z <= pathPoints[0].y) return pathPoints[0].x;
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            Vector2 a = pathPoints[i], b = pathPoints[i + 1];
            if (z <= b.y)
            {
                float t = Mathf.InverseLerp(a.y, b.y, z);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(a.x, b.x, t) + Mathf.Sin(z * .11f + seed) * .9f * Mathf.Sin(t * Mathf.PI);
            }
        }
        return pathPoints[pathPoints.Length - 1].x;
    }

    public float PathStartZ => pathPoints != null && pathPoints.Length > 0 ? pathPoints[0].y : 0f;
    public float PathEndZ => pathPoints != null && pathPoints.Length > 0 ? pathPoints[pathPoints.Length - 1].y : 0f;

    /// <summary>오솔길 가운데선까지 가로 거리 (길 범위 밖이면 큰 값)</summary>
    public float PathDistance(float x, float z)
    {
        if (z < PathStartZ - 2f || z > PathEndZ + 2f) return 999f;
        return Mathf.Abs(x - PathX(z));
    }

    /// <summary>평평한 가운데에서 얼마나 바깥인지 (m). 안쪽이면 0</summary>
    public float OutsideCore(float x, float z)
    {
        float dx = Mathf.Max(coreMin.x - x, 0f, x - coreMax.x);
        float dz = Mathf.Max(coreMin.y - z, 0f, z - coreMax.y);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>땅 높이 (이 오브젝트 기준 좌표)</summary>
    public float Height(float x, float z)
    {
        float d = OutsideCore(x, z);
        float ripple = (Mathf.Sin(x * .13f + seed) + Mathf.Cos(z * .11f - seed * .7f) + 2f) * .03f;
        float rise = hillHeight * (1f - Mathf.Exp(-d / Mathf.Max(1f, hillReach)));
        float roll = (Mathf.Sin(x * .045f + seed * 1.3f) * Mathf.Cos(z * .05f + seed)
                    + .6f * Mathf.Sin((x + z) * .08f + seed * 2f)) * rolling * Mathf.Clamp01(d / 25f);
        float y = ripple + rise + roll;
        if (PathDistance(x, z) < pathWidth) y *= .4f;
        return y;
    }
}
