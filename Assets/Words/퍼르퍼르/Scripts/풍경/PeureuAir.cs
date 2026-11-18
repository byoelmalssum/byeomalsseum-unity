using UnityEngine;

// [퍼르퍼르 풍경] 바람 정보를 한 곳에서 나눠 주는 도우미 (붙이는 스크립트 아님)
//  - 바람 세기·방향: 공간의 PeureuWind 값을 그대로 씀 (천줄과 풍경이 같은 바람에 흔들림)
//  - 카메라 근처 바람: 관객이 지나가면 주변 리본·바람개비·휘장이 더 세게 나부낌
//  - 풀 셰이더용 전역 값(_PeureuWind, _PeureuWindDir)도 여기서 매 프레임 넣어 줌
public static class PeureuAir
{
    static int frame = -1;
    static float amount = 1f;
    static Vector3 dir = new Vector3(1f, 0f, .4f).normalized;
    static Vector3 camPos = new Vector3(0, -9999, 0), lastCamPos;
    static float camSpeed;
    static bool hasCam;

    static readonly int WindId = Shader.PropertyToID("_PeureuWind");
    static readonly int WindDirId = Shader.PropertyToID("_PeureuWindDir");

    /// <summary>지금 바람 세기 (보통 0.5~1.4, 돌풍 때 2 이상)</summary>
    public static float Amount { get { Tick(); return amount; } }
    /// <summary>바람 방향 (월드, 길이 1)</summary>
    public static Vector3 Dir { get { Tick(); return dir; } }
    /// <summary>바람 방향을 수평으로만 (길이 1)</summary>
    public static Vector3 FlatDir
    {
        get
        {
            Tick();
            var d = new Vector3(dir.x, 0, dir.z);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.right;
        }
    }
    /// <summary>지금 화면을 그리는 카메라 위치</summary>
    public static Vector3 CameraPosition { get { Tick(); return camPos; } }

    /// <summary>카메라가 가까울수록 1 (지나가는 속도가 빠르면 더 큼). 멀면 0</summary>
    public static float Near(Vector3 p, float radius)
    {
        Tick();
        if (!hasCam || radius <= 0f) return 0f;
        float d = Vector3.Distance(p, camPos);
        if (d >= radius) return 0f;
        float k = 1f - d / radius;
        k = k * k * (3f - 2f * k);
        return k * (.55f + Mathf.Clamp01(camSpeed / 6f) * .9f);
    }

    public static void Tick()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;

        if (!Application.isPlaying)
        {
            amount = 1f;
            var w0 = Object.FindAnyObjectByType<PeureuWind>();
            if (w0) dir = w0.Direction;
            hasCam = false;
        }
        else
        {
            var w = PeureuWind.Instance;
            if (w != null)
            {
                amount = Mathf.Max(0f, w.Amount);
                dir = w.Direction;
            }
            else
            {
                // PeureuWind가 없을 때: 혼자서 세졌다 약해졌다
                amount = .7f + Mathf.PerlinNoise(Time.time * .25f, .37f) * .7f;
            }

            Camera cam = ActiveCamera();
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                float sp = hasCam ? (p - lastCamPos).magnitude / dt : 0f;
                if (sp > 60f) sp = 0f; // 순간이동(시작 지점 배치 등)은 무시
                camSpeed = Mathf.Lerp(camSpeed, sp, 1f - Mathf.Exp(-dt * 4f));
                lastCamPos = p;
                camPos = p;
                hasCam = true;
            }
            else hasCam = false;
        }

        Shader.SetGlobalFloat(WindId, amount);
        Shader.SetGlobalVector(WindDirId, new Vector4(dir.x, dir.y, dir.z, 0));
    }

    // 켜져 있는 카메라 중 Depth가 가장 큰 것 = 지금 화면에 보이는 카메라 (BreezeZone과 같은 방식)
    static Camera ActiveCamera()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
            if (c.isActiveAndEnabled && (best == null || c.depth > best.depth)) best = c;
        return best;
    }
}
