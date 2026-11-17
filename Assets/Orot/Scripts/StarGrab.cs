using System.Collections.Generic;
using UnityEngine;

// 별 잡기 v2
// 손바닥 커서로 별을 가리키고 → 주먹을 꾹 쥐고 있으면 별이 서서히 커지며 앞으로 온다
// 끝까지 쥐고 있으면 '선택 완료', 언제든 손을 펴면 부드럽게 원래대로 돌아간다
public class StarGrab : MonoBehaviour
{
    [Header("꼭 연결하기")]
    public Camera cam;
    public RectTransform cursor;

    [Header("기본")]
    public bool mirrorX = false;
    [Tooltip("켜면 StarWord가 없는 오브젝트도 이 태그가 있으면 별로 취급 (태그를 Project Settings에 만들어야 함)")]
    public bool useTag = false;
    public string starTag = "Star";

    [Header("가리키기")]
    public float hoverScale = 1.3f;      // 가리키면 이만큼 커짐
    public float hoverSmooth = 0.15f;    // 커지고 작아지는 데 걸리는 시간 (클수록 느긋)
    public float hoverGrace = 0.25f;     // 커서가 잠깐 벗어나도 봐주는 시간

    [Header("주먹 꾹 쥐기")]
    public float holdTime = 1.5f;        // 이만큼 쥐고 있으면 선택 완료
    public float returnTime = 0.25f;     // 손을 펴면 이 시간 안에 원래대로 (작을수록 빠름)
    public float growScale = 2.5f;       // 다 커졌을 때 배율
    public float frontDistance = 4f;     // 다 왔을 때 카메라 앞 거리 (다른 별보다 뒤면 자동으로 더 앞으로)
    [Range(0, 1)] public float moveForward = 1f; // 0이면 제자리에서 커지기만 함

    [Header("나머지 별 물러나기")]
    public float pushBack = 3f;          // 한 별을 잡으면 나머지 별이 이만큼 뒤로 물러남
    public float othersShrink = 0.8f;    // 나머지 별 크기 배율
    public int fistFrames = 4;           // 주먹을 이만큼 연속으로 쥐어야 인정
    public int openFrames = 2;           // 손을 이만큼 연속으로 펴면 바로 놓기

    [Header("안정성")]
    public float handLostGrace = 0.3f;   // 손이 잠깐 안 잡혀도 봐주는 시간
    public float minCutoff = 1.0f;       // 작을수록 커서가 부드러움
    public float beta = 3f;              // 클수록 빠른 움직임을 잘 따라옴

    // ── 플러그인에서 손 데이터 받기 (다른 스레드) ──
    static readonly object lockObj = new object();
    static Vector3[] latestHand;
    static bool hasNewHand;

    public static void SetHand(Vector3[] points)
    {
        lock (lockObj) { latestHand = points; hasNewHand = true; }
    }

    class StarState
    {
        public Vector3 home, baseScale, front;
        public float hover, hoverVel, progress;
    }

    readonly Dictionary<Transform, StarState> stars = new Dictionary<Transform, StarState>();

    Vector3[] hand;
    float lastHandTime = -10f;
    Vector2 cursorPos;
    float cursorScaleVel, cursorScale = 1f;
    readonly OneEuro fx = new OneEuro(), fy = new OneEuro();

    bool isFist, fistDown;
    int fistCount, openCount;

    Transform hovered, held, selected;
    bool loading;
    float lastSeenTime;

    RectTransform cursorParent;
    Canvas canvas;

    void Start()
    {
        // Cam 칸이 비어 있으면 씬의 Main Camera를 자동으로 사용 (프리팹용)
        if (cam == null) cam = Camera.main;

        // 별 = StarWord가 붙은 오브젝트 (태그 설정 없이 동작)
        foreach (var sw in FindObjectsByType<StarWord>(FindObjectsSortMode.None)) Register(sw.transform);
        if (useTag)
        {
            try { foreach (var go in GameObject.FindGameObjectsWithTag(starTag)) Register(go.transform); }
            catch (UnityException) { Debug.LogWarning($"'{starTag}' 태그가 없어요. useTag를 끄거나 태그를 만드세요."); }
        }

        if (cursor)
        {
            // 커서만 따로 맨 위 캔버스로 옮겨서 별보다 항상 위에 보이게 하기
            var go = new GameObject("CursorCanvas", typeof(Canvas));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            cursor.SetParent(go.transform, false);
            cursorParent = go.transform as RectTransform;
        }
    }

    void Update()
    {
        // 1) 새 손 데이터 받기
        Vector3[] incoming = null;
        bool fresh = false;
        lock (lockObj)
        {
            if (hasNewHand) { incoming = latestHand; hasNewHand = false; fresh = true; }
        }
        if (fresh && incoming != null && incoming.Length >= 21)
        {
            hand = incoming;
            lastHandTime = Time.time;
            UpdateCursor();
            UpdateFist();
        }

        bool handVisible = hand != null && Time.time - lastHandTime < handLostGrace;
        if (!handVisible) { isFist = false; fistDown = false; fistCount = openCount = 0; }

        // 2) 어떤 별을 가리키고 / 잡고 있는지 정하기
        if (selected != null)
        {
            if (!isFist && !loading) selected = null;    // 선택 완료 후에도 손을 펴면 바로 놓기 (씬 이동 중에는 유지)
        }
        else
        {
            if (held == null)
            {
                Transform t = handVisible ? RaycastStar() : null;
                if (t != null) { hovered = t; lastSeenTime = Time.time; }
                else if (Time.time - lastSeenTime > hoverGrace) hovered = null;

                // 별을 가리킨 상태에서 주먹을 '새로' 쥐었을 때만 잡기 시작
                if (fistDown && hovered != null)
                {
                    held = hovered;
                    stars[held].front = ComputeFront(held);
                }
            }
            else if (!isFist)
            {
                held = null;             // 중간에 손을 펴면 놓기
            }
        }
        fistDown = false;

        // 3) 진행도 계산
        Transform leader = null;
        float leaderE = 0f;
        foreach (var kv in stars)
        {
            Transform tr = kv.Key;
            if (tr == null) continue;
            StarState s = kv.Value;

            float hoverTarget = (tr == hovered || tr == held || tr == selected) ? 1f : 0f;
            s.hover = Mathf.SmoothDamp(s.hover, hoverTarget, ref s.hoverVel, hoverSmooth);

            if (tr == selected) s.progress = 1f;
            else if (tr == held) s.progress = Mathf.MoveTowards(s.progress, 1f, Time.deltaTime / holdTime);
            else s.progress = Mathf.MoveTowards(s.progress, 0f, Time.deltaTime / returnTime);

            float e = Mathf.SmoothStep(0f, 1f, s.progress);   // 처음과 끝이 부드러운 곡선
            if (e > leaderE) { leaderE = e; leader = tr; }
        }

        // 4) 별 움직이기: 잡은 별은 앞으로, 나머지는 뒤로 물러나며 살짝 작아짐
        Vector3 camPos = cam.transform.position;
        foreach (var kv in stars)
        {
            Transform tr = kv.Key;
            if (tr == null) continue;
            StarState s = kv.Value;
            float hoverMul = Mathf.Lerp(1f, hoverScale, s.hover);

            if (tr == leader)
            {
                tr.localScale = s.baseScale * hoverMul * Mathf.Lerp(1f, growScale, leaderE);
                tr.position = Vector3.Lerp(s.home, s.front, leaderE * moveForward);
            }
            else
            {
                Vector3 away = (s.home - camPos).normalized;
                tr.localScale = s.baseScale * hoverMul * Mathf.Lerp(1f, othersShrink, leaderE);
                tr.position = s.home + away * pushBack * leaderE;
            }
        }

        // 5) 끝까지 쥐고 있었으면 선택 완료
        if (held != null && stars[held].progress >= 1f)
        {
            selected = held;
            held = null;
            Debug.Log(selected.name + " 선택 완료!");

            // 별에 StarWord가 붙어 있으면 그쪽에 알려서 단어 공간으로 넘어감 (이후 손을 펴도 놓지 않음)
            if (selected.GetComponent("StarWord") != null)
            {
                loading = true;
                selected.gameObject.SendMessage("OnStarSelected", SendMessageOptions.DontRequireReceiver);
            }
        }

        // 6) 커서 표시 (주먹을 쥐고 있으면 살짝 작아져서 '꾹' 누르는 느낌)
        if (cursor)
        {
            cursor.gameObject.SetActive(handVisible);
            float target = isFist ? 0.7f : 1f;
            cursorScale = Mathf.SmoothDamp(cursorScale, target, ref cursorScaleVel, 0.1f);
            cursor.localScale = Vector3.one * cursorScale;
        }
    }

    // 잡은 별이 갈 자리: 다른 별들보다 항상 카메라에 더 가깝게
    Vector3 ComputeFront(Transform t)
    {
        Vector3 camPos = cam.transform.position, fwd = cam.transform.forward;
        StarState me = stars[t];
        float myRadius = me.baseScale.x * hoverScale * growScale * 0.5f;

        float nearest = float.MaxValue;
        foreach (var kv in stars)
        {
            if (kv.Key == null || kv.Key == t) continue;
            float depth = Vector3.Dot(kv.Value.home - camPos, fwd) - kv.Value.baseScale.x * 0.5f;
            nearest = Mathf.Min(nearest, depth);
        }

        float dist = Mathf.Min(frontDistance, nearest - myRadius - 0.3f);
        dist = Mathf.Max(dist, cam.nearClipPlane + myRadius + 0.2f);
        return camPos + fwd * dist;
    }

    Transform RaycastStar()
    {
        if (Physics.Raycast(cam.ScreenPointToRay(cursorPos), out RaycastHit hit, 1000f)
            && IsStar(hit.transform))
        {
            Register(hit.transform);
            return hit.transform;
        }
        return null;
    }

    bool IsStar(Transform t)
    {
        if (t.GetComponent<StarWord>() != null) return true;
        return useTag && t.CompareTag(starTag);
    }

    void Register(Transform t)
    {
        if (stars.ContainsKey(t)) return;
        stars[t] = new StarState { home = t.position, baseScale = t.localScale };
    }

    void UpdateCursor()
    {
        // 손바닥 중앙 = 손목(0)과 손가락 뿌리 4개(5, 9, 13, 17)의 평균
        Vector3 palm = (hand[0] + hand[5] + hand[9] + hand[13] + hand[17]) / 5f;
        float x = palm.x;
        if (mirrorX) x = 1f - x;
        float y = 1f - palm.y;

        float t = Time.time;
        fx.minCutoff = fy.minCutoff = minCutoff;
        fx.beta = fy.beta = beta;
        cursorPos = new Vector2(fx.Filter(x, t) * Screen.width, fy.Filter(y, t) * Screen.height);

        // 캔버스 종류와 상관없이 화면 좌표에 커서를 놓기
        if (cursor && cursorParent)
        {
            Camera uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(cursorParent, cursorPos, uiCam, out Vector3 world))
                cursor.position = world;
        }
    }

    // 손가락 끝이 둘째 마디보다 손목에 더 가까우면 접힌 것
    void UpdateFist()
    {
        int[] tips = { 8, 12, 16, 20 };
        int[] pips = { 6, 10, 14, 18 };
        int folded = 0;
        for (int i = 0; i < 4; i++)
            if (Dist(tips[i], 0) < Dist(pips[i], 0)) folded++;

        bool fistNow = folded >= 3;
        if (fistNow) { fistCount++; openCount = 0; }
        else { openCount++; fistCount = 0; }

        if (!isFist && fistCount >= fistFrames) { isFist = true; fistDown = true; }
        if (isFist && openCount >= openFrames) isFist = false;
    }

    float Dist(int a, int b) => Vector2.Distance(hand[a], hand[b]);

    // 떨림 보정 필터 (1€ 필터)
    class OneEuro
    {
        public float minCutoff = 1f, beta = 3f, dCutoff = 1f;
        float xPrev, dxPrev, tPrev = -1f;

        public float Filter(float x, float t)
        {
            if (tPrev < 0f) { tPrev = t; xPrev = x; dxPrev = 0f; return x; }
            float dt = t - tPrev;
            if (dt <= 0f) return xPrev;
            tPrev = t;

            float dx = (x - xPrev) / dt;
            dxPrev = Mathf.Lerp(dxPrev, dx, Alpha(dt, dCutoff));
            float cutoff = minCutoff + beta * Mathf.Abs(dxPrev);
            xPrev = Mathf.Lerp(xPrev, x, Alpha(dt, cutoff));
            return xPrev;
        }

        static float Alpha(float dt, float cutoff)
        {
            float r = 2f * Mathf.PI * cutoff * dt;
            return r / (r + 1f);
        }
    }
}
