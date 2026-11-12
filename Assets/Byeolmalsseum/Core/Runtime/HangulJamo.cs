using System.Collections.Generic;
using System.Text;

namespace Byeolmalsseum
{
    /// <summary>
    /// 한글 음절 → 초성/중성/종성 분해 & 재조립 유틸리티
    /// 공식: code = 음절 - 0xAC00
    ///       초성 = code / 588   (588 = 중성 21 × 종성 28)
    ///       중성 = (code % 588) / 28
    ///       종성 = code % 28    (0이면 받침 없음)
    /// </summary>
    public static class HangulJamo
    {
        public const int SyllableStart = 0xAC00; // '가'
        public const int SyllableEnd   = 0xD7A3; // '힣'

        public static readonly string[] Choseong =
        {
            "ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ","ㅅ",
            "ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
        }; // 19개

        public static readonly string[] Jungseong =
        {
            "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ","ㅘ",
            "ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"
        }; // 21개

        public static readonly string[] Jongseong =
        {
            "",  "ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ","ㄺ",
            "ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ","ㅄ","ㅅ",
            "ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
        }; // 28개 (0 = 받침 없음)

        /// <summary>겹자모 → 기본 자모 (획 조각 단계에서 더 잘게 쪼갤 때 사용)</summary>
        public static readonly Dictionary<string, string[]> CompoundSplit = new Dictionary<string, string[]>
        {
            // 쌍자음
            {"ㄲ", new[]{"ㄱ","ㄱ"}}, {"ㄸ", new[]{"ㄷ","ㄷ"}}, {"ㅃ", new[]{"ㅂ","ㅂ"}},
            {"ㅆ", new[]{"ㅅ","ㅅ"}}, {"ㅉ", new[]{"ㅈ","ㅈ"}},
            // 겹받침
            {"ㄳ", new[]{"ㄱ","ㅅ"}}, {"ㄵ", new[]{"ㄴ","ㅈ"}}, {"ㄶ", new[]{"ㄴ","ㅎ"}},
            {"ㄺ", new[]{"ㄹ","ㄱ"}}, {"ㄻ", new[]{"ㄹ","ㅁ"}}, {"ㄼ", new[]{"ㄹ","ㅂ"}},
            {"ㄽ", new[]{"ㄹ","ㅅ"}}, {"ㄾ", new[]{"ㄹ","ㅌ"}}, {"ㄿ", new[]{"ㄹ","ㅍ"}},
            {"ㅀ", new[]{"ㄹ","ㅎ"}}, {"ㅄ", new[]{"ㅂ","ㅅ"}},
            // 복합 모음
            {"ㅘ", new[]{"ㅗ","ㅏ"}}, {"ㅙ", new[]{"ㅗ","ㅐ"}}, {"ㅚ", new[]{"ㅗ","ㅣ"}},
            {"ㅝ", new[]{"ㅜ","ㅓ"}}, {"ㅞ", new[]{"ㅜ","ㅔ"}}, {"ㅟ", new[]{"ㅜ","ㅣ"}},
            {"ㅢ", new[]{"ㅡ","ㅣ"}},
        };

        /// <summary>한 음절 분해 결과</summary>
        public struct Syllable
        {
            public char Original;
            public bool IsHangul;
            public int ChoIndex, JungIndex, JongIndex;

            public string Cho  => IsHangul ? Choseong[ChoIndex]   : "";
            public string Jung => IsHangul ? Jungseong[JungIndex] : "";
            public string Jong => IsHangul ? Jongseong[JongIndex] : "";
            public bool HasJong => IsHangul && JongIndex != 0;

            /// <summary>초·중·종 자모 목록 (받침 없으면 2개)</summary>
            public List<string> Jamos()
            {
                var list = new List<string>();
                if (!IsHangul) { list.Add(Original.ToString()); return list; }
                list.Add(Cho);
                list.Add(Jung);
                if (HasJong) list.Add(Jong);
                return list;
            }

            /// <summary>겹자모까지 기본 자모로 쪼갠 목록 (예: 값 → ㄱ ㅏ ㅂ ㅅ)</summary>
            public List<string> BasicJamos()
            {
                var result = new List<string>();
                foreach (var j in Jamos())
                {
                    if (CompoundSplit.TryGetValue(j, out var parts)) result.AddRange(parts);
                    else result.Add(j);
                }
                return result;
            }

            public override string ToString()
            {
                if (!IsHangul) return $"'{Original}' (한글 아님)";
                return $"'{Original}' U+{(int)Original:X4} → 초성 {Cho}({ChoIndex}) / 중성 {Jung}({JungIndex}) / 종성 {(HasJong ? Jong : "없음")}({JongIndex})";
            }
        }

        public static bool IsHangulSyllable(char c) => c >= SyllableStart && c <= SyllableEnd;

        /// <summary>한 글자 분해</summary>
        public static Syllable Decompose(char c)
        {
            var s = new Syllable { Original = c, IsHangul = IsHangulSyllable(c) };
            if (!s.IsHangul) return s;

            int code = c - SyllableStart;
            s.ChoIndex  = code / 588;
            s.JungIndex = (code % 588) / 28;
            s.JongIndex = code % 28;
            return s;
        }

        /// <summary>단어 전체 분해</summary>
        public static List<Syllable> Decompose(string word)
        {
            var list = new List<Syllable>();
            if (string.IsNullOrEmpty(word)) return list;
            foreach (char c in word) list.Add(Decompose(c));
            return list;
        }

        /// <summary>인덱스로 재조립 (재조립 연출용)</summary>
        public static char Compose(int cho, int jung, int jong = 0)
        {
            return (char)(SyllableStart + cho * 588 + jung * 28 + jong);
        }

        /// <summary>자모 문자열로 재조립 (예: "ㅇ","ㅠ","ㄴ" → '윤'). 실패하면 '\0'</summary>
        public static char Compose(string cho, string jung, string jong = "")
        {
            int ci = System.Array.IndexOf(Choseong, cho);
            int ji = System.Array.IndexOf(Jungseong, jung);
            int ki = System.Array.IndexOf(Jongseong, jong ?? "");
            if (ci < 0 || ji < 0 || ki < 0) return '\0';
            return Compose(ci, ji, ki);
        }

        /// <summary>디버그용: 단어 전체를 보기 좋은 문자열로</summary>
        public static string Describe(string word, bool splitCompound = false)
        {
            var sb = new StringBuilder();
            foreach (var s in Decompose(word))
            {
                sb.AppendLine(s.ToString());
                if (s.IsHangul)
                {
                    var jamos = splitCompound ? s.BasicJamos() : s.Jamos();
                    sb.AppendLine("    자모: " + string.Join(" · ", jamos));
                }
            }
            return sb.ToString();
        }
    }
}
