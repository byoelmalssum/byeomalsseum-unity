using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 작업용 자유 카메라 (플레이 중 Tab으로 전환).
    /// 우클릭 드래그: 둘러보기 / WASD: 이동 / Q·E: 아래·위 / Shift: 빠르게
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WorkCamera : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float fastMultiplier = 3f;
        public float lookSensitivity = .15f;

        float yaw, pitch;
        bool wasActive;

        void SyncAngles()
        {
            Vector3 e = transform.eulerAngles;
            yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x;
        }

        void Update()
        {
            var cam = GetComponent<Camera>();
            if (!cam.enabled) { wasActive = false; return; }
            if (!wasActive) { SyncAngles(); wasActive = true; } // 전환된 순간 현재 방향에서 시작
            float dt = Time.unscaledDeltaTime;

            if (BmInput.RightMouseHeld)
            {
                Vector2 d = BmInput.MouseDelta;
                yaw += d.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }

            Vector3 move = Vector3.zero;
            if (BmInput.Held(BmKey.W)) move += transform.forward;
            if (BmInput.Held(BmKey.S)) move -= transform.forward;
            if (BmInput.Held(BmKey.D)) move += transform.right;
            if (BmInput.Held(BmKey.A)) move -= transform.right;
            if (BmInput.Held(BmKey.E)) move += Vector3.up;
            if (BmInput.Held(BmKey.Q)) move -= Vector3.up;
            float sp = moveSpeed * (BmInput.Held(BmKey.Shift) ? fastMultiplier : 1f);
            transform.position += move.normalized * sp * dt;
        }
    }
}
