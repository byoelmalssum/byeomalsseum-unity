using System.Collections.Generic;
using UnityEngine;

// [살천스럽다] 눈사람들이 '안 볼 때만' 몸을 돌려 체험자를 바라봄
//  - 화면에 보이는 동안엔 꼼짝 안 함. 고개를 돌렸다 다시 보면 어느새 이쪽을 보고 있음.
//  - creep을 켜면 안 볼 때 아주 조금씩 다가옴 (최대 maxCreep m까지)
//  - SalcheonSpace 버튼이 자동으로 '눈 소품' 그룹에 붙이고, 눈사람들을 채워 줌
public class SalcheonWatchers : MonoBehaviour
{
    public List<Transform> watchers = new List<Transform>();
    [Tooltip("이 거리(m) 안에 있는 눈사람만 돌아봄")] public float range = 40f;
    [Tooltip("안 볼 때 도는 빠르기 (도/초)")] public float turnSpeed = 120f;

    [Header("다가오기")]
    public bool creep = true;
    [Tooltip("안 볼 때 다가오는 빠르기 (m/초)")] public float creepSpeed = 0.35f;
    [Tooltip("처음 자리에서 최대 몇 m까지")] public float maxCreep = 3f;
    [Tooltip("체험자와 이 거리(m)보다는 가까이 안 옴")] public float keepAway = 4f;

    readonly List<Vector3> home = new List<Vector3>();

    void Start()
    {
        foreach (var w in watchers) home.Add(w ? w.position : Vector3.zero);
    }

    void Update()
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 cp = cam.transform.position;
        float dt = Time.deltaTime;
        for (int i = 0; i < watchers.Count; i++)
        {
            var w = watchers[i];
            if (!w) continue;
            Vector3 to = cp - w.position; to.y = 0;
            float dist = to.magnitude;
            if (dist > range || dist < 0.01f) continue;
            if (Seen(cam, w.position + Vector3.up * 1f)) continue;

            Quaternion want = Quaternion.LookRotation(to / dist, Vector3.up);
            Vector3 e = w.rotation.eulerAngles;
            Quaternion keepTilt = Quaternion.Euler(e.x, 0, e.z);
            w.rotation = Quaternion.RotateTowards(Quaternion.Euler(0, e.y, 0), want, turnSpeed * dt) * keepTilt;

            if (creep && dist > keepAway)
            {
                Vector3 next = w.position + to / dist * creepSpeed * dt;
                Vector3 off = next - home[i]; off.y = 0;
                if (off.magnitude <= maxCreep) w.position = next;
            }
        }
    }

    static bool Seen(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0 && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }
}
