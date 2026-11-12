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
        GameObject endButton;
        CanvasGroup endButtonGroup;
        Font uiFont;

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
            if (experienceCamera)
            {
                experienceCamera.MouseLookEnabled = true;
                experienceCamera.BeginMoving();
            }
            StartCoroutine(ShowEndButton());
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
            if (endButton) endButton.SetActive(false);
            Debug.Log($"[별말씀] 체험 종료: {word}");
            LastFinishedWord = word;
            onExperienceFinished?.Invoke();
            ExperienceFinished?.Invoke(word);
            if (!string.IsNullOrEmpty(nextSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }

        IEnumerator ShowEndButton()
        {
            if (endButtonDelay > 0) yield return new WaitForSeconds(endButtonDelay);
            if (!endButton) yield break;
            endButton.SetActive(true);
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                endButtonGroup.alpha = t;
                yield return null;
            }
            endButtonGroup.alpha = 1;
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

            endButton = new GameObject("체험 종료 버튼", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(Outline));
            endButton.transform.SetParent(canvasGo.transform, false);
            var rt = endButton.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-64, 64);
            rt.sizeDelta = new Vector2(240, 72);
            var img = endButton.GetComponent<Image>();
            img.color = new Color(.08f, .09f, .16f, .7f);
            var outline = endButton.GetComponent<Outline>();
            outline.effectColor = new Color(1, 1, 1, .55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var btn = endButton.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(.75f, .85f, 1f);
            cb.pressedColor = new Color(.6f, .7f, .9f);
            btn.colors = cb;
            btn.onClick.AddListener(FinishExperience);

            var txtGo = new GameObject("글자", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(endButton.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = txtGo.GetComponent<Text>();
            txt.font = uiFont;
            txt.text = endButtonText;
            txt.fontSize = 28;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            endButtonGroup = endButton.GetComponent<CanvasGroup>();
            endButtonGroup.alpha = 0;
            endButton.SetActive(false);
        }

        // ───────────────────────────── 작업용 카메라 전환 (플레이 중 Tab)
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
        }

        void Update()
        {
            // 작업용 카메라 전환은 에디터에서만 (전시용 빌드에서는 관람객이 Tab을 눌러도 무시)
            if (Application.isEditor && BmInput.Down(BmKey.Tab)) SetWorkCameraActive(!WorkCameraActive);
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
