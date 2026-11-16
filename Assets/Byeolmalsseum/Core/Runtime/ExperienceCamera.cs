using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 체험 카메라 (v4): 날아다니며 공간 체험.
    /// - 이동: W A S D / 방향키 (바라보는 방향 기준), 위·아래: Space·E / Ctrl·Q, Shift: 빠르게
    /// - 시선: 마우스를 움직이면 회전 (드래그 X, 커서는 숨겨지고 고정됨)
    /// - 시작 위치·방향: 'startPoint' 오브젝트 (없으면 예전 카메라 경로의 시작점)
    /// - 이동 범위: WordSpace가 계산해서 Limit에 넣어 줌
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ExperienceCamera : MonoBehaviour
    {
        [Header("시작 위치")]
        [Tooltip("체험이 시작되는 위치·방향. 글자가 이 앞에 나타나요")]
        public Transform startPoint;
        [Tooltip("(예전 방식) startPoint가 비어 있으면 이 경로의 시작점에서 출발")]
        public CameraPath path;

        [Header("이동 (날기)")]
        [Tooltip("이동 속도 (m/초)")] public float moveSpeed = 3f;
        [Tooltip("Shift를 누르면 몇 배 빨라질지")] public float fastMultiplier = 2.5f;
        [Tooltip("클수록 출발·멈춤이 빠릿함")] public float acceleration = 8f;
        [Tooltip("Space·E / Ctrl·Q로 위아래 이동 허용")] public bool allowVertical = true;

        [Header("마우스 시선")]
        [Tooltip("마우스 감도")] public float mouseSensitivity = .12f;
        [Tooltip("위아래로 볼 수 있는 최대 각도")] public float maxPitch = 80f;
        public bool invertY = false;

        /// <summary>시선 회전 허용 (글자·날아가는 연출 중에는 꺼짐)</summary>
        public bool MouseLookEnabled { get; set; }
        /// <summary>이동 허용</summary>
        public bool MovementEnabled { get; set; }
        /// <summary>ESC 메뉴가 열려 있으면 true (이동·시선 멈춤)</summary>
        public bool Paused { get; set; }
        /// <summary>이동 가능 범위 (WordSpace가 설정, null이면 제한 없음)</summary>
        public Bounds? Limit { get; set; }

        // 예전 코드와의 호환용
        public bool IsMoving => MovementEnabled;
        public void BeginMoving() => MovementEnabled = true;

        public Camera Cam => GetComponent<Camera>();

        float yaw, pitch;
        Vector3 velocity;

        /// <summary>시작 위치에 놓기 (시작할 때 + 에디터 버튼 + 별우주 연결)</summary>
        public void PlaceAtStart()
        {
            velocity = Vector3.zero;
            if (startPoint)
            {
                transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            }
            else if (path != null && path.IsValid)
            {
                path.Rebuild();
                Vector3 pos = path.EvaluateByDistance(0);
                Vector3 ahead = path.EvaluateByDistance(2.5f);
                transform.position = pos;
                if ((ahead - pos).sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(ahead - pos, Vector3.up);
            }
            SyncAngles();
        }

        void SyncAngles()
        {
            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180 ? e.x - 360 : e.x;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        }

        void OnEnable() => SyncAngles();

        void Update()
        {
            if (!Cam.enabled) return;
            float dt = Time.deltaTime;
            bool active = !Paused;

            // ── 시선: 마우스 이동량으로 회전
            if (active && MouseLookEnabled)
            {
                Vector2 d = BmInput.MouseDelta;
                yaw += d.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity * (invertY ? -1f : 1f), -maxPitch, maxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }

            // ── 이동: 바라보는 방향 기준으로 날기
            Vector3 input = Vector3.zero;
            if (active && MovementEnabled)
            {
                if (BmInput.Held(BmKey.W) || BmInput.Held(BmKey.Up)) input += transform.forward;
                if (BmInput.Held(BmKey.S) || BmInput.Held(BmKey.Down)) input -= transform.forward;
                if (BmInput.Held(BmKey.D) || BmInput.Held(BmKey.Right)) input += transform.right;
                if (BmInput.Held(BmKey.A) || BmInput.Held(BmKey.Left)) input -= transform.right;
                if (allowVertical)
                {
                    if (BmInput.Held(BmKey.Space) || BmInput.Held(BmKey.E)) input += Vector3.up;
                    if (BmInput.Held(BmKey.Ctrl) || BmInput.Held(BmKey.Q)) input -= Vector3.up;
                }
            }
            float sp = moveSpeed * (BmInput.Held(BmKey.Shift) ? fastMultiplier : 1f);
            Vector3 target = input.sqrMagnitude > 0 ? input.normalized * sp : Vector3.zero;
            velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-acceleration * dt));

            if (velocity.sqrMagnitude > 1e-6f)
            {
                Vector3 p = transform.position + velocity * dt;
                if (Limit.HasValue)
                {
                    // 범위 밖으로 나가려는 방향의 속도만 없앰 (벽을 따라 미끄러지듯)
                    Vector3 c = Limit.Value.ClosestPoint(p);
                    if (c.x != p.x) velocity.x = 0;
                    if (c.y != p.y) velocity.y = 0;
                    if (c.z != p.z) velocity.z = 0;
                    p = c;
                }
                transform.position = p;
            }
        }

        void OnDrawGizmos()
        {
            if (!startPoint) return;
            Gizmos.color = Color.yellow;
            Vector3 p = startPoint.position;
            Gizmos.DrawWireSphere(p, .25f);
            Gizmos.DrawLine(p, p + startPoint.forward * 1.5f);
            Gizmos.DrawWireSphere(p + startPoint.forward * 1.5f, .08f);
        }
    }
}
