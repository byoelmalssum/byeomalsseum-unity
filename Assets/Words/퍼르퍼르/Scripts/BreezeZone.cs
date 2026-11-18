using System.Collections;
using UnityEngine;

// [퍼르퍼르] 관객이 가까이 오면 '퍼르르' 날아오르는 구역
//  - 빈 오브젝트를 원하는 자리(바닥 높이)에 두고 이 스크립트를 붙인다. Scene 뷰에 하늘색 원으로 보임.
//  - 카메라가 반경 안으로 들어오는 순간: 바닥 꽃잎 더미가 사라지고 + 꽃잎이 한꺼번에 날아오름 + 돌풍 + 소리.
//  - 잠시 뒤 바닥 꽃잎 더미가 다시 쌓여서, 카메라가 경로를 한 바퀴 돌고 와도 또 발동한다.
//  - 지금 화면을 그리고 있는 카메라(체험/작업용 어느 쪽이든)를 기준으로 거리를 잰다.
public class BreezeZone : MonoBehaviour
{
    [Tooltip("이 거리(m) 안으로 들어오면 발동 (가로 거리만 잼)")]
    public float radius = 4f;
    [Tooltip("이 높이(m)보다 카메라가 높으면 발동 안 함")]
    public float maxHeight = 6f;
    [Tooltip("한 번 발동한 뒤 다시 발동하기까지 (초)")]
    public float cooldown = 5f;

    [Header("바닥에 쌓인 꽃잎 (선택)")]
    [Tooltip("바닥에 깔려 있는 꽃잎 파티클. 발동하면 사라졌다가 다시 쌓임")]
    public ParticleSystem pile;
    public int pileCount = 250;
    [Tooltip("발동 후 바닥 꽃잎이 다시 쌓이기까지 (초)")]
    public float regrowSeconds = 8f;

    [Header("날아오를 꽃잎")]
    [Tooltip("Emission Rate 0인 파티클. 여기서 한꺼번에 뿜음")]
    public ParticleSystem burst;
    public int burstCount = 150;

    [Header("돌풍 (공간에 PeureuWind 필요)")]
    public float windBoost = 18f;
    public float boostSeconds = 2.5f;

    [Header("소리 (선택)")]
    public AudioSource sound;

    bool inside;
    float lastTime = -999f;

    void Start()
    {
        FillPile();
    }

    [Tooltip("체크하면 Console에 '발동!' 로그가 찍힘 (테스트용)")]
    public bool debugLog = true;

    void Update()
    {
        // 켜져 있는 카메라 중 하나라도 원 안에 들어오면 발동
        // (자유 이동 플레이어 카메라 / 체험 카메라 / 작업용 카메라 어느 쪽이든)
        bool now = false;
        foreach (var cam in Camera.allCameras)
        {
            if (!cam.isActiveAndEnabled) continue;
            Vector3 d = cam.transform.position - transform.position;
            if ((d.x * d.x + d.z * d.z) < radius * radius && d.y < maxHeight && d.y > -2f) { now = true; break; }
        }

        if (now && !inside && Time.time - lastTime > cooldown) Trigger();
        inside = now;
    }

    [ContextMenu("지금 발동 (테스트)")]
    public void Trigger()
    {
        lastTime = Time.time;
        if (debugLog) Debug.Log($"[퍼르르] {name} 발동! (Pile:{(pile ? pile.name : "없음")}, Burst:{(burst ? burst.name : "없음")})");

        if (pile != null)
        {
            pile.Clear();
            StopAllCoroutines();
            StartCoroutine(Regrow());
        }

        if (burst != null)
        {
            if (!burst.isPlaying) burst.Play();
            burst.Emit(burstCount);
        }

        if (sound != null) sound.Play();

        var wind = PeureuWind.Instance;
        if (wind != null) wind.Boost(windBoost, boostSeconds);
    }

    IEnumerator Regrow()
    {
        yield return new WaitForSeconds(regrowSeconds);
        FillPile();
    }

    void FillPile()
    {
        if (pile == null) return;
        pile.Clear();
        if (!pile.isPlaying) pile.Play();
        pile.Emit(pileCount);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
