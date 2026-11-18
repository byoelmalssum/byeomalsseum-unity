using UnityEngine;

// [퍼르퍼르 풍경] 바람개비를 한꺼번에 돌리는 스크립트 (바람개비 그룹에 하나만)
//  - 이 오브젝트 아래에서 이름이 '날개'로 시작하는 오브젝트를 전부 찾아서 돌림 (앞 = Z축 기준으로 회전)
//  - 바람이 세지면 빨리, 카메라가 옆을 지나가면 '휘리릭' 더 빨리 돌아요.
public class PeureuSpinners : MonoBehaviour
{
    [Tooltip("보통 바람일 때 도는 빠르기 (도/초)")] public float baseSpeed = 220f;
    [Tooltip("카메라가 가까이 지나갈 때 더해지는 빠르기 (도/초)")] public float nearBoost = 1100f;
    [Tooltip("이 거리 안이면 카메라 바람을 받음 (m)")] public float nearRadius = 7f;
    [Tooltip("빨라지고 느려지는 무게감 (클수록 묵직)")] public float inertia = 1.2f;

    Transform[] rotors;
    Quaternion[] baseRot;
    float[] angle, speed, factor, radiusScale;

    void Start()
    {
        var all = GetComponentsInChildren<Transform>(true);
        int n = 0;
        foreach (var t in all) if (t.name.StartsWith("날개")) n++;
        rotors = new Transform[n];
        baseRot = new Quaternion[n];
        angle = new float[n]; speed = new float[n]; factor = new float[n]; radiusScale = new float[n];
        int i = 0;
        foreach (var t in all)
        {
            if (!t.name.StartsWith("날개")) continue;
            rotors[i] = t;
            baseRot[i] = t.localRotation;
            angle[i] = Random.Range(0f, 360f);
            factor[i] = Random.Range(.75f, 1.25f);
            // 큰 바람개비는 천천히
            float s = Mathf.Max(.2f, t.lossyScale.x);
            radiusScale[i] = 1f / Mathf.Sqrt(s);
            i++;
        }
    }

    void Update()
    {
        if (rotors == null) return;
        float amt = Mathf.Min(PeureuAir.Amount, 3f);
        float dt = Time.deltaTime;
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(.05f, inertia));
        for (int i = 0; i < rotors.Length; i++)
        {
            var r = rotors[i];
            if (r == null) continue;
            float near = PeureuAir.Near(r.position, nearRadius * Mathf.Max(1f, r.lossyScale.x * .6f));
            float target = (baseSpeed * (.25f + amt) + nearBoost * near) * radiusScale[i] * factor[i];
            speed[i] = Mathf.Lerp(speed[i], target, k);
            angle[i] -= speed[i] * dt;
            r.localRotation = baseRot[i] * Quaternion.Euler(0, 0, angle[i]);
        }
    }
}
