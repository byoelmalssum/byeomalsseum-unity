using UnityEngine;

// [퍼르퍼르] 하늘을 천천히 떠다니는 큰 천 / 종이
//  - 떠다니게 할 오브젝트(천 등)에 붙인다. 바람 방향으로 천천히 흘러가고 위아래로 둥실거림.
//  - 정해 둔 범위를 벗어나면 반대편에서 다시 들어옴.
public class Drift : MonoBehaviour
{
    [Tooltip("흘러가는 방향과 빠르기 (m/초)")]
    public Vector3 velocity = new Vector3(0.6f, 0f, 0.2f);
    [Tooltip("위아래로 둥실거리는 폭 (m)")]
    public float bob = 0.6f;
    public float bobSpeed = 0.4f;
    [Tooltip("천천히 도는 속도 (도/초)")]
    public float spin = 6f;

    [Header("떠다닐 범위 (공간 중심 기준)")]
    public Vector3 areaCenter = new Vector3(0f, 0f, 35f);
    public Vector3 areaSize = new Vector3(100f, 0f, 110f);

    Vector3 basePos;
    float seed;

    void Start()
    {
        basePos = transform.position;
        seed = Random.value * 100f;
    }

    void Update()
    {
        basePos += velocity * Time.deltaTime;

        Vector3 half = areaSize * 0.5f;
        if (basePos.x > areaCenter.x + half.x) basePos.x -= areaSize.x;
        if (basePos.x < areaCenter.x - half.x) basePos.x += areaSize.x;
        if (basePos.z > areaCenter.z + half.z) basePos.z -= areaSize.z;
        if (basePos.z < areaCenter.z - half.z) basePos.z += areaSize.z;

        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + seed) * bob;
        transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
    }
}
