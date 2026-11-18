using UnityEngine;

// [살천스럽다] 무서운 분위기 (플레이할 때만, 끝나면 원래대로)
//  - 안개를 어둡고 짙게, 주변광·햇빛을 낮춤 → 숲 너머가 잘 안 보이고 실루엣만 남음
//  - 가끔 '숨 멎는 순간': 바람·눈보라가 뚝 멎고 안개가 확 짙어졌다가 풀림
//  - SalcheonSpace 버튼(무서운 분위기 켜짐)이 자동으로 붙여 줌. 숫자는 여기서 조절.
public class SalcheonMood : MonoBehaviour
{
    [Header("안개")]
    public Color fogColor = new Color32(70, 80, 94, 255);
    [Tooltip("원래 안개 짙기에 더할 값")] public float fogAdd = 0.012f;

    [Header("빛")]
    [Range(0f, 1f)] public float ambientScale = 0.55f;
    [Range(0f, 1f)] public float sunScale = 0.45f;

    [Header("숨 멎는 순간")]
    public bool hush = true;
    public Vector2 hushEvery = new Vector2(25f, 45f);
    public float hushSeconds = 4f;
    public ParticleSystem[] stopDuringHush;

    Color oldFog, oldAmbSky, oldAmbEq, oldAmbGround, oldAmbLight;
    float oldDensity, oldAmbIntensity;
    Light sun; float oldSun;
    bool applied;

    void OnEnable()
    {
        oldFog = RenderSettings.fogColor; oldDensity = RenderSettings.fogDensity;
        oldAmbSky = RenderSettings.ambientSkyColor; oldAmbEq = RenderSettings.ambientEquatorColor;
        oldAmbGround = RenderSettings.ambientGroundColor; oldAmbLight = RenderSettings.ambientLight;
        oldAmbIntensity = RenderSettings.ambientIntensity;
        sun = RenderSettings.sun;
        if (!sun) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun) oldSun = sun.intensity;

        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = oldDensity + fogAdd;
        RenderSettings.ambientSkyColor = oldAmbSky * ambientScale;
        RenderSettings.ambientEquatorColor = oldAmbEq * ambientScale;
        RenderSettings.ambientGroundColor = oldAmbGround * ambientScale;
        RenderSettings.ambientLight = oldAmbLight * ambientScale;
        RenderSettings.ambientIntensity = oldAmbIntensity * ambientScale;
        if (sun) sun.intensity = oldSun * sunScale;
        applied = true;
        if (hush) StartCoroutine(HushLoop());
    }

    void OnDisable()
    {
        if (!applied) return;
        RenderSettings.fogColor = oldFog; RenderSettings.fogDensity = oldDensity;
        RenderSettings.ambientSkyColor = oldAmbSky; RenderSettings.ambientEquatorColor = oldAmbEq;
        RenderSettings.ambientGroundColor = oldAmbGround; RenderSettings.ambientLight = oldAmbLight;
        RenderSettings.ambientIntensity = oldAmbIntensity;
        if (sun) sun.intensity = oldSun;
        applied = false;
    }

    System.Collections.IEnumerator HushLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(hushEvery.x, hushEvery.y));
            float baseD = RenderSettings.fogDensity;
            if (stopDuringHush != null) foreach (var p in stopDuringHush) if (p) { var m = p.main; m.simulationSpeed = 0.05f; }
            for (float t = 0; t < hushSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t / hushSeconds) * Mathf.PI);
                RenderSettings.fogDensity = baseD + 0.02f * k;
                yield return null;
            }
            RenderSettings.fogDensity = baseD;
            if (stopDuringHush != null) foreach (var p in stopDuringHush) if (p) { var m = p.main; m.simulationSpeed = 1f; }
        }
    }
}
