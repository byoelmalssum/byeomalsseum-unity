using UnityEditor;
using UnityEngine;

namespace Byeolmalsseum.EditorTools
{
    [CustomEditor(typeof(CameraPath))]
    public class CameraPathEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var path = (CameraPath)target;
            EditorGUILayout.HelpBox(
                "자식 오브젝트 = 경로 점 (위에서부터 순서대로).\n" +
                "• 이 오브젝트를 선택하면 Scene 뷰에서 모든 점을 바로 끌어 옮길 수 있어요.\n" +
                "• 노란 점 = 시작점 (글자가 이 앞에 나타나요).\n" +
                "• 점 삭제: 하이어라키에서 Delete / 순서 바꾸기: 하이어라키에서 드래그", MessageType.None);

            EditorGUILayout.LabelField($"점 {path.PointCount}개 · 한 바퀴 길이 {path.Length:0.0} m");
            var ws = path.GetComponentInParent<WordSpace>();
            if (ws && ws.experienceCamera && ws.experienceCamera.speed > 0)
                EditorGUILayout.LabelField($"한 바퀴 시간 ≈ {path.Length / ws.experienceCamera.speed:0} 초 (속도 {ws.experienceCamera.speed} m/s)");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("경로점 추가 (끝에)")) AddPoint(path, path.PointCount);
                var sel = Selection.activeTransform;
                bool childSel = sel && sel.parent == path.transform;
                using (new EditorGUI.DisabledScope(!childSel))
                    if (GUILayout.Button("선택한 점 뒤에 추가") && childSel) AddPoint(path, sel.GetSiblingIndex() + 1);
            }
            if (GUILayout.Button("점 이름 번호 다시 매기기"))
                for (int i = 0; i < path.PointCount; i++)
                {
                    Undo.RecordObject(path.transform.GetChild(i).gameObject, "이름 정리");
                    path.transform.GetChild(i).name = $"경로점 {i + 1}";
                }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        static void AddPoint(CameraPath path, int index)
        {
            int n = path.PointCount;
            Vector3 pos;
            if (n == 0) pos = path.transform.position + Vector3.up * 1.6f;
            else if (n == 1) pos = path.transform.GetChild(0).position + Vector3.right * 3f;
            else
            {
                // 앞 점과 다음 점 사이 (끝이면 진행 방향으로 3m)
                Transform prev = path.transform.GetChild(Mathf.Clamp(index - 1, 0, n - 1));
                if (index < n) pos = (prev.position + path.transform.GetChild(index).position) * .5f;
                else
                {
                    Transform pp = path.transform.GetChild(n - 2);
                    pos = prev.position + (prev.position - pp.position).normalized * 3f;
                }
            }
            var go = new GameObject($"경로점 {index + 1}");
            Undo.RegisterCreatedObjectUndo(go, "경로점 추가");
            go.transform.SetParent(path.transform, true);
            go.transform.position = pos;
            go.transform.SetSiblingIndex(index);
            Selection.activeGameObject = path.gameObject;
        }

        void OnSceneGUI()
        {
            var path = (CameraPath)target;
            for (int i = 0; i < path.PointCount; i++)
            {
                Transform t = path.transform.GetChild(i);
                Handles.Label(t.position + Vector3.up * .4f, (i + 1).ToString(), EditorStyles.whiteLargeLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.PositionHandle(t.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "경로점 이동");
                    t.position = np;
                }
            }
        }
    }
}
