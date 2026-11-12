using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 자모 → 획(stroke) 데이터.
    /// 각 자모는 0~1 정사각형 안의 획 목록으로 정의. 획 하나 = 꺾은선(점 배열). ㅇ은 원 한 획.
    /// 나중에 Blender로 만든 획 메시로 바꿔 끼울 자리.
    /// </summary>
    public static class JamoStrokes
    {
        static Vector2 P(float x, float y) => new Vector2(x, y);

        static Vector2[] Circle(float cx, float cy, float r, int n = 20)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static readonly Dictionary<string, Vector2[][]> Basic = new Dictionary<string, Vector2[][]>
        {
            // ── 자음
            {"ㄱ", new[]{ new[]{P(.12f,.88f),P(.85f,.88f),P(.85f,.1f)} }},
            {"ㄴ", new[]{ new[]{P(.15f,.9f),P(.15f,.15f),P(.9f,.15f)} }},
            {"ㄷ", new[]{ new[]{P(.88f,.85f),P(.15f,.85f)}, new[]{P(.15f,.85f),P(.15f,.15f),P(.9f,.15f)} }},
            {"ㄹ", new[]{ new[]{P(.15f,.88f),P(.85f,.88f),P(.85f,.5f)}, new[]{P(.85f,.5f),P(.15f,.5f)}, new[]{P(.15f,.5f),P(.15f,.12f),P(.9f,.12f)} }},
            {"ㅁ", new[]{ new[]{P(.15f,.85f),P(.15f,.15f)}, new[]{P(.15f,.85f),P(.85f,.85f),P(.85f,.15f)}, new[]{P(.15f,.15f),P(.85f,.15f)} }},
            {"ㅂ", new[]{ new[]{P(.15f,.9f),P(.15f,.12f)}, new[]{P(.85f,.9f),P(.85f,.12f)}, new[]{P(.15f,.5f),P(.85f,.5f)}, new[]{P(.15f,.12f),P(.85f,.12f)} }},
            {"ㅅ", new[]{ new[]{P(.5f,.9f),P(.1f,.1f)}, new[]{P(.4f,.7f),P(.9f,.1f)} }},
            {"ㅇ", new[]{ Circle(.5f,.5f,.38f) }},
            {"ㅈ", new[]{ new[]{P(.15f,.88f),P(.85f,.88f)}, new[]{P(.5f,.88f),P(.1f,.1f)}, new[]{P(.4f,.68f),P(.9f,.1f)} }},
            {"ㅊ", new[]{ new[]{P(.5f,1f),P(.5f,.86f)}, new[]{P(.15f,.78f),P(.85f,.78f)}, new[]{P(.5f,.78f),P(.1f,.05f)}, new[]{P(.4f,.58f),P(.9f,.05f)} }},
            {"ㅋ", new[]{ new[]{P(.12f,.88f),P(.85f,.88f),P(.85f,.1f)}, new[]{P(.12f,.5f),P(.85f,.5f)} }},
            {"ㅌ", new[]{ new[]{P(.88f,.88f),P(.15f,.88f)}, new[]{P(.15f,.5f),P(.85f,.5f)}, new[]{P(.15f,.88f),P(.15f,.12f),P(.9f,.12f)} }},
            {"ㅍ", new[]{ new[]{P(.08f,.85f),P(.92f,.85f)}, new[]{P(.35f,.85f),P(.35f,.15f)}, new[]{P(.65f,.85f),P(.65f,.15f)}, new[]{P(.08f,.15f),P(.92f,.15f)} }},
            {"ㅎ", new[]{ new[]{P(.5f,1f),P(.5f,.86f)}, new[]{P(.15f,.76f),P(.85f,.76f)}, Circle(.5f,.32f,.27f) }},

            // ── 모음 (세로형)
            {"ㅏ", new[]{ new[]{P(.4f,.98f),P(.4f,.02f)}, new[]{P(.4f,.5f),P(.85f,.5f)} }},
            {"ㅐ", new[]{ new[]{P(.3f,.98f),P(.3f,.02f)}, new[]{P(.3f,.5f),P(.58f,.5f)}, new[]{P(.75f,.98f),P(.75f,.02f)} }},
            {"ㅑ", new[]{ new[]{P(.4f,.98f),P(.4f,.02f)}, new[]{P(.4f,.65f),P(.85f,.65f)}, new[]{P(.4f,.35f),P(.85f,.35f)} }},
            {"ㅒ", new[]{ new[]{P(.3f,.98f),P(.3f,.02f)}, new[]{P(.3f,.62f),P(.58f,.62f)}, new[]{P(.3f,.38f),P(.58f,.38f)}, new[]{P(.75f,.98f),P(.75f,.02f)} }},
            {"ㅓ", new[]{ new[]{P(.15f,.5f),P(.6f,.5f)}, new[]{P(.6f,.98f),P(.6f,.02f)} }},
            {"ㅔ", new[]{ new[]{P(.02f,.5f),P(.35f,.5f)}, new[]{P(.35f,.98f),P(.35f,.02f)}, new[]{P(.75f,.98f),P(.75f,.02f)} }},
            {"ㅕ", new[]{ new[]{P(.15f,.65f),P(.6f,.65f)}, new[]{P(.15f,.35f),P(.6f,.35f)}, new[]{P(.6f,.98f),P(.6f,.02f)} }},
            {"ㅖ", new[]{ new[]{P(.02f,.62f),P(.35f,.62f)}, new[]{P(.02f,.38f),P(.35f,.38f)}, new[]{P(.35f,.98f),P(.35f,.02f)}, new[]{P(.75f,.98f),P(.75f,.02f)} }},
            {"ㅣ", new[]{ new[]{P(.5f,.98f),P(.5f,.02f)} }},
            // ── 모음 (가로형)
            {"ㅗ", new[]{ new[]{P(.5f,.78f),P(.5f,.35f)}, new[]{P(.02f,.35f),P(.98f,.35f)} }},
            {"ㅛ", new[]{ new[]{P(.35f,.78f),P(.35f,.35f)}, new[]{P(.65f,.78f),P(.65f,.35f)}, new[]{P(.02f,.35f),P(.98f,.35f)} }},
            {"ㅜ", new[]{ new[]{P(.02f,.65f),P(.98f,.65f)}, new[]{P(.5f,.65f),P(.5f,.18f)} }},
            {"ㅠ", new[]{ new[]{P(.02f,.65f),P(.98f,.65f)}, new[]{P(.35f,.65f),P(.35f,.18f)}, new[]{P(.65f,.65f),P(.65f,.18f)} }},
            {"ㅡ", new[]{ new[]{P(.02f,.5f),P(.98f,.5f)} }},
        };

        static readonly HashSet<string> CompoundVowels = new HashSet<string> { "ㅘ", "ㅙ", "ㅚ", "ㅝ", "ㅞ", "ㅟ", "ㅢ" };

        /// <summary>자모 하나를 rect 안에 배치할 때, 기본 자모 단위 조각 목록 (겹자모는 2개로 나뉨)</summary>
        public static List<(string name, Rect rect)> Parts(string jamo, Rect r)
        {
            var list = new List<(string, Rect)>();
            if (HangulJamo.CompoundSplit.TryGetValue(jamo, out var parts))
            {
                if (CompoundVowels.Contains(jamo))
                {
                    // 가로 모음은 아래 왼쪽, 세로 모음은 오른쪽
                    list.Add((parts[0], new Rect(r.x, r.y, r.width * .62f, r.height * .45f)));
                    list.Add((parts[1], new Rect(r.x + r.width * .6f, r.y, r.width * .4f, r.height)));
                }
                else
                {
                    // 쌍자음/겹받침: 좌우 반반
                    float w = r.width * .5f;
                    list.Add((parts[0], new Rect(r.x, r.y, w * .95f, r.height)));
                    list.Add((parts[1], new Rect(r.x + w * 1.05f, r.y, w * .95f, r.height)));
                }
            }
            else list.Add((jamo, r));
            return list;
        }

        /// <summary>기본 자모의 획들을 rect 좌표로 변환해서 반환</summary>
        public static List<Vector2[]> Strokes(string basicJamo, Rect r)
        {
            var result = new List<Vector2[]>();
            if (!Basic.TryGetValue(basicJamo, out var strokes)) return result;
            foreach (var s in strokes)
            {
                var pts = new Vector2[s.Length];
                for (int i = 0; i < s.Length; i++)
                    pts[i] = new Vector2(r.x + s[i].x * r.width, r.y + s[i].y * r.height);
                result.Add(pts);
            }
            return result;
        }

        /// <summary>음절 칸(0~1) 안에서 초/중/종성 자리 계산 (모음 모양에 따라 모아쓰기)</summary>
        public static void Layout(HangulJamo.Syllable s, out Rect cho, out Rect jung, out Rect jong)
        {
            int j = s.JungIndex;
            bool vertical   = j <= 7 || j == 20;
            bool horizontal = j == 8 || j == 12 || j == 13 || j == 17 || j == 18;

            Rect top = s.HasJong ? new Rect(0, .36f, 1, .64f) : new Rect(0, 0, 1, 1);
            jong = new Rect(.12f, 0, .76f, .3f);

            if (vertical)
            {
                cho  = new Rect(0, top.y + top.height * .08f, .6f, top.height * .84f);
                jung = new Rect(.58f, top.y, .42f, top.height);
            }
            else if (horizontal)
            {
                cho  = new Rect(.15f, top.y + top.height * .45f, .7f, top.height * .55f);
                jung = new Rect(0, top.y, 1, top.height * .45f);
            }
            else
            {
                cho  = new Rect(0, top.y + top.height * .45f, .58f, top.height * .55f);
                jung = top;
            }
        }
    }
}
