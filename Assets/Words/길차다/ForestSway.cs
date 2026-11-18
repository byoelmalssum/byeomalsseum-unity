using UnityEngine;

// 길차다 숲: 잎·고사리가 바람에 눈에 보이게 흔들림
public class ForestSway : MonoBehaviour
{
    [Tooltip("흔들리는 각도 (도)")] public float amount = 2.5f;
    [Tooltip("흔들리는 빠르기")] public float speed = 1f;
    public float phase;

    Quaternion baseRot;

    void Awake() => baseRot = transform.localRotation;

    void Update()
    {
        float t = Time.time;
        float gust = .6f + .4f * Mathf.Sin(t * .23f + phase * .1f);      // 바람이 세졌다 약해졌다
        float z = Mathf.Sin(t * speed + phase) * amount * gust;
        float x = Mathf.Sin(t * speed * .7f + phase * 1.3f) * amount * .5f * gust;
        transform.localRotation = baseRot * Quaternion.Euler(x, 0, z);
    }
}
