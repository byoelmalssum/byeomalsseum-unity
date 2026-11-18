using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// [역할] 체험 6단계(첫인상 남기기) → 7단계(뜻과 마주하기 + QR 영수증)
//   화면 전체를 코드로 만든다. 씬에 이 스크립트 하나 붙이고 폰트만 넣으면 됨.
//   5단계(단어의 내부) 담당 조원은 공간 체험이 끝나면 Open("윤슬", "뜻...")만 호출하면 된다.
public class FirstImpressionUI : MonoBehaviour
{
    [Serializable]
    public class Feeling
    {
        public string name;
        public Color color;
    }

    [Header("연결")]
    public ReceiptManager receiptManager;
    [Tooltip("한글이 들어 있는 TMP 폰트 에셋 (필수). 예) Pretendard-Regular SDF")]
    public TMP_FontAsset font;
    [Tooltip("굵은 글씨용 폰트 (선택). 비우면 font를 굵게 처리")]
    public TMP_FontAsset boldFont;

    [Header("테스트")]
    public bool openOnStart = true;
    public string testWord = "윤슬";
    public string testMeaning = "햇빛이나 달빛에 비치어 반짝이는 잔물결";

    [Header("설정")]
    public int requiredCount = 3;
    public int maxChars = 30;
    [Tooltip("QR이 뜬 뒤 아무도 키를 안 누르면 몇 초 후 우주로 돌아갈지 (0이면 자동으로 안 돌아감)")]
    public float resetAfterSeconds = 60f;

    [Header("단어 뒤 빛")]
    public bool showWordGlow = true;
    [Range(0f, 0.5f)] public float wordGlowAlpha = 0.12f;

    [Tooltip("느낌 단어와 점 색. 여기 순서대로 5개씩 2줄로 배치된다\n(코드 기본값으로 되돌리려면 컴포넌트 ⋮ → 느낌 목록 기본값으로)")]
    public List<Feeling> feelings = DefaultFeelings();

    static List<Feeling> DefaultFeelings() => new List<Feeling>
    {
        F("설렘", "#F58FB1"), F("가벼움", "#BFE3F7"), F("서늘함", "#7FA8E8"), F("쓸쓸함", "#9B8FD9"), F("풍성함", "#F5B04A"),
        F("날카로움", "#5FD0DC"), F("정겨움", "#F2A07B"), F("생동감", "#8FDB6F"), F("아늑함", "#E8C27A"), F("고요함", "#6E7FE0"),
    };

    // 씬에 이미 저장된 옛 목록을 코드의 새 목록으로 바꿀 때 (Play 멈춘 상태에서 누르고 Ctrl+S)
    [ContextMenu("느낌 목록 기본값으로")]
    void ResetFeelingsToDefault()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "느낌 목록 기본값");
#endif
        feelings = DefaultFeelings();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public event Action Finished; // 체험 끝 → 우주 화면으로 돌아가기

    // ===== 색 =====
    static readonly Color C_PANEL = Hex("#0D0F26", 0.94f);
    static readonly Color C_PANEL_LINE = Hex("#FFFFFF", 0.06f);
    static readonly Color C_CHIP = Hex("#1A1D3B", 0.92f);
    static readonly Color C_CHIP_LINE = Hex("#343A63");
    static readonly Color C_TEXT = Hex("#F2F3FA");
    static readonly Color C_SUB = Hex("#8C91B4");
    static readonly Color C_PINK = Hex("#FF8FB1");
    static readonly Color C_RED = Hex("#FF6B7D");
    static readonly Color C_INPUT = Hex("#12142B", 0.95f);
    static readonly Color C_INPUT_LINE = Hex("#4B5180");
    static readonly Color C_INPUT_LINE_ON = Hex("#8E96D6");
    static readonly Color C_BTN_L = Hex("#FFB3C7");
    static readonly Color C_BTN_R = Hex("#FFDCA8");
    static readonly Color C_BTN_TEXT = Hex("#2A2238");

    const float PW = 1600f, PH = 540f; // 패널 크기 (1920x1080 기준)

    enum State { Hidden, Form, Result }
    State _state = State.Hidden;

    class Chip
    {
        public Feeling f;
        public RectTransform rt;
        public Image glow, bg, line, dot, check;
        public TextMeshProUGUI label;
        public bool on;
    }

    readonly List<Chip> _chips = new List<Chip>();
    readonly List<Chip> _selected = new List<Chip>();

    string _word, _meaning, _impression;
    CanvasGroup _panelGroup, _formGroup, _resultGroup, _meaningGroup, _submitGroup;
    TextMeshProUGUI _wordText, _counterText, _charCount, _meaningText, _resultText, _qrStatus, _submitLabel;
    TMP_InputField _input;
    Image _inputLine;
    Button _submit;
    RawImage _qrImage;
    Image _wordGlow;
    bool _receiptReady;
    Coroutine _countdown;
    Vector2 _meaningPos, _counterPos;

    // ================================================================
    void Start()
    {
        if (!font) { Debug.LogError("[별말씀] FirstImpressionUI: 한글 TMP 폰트를 넣어 주세요"); enabled = false; return; }
        if (!receiptManager) { Debug.LogError("[별말씀] FirstImpressionUI: ReceiptManager를 연결해 주세요"); enabled = false; return; }

        Build();
        SyncFeelingColors();

        receiptManager.onReceiptReady.AddListener(OnReceiptReady);
        receiptManager.onReceiptFailed.AddListener(OnReceiptFailed);

        _panelGroup.alpha = 0f;
        _panelGroup.blocksRaycasts = false;
        if (openOnStart) Open(testWord, testMeaning);
    }

    // ===== 외부에서 부르는 함수 =====
    public void Open(string word, string meaning)
    {
        StopAllCoroutines();
        _countdown = null;
        _word = word;
        _meaning = meaning;
        _receiptReady = false;
        _wordText.text = word;
        FitWordGlow();

        foreach (var c in _chips) { c.on = false; c.rt.localScale = Vector3.one; UpdateChip(c); }
        ((RectTransform)_meaningGroup.transform).anchoredPosition = _meaningPos;
        _counterText.rectTransform.anchoredPosition = _counterPos;
        _selected.Clear();
        _input.text = "";
        _meaningText.text = $"<size=62%><color=#{ColorUtility.ToHtmlStringRGB(C_PINK)}>{word}의 진짜 뜻</color></size>\n{meaning}";

        SetGroup(_formGroup, 1f);
        SetGroup(_resultGroup, 0f);
        SetGroup(_meaningGroup, 0f);
        _qrImage.gameObject.SetActive(false);
        receiptManager.qr.Hide();

        _state = State.Form;
        RefreshState();
        StartCoroutine(Fade(_panelGroup, 1f, 0.6f));
        _input.ActivateInputField();
    }

    public void Close()
    {
        if (_state == State.Hidden) return;
        _state = State.Hidden;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        yield return Fade(_panelGroup, 0f, 0.6f);
        receiptManager.qr.Hide();
        Finished?.Invoke();
    }

    // ===== 상태 =====
    bool IsReady => _selected.Count == requiredCount && _input.text.Trim().Length > 0;

    void RefreshState()
    {
        int n = _selected.Count;
        _counterText.text = $"{n} / {requiredCount} 선택";
        _counterText.color = n == requiredCount ? C_PINK : C_SUB;
        _charCount.text = $"{_input.text.Length} / {maxChars}";

        bool ready = IsReady && _state == State.Form;
        _submit.interactable = ready;
        _submitGroup.alpha = ready ? 1f : 0.35f;
    }

    void Toggle(Chip ch)
    {
        if (_state != State.Form) return;
        if (ch.on)
        {
            ch.on = false;
            _selected.Remove(ch);
        }
        else
        {
            if (_selected.Count >= requiredCount) { StartCoroutine(Shake(_counterText.rectTransform, _counterPos)); return; }
            ch.on = true;
            _selected.Add(ch);
        }
        UpdateChip(ch);
        StartCoroutine(Pop(ch.rt));
        RefreshState();
    }

    void OnSubmit()
    {
        if (_state != State.Form || !IsReady) return;
        _state = State.Result;
        _impression = _input.text.Trim();

        var names = new string[_selected.Count];
        var colored = new StringBuilder();
        for (int i = 0; i < _selected.Count; i++)
        {
            names[i] = _selected[i].f.name;
            if (i > 0) colored.Append("<color=#5A5F82>  ·  </color>");
            colored.Append($"<color=#{ColorUtility.ToHtmlStringRGB(_selected[i].f.color)}>{names[i]}</color>");
        }

        _resultText.text =
            $"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(C_SUB)}>나의 첫인상</color></size>\n" +
            $"“<noparse>{_impression}</noparse>”\n" +
            $"<size=80%>{colored}</size>";
        _qrStatus.text = "영수증을 만드는 중…";
        _qrStatus.color = C_SUB;

        receiptManager.qr.target = _qrImage;
        receiptManager.Submit(_word, _meaning, _impression, names);

        _input.DeactivateInputField();
        RefreshState();
        StartCoroutine(ShowResult());
    }

    IEnumerator ShowResult()
    {
        StartCoroutine(Fade(_formGroup, 0f, 0.4f));
        yield return new WaitForSeconds(0.3f);
        StartCoroutine(Rise(_meaningGroup.transform as RectTransform, _meaningPos, 0.8f));
        StartCoroutine(Fade(_meaningGroup, 1f, 0.8f));
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(Fade(_resultGroup, 1f, 0.6f));
    }

    void OnReceiptReady(string url)
    {
        if (_state != State.Result) return;
        _receiptReady = true;
        _qrStatus.text = BackHint(0);
        if (_countdown != null) StopCoroutine(_countdown);
        if (resetAfterSeconds > 0f) _countdown = StartCoroutine(Countdown());
    }

    // QR이 뜬 뒤 Space / ESC → 바로 우주로
    void Update()
    {
        if (_state == State.Result && _receiptReady && BackPressed()) Close();
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
        return false;
    }

    string BackHint(int secondsLeft)
    {
        string key = $"<color=#{ColorUtility.ToHtmlStringRGB(C_TEXT)}><b>Space</b></color> 키를 누르면 우주로 돌아가요";
        return secondsLeft > 0 ? $"{key}  <size=80%>({secondsLeft})</size>" : key;
    }

    void OnReceiptFailed()
    {
        if (_state != State.Result) return;
        _state = State.Form;
        StopAllCoroutines();
        SetGroup(_resultGroup, 0f);
        SetGroup(_meaningGroup, 0f);
        SetGroup(_formGroup, 1f);
        RefreshState();
        _counterText.text = "저장에 실패했어요 · 다시 눌러 주세요";
        _counterText.color = C_RED;
    }

    IEnumerator Countdown()
    {
        float left = resetAfterSeconds;
        while (left > 0f)
        {
            _qrStatus.text = BackHint(Mathf.CeilToInt(left));
            yield return new WaitForSeconds(1f);
            left -= 1f;
        }
        Close();
    }

    // ReceiptManager의 행성 색을 이 화면의 칩 색과 똑같이 맞춘다
    void SyncFeelingColors()
    {
        var list = new List<FeelingColor>();
        foreach (var f in feelings)
        {
            Color.RGBToHSV(f.color, out float h, out _, out _);
            list.Add(new FeelingColor { feeling = f.name, hue = h * 360f });
        }
        receiptManager.feelingColors = list;
    }

    // ================================================================
    // 화면 만들기
    // ================================================================
    void Build()
    {
        if (EventSystem.current == null)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#else
            Debug.LogWarning("[별말씀] EventSystem이 없어요. Hierarchy 우클릭 → UI → Event System 을 추가해 주세요");
#endif
        }

        // Canvas
        var canvasGo = new GameObject("FirstImpressionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 패널
        var panel = NewRect("Panel", canvasGo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PW, PH);
        _panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var shadow = NewImage("Shadow", panel, Soft(28, 60), new Color(0, 0, 0, 0.55f), true);
        Stretch(shadow.rectTransform, -60, -60, -60, -60);
        Stretch(NewImage("Bg", panel, RoundFill(28), C_PANEL, true).rectTransform, 0, 0, 0, 0);
        Stretch(NewImage("Line", panel, RoundLine(28, 1.5f), C_PANEL_LINE, true).rectTransform, 0, 0, 0, 0);
        BuildStars(panel);

        // 왼쪽 위 라벨 + 단어 (항상 보임)
        var step = NewText("Step", panel, "0 6    첫인상 남기기", 22, C_SUB, TextAlignmentOptions.TopLeft);
        step.characterSpacing = 18;
        Place(step.rectTransform, 72, 34, 500, 30);

        // 단어 뒤 은은한 빛 (이미지로) — 크기는 Open()에서 단어 길이에 맞춰 다시 정함
        _wordGlow = NewImage("WordGlow", panel, Soft(60, 90), new Color(0.56f, 0.64f, 1f, wordGlowAlpha), true);
        Place(_wordGlow.rectTransform, 40 - 60, 70 - 60, 420 + 120, 170 + 120);

        _wordText = NewText("Word", panel, "", 180, Color.white, TextAlignmentOptions.TopLeft, true);
        _wordText.enableAutoSizing = true;
        _wordText.fontSizeMax = 180;
        _wordText.fontSizeMin = 80;
        _wordText.characterSpacing = 4;
        Place(_wordText.rectTransform, 64, 62, 560, 196);
        AddGlow(_wordText);

        // ---------- 입력 화면 ----------
        var form = NewRect("Form", panel);
        Stretch(form, 0, 0, 0, 0);
        _formGroup = form.gameObject.AddComponent<CanvasGroup>();

        var sub = NewText("Sub", form, "뜻은 아직 비밀이에요.\n방금 지나온 공간은 어떤 느낌이었나요?", 27, C_SUB, TextAlignmentOptions.TopLeft);
        sub.lineSpacing = 0;
        Place(sub.rectTransform, 72, 262, 560, 80);

        var q = NewText("Question", form,
            $"<b>어떤 느낌이었나요?</b>   <size=72%><color=#{ColorUtility.ToHtmlStringRGB(C_SUB)}>{requiredCount}개를 골라 주세요</color></size>",
            26, C_TEXT, TextAlignmentOptions.BottomLeft);
        Place(q.rectTransform, 640, 70, 620, 40);

        _counterText = NewText("Counter", form, "0 / 3 선택", 21, C_SUB, TextAlignmentOptions.BottomRight);
        Place(_counterText.rectTransform, 1130, 70, 400, 40);

        // 칩 5 x 2
        const float chipW = 166f, chipH = 72f, gap = 14f;
        for (int i = 0; i < feelings.Count; i++)
        {
            int col = i % 5, row = i / 5;
            var chip = BuildChip(form, feelings[i], chipW, chipH);
            Place(chip.rt, 640 + col * (chipW + gap), 130 + row * (chipH + gap), chipW, chipH);
            _chips.Add(chip);
        }

        var label = NewText("InputLabel", form, "이런 뜻일 것 같아요", 24, C_TEXT, TextAlignmentOptions.BottomLeft, true);
        Place(label.rectTransform, 72, 352, 500, 36);

        BuildInput(form, 72, 400, 1012, 88);
        BuildSubmit(form, 1100, 400, 430, 88);

        // ---------- 결과 화면 ----------
        var meaning = NewRect("Meaning", panel);
        Place(meaning, 72, 262, 560, 240);
        _meaningGroup = meaning.gameObject.AddComponent<CanvasGroup>();
        _meaningText = NewText("MeaningText", meaning, "", 34, C_TEXT, TextAlignmentOptions.TopLeft);
        _meaningText.lineSpacing = 12;
        Stretch(_meaningText.rectTransform, 0, 0, 0, 0);

        var result = NewRect("Result", panel);
        Stretch(result, 0, 0, 0, 0);
        _resultGroup = result.gameObject.AddComponent<CanvasGroup>();

        _resultText = NewText("ResultText", result, "", 32, C_TEXT, TextAlignmentOptions.TopLeft);
        _resultText.lineSpacing = 30;
        Place(_resultText.rectTransform, 660, 110, 470, 260);

        var guide = NewText("QrGuide", result, "QR을 찍어 나만의 영수증을 받아 가세요  →", 24, C_PINK, TextAlignmentOptions.BottomLeft, true);
        Place(guide.rectTransform, 660, 390, 520, 40);

        var cardGlow = NewImage("CardGlow", result, Soft(24, 40), new Color(C_PINK.r, C_PINK.g, C_PINK.b, 0.25f), true);
        Place(cardGlow.rectTransform, 1190 - 40, 80 - 40, 340 + 80, 340 + 80);
        var card = NewImage("QrCard", result, RoundFill(24), Color.white, true);
        Place(card.rectTransform, 1190, 80, 340, 340);

        var qrGo = new GameObject("QR", typeof(RectTransform), typeof(RawImage));
        qrGo.transform.SetParent(card.transform, false);
        _qrImage = qrGo.GetComponent<RawImage>();
        var qrRt = _qrImage.rectTransform;
        qrRt.anchorMin = qrRt.anchorMax = qrRt.pivot = new Vector2(0.5f, 0.5f);
        qrRt.sizeDelta = new Vector2(300, 300);
        qrGo.SetActive(false);

        _qrStatus = NewText("QrStatus", result, "", 19, C_SUB, TextAlignmentOptions.Top);
        Place(_qrStatus.rectTransform, 1150, 436, 420, 64);

        SetGroup(_resultGroup, 0f);
        SetGroup(_meaningGroup, 0f);
        _meaningPos = meaning.anchoredPosition;
        _counterPos = _counterText.rectTransform.anchoredPosition;
    }

    Chip BuildChip(Transform parent, Feeling f, float w, float h)
    {
        var ch = new Chip { f = f };
        ch.rt = NewRect("Chip_" + f.name, parent);

        int r = Mathf.RoundToInt(h / 2f);
        ch.glow = NewImage("Glow", ch.rt, Soft(r, 18), f.color, true);
        Stretch(ch.glow.rectTransform, -18, -18, -18, -18);
        ch.bg = NewImage("Bg", ch.rt, RoundFill(r), C_CHIP, true);
        Stretch(ch.bg.rectTransform, 0, 0, 0, 0);
        ch.bg.raycastTarget = true;
        ch.line = NewImage("Line", ch.rt, RoundLine(r, 2f), C_CHIP_LINE, true);
        Stretch(ch.line.rectTransform, 0, 0, 0, 0);

        ch.dot = NewImage("Dot", ch.rt, Circle(6), f.color, false);
        Center(ch.dot.rectTransform, 12, 12);

        ch.label = NewText("Label", ch.rt, f.name, 24, C_TEXT, TextAlignmentOptions.MidlineLeft);
        var lr = ch.label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.sizeDelta = new Vector2(120, h);

        ch.check = NewImage("Check", ch.rt, CheckSprite(), f.color, false);
        Center(ch.check.rectTransform, 18, 18);

        var btn = ch.rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = ch.bg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => Toggle(ch));

        UpdateChip(ch);
        return ch;
    }

    void UpdateChip(Chip ch)
    {
        Color c = ch.f.color;
        ch.glow.color = new Color(c.r, c.g, c.b, 0.35f);
        ch.glow.gameObject.SetActive(ch.on);
        ch.bg.color = ch.on ? Color.Lerp(C_CHIP, c, 0.2f) : C_CHIP;
        ch.line.color = ch.on ? c : C_CHIP_LINE;
        ch.check.gameObject.SetActive(ch.on);

        SetBold(ch.label, ch.on);
        ch.label.color = ch.on ? Color.white : new Color(C_TEXT.r, C_TEXT.g, C_TEXT.b, 0.85f);

        // 점 + 글자 (+ 체크) 묶음을 가운데 정렬
        float labelW = ch.label.GetPreferredValues(ch.label.text).x;
        float checkW = ch.on ? 8f + 18f : 0f;
        float total = 12f + 10f + labelW + checkW;
        float x = -total / 2f;
        ch.dot.rectTransform.anchoredPosition = new Vector2(x + 6f, 0f);
        ch.label.rectTransform.anchoredPosition = new Vector2(x + 22f, 0f);
        ch.label.rectTransform.sizeDelta = new Vector2(labelW + 4f, ch.label.rectTransform.sizeDelta.y);
        ch.check.rectTransform.anchoredPosition = new Vector2(x + 22f + labelW + 8f + 9f, 0f);
    }

    void BuildInput(Transform parent, float x, float y, float w, float h)
    {
        var root = NewRect("Input", parent);
        Place(root, x, y, w, h);
        root.gameObject.SetActive(false); // 컴포넌트 다 붙인 뒤 켜기

        var bg = NewImage("Bg", root, RoundFill(20), C_INPUT, true);
        Stretch(bg.rectTransform, 0, 0, 0, 0);
        bg.raycastTarget = true;
        _inputLine = NewImage("Line", root, RoundLine(20, 2f), C_INPUT_LINE, true);
        Stretch(_inputLine.rectTransform, 0, 0, 0, 0);

        var area = NewRect("TextArea", root);
        Stretch(area, 28, 0, 110, 0);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = NewText("Placeholder", area, "한 문장으로 적어 주세요", 26, new Color(C_SUB.r, C_SUB.g, C_SUB.b, 0.6f), TextAlignmentOptions.MidlineLeft);
        Stretch(ph.rectTransform, 0, 0, 0, 0);
        var txt = NewText("Text", area, "", 26, C_TEXT, TextAlignmentOptions.MidlineLeft);
        txt.richText = false;
        Stretch(txt.rectTransform, 0, 0, 0, 0);

        _charCount = NewText("Count", root, "0 / 30", 20, C_SUB, TextAlignmentOptions.MidlineRight);
        Stretch(_charCount.rectTransform, 0, 0, 28, 0);

        _input = root.gameObject.AddComponent<TMP_InputField>();
        _input.targetGraphic = bg;
        _input.textViewport = area;
        _input.textComponent = txt;
        _input.placeholder = ph;
        _input.characterLimit = maxChars;
        _input.lineType = TMP_InputField.LineType.SingleLine;
        _input.richText = false;
        _input.customCaretColor = true;
        _input.caretColor = C_TEXT;
        _input.caretWidth = 2;
        _input.selectionColor = new Color(C_PINK.r, C_PINK.g, C_PINK.b, 0.35f);
        _input.transition = Selectable.Transition.None;

        root.gameObject.SetActive(true);

        _input.onValueChanged.AddListener(_ => RefreshState());
        _input.onSubmit.AddListener(_ => OnSubmit());
        _input.onSelect.AddListener(_ => _inputLine.color = C_INPUT_LINE_ON);
        _input.onDeselect.AddListener(_ => _inputLine.color = C_INPUT_LINE);
    }

    void BuildSubmit(Transform parent, float x, float y, float w, float h)
    {
        var root = NewRect("Submit", parent);
        Place(root, x, y, w, h);
        _submitGroup = root.gameObject.AddComponent<CanvasGroup>();

        var glow = NewImage("Glow", root, Soft(Mathf.RoundToInt(h / 2f), 30), new Color(C_BTN_L.r, C_BTN_L.g, C_BTN_L.b, 0.35f), true);
        Stretch(glow.rectTransform, -30, -30, -30, -30);

        var img = NewImage("Bg", root, GradientPill((int)w, (int)h, C_BTN_L, C_BTN_R), Color.white, false);
        Stretch(img.rectTransform, 0, 0, 0, 0);
        img.raycastTarget = true;

        _submitLabel = NewText("Label", root, "첫인상 남기고 뜻 보기", 26, C_BTN_TEXT, TextAlignmentOptions.Center, true);
        Stretch(_submitLabel.rectTransform, 0, 0, 0, 0);

        _submit = root.gameObject.AddComponent<Button>();
        _submit.targetGraphic = img;
        _submit.transition = Selectable.Transition.ColorTint;
        var cb = _submit.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.disabledColor = Color.white; // 비활성은 CanvasGroup 알파로 표현
        _submit.colors = cb;
        _submit.onClick.AddListener(OnSubmit);
    }

    // 단어 뒤 빛을 실제 글자 폭에 맞춘다 (긴 단어도 빛이 글자 전체를 고르게 감싸게)
    void FitWordGlow()
    {
        if (_wordGlow == null) return;
        _wordGlow.gameObject.SetActive(showWordGlow);
        if (!showWordGlow) return;

        _wordText.ForceMeshUpdate();
        Bounds b = _wordText.textBounds;              // 글자 영역 (텍스트 왼쪽 위 기준)
        float w = Mathf.Max(160f, b.size.x);
        float h = Mathf.Max(110f, b.size.y);
        float cx = 64f + b.center.x;                  // 패널 왼쪽 위 기준 중심
        float cy = 62f - b.center.y;
        const float pad = 80f;
        Place(_wordGlow.rectTransform, cx - w / 2f - pad, cy - h / 2f - pad, w + pad * 2f, h + pad * 2f);
        _wordGlow.color = new Color(0.56f, 0.64f, 1f, wordGlowAlpha);
    }

    void BuildStars(RectTransform panel)
    {
        var rnd = new System.Random(7);
        var sprite = Circle(2);
        for (int i = 0; i < 70; i++)
        {
            var s = NewImage("Star", panel, sprite, new Color(1, 1, 1, 0.08f + (float)rnd.NextDouble() * 0.35f), false);
            float size = rnd.NextDouble() < 0.85 ? 3f : 5f;
            Place(s.rectTransform, (float)rnd.NextDouble() * (PW - 20) + 10, (float)rnd.NextDouble() * (PH - 20) + 10, size, size);
        }
    }

    // 단어 글자 뒤에 은은한 빛 (TMP Underlay 기능)
    void AddGlow(TextMeshProUGUI t)
    {
        var m = t.fontMaterial; // 인스턴스 머티리얼
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        // 값이 크면 글자마다 네모난 빛 덩어리가 생기니 작게 유지
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.62f, 0.7f, 1f, 0.45f));
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.45f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
    }

    // ================================================================
    // 작은 도우미들
    // ================================================================
    static Feeling F(string n, string hex) => new Feeling { name = n, color = Hex(hex) };

    static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        c.a = a;
        return c;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 패널 왼쪽 위 기준 (x, y) 위치에 (w, h) 크기
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모를 꽉 채우되 (왼, 위, 오른, 아래) 여백
    static void Stretch(RectTransform rt, float l, float t, float r, float b)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        SetBold(t, bold);
        return t;
    }

    void SetBold(TextMeshProUGUI t, bool bold)
    {
        if (boldFont)
        {
            t.font = bold ? boldFont : font;
            t.fontStyle = FontStyles.Normal;
        }
        else
        {
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    static void SetGroup(CanvasGroup g, float a)
    {
        g.alpha = a;
        g.interactable = g.blocksRaycasts = a > 0.5f;
    }

    // ================================================================
    // 애니메이션
    // ================================================================
    static IEnumerator Fade(CanvasGroup g, float to, float dur)
    {
        float from = g.alpha;
        g.interactable = g.blocksRaycasts = to > 0.5f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }
        g.alpha = to;
    }

    static IEnumerator Rise(RectTransform rt, Vector2 end, float dur)
    {
        Vector2 start = end + new Vector2(0, -24);
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = Vector2.Lerp(start, end, 1 - Mathf.Pow(1 - t / dur, 3));
            yield return null;
        }
        rt.anchoredPosition = end;
    }

    static IEnumerator Pop(RectTransform rt)
    {
        const float dur = 0.18f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(t / dur * Mathf.PI);
            rt.localScale = Vector3.one * (1f + 0.06f * k);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    static IEnumerator Shake(RectTransform rt, Vector2 p)
    {
        const float dur = 0.3f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = p + new Vector2(Mathf.Sin(t * 60f) * 8f * (1 - t / dur), 0);
            yield return null;
        }
        rt.anchoredPosition = p;
    }

    // ================================================================
    // 둥근 모양 스프라이트를 코드로 생성 (이미지 파일 불필요)
    // ================================================================
    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    // 둥근 사각형의 '경계까지 거리' (안쪽 음수, 바깥 양수)
    static float RoundRectDist(float px, float py, float w, float h, float r)
    {
        float qx = Mathf.Abs(px - w / 2f) - (w / 2f - r);
        float qy = Mathf.Abs(py - h / 2f) - (h / 2f - r);
        float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    static Sprite MakeSprite(string key, int w, int h, Vector4 border, Func<float, float, Color> pixel)
    {
        if (_cache.TryGetValue(key, out var s) && s) return s;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(x + 0.5f, y + 0.5f);
        tex.SetPixels(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _cache[key] = s;
        return s;
    }

    static Sprite RoundFill(int r)
    {
        int size = r * 2 + 2;
        return MakeSprite($"fill{r}", size, size, new Vector4(r, r, r, r), (x, y) =>
            new Color(1, 1, 1, Mathf.Clamp01(0.5f - RoundRectDist(x, y, size, size, r))));
    }

    static Sprite RoundLine(int r, float width)
    {
        int size = r * 2 + 2;
        return MakeSprite($"line{r}_{width}", size, size, new Vector4(r, r, r, r), (x, y) =>
        {
            float d = RoundRectDist(x, y, size, size, r);
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - (Mathf.Abs(d + width / 2f) - width / 2f)));
        });
    }

    // 바깥으로 퍼지는 부드러운 빛
    static Sprite Soft(int r, int blur)
    {
        int size = (r + blur) * 2 + 2;
        int b = r + blur;
        return MakeSprite($"soft{r}_{blur}", size, size, new Vector4(b, b, b, b), (x, y) =>
        {
            float d = RoundRectDist(x, y, size, size, r + blur) + blur; // 안쪽 사각형 기준 거리
            float a = 1f - Mathf.Clamp01(d / blur);
            return new Color(1, 1, 1, a * a);
        });
    }

    static Sprite Circle(int r)
    {
        int size = r * 2 + 2;
        return MakeSprite($"circle{r}", size, size, Vector4.zero, (x, y) =>
        {
            float d = new Vector2(x - size / 2f, y - size / 2f).magnitude - r;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
    }

    static Sprite CheckSprite()
    {
        const int size = 36;
        Vector2 a = new Vector2(7, 18), b = new Vector2(15, 10), c = new Vector2(29, 27);
        return MakeSprite("check", size, size, Vector4.zero, (x, y) =>
        {
            var p = new Vector2(x, y);
            float d = Mathf.Min(SegDist(p, a, b), SegDist(p, b, c)) - 2.2f;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    static Sprite GradientPill(int w, int h, Color left, Color right)
    {
        int r = h / 2;
        return MakeSprite($"grad{w}x{h}", w, h, Vector4.zero, (x, y) =>
        {
            Color c = Color.Lerp(left, right, x / w);
            c.a = Mathf.Clamp01(0.5f - RoundRectDist(x, y, w, h, r));
            return c;
        });
    }
}
