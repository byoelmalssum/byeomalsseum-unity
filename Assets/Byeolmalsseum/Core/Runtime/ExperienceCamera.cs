using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 체험 카메라: 경로를 따라 일정 속도로 반복 이동, 마우스 위치에 따라 시선만 돌아감.
    /// (체험자는 이동 조작 불가)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ExperienceCamera : MonoBehaviour
    {
        public CameraPath path;
        [Tooltip("이동 속도 (m/초)")] public float speed = 1.0f;
        [Tooltip("경로 앞쪽 몇 m 지점을 바라볼지")] public float lookAhead = 2.5f;
        [Tooltip("지정하면 이동 중 항상 이 대상을 바라봄 (비우면 진행 방향)")] public Transform lookTarget;

        [Header("마우스 시선")]
        [Tooltip("마우스를 화면 끝까지 옮겼을 때 좌우로 돌아가는 각도")] public float maxYaw = 55f;
        [Tooltip("마우스를 화면 끝까지 옮겼을 때 위아래로 돌아가는 각도")] public float maxPitch = 30f;
        [Tooltip("클수록 시선이 빨리 따라옴")] public float lookSmooth = 4f;
        public bool invertY = false;

        public bool MouseLookEnabled { get; set; }
        public bool IsMoving { get; private set; }

        float distance, speedFactor, yaw, pitch;
        Quaternion baseRot;
        bool initialized;

        public Camera Cam => GetComponent<Camera>();

        /// <summary>경로 시작점에 놓기 (시작할 때 + 에디터 버튼)</summary>
        public void PlaceAtStart()
        {
            distance = 0; speedFactor = 0; yaw = pitch = 0;
            if (path != null && path.IsValid)
            {
                path.Rebuild();
                Vector3 pos = path.EvaluateByDistance(0);
                transform.position = pos;
                baseRot = Quaternion.LookRotation(LookPoint(pos) - pos, Vector3.up);
                transform.rotation = baseRot;
            }
            else baseRot = transform.rotation;
            initialized = true;
        }

        public void BeginMoving() => IsMoving = true;

        Vector3 LookPoint(Vector3 pos)
        {
            if (lookTarget) return lookTarget.position;
            Vector3 p = path.EvaluateByDistance(distance + lookAhead);
            return (p - pos).sqrMagnitude < 1e-6f ? pos + transform.forward : p;
        }

        void LateUpdate()
        {
            if (!initialized) PlaceAtStart();
            float dt = Time.deltaTime;

            if (path != null && path.IsValid)
            {
                if (IsMoving)
                {
                    speedFactor = Mathf.MoveTowards(speedFactor, 1f, dt / 2f); // 2초 동안 서서히 출발
                    distance += speed * speedFactor * dt;
                }
                Vector3 pos = path.EvaluateByDistance(distance);
                transform.position = pos;
                Quaternion want = Quaternion.LookRotation(LookPoint(pos) - pos, Vector3.up);
                baseRot = Quaternion.Slerp(baseRot, want, 1f - Mathf.Exp(-3f * dt));
            }

            // 마우스 위치(화면 중앙 기준 -1~1) → 시선 각도
            float tYaw = 0, tPitch = 0;
            if (MouseLookEnabled && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 m = BmInput.MousePosition;
                float nx = Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f);
                float ny = Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f);
                tYaw = nx * maxYaw;
                tPitch = -ny * maxPitch * (invertY ? -1f : 1f);
            }
            float k = 1f - Mathf.Exp(-lookSmooth * dt);
            yaw = Mathf.Lerp(yaw, tYaw, k);
            pitch = Mathf.Lerp(pitch, tPitch, k);

            transform.rotation = baseRot * Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
