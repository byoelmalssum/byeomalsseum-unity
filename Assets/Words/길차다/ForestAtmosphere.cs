using UnityEngine;

// 길차다 숲 분위기: 카메라가 깊이 들어갈수록 남색 어둠 → 하늘빛 안개 속 세계수
public class ForestAtmosphere : MonoBehaviour
{
    [Header("따라갈 카메라 (비우면 메인 카메라)")]
    public Transform target;
    public Camera[] cameras;

    [Header("이 구간에서 분위기가 바뀜 (z, m)")]
    public float darkUntilZ = 40f;
    public float lightFromZ = 90f;

    [Header("안개")]
    public Color fogDark = new Color32(0x0b, 0x1c, 0x26, 255);
    public Color fogLight = new Color32(0x7f, 0xb0, 0xbf, 255);
    public float densityDark = .05f;
    public float densityLight = .022f;

    [Header("주변광 (하늘 · 지평선 · 땅)")]
    public Color ambientSky = new Color32(0x7f, 0xb3, 0xc9, 255);
    public Color ambientEquator = new Color32(0x3a, 0x5c, 0x5a, 255);
    public Color ambientGround = new Color32(0x1f, 0x3a, 0x22, 255);
    public float ambientDark = .28f, ambientLight = .75f;

    [Header("빛")]
    public Light sun;
    public float sunDark = .6f, sunLight = 1.1f;
    public Light treeLight;     // 세계수 아래 초록빛
    public float treeLightMax = 3f;
    public Light spiritLight;   // 세계수 위 정령빛
    public float spiritLightMax = 2.5f;
    public Light[] warmLights;  // 세계수 공간에 새어 드는 노란 햇살
    public float warmLightMax = 6f;

    [Header("미리보기 (에디터에서 0~1로 바꿔 보기)")]
    [Range(0, 1)] public float editorPreview = 0f;

    void Update()
    {
        Transform t = target ? target : (Camera.main ? Camera.main.transform : null);
        if (t == null) return;
        float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(darkUntilZ, lightFromZ, t.position.z));
        Apply(k, Time.time);
    }

    // 에디터에서 미리보기 값을 바꾸면 씬 화면에 바로 반영
    void OnValidate()
    {
        if (!Application.isPlaying) Apply(editorPreview, 0f);
    }

    public void Apply(float k, float time)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        Color fog = Color.Lerp(fogDark, fogLight, k);
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = Mathf.Lerp(densityDark, densityLight, k);

        float a = Mathf.Lerp(ambientDark, ambientLight, k);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky * a;
        RenderSettings.ambientEquatorColor = ambientEquator * a;
        RenderSettings.ambientGroundColor = ambientGround * a;

        if (cameras != null)
            foreach (var c in cameras)
                if (c) { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = fog; }

        if (sun) sun.intensity = Mathf.Lerp(sunDark, sunLight, k);
        if (treeLight) treeLight.intensity = Mathf.Lerp(0, treeLightMax, k) * (1 + .1f * Mathf.Sin(time * 1.1f));
        if (warmLights != null)
            foreach (var w in warmLights)
                if (w) w.intensity = Mathf.Lerp(0, warmLightMax, k) * (1 + .06f * Mathf.Sin(time * .5f + w.transform.GetSiblingIndex() * 1.7f));
        if (spiritLight) spiritLight.intensity = Mathf.Lerp(0, spiritLightMax, k) * (1 + .15f * Mathf.Sin(time * .7f));
    }
}
