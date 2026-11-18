using UnityEngine;

// [살천스럽다] 공간 분위기 (플레이할 때만 동작)
//  - 카메라 주변 눈보라 파티클이 체험 카메라를 따라다님 (어디를 가도 눈보라 속)
//  - 가로등 빛이 찬바람에 흔들리듯 깜빡임
//  - SalcheonSpace 버튼이 자동으로 만들어 줌
public class SalcheonAmbience : MonoBehaviour
{
    [Header("카메라 따라다니는 눈보라")]
    public Transform blizzard;
    [Tooltip("카메라보다 얼마나 앞(바람이 불어오는 쪽)에 둘지 (m)")]
    public Vector3 blizzardOffset = new Vector3(6f, 1f, 2f);

    [Header("가로등 깜빡임")]
    public Light[] flickerLights;
    [Range(0f, 1f)] public float flickerAmount = 0.15f;
    public float flickerSpeed = 7f;

    float[] baseIntensity;

    void Start()
    {
        if (flickerLights == null) flickerLights = new Light[0];
        baseIntensity = new float[flickerLights.Length];
        for (int i = 0; i < flickerLights.Length; i++)
            if (flickerLights[i]) baseIntensity[i] = flickerLights[i].intensity;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (blizzard && cam)
        {
            Vector3 p = cam.transform.position + blizzardOffset;
            blizzard.position = p;
        }

        float t = Time.time;
        for (int i = 0; i < flickerLights.Length; i++)
        {
            var l = flickerLights[i];
            if (!l) continue;
            float f = 1f - flickerAmount * 0.5f
                      + flickerAmount * 0.5f * Mathf.Sin(t * flickerSpeed + i * 1.7f) * Mathf.Sin(t * flickerSpeed * 0.43f + i);
            if (Mathf.PerlinNoise(t * 3f, i * 10f) > 0.9f) f *= 0.4f; // 가끔 툭 어두워짐
            l.intensity = baseIntensity[i] * f;
        }
    }
}
