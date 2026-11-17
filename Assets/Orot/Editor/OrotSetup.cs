using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴 Tools > 별말씀 : 별우주 씬을 자동으로 만들어 주는 도구
public static class OrotSetup
{
    const string ScenePath = "Assets/Orot/별우주.unity";
    static readonly string[] RoleNames = { "초성", "중성", "종성" };

    static List<string> Words()
    {
        var list = new List<string>();
        if (Directory.Exists("Assets/Words"))
            foreach (var d in Directory.GetDirectories("Assets/Words"))
                list.Add(Path.GetFileName(d));
        if (list.Count == 0) list.Add("길차다");
        return list;
    }

    [MenuItem("Tools/별말씀/별우주 씬 만들기 · 열기", priority = 200)]
    static void CreateOrOpen()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log($"[오롯] 이미 있는 별우주 씬을 열었어요 (덮어쓰지 않음): {ScenePath}");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 1.6f, 0);
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.02f, .03f, .08f);
        cam.fieldOfView = 60;

        // 조명
        var lightGo = new GameObject("Directional Light", typeof(Light));
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(35, -25, 0);

        // 손 인식
        new GameObject("HandTracker", typeof(HandTracker));

        // 커서 (실행하면 StarGrab이 맨 위 캔버스로 옮김)
        var canvasGo = new GameObject("Cursor Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
        cursorGo.transform.SetParent(canvasGo.transform, false);
        var rt = cursorGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(28, 28);
        var img = cursorGo.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = new Color(1, 1, 1, .85f);
        img.raycastTarget = false;

        // 별 잡기
        var grab = new GameObject("StarGrab", typeof(StarGrab)).GetComponent<StarGrab>();
        grab.cam = cam;
        grab.cursor = rt;

        // 단어 별 (Assets/Words 안의 단어마다 하나씩)
        var root = new GameObject("StarSystem").transform;
        Vector3[] slots =
        {
            new Vector3(-4.2f, 2.4f, 9f), new Vector3(-1.4f, 1.1f, 7f),
            new Vector3(1.7f, 2.8f, 10f), new Vector3(4.4f, 1.3f, 8f),
            new Vector3(-2.8f, 3.6f, 12f), new Vector3(2.9f, 0.6f, 11f),
        };
        var words = Words();
        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "별 " + w;
            star.transform.SetParent(root, false);
            star.transform.position = slots[i % slots.Length] + Vector3.forward * 3f * (i / slots.Length);

            var sw = star.AddComponent<StarWord>();
            sw.word = w;
            sw.sceneName = w + "_공간";
            sw.cam = cam;
            sw.roleMaterials = new Material[3];
            for (int r = 0; r < 3; r++)
                sw.roleMaterials[r] = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Words/{w}/Materials/{RoleNames[r]}.mat");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Orot")) AssetDatabase.CreateFolder("Assets", "Orot");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[오롯] 별우주 씬을 만들었어요: {ScenePath} (단어 별 {words.Count}개)");
    }

    [MenuItem("Tools/별말씀/씬 목록 등록 (별우주 + 단어 공간들)", priority = 201)]
    static void RegisterScenes()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        if (File.Exists(ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

        int added = 0;
        foreach (var w in Words())
        {
            string p = $"Assets/Words/{w}/{w}_공간.unity";
            if (!File.Exists(p) || list.Exists(s => s.path == p)) continue;
            list.Add(new EditorBuildSettingsScene(p, true));
            added++;
        }
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[오롯] 씬 목록 정리 완료: 별우주 맨 위 + 단어 공간 {added}개 추가 (ProjectSettings가 바뀌니 단톡방에 공지!)");
    }
}
