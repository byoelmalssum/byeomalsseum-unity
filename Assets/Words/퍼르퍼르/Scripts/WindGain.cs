using UnityEngine;

// [퍼르퍼르] 이 오브젝트 아래 천에만 바람을 더 세게 / 천을 더 빳빳하게
//  - 깃발처럼 '바람에 쭉 뻗어야 하는 천'의 부모(예: 깃발_ㅓ1)에 붙인다.
//  - ClothLine으로 천을 다시 걸어도 이 설정은 Play할 때 자동으로 다시 적용된다.
public class WindGain : MonoBehaviour
{
    [Tooltip("PeureuWind 바람에 곱할 배수. 깃발은 3~5")]
    public float gain = 4f;

    [Header("천 성질 (Play 시작할 때 적용)")]
    [Tooltip("끄면 축 처지지 않고 바람에만 날림 (깃발 추천: 끔)")]
    public bool useGravity = false;
    [Tooltip("늘어나지 않는 정도 (0~1). 깃발 추천 1")]
    [Range(0f, 1f)] public float stretchingStiffness = 1f;
    [Tooltip("접히지 않고 펴져 있는 정도 (0~1). 높을수록 빳빳. 깃발 추천 0.6")]
    [Range(0f, 1f)] public float bendingStiffness = 0.6f;
    [Tooltip("움직임이 잦아드는 정도 (0~1). 깃발 추천 0.05")]
    [Range(0f, 1f)] public float damping = 0.05f;

    void Start()
    {
        foreach (var c in GetComponentsInChildren<Cloth>(true))
        {
            c.useGravity = useGravity;
            c.stretchingStiffness = stretchingStiffness;
            c.bendingStiffness = bendingStiffness;
            c.damping = damping;
        }
        if (PeureuWind.Instance != null) PeureuWind.Instance.Refresh();
    }
}
