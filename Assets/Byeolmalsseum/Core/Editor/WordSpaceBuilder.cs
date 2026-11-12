using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Byeolmalsseum.EditorTools
{
    /// <summary>자모 조각 생성·동기화, 머티리얼, 백업 저장/복원 (에디터 전용)</summary>
    public static class WordSpaceBuilder
    {
        public const string WordsRoot = "Assets/Words";
        public const float SpawnScale = 1.5f; // 처음 생성될 때 글자 한 칸 크기 (m)

        public static string WordFolder(string word) => $"{WordsRoot}/{word}";
        public static string BackupFolder(string word) => $"{WordFolder(word)}/Backups";
        public static string AutoBackupFolder(string word) => $"{BackupFolder(word)}/Auto";

        static readonly Color[] RoleColors =
        {
            new Color(1f, .78f, .3f),   // 초성
            new Color(.4f, .72f, 1f),   // 중성
            new Color(1f, .45f, .68f),  // 종성
        };

        // ───────────────────────────── 폴더
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ───────────────────────────── 머티리얼 (초성/중성/종성)
        public static Material[] EnsureMaterials(string word)
        {
            string folder = $"{WordFolder(word)}/Materials";
            EnsureFolder(folder);
            var mats = new Material[3];
            for (int r = 0; r < 3; r++)
            {
                string name = ((JamoRole)r).ToString();
                string path = $"{folder}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = MakeDefaultMaterial(RoleColors[r]);
                    AssetDatabase.CreateAsset(m, path);
                }
                mats[r] = m;
            }
            return mats;
        }

        public static Material MakeDefaultMaterial(Color c)
        {
            // 현재 렌더 파이프라인 기본 머티리얼을 복사 (URP/HDRP/Built-in 모두 대응)
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
            UnityEngine.Object.DestroyImmediate(tmp);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .6f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * .25f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        // ───────────────────────────── 자모 만들기 / 다시 만들기
        public class SyncResult
        {
            public int created, updated;
            public List<string> extras = new List<string>();
        }

        /// <summary>
        /// 단어에 맞게 자모(초·중·종성) 조각을 만들거나 갱신.
        /// 이미 있는 자모(같은 ID)는 모양 데이터만 새로 고치고 위치·회전·크기·머티리얼은 그대로 둠.
        /// 없는 자모만 처음 글자 자리에 새로 만듦. 단어에 없는 조각은 지우지 않고 알려만 줌.
        /// </summary>
        public static SyncResult SyncJamos(WordSpace ws)
        {
            var result = new SyncResult();
            if (ws.strokesRoot == null)
            {
                var r = new GameObject("글자 (자모를 옮겨서 공간 만들기)").transform;
                Undo.RegisterCreatedObjectUndo(r.gameObject, "글자 루트");
                r.SetParent(ws.transform, false);
                ws.strokesRoot = r;
            }

            var expected = WordGlyph.BuildJamos(ws.word);
            var mats = EnsureMaterials(ws.word);

            var pieceById = new Dictionary<string, JamoPiece>();
            foreach (var p in ws.Pieces) if (!string.IsNullOrEmpty(p.id) && !pieceById.ContainsKey(p.id)) pieceById[p.id] = p;
            var sylById = new Dictionary<string, SyllableGroup>();
            foreach (var g in ws.strokesRoot.GetComponentsInChildren<SyllableGroup>(true))
                if (!string.IsNullOrEmpty(g.id) && !sylById.ContainsKey(g.id)) sylById[g.id] = g;

            Rect b = WordGlyph.Bounds(expected.SelectMany(e => e.strokes));
            Vector3 origin = ws.transform.position;
            Func<Vector2, Vector3> spawn = v =>
                origin + new Vector3((v.x - b.center.x) * SpawnScale, (v.y - b.yMin) * SpawnScale + 1f, 0);

            foreach (var sylGroup in expected.GroupBy(e => e.syllableIndex))
            {
                var first = sylGroup.First();
                string sid = WordGlyph.SyllableId(first.syllableIndex);
                if (!sylById.TryGetValue(sid, out var sg))
                {
                    var go = new GameObject($"{first.syllableIndex + 1} {first.syllable}");
                    Undo.RegisterCreatedObjectUndo(go, "음절 그룹 생성");
                    go.transform.SetParent(ws.strokesRoot, false);
                    go.transform.position = Average(sylGroup.Select(e => spawn(e.Center)));
                    sg = go.AddComponent<SyllableGroup>();
                    sg.id = sid; sg.syllable = first.syllable.ToString();
                    sylById[sid] = sg;
                }

                foreach (var e in sylGroup)
                {
                    if (pieceById.TryGetValue(e.id, out var piece))
                    {
                        Undo.RecordObject(piece, "자모 모양 갱신");
                        Fill(piece, e);
                        piece.RebuildMesh();
                        EditorUtility.SetDirty(piece);
                        result.updated++;
                    }
                    else
                    {
                        piece = NewPiece(e, sg.transform, spawn(e.Center), mats[(int)e.role]);
                        pieceById[e.id] = piece;
                        result.created++;
                    }
                }
            }

            var expectedIds = new HashSet<string>(expected.Select(e => e.id));
            foreach (var kv in pieceById)
                if (!expectedIds.Contains(kv.Key)) result.extras.Add(kv.Value.name);

            EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
            return result;
        }

        static Vector3 Average(IEnumerable<Vector3> v)
        {
            Vector3 s = Vector3.zero; int n = 0;
            foreach (var x in v) { s += x; n++; }
            return n > 0 ? s / n : Vector3.zero;
        }

        static void Fill(JamoPiece p, GlyphJamo e)
        {
            p.id = e.id;
            p.syllableIndex = e.syllableIndex;
            p.syllable = e.syllable.ToString();
            p.role = e.role;
            p.jamo = e.jamo;
            p.SetStrokes(e.strokes);
            p.pivot = e.Center;
        }

        static JamoPiece NewPiece(GlyphJamo e, Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject($"{e.role} {e.jamo}");
            Undo.RegisterCreatedObjectUndo(go, "자모 생성");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * SpawnScale;

            var vis = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            vis.transform.SetParent(go.transform, false);
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var p = go.AddComponent<JamoPiece>();
            p.visual = vis.transform;
            Fill(p, e);
            p.spawnPosition = pos;
            p.RebuildMesh();
            return p;
        }

        // ───────────────────────────── 백업
        public static string Stamp() => DateTime.Now.ToString("MMdd_HHmmss");

        /// <summary>배치 백업 저장. auto=false면 씬 파일 통째 복사본도 같이 저장.</summary>
        public static string SaveBackup(WordSpace ws, bool auto)
        {
            string folder = auto ? AutoBackupFolder(ws.word) : BackupFolder(ws.word);
            EnsureFolder(folder);
            string stamp = Stamp();

            var bk = ScriptableObject.CreateInstance<LayoutBackup>();
            bk.word = ws.word;
            bk.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (ws.strokesRoot)
            {
                foreach (var g in ws.strokesRoot.GetComponentsInChildren<SyllableGroup>(true)) bk.entries.Add(Entry(g.id, true, g.transform));
                foreach (var p in ws.Pieces) bk.entries.Add(Entry(p.id, false, p.transform));
            }
            string path = $"{folder}/{ws.word}_배치_{stamp}.asset";
            AssetDatabase.CreateAsset(bk, path);

            if (!auto)
            {
                var scene = ws.gameObject.scene;
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                if (!string.IsNullOrEmpty(scene.path))
                    AssetDatabase.CopyAsset(scene.path, $"{folder}/{ws.word}_씬통째_{stamp}.unity");
            }
            else PruneAuto(folder, 20);

            AssetDatabase.SaveAssets();
            return path;
        }

        static LayoutBackup.Entry Entry(string id, bool isGroup, Transform t) => new LayoutBackup.Entry
        {
            id = id, isGroup = isGroup,
            localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale
        };

        static void PruneAuto(string folder, int keep)
        {
            var files = AssetDatabase.FindAssets("t:LayoutBackup", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder)
                .OrderByDescending(p => p).ToList();
            for (int i = keep; i < files.Count; i++) AssetDatabase.DeleteAsset(files[i]);
        }

        /// <summary>백업 적용 (적용 전에 현재 상태를 자동 백업). 실행 취소(Ctrl+Z) 가능.</summary>
        public static (int applied, int missing) ApplyBackup(WordSpace ws, LayoutBackup bk)
        {
            SaveBackup(ws, true);
            var map = new Dictionary<string, Transform>();
            foreach (var g in ws.strokesRoot.GetComponentsInChildren<SyllableGroup>(true)) map["G" + g.id] = g.transform;
            foreach (var p in ws.Pieces) map["P" + p.id] = p.transform;

            int applied = 0, missing = 0;
            foreach (var e in bk.entries)
            {
                if (!map.TryGetValue((e.isGroup ? "G" : "P") + e.id, out var t)) { missing++; continue; }
                Undo.RecordObject(t, "배치 불러오기");
                t.localPosition = e.localPosition;
                t.localRotation = e.localRotation;
                t.localScale = e.localScale;
                applied++;
            }
            EditorSceneManager.MarkSceneDirty(ws.gameObject.scene);
            return (applied, missing);
        }
    }
}
