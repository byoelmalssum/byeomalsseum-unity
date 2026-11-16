using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Byeolmalsseum.EditorTools
{
    [CustomEditor(typeof(WordSpace))]
    public class WordSpaceEditor : Editor
    {
        LayoutBackup pickedBackup;

        public override void OnInspectorGUI()
        {
            var ws = (WordSpace)target;
            var pieces = ws.Pieces;
            int placed = pieces.Count(p => p.IsPlaced);
            bool playing = Application.isPlaying;

            // ── 상태
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"「{ws.word}」 공간", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            if (playing)
                EditorGUILayout.LabelField("현재 단계", ws.CurrentPhase.ToString());

            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, pieces.Length > 0 ? placed / (float)pieces.Length : 0, $"공간에 옮긴 자모 {placed} / {pieces.Length}");
            if (!playing && placed < pieces.Length)
            {
                EditorGUILayout.HelpBox("모든 자모가 공간 어딘가에 들어가야 해요. 아직 처음 자리에 있는 자모가 있어요.", MessageType.Info);
                if (GUILayout.Button($"안 옮긴 자모 선택하기 ({pieces.Length - placed}개)"))
                    Selection.objects = pieces.Where(p => !p.IsPlaced).Select(p => (Object)p.gameObject).ToArray();
            }

            // ── 게임 뷰 카메라
            Section("게임 뷰 카메라");
            using (new EditorGUILayout.HorizontalScope())
            {
                bool workOn = ws.WorkCameraActive;
                if (Toggle(!workOn, "체험 카메라")) SetWork(ws, false);
                if (Toggle(workOn, "작업용 카메라")) SetWork(ws, true);
            }
            using (new EditorGUI.DisabledScope(playing))
            {
                if (GUILayout.Button("작업용 카메라를 지금 Scene 뷰 시점으로 옮기기") && ws.workCamera && SceneView.lastActiveSceneView)
                {
                    var sc = SceneView.lastActiveSceneView.camera.transform;
                    Undo.RecordObject(ws.workCamera.transform, "작업 카메라 이동");
                    ws.workCamera.transform.SetPositionAndRotation(sc.position, sc.rotation);
                    SetWork(ws, true);
                }
            }
            EditorGUILayout.LabelField("플레이 중에는 Tab 키로 전환", EditorStyles.miniLabel);

            // ── 체험 시작 지점 · 이동 범위
            Section("체험 시작 지점 · 이동 범위");
            var ec = ws.experienceCamera;
            using (new EditorGUI.DisabledScope(playing || ec == null))
            {
                if (ec != null && ec.startPoint == null)
                {
                    EditorGUILayout.HelpBox("시작 지점이 없어요 (예전 버전 씬). 버튼을 누르면 지금 체험 카메라 자리에 만들어져요.\n그다음 '체험 카메라 경로'는 지워도 돼요.", MessageType.Warning);
                    if (GUILayout.Button("체험 시작 지점 만들기", GUILayout.Height(26)))
                    {
                        ec.PlaceAtStart();
                        var go = new GameObject("체험 시작 지점");
                        Undo.RegisterCreatedObjectUndo(go, "시작 지점 만들기");
                        go.transform.SetParent(ws.transform, false);
                        go.transform.SetPositionAndRotation(ec.transform.position, ec.transform.rotation);
                        Undo.RecordObject(ec, "시작 지점 연결");
                        ec.startPoint = go.transform;
                        Selection.activeGameObject = go;
                    }
                }
                else if (ec != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("시작 지점 선택")) Selection.activeGameObject = ec.startPoint.gameObject;
                        if (GUILayout.Button("지금 Scene 뷰 시점으로") && SceneView.lastActiveSceneView)
                        {
                            var sc = SceneView.lastActiveSceneView.camera.transform;
                            Undo.RecordObject(ec.startPoint, "시작 지점 이동");
                            ec.startPoint.SetPositionAndRotation(sc.position, sc.rotation);
                        }
                    }
                    if (GUILayout.Button("체험 카메라를 시작 지점에 놓기 (Game 뷰로 확인)"))
                    {
                        Undo.RecordObject(ec.transform, "체험 카메라 이동");
                        ec.PlaceAtStart();
                    }
                }
            }
            var mb = ws.ComputeMoveBounds();
            EditorGUILayout.LabelField(mb.HasValue
                ? $"이동 범위: 가로 {mb.Value.size.x:0}m × 높이 {mb.Value.size.y:0}m × 세로 {mb.Value.size.z:0}m ({ws.boundsMode})"
                : "이동 범위: 제한 없음", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("이 오브젝트를 선택하면 Scene 뷰에 하늘색 상자로 범위가 보여요.", EditorStyles.miniLabel);

            // ── 백업
            Section("백업 (공간 날아감 방지)");
            using (new EditorGUI.DisabledScope(playing))
            {
                if (GUILayout.Button("지금 배치 저장 (백업)", GUILayout.Height(28)))
                {
                    string p = WordSpaceBuilder.SaveBackup(ws, false);
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(p));
                    Debug.Log($"[별말씀] 백업 저장: {p} (+ 씬 통째 복사본)");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    pickedBackup = (LayoutBackup)EditorGUILayout.ObjectField(pickedBackup, typeof(LayoutBackup), false);
                    using (new EditorGUI.DisabledScope(pickedBackup == null))
                        if (GUILayout.Button("이 백업 불러오기", GUILayout.Width(110)))
                        {
                            if (EditorUtility.DisplayDialog("배치 불러오기",
                                $"'{pickedBackup.name}' ({pickedBackup.savedAt}) 배치로 자모 위치를 되돌릴까요?\n\n지금 상태는 자동 백업되고, Ctrl+Z로도 취소할 수 있어요.", "불러오기", "취소"))
                            {
                                var (applied, missing) = WordSpaceBuilder.ApplyBackup(ws, pickedBackup);
                                Debug.Log($"[별말씀] 배치 불러오기 완료: {applied}개 적용, {missing}개 못 찾음");
                            }
                        }
                }
                if (GUILayout.Button("백업 폴더 보기"))
                {
                    WordSpaceBuilder.EnsureFolder(WordSpaceBuilder.BackupFolder(ws.word));
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(WordSpaceBuilder.BackupFolder(ws.word)));
                }
                EditorGUILayout.LabelField("씬을 저장(Ctrl+S)할 때마다 Backups/Auto에 자동 백업돼요 (최근 20개).", EditorStyles.wordWrappedMiniLabel);
            }

            // ── 자모 다시 만들기
            Section("자모 관리");
            using (new EditorGUI.DisabledScope(playing))
            {
                if (GUILayout.Button("자모 모양 다시 만들기 (자리·크기·머티리얼 유지)"))
                {
                    if (EditorUtility.DisplayDialog("자모 모양 다시 만들기",
                        "모양 데이터만 새로 고쳐요. 옮겨 둔 위치·회전·크기·머티리얼은 그대로 유지되고, 빠진 자모만 새로 생겨요.\n(실행 전 자동 백업)", "실행", "취소"))
                    {
                        WordSpaceBuilder.SaveBackup(ws, true);
                        var res = WordSpaceBuilder.SyncJamos(ws);
                        Debug.Log($"[별말씀] 자모 갱신 {res.updated}개, 새로 생성 {res.created}개" +
                                  (res.extras.Count > 0 ? $", 단어에 없는 조각 {res.extras.Count}개: {string.Join(", ", res.extras)}" : ""));
                    }
                }
            }

            // ── 설정값
            Section("설정");
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("word"), new GUIContent("단어 (변경 불가)"));
            DrawPropertiesExcluding(serializedObject, "m_Script", "word");
            serializedObject.ApplyModifiedProperties();
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static bool Toggle(bool on, string label)
        {
            var c = GUI.backgroundColor;
            if (on) GUI.backgroundColor = new Color(.5f, .85f, 1f);
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.backgroundColor = c;
            return clicked;
        }

        static void SetWork(WordSpace ws, bool on)
        {
            if (!ws.workCamera || !ws.experienceCamera) return;
            if (!Application.isPlaying)
            {
                Undo.RecordObjects(new Object[] {
                    ws.workCamera, ws.experienceCamera.Cam,
                    ws.workCamera.GetComponent<AudioListener>(), ws.experienceCamera.GetComponent<AudioListener>()
                }.Where(o => o).ToArray(), "게임 뷰 카메라 전환");
                ws.workCamera.enabled = on;
                ws.experienceCamera.Cam.enabled = !on;
                var wl = ws.workCamera.GetComponent<AudioListener>(); if (wl) wl.enabled = on;
                var el = ws.experienceCamera.GetComponent<AudioListener>(); if (el) el.enabled = !on;
            }
            else ws.SetWorkCameraActive(on);
        }
    }
}
