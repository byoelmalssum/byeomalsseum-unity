using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Byeolmalsseum.EditorTools
{
    [CustomEditor(typeof(JamoPiece)), CanEditMultipleObjects]
    public class JamoPieceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var p = (JamoPiece)target;
            EditorGUILayout.LabelField($"'{p.syllable}'의 {p.role} {p.jamo}", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            EditorGUILayout.LabelField("ID", p.id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(p.IsPlaced ? "✔ 공간에 옮겨짐" : "● 아직 처음 자리", EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"음절 '{p.syllable}' 그룹 선택"))
                {
                    var sel = targets.Cast<JamoPiece>().Select(t => t.GetComponentInParent<SyllableGroup>())
                        .Where(g => g).Select(g => (Object)g.gameObject).Distinct().ToArray();
                    if (sel.Length > 0) Selection.objects = sel;
                }
                if (GUILayout.Button("같은 음절의 자모 모두 선택"))
                {
                    var g = p.GetComponentInParent<SyllableGroup>();
                    if (g) Selection.objects = g.GetComponentsInChildren<JamoPiece>(true).Select(x => (Object)x.gameObject).ToArray();
                }
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("처음 글자 자리로 되돌리기"))
                    foreach (var t in targets.Cast<JamoPiece>())
                    {
                        Undo.RecordObject(t.transform, "자모 처음 자리로");
                        t.transform.position = t.spawnPosition;
                        t.transform.rotation = Quaternion.identity;
                        t.transform.localScale = Vector3.one * WordSpaceBuilder.SpawnScale;
                    }

            EditorGUILayout.HelpBox("이 오브젝트를 옮기고·돌리고·Scale을 바꿔서 공간에 넣으세요.\n색·재질은 자식 'Visual'의 머티리얼을 바꾸면 돼요.", MessageType.None);
        }
    }

    [CustomEditor(typeof(SyllableGroup))]
    public class SyllableGroupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var g = (SyllableGroup)target;
            var pieces = g.GetComponentsInChildren<JamoPiece>(true);
            EditorGUILayout.LabelField($"음절 '{g.syllable}' · 자모 {pieces.Length}개", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이 그룹을 옮기면 안의 초·중·종성이 한꺼번에 움직여요.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
                foreach (var p in pieces)
                    if (GUILayout.Button(p.name)) Selection.activeGameObject = p.gameObject;
        }
    }
}
