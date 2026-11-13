using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Byeolmalsseum;

// 단어 별: 별 자체가 단어 글자 모양으로 보이고,
// 끝까지 잡으면 글자가 화면 정면으로 커지며 → 단어 공간 씬의 글자와 그대로 바뀌어 → 바로 해체되어 날아감
// ※ Assets/Orot 폴더에 둘 것 (팀 Core 코드를 쓰기 때문에 MediaPipe 샘플 폴더에 두면 안 됨)
public class StarWord : MonoBehaviour
{
    [Header("단어")]
    public string word = "길차다";
    [Tooltip("넘어갈 단어 공간 씬 이름 (Build Profiles의 Scene List에 있어야 함)")]
    public string sceneName = "길차다_공간";

    [Header("별 모양")]
    [Tooltip("별 상태에서 글자 가로 길이 (별 크기 기준)")]
    public float starWidth = 1.8f;
    [Tooltip("초성 / 중성 / 종성 머티리얼. Assets/Words/(단어)/Materials 의 것을 넣으면 공간 씬과 색이 똑같아짐")]
    public Material[] roleMaterials = new Material[3];
    [Tooltip("원래 구(Sphere) 모양 숨기기")]
    public bool hideSphere = true;

    [Header("공간 씬 값과 똑같이 (WordSpace 인스펙터 값)")]
    public float strokeThickness = .07f;
    public float strokeDepth = .2f;
    public float glyphDistance = 6f;
    [Range(.3f, .95f)] public float glyphScreenWidth = .65f;

    [Header("넘어가기 연출")]
    [Tooltip("선택 완료 후 글자가 화면 정면 자리로 맞춰지는 시간")]
    public float frameSeconds = .6f;
    [Tooltip("공간 씬으로 바뀐 뒤 해체가 시작되기까지")]
    public float holdAfterSwap = .25f;

    public Camera cam;

    Transform glyphRoot;
    Rect bounds;
    bool started;

    static readonly Color[] DefaultColors = { new Color(1f, .78f, .3f), new Color(.4f, .72f, 1f), new Color(1f, .45f, .68f) };

    void Start()
    {
        if (cam == null) cam = Camera.main;
        BuildGlyph();

        if (hideSphere)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr) mr.enabled = false;
        }

        // 손 커서로 글자 전체를 가리킬 수 있게 충돌 상자 추가
        var box = gameObject.AddComponent<BoxCollider>();
        float k = starWidth / Mathf.Max(bounds.width, .01f);
        Vector3 ls = transform.lossyScale;
        box.size = new Vector3(starWidth / Mathf.Max(ls.x, 1e-4f), bounds.height * k / Mathf.Max(ls.y, 1e-4f), .3f);
    }

    void BuildGlyph()
    {
        var jamos = WordGlyph.BuildJamos(word);
        var all = new List<Vector2[]>();
        foreach (var j in jamos) all.AddRange(j.strokes);
        bounds = WordGlyph.Bounds(all);

        glyphRoot = new GameObject("Glyph " + word).transform;
        glyphRoot.SetParent(transform, false);
        float k = starWidth / Mathf.Max(bounds.width, .01f);
        Vector3 ls = transform.lossyScale;
        glyphRoot.localScale = Vector3.one * k / Mathf.Max(ls.x, 1e-4f);

        foreach (var j in jamos)
        {
            var go = new GameObject($"{j.role} {j.jamo}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(glyphRoot, false);
            Vector2 c = j.Center;
            go.transform.localPosition = new Vector3(c.x - bounds.center.x, c.y - bounds.center.y, 0);
            go.GetComponent<MeshFilter>().sharedMesh = StrokeMeshBuilder.Build(j.strokes, c, strokeThickness, strokeDepth);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor((int)j.role);
        }
    }

    Material MaterialFor(int role)
    {
        if (roleMaterials != null && role < roleMaterials.Length && roleMaterials[role] != null) return roleMaterials[role];
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
        Destroy(tmp);
        Color col = DefaultColors[Mathf.Clamp(role, 0, 2)];
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        return m;
    }

    void LateUpdate()
    {
        // 별 상태일 때는 글자가 항상 카메라를 바라봄
        if (!started && glyphRoot && cam) glyphRoot.rotation = cam.transform.rotation;
    }

    // 화면에서 WordSpace와 똑같은 자리·크기 (가운데, 화면 가로의 glyphScreenWidth)
    void Framing(Camera c, out Vector3 pos, out Quaternion rot, out float scale)
    {
        Transform ct = c.transform;
        float visH = 2f * glyphDistance * Mathf.Tan(c.fieldOfView * .5f * Mathf.Deg2Rad);
        float visW = visH * c.aspect;
        scale = Mathf.Min(visW * glyphScreenWidth / Mathf.Max(bounds.width, .01f), visH * .6f / Mathf.Max(bounds.height, .01f));
        pos = ct.position + ct.forward * glyphDistance;
        rot = ct.rotation;
    }

    // StarGrab이 끝까지 잡았을 때 SendMessage로 부름
    void OnStarSelected()
    {
        if (started) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"'{sceneName}' 씬을 찾을 수 없어요. File > Build Profiles > Scene List에 추가했는지, 이름이 맞는지 확인하세요.");
            return;
        }
        started = true;
        var runner = new GameObject("[별말씀] 단어 공간으로").AddComponent<Runner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(Transition(runner));
    }

    IEnumerator Transition(Runner runner)
    {
        Scene starScene = gameObject.scene;

        // ① 뒤에서 공간 씬 미리 불러오기
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        // ② 글자를 화면 정면 자리로 (별에서 떼어 냄)
        glyphRoot.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(glyphRoot.gameObject, starScene);
        Vector3 p0 = glyphRoot.position; Quaternion r0 = glyphRoot.rotation; float s0 = glyphRoot.localScale.x;
        for (float t = 0; t < frameSeconds; t += Time.deltaTime)
        {
            Framing(cam, out var p1, out var r1, out var s1);
            float e = Mathf.SmoothStep(0, 1, t / frameSeconds);
            glyphRoot.SetPositionAndRotation(Vector3.Lerp(p0, p1, e), Quaternion.Slerp(r0, r1, e));
            glyphRoot.localScale = Vector3.one * Mathf.Lerp(s0, s1, e);
            yield return null;
        }
        {
            Framing(cam, out var p1, out var r1, out var s1);
            glyphRoot.SetPositionAndRotation(p1, r1);
            glyphRoot.localScale = Vector3.one * s1;
        }

        while (op.progress < .9f) yield return null;

        // ③ 공간 씬이 켜지는 순간 (Start 전에) 바꿔치기 준비
        Transform glyph = glyphRoot;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = null;
        onLoaded = (scene, mode) =>
        {
            if (scene.name != sceneName) return;
            SceneManager.sceneLoaded -= onLoaded;

            WordSpace ws = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                ws = root.GetComponentInChildren<WordSpace>(true);
                if (ws) break;
            }

            // 별 씬의 EventSystem은 공간 씬으로 옮겨서 계속 사용 (체험 종료 버튼용)
            var es = FindAnyObjectByType<EventSystem>();
            if (es && es.gameObject.scene == starScene && es.transform.parent == null)
                SceneManager.MoveGameObjectToScene(es.gameObject, scene);

            // 별 씬은 글자만 남기고 전부 끄기
            foreach (var root in starScene.GetRootGameObjects())
                if (root.transform != glyph) root.SetActive(false);

            SceneManager.SetActiveScene(scene);

            if (ws == null) { Debug.LogWarning("[별말씀] 공간 씬에서 WordSpace를 못 찾았어요."); return; }

            // 공간 씬 연출 값: 글자 등장 생략, 잠깐 보여주고 바로 해체
            ws.skipIntro = false;
            ws.glyphAppearSeconds = .0001f;
            ws.glyphHoldSeconds = holdAfterSwap;
            ws.glyphDistance = glyphDistance;
            ws.glyphScreenWidth = glyphScreenWidth;
            ws.SetWorkCameraActive(false);
            foreach (var piece in ws.Pieces) if (piece && piece.visual) piece.visual.localScale = Vector3.zero;

            // 내 글자를 공간 씬 카메라 기준 같은 자리로 옮김 → 화면에서는 그대로 있는 것처럼 보임
            if (ws.experienceCamera)
            {
                ws.experienceCamera.PlaceAtStart();
                Framing(ws.experienceCamera.Cam, out var p, out var r, out var s);
                glyph.SetPositionAndRotation(p, r);
                glyph.localScale = Vector3.one * s;
            }
        };
        SceneManager.sceneLoaded += onLoaded;

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // ④ 공간 씬의 글자가 나타날 때까지 몇 프레임 겹쳐 두었다가 정리
        for (int i = 0; i < 3; i++) yield return null;
        if (glyph) Destroy(glyph.gameObject);
        yield return SceneManager.UnloadSceneAsync(starScene);
        Destroy(runner.gameObject);
    }

    class Runner : MonoBehaviour { }
}
