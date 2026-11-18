using Byeolmalsseum;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 길차다 씬 전용: 날지 않고 땅을 따라 걷기 + 숲 전체를 돌아다닐 수 있게 범위 넓히기
// + 세계수 입구에 들어서면 잠깐 천천히 걸어가며 세계수를 올려다보는 연출
// 팀원 스크립트(ExperienceCamera)는 고치지 않고, 이 씬에서만 덧붙여서 바꿔요.
[DefaultExecutionOrder(1000)] // 팀원 카메라가 움직인 다음에 높이를 맞춤
public class GilchadaWalk : MonoBehaviour
{
    [Header("걷기")]
    [Tooltip("눈높이 (m)")] public float eyeHeight = 1.7f;
    [Tooltip("걷는 속도 (m/초), Shift 누르면 빨라짐")] public float walkSpeed = 3f;
    [Tooltip("땅 높이를 따라가는 부드러움 (클수록 빠릿)")] public float groundFollow = 10f;

    [Header("다닐 수 있는 곳: 숲길 + 세계수 둘레 공터")]
    [Tooltip("숲길 가운데에서 양옆으로 갈 수 있는 거리 (m)")] public float pathHalfWidth = 3f;
    [Tooltip("숲길이 시작하는 곳 (z, m)")] public float pathStartZ = -20f;
    [Tooltip("세계수 둘레 공터 반지름 (m)")] public float clearingRadius = 20f;
    [Tooltip("세계수 중심에서 이 거리보다 가까이 못 감 (m)")] public float worldTreeKeepOut = 13f;

    [Header("시작 위치")]
    [Tooltip("켜면 숲길 입구에서 세계수 쪽을 보며 시작")] public bool startAtForestEntrance = true;

    [Header("세계수 입구 연출 (씬에서 상자·점을 옮겨서 조절)")]
    [Tooltip("이 상자 안에 들어가면 연출 시작")] public BoxCollider entranceZone;
    [Tooltip("연출 동안 천천히 걸어갈 자리")] public Transform walkTo;
    [Tooltip("연출 동안 올려다볼 곳")] public Transform lookAt;
    [Tooltip("켜면 '올려다볼 곳' 대신 세계수의 실제 위치·높이를 자동으로 봄 (세계수를 옮기거나 내려도 맞음)")] public bool lookAtWorldTreeAuto = true;
    [Tooltip("연출 시간 (초)")] public float introSeconds = 5f;
    [Tooltip("연출 중 걷는 속도 (m/초)")] public float introSpeed = 1f;
    [Tooltip("켜면 한 번만, 끄면 상자를 나갔다 들어올 때마다")] public bool introOnlyOnce = true;

    ExperienceCamera ec;
    Transform origin;
    enum Intro { Ready, Playing, WaitExit, Done }
    Intro intro = Intro.Ready;
    float introT;
    Quaternion introStartRot;
    Vector3 introLook;

    Vector3 LookPoint()
    {
        if (lookAtWorldTreeAuto)
        {
            var forest = FindAnyObjectByType<GilchadaForest>();
            Transform tree = null;
            if (forest)
                foreach (var tr in forest.GetComponentsInChildren<Transform>())
                    if (tr.name == "세계수") { tree = tr; break; }
            if (tree)
            {
                var rs = tree.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    // 몸통 위쪽 ~ 수관 아래쯤을 올려다봄
                    return new Vector3(tree.position.x, b.min.y + b.size.y * .62f, tree.position.z);
                }
            }
        }
        if (lookAt) return lookAt.position;
        return ec.transform.position + ec.transform.forward * 10;
    }

    // 연출이 끝난 뒤 팀원 카메라가 지금 시선 그대로 이어받게 맞춤 (튀지 않게)
    void SyncCamera(Transform t)
    {
        Vector3 e = t.rotation.eulerAngles;
        float pitch = e.x > 180 ? e.x - 360 : e.x;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var fy = typeof(ExperienceCamera).GetField("yaw", flags);
        var fp = typeof(ExperienceCamera).GetField("pitch", flags);
        var fv = typeof(ExperienceCamera).GetField("velocity", flags);
        if (fy != null && fp != null)
        {
            fy.SetValue(ec, e.y);
            fp.SetValue(ec, Mathf.Clamp(pitch, -ec.maxPitch, ec.maxPitch));
        }
        else { ec.enabled = false; ec.enabled = true; }
        if (fv != null) fv.SetValue(ec, Vector3.zero);
        t.rotation = Quaternion.Euler(Mathf.Clamp(pitch, -ec.maxPitch, ec.maxPitch), e.y, 0);
    }

    void Awake()
    {
        ec = FindAnyObjectByType<ExperienceCamera>();
        if (ec == null) { Debug.LogWarning("[길차다 걷기] 체험 카메라를 못 찾았어요."); enabled = false; return; }

        var forest = FindAnyObjectByType<GilchadaForest>();
        origin = forest ? forest.transform : transform;

        ec.allowVertical = false;   // Space·Q 로 위아래 날기 끄기
        ec.moveSpeed = walkSpeed;

        if (startAtForestEntrance && ec.startPoint)
        {
            float z = -16, x = GilchadaForest.PathX(z);
            Vector3 p = new Vector3(x, GilchadaForest.H(x, z) + eyeHeight, z);
            ec.startPoint.SetPositionAndRotation(origin.TransformPoint(p), origin.rotation);
        }

        if (entranceZone == null || walkTo == null || lookAt == null) CreateIntroPoints();
    }

    void LateUpdate()
    {
        // 이동 범위: 숲 전체 (팀원 도구가 정한 범위를 이 씬에서만 덮어씀)
        ec.Limit = new Bounds(origin.TransformPoint(new Vector3(0, 0, 60)), new Vector3(400, 800, 400)); // 실제 제한은 아래에서

        Transform t = ec.transform;
        UpdateIntro(t);
        if (!ec.MovementEnabled && intro != Intro.Playing) return; // 글자·날아가는 연출 중에는 그대로

        Vector3 l = origin.InverseTransformPoint(t.position);

        // 숲길 + 공터 안에서만 (벽을 따라 미끄러지듯)
        Vector2 q = Constrain(new Vector2(l.x, l.z));
        l.x = q.x; l.z = q.y;

        // 땅 높이 + 눈높이로 맞추기 (날지 않고 걷기)
        float groundY = GilchadaForest.H(l.x, l.z) + eyeHeight;
        l.y = Mathf.Lerp(l.y, groundY, 1f - Mathf.Exp(-groundFollow * Time.deltaTime));

        t.position = origin.TransformPoint(l);
    }

    Vector2 Constrain(Vector2 p)
    {
        Vector2 c = new Vector2(GilchadaForest.TREE.x, GilchadaForest.TREE.z);
        float dc = Vector2.Distance(p, c);
        bool inClearing = dc <= clearingRadius;
        bool inPath = p.y >= pathStartZ && p.y <= c.y && Mathf.Abs(p.x - GilchadaForest.PathX(p.y)) <= pathHalfWidth;

        if (!inClearing && !inPath)
        {
            // 공터 가장자리와 숲길 가장자리 중 더 가까운 곳으로
            Vector2 dir = dc > 1e-4f ? (p - c) / dc : Vector2.down;
            Vector2 onClearing = c + dir * clearingRadius;
            float z = Mathf.Clamp(p.y, pathStartZ, c.y), px = GilchadaForest.PathX(z);
            Vector2 onPath = new Vector2(Mathf.Clamp(p.x, px - pathHalfWidth, px + pathHalfWidth), z);
            p = (p - onClearing).sqrMagnitude < (p - onPath).sqrMagnitude ? onClearing : onPath;
            dc = Vector2.Distance(p, c);
        }
        // 세계수 몸통 안으로는 못 들어감 (둘레를 따라 돌 수 있음)
        if (dc < worldTreeKeepOut)
        {
            Vector2 dir = dc > 1e-4f ? (p - c) / dc : Vector2.down;
            p = c + dir * worldTreeKeepOut;
        }
        return p;
    }

    // ───────────────────────────── 세계수 입구 연출
    void UpdateIntro(Transform t)
    {
        if (entranceZone == null || walkTo == null) return;
        bool inside = entranceZone.bounds.Contains(t.position);

        switch (intro)
        {
            case Intro.Ready:
                if (inside && ec.MovementEnabled)
                {
                    intro = Intro.Playing; introT = 0; introStartRot = t.rotation;
                    introLook = LookPoint();
                    ec.MovementEnabled = false;   // 잠깐 조작 멈춤
                    ec.MouseLookEnabled = false;
                }
                break;

            case Intro.Playing:
                if (ec.Paused) return;
                introT += Time.deltaTime;
                float u = Mathf.Clamp01(introT / introSeconds);

                // 정해진 자리로 천천히 걸어감 (처음·끝은 부드럽게)
                Vector3 to = walkTo.position; to.y = t.position.y;
                float ease = Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.15f));
                t.position = Vector3.MoveTowards(t.position, to, introSpeed * Mathf.Max(.2f, ease) * Time.deltaTime);

                // 시선이 서서히 위로 (세계수를 올려다봄)
                Quaternion look = Quaternion.LookRotation(introLook - t.position, Vector3.up);
                t.rotation = Quaternion.Slerp(introStartRot, look, Mathf.SmoothStep(0, 1, Mathf.Min(1, u * 1.3f)));

                if (u >= 1)
                {
                    // 팀원 카메라가 지금 시선에서 이어받도록 각도 다시 맞춤 (껐다 켜면 OnEnable에서 맞춰짐)
                    SyncCamera(t);
                    ec.MovementEnabled = true;
                    ec.MouseLookEnabled = true;
                    intro = introOnlyOnce ? Intro.Done : Intro.WaitExit;
                }
                break;

            case Intro.WaitExit:
                if (!inside) intro = Intro.Ready;
                break;
        }
    }

    // 입구 상자·걸어갈 자리·올려다볼 곳 만들기 (씬에서 옮겨서 조절 가능)
    public void CreateIntroPoints()
    {
        var root = transform.Find("세계수 입구 연출");
        if (root == null)
        {
            root = new GameObject("세계수 입구 연출").transform;
            root.SetParent(transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(root.gameObject, "입구 연출");
#endif
        }

        if (entranceZone == null)
        {
            var go = new GameObject("입구 감지 상자 (여기 들어오면 시작)");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 3, 80);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;           // 부딪히지 않음 (감지만)
            box.size = new Vector3(26, 8, 6);
            entranceZone = box;
        }
        if (walkTo == null)
        {
            walkTo = new GameObject("걸어갈 자리").transform;
            walkTo.SetParent(root, false);
            walkTo.localPosition = new Vector3(0, 0, 87);
        }
        if (lookAt == null)
        {
            lookAt = new GameObject("올려다볼 곳").transform;
            lookAt.SetParent(root, false);
            lookAt.localPosition = new Vector3(GilchadaForest.TREE.x, 30, GilchadaForest.TREE.z);
        }
    }

    void OnDrawGizmos()
    {
        if (walkTo) { Gizmos.color = new Color(1, .85f, .4f); Gizmos.DrawWireSphere(walkTo.position, .5f); }
        if (lookAt) { Gizmos.color = new Color(.6f, 1, .8f); Gizmos.DrawWireSphere(lookAt.position, 1.5f); }
        if (walkTo && lookAt) Gizmos.DrawLine(walkTo.position + Vector3.up * 1.7f, lookAt.position);
        if (entranceZone) { Gizmos.color = new Color(1, .85f, .4f, .25f); Gizmos.matrix = entranceZone.transform.localToWorldMatrix; Gizmos.DrawCube(entranceZone.center, entranceZone.size); }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GilchadaWalk))]
class GilchadaWalkInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var w = (GilchadaWalk)target;
        EditorGUILayout.Space(6);
        if (GUILayout.Button("세계수 입구 연출 자리 만들기", GUILayout.Height(28)))
        {
            Undo.RecordObject(w, "입구 연출");
            w.CreateIntroPoints();
            EditorUtility.SetDirty(w);
        }
        EditorGUILayout.HelpBox("만들어진 '세계수 입구 연출' 안의 노란 상자·점을 씬에서 옮기면 연출 위치가 바뀌어요.", MessageType.Info);
    }
}
#endif
