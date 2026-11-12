using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Byeolmalsseum.EditorTools
{
    /// <summary>
    /// 메뉴: Tools > 별말씀 > 공간 씬 만들기·열기 > (단어)
    /// 이미 씬이 있으면 절대 덮어쓰지 않고 그냥 열어줌.
    /// 씬을 저장할 때마다 자모 배치를 자동 백업 (단어별 최근 20개).
    /// </summary>
    [InitializeOnLoad]
    public static class WordSpaceMenu
    {
        static WordSpaceMenu()
        {
            EditorSceneManager.sceneSaved += scene =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (!scene.IsValid() || !scene.isLoaded) return;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var ws in root.GetComponentsInChildren<WordSpace>(true))
                            if (ws.strokesRoot) WordSpaceBuilder.SaveBackup(ws, true);
                };
            };
        }

        const string Menu = "Tools/별말씀/공간 씬 만들기 · 열기/";
        [MenuItem(Menu + "길차다", false, 1)] static void W1() => CreateOrOpen("길차다");
        [MenuItem(Menu + "논틀밭틀", false, 2)] static void W2() => CreateOrOpen("논틀밭틀");
        [MenuItem(Menu + "살천스럽다", false, 3)] static void W3() => CreateOrOpen("살천스럽다");
        [MenuItem(Menu + "퍼르퍼르", false, 4)] static void W4() => CreateOrOpen("퍼르퍼르");
        [MenuItem(Menu + "다른 단어로…", false, 20)] static void Other() => OtherWordWindow.Open();

        public static string ScenePath(string word) => $"{WordSpaceBuilder.WordFolder(word)}/{word}_공간.unity";

        public static void CreateOrOpen(string word)
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("별말씀", "플레이를 끄고 다시 시도해 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string scenePath = ScenePath(word);
            if (File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
                SelectWordSpace();
                Debug.Log($"[별말씀] 이미 있는 '{word}' 씬을 열었어요 (덮어쓰지 않음): {scenePath}");
                return;
            }

            WordSpaceBuilder.EnsureFolder(WordSpaceBuilder.WordFolder(word));
            WordSpaceBuilder.EnsureFolder(WordSpaceBuilder.BackupFolder(word));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 조명
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 단어 공간 루트
            var root = new GameObject($"[별말씀] {word}");
            var ws = root.AddComponent<WordSpace>();
            ws.word = word;

            var strokes = new GameObject("글자 (자모를 옮겨서 공간 만들기)").transform;
            strokes.SetParent(root.transform, false);
            ws.strokesRoot = strokes;

            // 공간 작업용 빈 그룹 + 바닥
            var space = new GameObject("공간 (소품·조명 등 여기에)").transform;
            space.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "바닥 (지워도 됨)";
            ground.transform.SetParent(space, false);
            ground.transform.localScale = new Vector3(4, 1, 4);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial(word);

            // 카메라 경로 (기본: 원형 4점)
            var pathGo = new GameObject("체험 카메라 경로");
            pathGo.transform.SetParent(root.transform, false);
            var path = pathGo.AddComponent<CameraPath>();
            Vector3[] pts = { new Vector3(0, 1.6f, -10), new Vector3(9, 1.8f, -2), new Vector3(4, 2.2f, 8), new Vector3(-7, 1.8f, 5) };
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject($"경로점 {i + 1}").transform;
                p.SetParent(pathGo.transform, false);
                p.localPosition = pts[i];
            }

            // 체험 카메라
            var ecGo = new GameObject("체험 카메라", typeof(Camera), typeof(AudioListener));
            ecGo.tag = "MainCamera";
            ecGo.transform.SetParent(root.transform, false);
            var ecCam = ecGo.GetComponent<Camera>();
            ecCam.fieldOfView = 60; ecCam.nearClipPlane = .05f;
            var ec = ecGo.AddComponent<ExperienceCamera>();
            ec.path = path;
            ws.experienceCamera = ec;

            // 작업용 카메라 (기본 꺼짐)
            var wcGo = new GameObject("작업용 카메라", typeof(Camera), typeof(AudioListener));
            wcGo.transform.SetParent(root.transform, false);
            wcGo.transform.position = new Vector3(0, 6, -16);
            wcGo.transform.rotation = Quaternion.Euler(18, 0, 0);
            wcGo.AddComponent<WorkCamera>();
            var wc = wcGo.GetComponent<Camera>();
            wc.enabled = false;
            wc.nearClipPlane = .05f;
            wcGo.GetComponent<AudioListener>().enabled = false;
            ws.workCamera = wc;

            WordSpaceBuilder.SyncJamos(ws);
            ec.PlaceAtStart();

            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[별말씀] '{word}' 공간 씬을 만들었어요: {scenePath}");
        }

        static Material GroundMaterial(string word)
        {
            string path = $"{WordSpaceBuilder.WordFolder(word)}/Materials/바닥.mat";
            WordSpaceBuilder.EnsureFolder($"{WordSpaceBuilder.WordFolder(word)}/Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = WordSpaceBuilder.MakeDefaultMaterial(new Color(.25f, .26f, .3f));
            if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void AddToBuildSettings(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void SelectWordSpace()
        {
            var ws = Object.FindAnyObjectByType<WordSpace>();
            if (ws) Selection.activeGameObject = ws.gameObject;
        }

        class OtherWordWindow : EditorWindow
        {
            string word = "";
            public static void Open()
            {
                var w = GetWindow<OtherWordWindow>(true, "다른 단어로 공간 씬 만들기");
                w.minSize = w.maxSize = new Vector2(320, 90);
            }
            void OnGUI()
            {
                word = EditorGUILayout.TextField("단어", word);
                GUI.enabled = !string.IsNullOrWhiteSpace(word);
                if (GUILayout.Button("만들기 / 열기")) { Close(); CreateOrOpen(word.Trim()); }
                GUI.enabled = true;
            }
        }
    }
}
