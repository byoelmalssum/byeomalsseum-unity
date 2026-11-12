using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    public enum JamoRole { 초성 = 0, 중성 = 1, 종성 = 2 }

    /// <summary>획 하나에 대한 설계 정보 (단어 → 자모 분해 → 획)</summary>
    public class GlyphStroke
    {
        public string id;              // 고정 ID: "음절번호:역할:조각번호:획번호"
        public int syllableIndex;
        public char syllable;
        public JamoRole role;
        public string jamo;            // 역할 자모 (예: ㄺ)
        public string partJamo;        // 실제 획이 속한 기본 자모 (예: ㄹ)
        public int partIndex, strokeIndex;
        public Vector2[] points;       // 단어 좌표 (한 칸 = 1)
        public Vector2 Centroid
        {
            get
            {
                Vector2 c = Vector2.zero;
                foreach (var p in points) c += p;
                return c / points.Length;
            }
        }
    }

    /// <summary>자모 하나 (초성/중성/종성 단위 — 공간에서 움직이는 단위)</summary>
    public class GlyphJamo
    {
        public string id;              // 고정 ID: "음절번호:역할"
        public int syllableIndex;
        public char syllable;
        public JamoRole role;
        public string jamo;
        public List<Vector2[]> strokes = new List<Vector2[]>();
        public Vector2 Center => WordGlyph.Bounds(strokes).center;
    }

    /// <summary>단어 전체를 획 목록으로 만들어 주는 계산기. 위치는 '글자 원형' 좌표.</summary>
    public static class WordGlyph
    {
        public const float SyllableGap = 0.12f; // 음절 사이 간격 (칸 비율)

        public static string SyllableId(int i) => $"{i}";
        public static string JamoId(int i, JamoRole r) => $"{i}:{(int)r}";

        /// <summary>단어 → 자모(초·중·종성) 목록. 각 자모 안에 획들이 들어 있음.</summary>
        public static List<GlyphJamo> BuildJamos(string word)
        {
            var result = new List<GlyphJamo>();
            var map = new Dictionary<string, GlyphJamo>();
            foreach (var st in Build(word))
            {
                string id = JamoId(st.syllableIndex, st.role);
                if (!map.TryGetValue(id, out var j))
                {
                    j = new GlyphJamo { id = id, syllableIndex = st.syllableIndex, syllable = st.syllable, role = st.role, jamo = st.jamo };
                    map[id] = j;
                    result.Add(j);
                }
                j.strokes.Add(st.points);
            }
            return result;
        }

        public static List<GlyphStroke> Build(string word)
        {
            var list = new List<GlyphStroke>();
            var syllables = HangulJamo.Decompose(word ?? "");
            for (int i = 0; i < syllables.Count; i++)
            {
                var s = syllables[i];
                if (!s.IsHangul) continue;
                float ox = i * (1f + SyllableGap);

                JamoStrokes.Layout(s, out Rect cho, out Rect jung, out Rect jong);
                AddRole(list, s, i, JamoRole.초성, s.Cho, cho, ox);
                AddRole(list, s, i, JamoRole.중성, s.Jung, jung, ox);
                if (s.HasJong) AddRole(list, s, i, JamoRole.종성, s.Jong, jong, ox);
            }
            return list;
        }

        static void AddRole(List<GlyphStroke> list, HangulJamo.Syllable s, int si, JamoRole role, string jamo, Rect rect, float ox)
        {
            var parts = JamoStrokes.Parts(jamo, rect);
            for (int p = 0; p < parts.Count; p++)
            {
                var strokes = JamoStrokes.Strokes(parts[p].name, parts[p].rect);
                for (int k = 0; k < strokes.Count; k++)
                {
                    var pts = strokes[k];
                    for (int n = 0; n < pts.Length; n++) pts[n].x += ox;
                    list.Add(new GlyphStroke
                    {
                        id = $"{si}:{(int)role}:{p}:{k}",
                        syllableIndex = si,
                        syllable = s.Original,
                        role = role,
                        jamo = jamo,
                        partJamo = parts[p].name,
                        partIndex = p,
                        strokeIndex = k,
                        points = pts
                    });
                }
            }
        }

        /// <summary>단어 전체 크기 (칸 단위)</summary>
        public static Rect Bounds(IEnumerable<Vector2[]> strokes)
        {
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (var pts in strokes)
                foreach (var p in pts)
                {
                    if (!any) { min = max = p; any = true; }
                    min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : new Rect(0, 0, 1, 1);
        }
    }
}
