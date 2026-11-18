using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Byeolmalsseum
{
    /// <summary>
    /// 단어 공간 하나의 총괄.
    /// 흐름: 글자 원형이 카메라 앞에 등장 → (3~5초) → 모든 자모(초·중·종성)가 한꺼번에 공간 자리로 날아감
    ///       → 카메라가 경로를 반복 이동 + 마우스로 시선 → [체험 종료] 클릭 → 종료 신호
    /// ※ 이 스크립트는 자모의 '껍데기' Transform을 읽기만 하고 절대 수정하지 않음.
    /// </summary>
    public class WordSpace : MonoBehaviour
    {
        public enum Phase { 준비, 글자, 날아가는중, 체험, 종료 }

        [Header("단어")]
        public string word = "길차다";

        [Header("글자 획 굵기 (바꾸면 모든 자모에 바로 적용)")]
        [Range(.02f, .2f)] public float strokeThickness = .07f;
        [Range(.02f, .6f)] public float strokeDepth = .2f;

        [Header("시작 연출")]
        [Tooltip("글자가 나타나는 데 걸리는 시간")] public float glyphAppearSeconds = .8f;
        [Tooltip("글자가 보여지는 시간 (3~5초)")][Range(1f, 10f)] public float glyphHoldSeconds = 4f;
        [Tooltip("공간 자리까지 날아가는 시간")] public float flySeconds = 3f;
        [Tooltip("날아갈 때 바깥으로 퍼지는 정도")] public float scatterAmount = 2f;
        [Tooltip("날아갈 때 빛 꼬리")] public bool flyTrails = true;
        [Tooltip("카메라에서 글자까지 거리 (m)")] public float glyphDistance = 6f;
        [Tooltip("글자가 화면 가로를 차지하는 비율")][Range(.3f, .95f)] public float glyphScreenWidth = .65f;

        [Header("테스트용")]
        [Tooltip("켜면 시작 연출 없이 바로 체험 단계부터 (작업 확인용)")] public bool skipIntro = false;

        [Header("체험 종료")]
        public string endButtonText = "체험 종료";
        [Tooltip("체험 단계 시작 후 버튼이 나타나기까지 (초)")] public float endButtonDelay = 0f;
        [Tooltip("체험 종료 후 바로 열 씬 이름 (비우면 신호만 보내고 그대로 있음). Build Settings에 등록된 씬이어야 함")]
        public string nextSceneName = "";
        [Tooltip("체험 종료를 누르면 호출 (다른 씬 전환 등은 여기에 연결)")]
        public UnityEvent onExperienceFinished = new UnityEvent();
        /// <summary>마지막으로 체험을 끝낸 단어 (다음 씬에서 읽기용, 예: 영수증)</summary>
        public static string LastFinishedWord { get; private set; }
        /// <summary>코드에서 받는 종료 신호. 인자 = 단어</summary>
        public static event Action<string> ExperienceFinished;

        public enum BoundsMode { 자동, 직접지정, 제한없음 }

        [Header("이동 범위")]
        [Tooltip("자동: 공간에 놓인 물체 전체 크기 + 여유 (공간을 넓히면 범위도 같이 넓어짐)\n직접지정: 아래 중심·크기 값\n제한없음: 어디든 갈 수 있음")]
        public BoundsMode boundsMode = BoundsMode.자동;
        [Tooltip("자동 범위: 가장 바깥 물체보다 얼마나 더 나갈 수 있는지 (m)")]
        public float boundsPadding = 5f;
        [Tooltip("자동 범위: 카메라가 이 높이 아래로는 못 내려감 (땅 뚫기 방지). 땅 아래 공간을 만들었다면 낮추세요")]
        public float floorHeight = 0f;
        public Vector3 manualBoundsCenter = new Vector3(0, 5, 0);
        public Vector3 manualBoundsSize = new Vector3(40, 15, 40);

        [Header("연결 (자동 생성됨)")]
        public Transform strokesRoot;
        public ExperienceCamera experienceCamera;
        public Camera workCamera;

        public Phase CurrentPhase { get; private set; } = Phase.준비;

        // 런타임 전용 데이터 (껍데기는 건드리지 않음)
        class Flight
        {
            public JamoPiece piece;
            public Transform visual;
            public Vector3 glyphPos, ctrlPos;
            public Quaternion glyphRot;
            public Vector3 glyphScale;
            public TrailRenderer trail;
        }
        readonly List<Flight> flights = new List<Flight>();
        Transform flightRoot;
        GameObject endButton, pausePanel, endLockedMsg;
        Text hintText;
        Font uiFont;
        bool paused;
        float exploreStartTime;

        public JamoPiece[] Pieces => strokesRoot ? strokesRoot.GetComponentsInChildren<JamoPiece>(true) : new JamoPiece[0];

        // ───────────────────────────── 에디터: 굵기 값 바뀌면 메시 다시 만들기
#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                foreach (var p in Pieces) if (p) p.RebuildMesh();
            };
        }
#endif

        // ───────────────────────────── 시작
        void Start()
        {
            uiFont = Resources.Load<Font>("Fonts/Pretendard-SemiBold");
            if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 32);

            SetWorkCameraActive(false);
            if (experienceCamera) experienceCamera.PlaceAtStart();

            BuildUI();
            PrepareFlights();
            StartCoroutine(Run());
        }

        void PrepareFlights()
        {
            flightRoot = new GameObject("[실행 중] 날아가는 자모").transform;
            var pieces = Pieces;
            if (pieces.Length == 0) return;

            Camera cam = experienceCamera ? experienceCamera.Cam : Camera.main;
            Transform ct = cam.transform;

            // 글자 크기: 화면 가로의 glyphScreenWidth 만큼
            var allPts = new List<Vector2[]>();
            foreach (var p in pieces) if (p.points != null) allPts.Add(p.points);
            Rect b = WordGlyph.Bounds(allPts);
            float visH = 2f * glyphDistance * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            float visW = visH * cam.aspect;
            float s = Mathf.Min(visW * glyphScreenWidth / Mathf.Max(b.width, .01f), visH * .6f / Mathf.Max(b.height, .01f));

            Vector3 origin = ct.position + ct.forward * glyphDistance;
            Quaternion rot = ct.rotation;

            foreach (var p in pieces)
            {
                if (p.visual == null || p.points == null) continue;
                Vector2 local = p.pivot - b.center;
                var f = new Flight
                {
                    piece = p,
                    visual = p.visual,
                    glyphPos = origin + rot * new Vector3(local.x * s, local.y * s, 0),
                    glyphRot = rot,
                    glyphScale = Vector3.one * s,
                };
                // 날아갈 때 곡선 경로의 중간점: 글자 중심에서 바깥쪽 + 약간의 무작위
                Vector3 target = p.transform.position;
                Vector3 outward = (f.glyphPos - origin).normalized;
                if (outward.sqrMagnitude < .01f) outward = UnityEngine.Random.onUnitSphere;
                float d = Vector3.Distance(f.glyphPos, target);
                f.ctrlPos = Vector3.Lerp(f.glyphPos, target, .35f)
                          + outward * scatterAmount
                          + UnityEngine.Random.insideUnitSphere * scatterAmount * .5f
                          + Vector3.up * Mathf.Min(d * .15f, 3f);

                // 알맹이만 런타임 루트로 옮겨서 자유롭게 움직임 (껍데기는 그대로)
                f.visual.SetParent(flightRoot, true);
                f.visual.SetPositionAndRotation(f.glyphPos, f.glyphRot);
                f.visual.localScale = Vector3.zero;
                flights.Add(f);
            }
        }

        IEnumerator Run()
        {
            if (skipIntro)
            {
                foreach (var f in flights) Land(f);
                BeginExplore();
                yield break;
            }

            // ① 글자 등장
            CurrentPhase = Phase.글자;
            for (float t = 0; t < glyphAppearSeconds; t += Time.deltaTime)
            {
                float e = EaseOutBack(Mathf.Clamp01(t / glyphAppearSeconds));
                foreach (var f in flights) f.visual.localScale = f.glyphScale * e;
                yield return null;
            }
            foreach (var f in flights) f.visual.localScale = f.glyphScale;

            // ② 글자 보여주기
            yield return new WaitForSeconds(glyphHoldSeconds);

            // ③ 모든 자모가 한꺼번에 날아감
            CurrentPhase = Phase.날아가는중;
            if (flyTrails) foreach (var f in flights) f.trail = AddTrail(f);
            for (float t = 0; t < flySeconds; t += Time.deltaTime)
            {
                float e = EaseInOutCubic(Mathf.Clamp01(t / flySeconds));
                foreach (var f in flights)
                {
                    Transform home = f.piece.transform;
                    Vector3 a = f.glyphPos, c = f.ctrlPos, b = home.position;
                    Vector3 pos = (1 - e) * (1 - e) * a + 2 * (1 - e) * e * c + e * e * b; // 2차 베지어
                    f.visual.SetPositionAndRotation(pos, Quaternion.Slerp(f.glyphRot, home.rotation, e));
                    f.visual.localScale = Vector3.Lerp(f.glyphScale, home.lossyScale, e);
                }
                yield return null;
            }
            foreach (var f in flights) Land(f);

            BeginExplore();
        }

        // 도착: 알맹이를 다시 껍데기 밑으로 정확히 붙임
        void Land(Flight f)
        {
            f.visual.SetParent(f.piece.transform, false);
            f.visual.localPosition = Vector3.zero;
            f.visual.localRotation = Quaternion.identity;
            f.visual.localScale = Vector3.one;
            if (f.trail)
            {
                f.trail.transform.SetParent(null, true); // 꼬리는 자연스럽게 사라지게 분리
                f.trail.emitting = false;
                Destroy(f.trail.gameObject, f.trail.time + .1f);
            }
        }

        void BeginExplore()
        {
            CurrentPhase = Phase.체험;
            exploreStartTime = Time.time;
            if (experienceCamera)
            {
                experienceCamera.Limit = ComputeMoveBounds();
                experienceCamera.MouseLookEnabled = true;
                experienceCamera.MovementEnabled = true;
            }
            if (!WorkCameraActive) SetPaused(false);
            StartCoroutine(FadeHint());
        }

        // ───────────────────────────── 이동 범위
        /// <summary>체험 카메라가 움직일 수 있는 범위. null = 제한 없음</summary>
        public Bounds? ComputeMoveBounds()
        {
            if (boundsMode == BoundsMode.제한없음) return null;
            if (boundsMode == BoundsMode.직접지정) return new Bounds(manualBoundsCenter, manualBoundsSize);

            bool any = false;
            Bounds raw = new Bounds();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is TrailRenderer || r is ParticleSystemRenderer) continue;
                if (!any) { raw = r.bounds; any = true; } else raw.Encapsulate(r.bounds);
            }
            if (experienceCamera)
            {
                Vector3 sp = experienceCamera.startPoint ? experienceCamera.startPoint.position : experienceCamera.transform.position;
                if (!any) { raw = new Bounds(sp, Vector3.zero); any = true; } else raw.Encapsulate(sp);
            }
            if (!any) return null;

            float p = Mathf.Max(0, boundsPadding);
            Vector3 min = raw.min - new Vector3(p, 0, p);
            min.y = Mathf.Max(raw.min.y + .3f, floorHeight + .5f); // 바닥(floorHeight) 아래로는 못 내려감 (눈높이 여유 0.5m)
            Vector3 max = raw.max + new Vector3(p, p, p);
            var b = new Bounds();
            b.SetMinMax(min, Vector3.Max(max, min + Vector3.one * .1f));
            return b;
        }

        void OnDrawGizmosSelected()
        {
            var b = ComputeMoveBounds();
            if (!b.HasValue) return;
            Gizmos.color = new Color(.3f, 1f, 1f, .8f);
            Gizmos.DrawWireCube(b.Value.center, b.Value.size);
        }

        TrailRenderer AddTrail(Flight f)
        {
            var go = new GameObject("빛 꼬리");
            go.transform.SetParent(f.visual, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = .7f;
            tr.minVertexDistance = .05f;
            float w = Mathf.Max(f.glyphScale.x * strokeThickness, .03f);
            tr.widthCurve = new AnimationCurve(new Keyframe(0, w), new Keyframe(1, 0));
            Color c = Color.white;
            var r = f.visual.GetComponent<Renderer>();
            if (r && r.sharedMaterial)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor")) c = r.sharedMaterial.GetColor("_BaseColor");
                else if (r.sharedMaterial.HasProperty("_Color")) c = r.sharedMaterial.color;
            }
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, .5f), 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tr;
        }

        // ───────────────────────────── 체험 종료
        public void FinishExperience()
        {
            if (CurrentPhase == Phase.종료) return;
            CurrentPhase = Phase.종료;
            if (pausePanel) pausePanel.SetActive(false);
            if (hintText) hintText.gameObject.SetActive(false);
            ReleaseCursor();
            if (experienceCamera) { experienceCamera.MovementEnabled = false; experienceCamera.MouseLookEnabled = false; }
            Debug.Log($"[별말씀] 체험 종료: {word}");
            LastFinishedWord = word;
            onExperienceFinished?.Invoke();
            ExperienceFinished?.Invoke(word);
            if (!string.IsNullOrEmpty(nextSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }

        // ───────────────────────────── ESC 메뉴 (일시정지)
        /// <summary>true = 커서 보이고 메뉴 열림(체험 종료 버튼) / false = 커서 숨김, 이동·시선 조작</summary>
        public void SetPaused(bool p)
        {
            paused = p;
            if (experienceCamera) experienceCamera.Paused = p;
            if (p) ReleaseCursor();
            else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (pausePanel)
            {
                pausePanel.SetActive(p);
                bool canEnd = Time.time - exploreStartTime >= endButtonDelay;
                if (endButton) endButton.SetActive(canEnd);
                if (endLockedMsg) endLockedMsg.SetActive(!canEnd);
            }
        }

        static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy() => ReleaseCursor();

        IEnumerator FadeHint()
        {
            if (!hintText) yield break;
            hintText.gameObject.SetActive(true);
            Color c = hintText.color;
            for (float t = 0; t < 8f; t += Time.deltaTime)
            {
                float a = t < 1f ? t : (t > 6f ? Mathf.Clamp01((8f - t) / 2f) : 1f);
                hintText.color = new Color(c.r, c.g, c.b, a * .85f);
                yield return null;
            }
            hintText.gameObject.SetActive(false);
        }

        void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var canvasGo = new GameObject("[실행 중] 체험 UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            // 조작 안내 (체험 시작 후 잠깐 보였다 사라짐)
            hintText = MakeText(canvasGo.transform, "W A S D · 방향키  이동     마우스  둘러보기     ESC  메뉴", 24,
                                new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(1200, 50));
            hintText.color = new Color(1, 1, 1, 0);
            hintText.gameObject.SetActive(false);

            // ESC 메뉴: 화면 전체를 덮는 반투명 판 (아무 데나 클릭하면 계속 둘러보기)
            pausePanel = new GameObject("ESC 메뉴", typeof(RectTransform), typeof(Image), typeof(Button));
            pausePanel.transform.SetParent(canvasGo.transform, false);
            var prt = pausePanel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            pausePanel.GetComponent<Image>().color = new Color(.02f, .03f, .08f, .55f);
            var resume = pausePanel.GetComponent<Button>();
            resume.transition = Selectable.Transition.None;
            resume.onClick.AddListener(() => SetPaused(false));

            MakeText(pausePanel.transform, "잠시 멈춤", 44, new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(800, 70));
            MakeText(pausePanel.transform, "계속 둘러보려면 화면을 클릭하거나 ESC를 누르세요", 24, new Vector2(.5f, .5f), new Vector2(0, 70), new Vector2(1200, 50))
                .color = new Color(1, 1, 1, .75f);
            endLockedMsg = MakeText(pausePanel.transform, "조금 더 둘러본 뒤에 체험을 끝낼 수 있어요", 22, new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(1000, 50)).gameObject;

            endButton = new GameObject("체험 종료 버튼", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            endButton.transform.SetParent(pausePanel.transform, false);
            var rt = endButton.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(0, -30);
            rt.sizeDelta = new Vector2(260, 76);
            endButton.GetComponent<Image>().color = new Color(.08f, .09f, .16f, .85f);
            var outline = endButton.GetComponent<Outline>();
            outline.effectColor = new Color(1, 1, 1, .6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var btn = endButton.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(.75f, .85f, 1f);
            cb.pressedColor = new Color(.6f, .7f, .9f);
            btn.colors = cb;
            btn.onClick.AddListener(FinishExperience);
            MakeText(endButton.transform, endButtonText, 28, new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 76));

            pausePanel.SetActive(false);
        }

        Text MakeText(Transform parent, string s, int size, Vector2 anchor, Vector2 pos, Vector2 boxSize)
        {
            var go = new GameObject("글자", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = boxSize;
            var t = go.GetComponent<Text>();
            t.font = uiFont; t.text = s; t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        // ───────────────────────────── 작업용 카메라 전환 (플레이 중 Tab, 에디터 전용)
        public bool WorkCameraActive => workCamera && workCamera.enabled;

        public void SetWorkCameraActive(bool on)
        {
            if (!workCamera || !experienceCamera) return;
            var ec = experienceCamera.Cam;
            if (on && !workCamera.enabled)
                workCamera.transform.SetPositionAndRotation(ec.transform.position, ec.transform.rotation); // 지금 보던 곳에서 시작
            workCamera.enabled = on;
            ec.enabled = !on;
            var wl = workCamera.GetComponent<AudioListener>();
            var el = experienceCamera.GetComponent<AudioListener>();
            if (wl) wl.enabled = on;
            if (el) el.enabled = !on;

            if (!Application.isPlaying) return;
            if (on)
            {
                experienceCamera.Paused = true;
                if (pausePanel) pausePanel.SetActive(false);
                ReleaseCursor();
            }
            else if (CurrentPhase == Phase.체험) SetPaused(false);
        }

        void Update()
        {
            // 작업용 카메라 전환은 에디터에서만 (전시용 빌드에서는 관람객이 Tab을 눌러도 무시)
            if (Application.isEditor && BmInput.Down(BmKey.Tab)) SetWorkCameraActive(!WorkCameraActive);

            if (CurrentPhase != Phase.체험 || WorkCameraActive) return;
            if (BmInput.Down(BmKey.Esc)) SetPaused(!paused);
            // 에디터에서 ESC로 커서가 풀리거나 창 밖을 눌러 잠금이 풀리면 → 메뉴 열기
            else if (!paused && Cursor.lockState != CursorLockMode.Locked) SetPaused(true);
        }

        GUIStyle hintStyle;
        void OnGUI()
        {
            if (!Application.isEditor) return; // 전시용 빌드에서는 안 보임
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 16 };
                hintStyle.normal.textColor = new Color(1, 1, 1, .8f);
            }
            string msg = WorkCameraActive
                ? "작업용 카메라 · 우클릭 드래그 둘러보기 · WASD 이동 · Q/E 아래/위 · Shift 빠르게 · Tab 체험 카메라로"
                : $"[{CurrentPhase}]  Tab: 작업용 카메라";
            GUI.Label(new Rect(12, 8, 1400, 28), msg, hintStyle);
        }

        // ───────────────────────────── 이징
        static float EaseInOutCubic(float x) => x < .5f ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;
        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }
    }
}
