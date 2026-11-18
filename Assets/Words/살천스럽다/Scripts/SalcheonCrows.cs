using System.Collections.Generic;
using UnityEngine;

// [살천스럽다] 까마귀: 가만히 앉아 체험자 쪽으로 고개를 돌리고 있다가, 가까이 가면 한꺼번에 날아오름
//  - 이 오브젝트 아래 자식(까마귀)들을 전부 관리
//  - SalcheonSpace 버튼이 자동으로 붙여 줌
public class SalcheonCrows : MonoBehaviour
{
    [Tooltip("이 거리(m) 안으로 오면 날아감")] public float scareDistance = 9f;
    [Tooltip("이 거리(m) 안이면 체험자를 쳐다봄")] public float watchDistance = 35f;
    public float flySpeed = 9f;
    public AudioSource caw; // 까악 소리 (없어도 됨)

    class Crow { public Transform t; public bool flying; public Vector3 vel; public float flap; public Vector3 baseScale; }
    readonly List<Crow> crows = new List<Crow>();

    void Start()
    {
        foreach (Transform c in transform) crows.Add(new Crow { t = c, baseScale = c.localScale });
    }

    void Update()
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 cp = cam.transform.position;
        float dt = Time.deltaTime;
        foreach (var c in crows)
        {
            if (!c.t || !c.t.gameObject.activeSelf) continue;
            Vector3 to = cp - c.t.position;
            if (!c.flying)
            {
                float d = to.magnitude;
                if (d < watchDistance)
                {
                    Vector3 flat = new Vector3(to.x, 0, to.z);
                    if (flat.sqrMagnitude > 0.01f)
                        c.t.rotation = Quaternion.Slerp(c.t.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), 1f - Mathf.Exp(-3f * dt));
                }
                if (d < scareDistance) ScareAll(cp);
                continue;
            }
            c.vel += Vector3.up * 2f * dt;
            c.t.position += c.vel * dt;
            if (c.vel.sqrMagnitude > 0.01f) c.t.rotation = Quaternion.LookRotation(c.vel.normalized, Vector3.up);
            c.flap += dt * 18f;
            c.t.localScale = Vector3.Scale(c.baseScale, new Vector3(1f + 0.5f * Mathf.Abs(Mathf.Sin(c.flap)), 1f, 1f));
            if ((c.t.position - cp).magnitude > 120f) c.t.gameObject.SetActive(false);
        }
    }

    void ScareAll(Vector3 from)
    {
        bool any = false;
        foreach (var c in crows)
        {
            if (!c.t || c.flying) continue;
            if ((c.t.position - from).magnitude > scareDistance * 2.5f) continue; // 근처 무리만
            Vector3 away = c.t.position - from; away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.onUnitSphere;
            c.vel = (away + Vector3.up * Random.Range(0.6f, 1.2f) + Random.insideUnitSphere * 0.3f).normalized * flySpeed * Random.Range(0.8f, 1.2f);
            c.flying = true; c.flap = Random.Range(0f, 6f);
            any = true;
        }
        if (any && caw) caw.Play();
    }
}
