using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// [역할] '영수증' 씬 담당 (체험 STEP 4 이후)
//   단어 공간 [체험 종료] → (Next Scene Name = 영수증) → 이 씬
//   → WordSpace.LastFinishedWord 로 방금 체험한 단어 확인
//   → 첫인상 남기기 → QR 영수증 → 시간 지나면 우주 씬으로
public class ReceiptSceneController : MonoBehaviour
{
    [Serializable]
    public class WordEntry
    {
        public string word;
        [TextArea(2, 4)] public string meaning;
    }

    public FirstImpressionUI firstImpression;

    [Tooltip("영수증 끝나고 돌아갈 씬 (별이 떠 있는 첫 화면)")]
    public string universeScene = "우주";

    [Tooltip("이 씬만 단독으로 Play했을 때 쓸 단어")]
    public string testWord = "길차다";

    [Tooltip("단어별 진짜 뜻 — 팀 기획서 문구로 확인해서 넣기")]
    public List<WordEntry> words = new List<WordEntry>
    {
        new WordEntry { word = "길차다",     meaning = "(뜻을 넣어 주세요)" },
        new WordEntry { word = "논틀밭틀",   meaning = "(뜻을 넣어 주세요)" },
        new WordEntry { word = "살천스럽다", meaning = "(뜻을 넣어 주세요)" },
        new WordEntry { word = "퍼르퍼르",   meaning = "(뜻을 넣어 주세요)" },
    };

    void Awake()
    {
        if (firstImpression) firstImpression.openOnStart = false;
    }

    IEnumerator Start()
    {
        if (!firstImpression) { Debug.LogError("[별말씀] ReceiptSceneController: First Impression 칸을 채워 주세요"); yield break; }

        yield return null; // FirstImpressionUI가 화면을 다 만든 다음에 열기

        string word = GetLastFinishedWord();
        if (string.IsNullOrEmpty(word))
        {
            word = testWord;
            Debug.Log($"[별말씀] 단어 공간을 거치지 않고 실행 → 테스트 단어 '{word}' 사용");
        }

        var entry = words.Find(w => w.word == word);
        if (entry == null) Debug.LogWarning($"[별말씀] '{word}'의 뜻이 words 목록에 없어요");
        string meaning = entry != null ? entry.meaning : "";

        firstImpression.Finished += BackToUniverse;
        firstImpression.Open(word, meaning);
    }

    // 팀 공용 코드의 WordSpace.LastFinishedWord 값을 읽는다.
    // (팀 코드의 namespace·어셈블리 구조와 상관없이 이름으로 찾아서 읽음 → 팀 코드가 바뀌어도 컴파일 에러 안 남)
    static string GetLastFinishedWord()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }

            foreach (var t in types)
            {
                if (t == null || t.Name != "WordSpace") continue;
                var p = t.GetProperty("LastFinishedWord", flags);
                if (p != null) return p.GetValue(null) as string;
                var f = t.GetField("LastFinishedWord", flags);
                if (f != null) return f.GetValue(null) as string;
                Debug.LogWarning("[별말씀] WordSpace는 찾았는데 LastFinishedWord가 없어요");
                return null;
            }
        }
        Debug.LogWarning("[별말씀] 팀 코드 WordSpace를 못 찾았어요 (Byeolmalsseum 패키지가 들어 있는지 확인)");
        return null;
    }

    void BackToUniverse()
    {
        if (Application.CanStreamedLevelBeLoaded(universeScene))
            SceneManager.LoadScene(universeScene);
        else
            Debug.LogWarning($"[별말씀] '{universeScene}' 씬이 Build Profiles 씬 목록에 없어서 돌아가지 못했어요");
    }
}
