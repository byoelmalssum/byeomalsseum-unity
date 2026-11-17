using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Firebase에 저장되는 영수증 한 장
[Serializable]
public class ReceiptData
{
    public string id;
    public string word;          // 만난 순우리말
    public string meaning;       // 진짜 뜻
    public string impression;    // 관객이 쓴 첫인상 한 문장
    public string[] feelings;    // 고른 느낌 3개
    public float hueA, hueB, hueC; // 나만의 행성 색 (0~360) : 핵, 대기, 고리
    public long createdAt;       // 밀리초 (UTC)
}

// 다른 관객에게 실시간으로 보여 줄 첫인상 (단어별로 모임)
[Serializable]
public class ImpressionData
{
    public string impression;
    public string[] feelings;
    public long createdAt;
}

[Serializable]
public class FeelingColor
{
    public string feeling;
    [Range(0, 360)] public float hue;
}

// [역할] 체험 7단계: 첫인상 기록이 끝나면 → Firebase 저장 → QR 표시 → 통계 카운터 +1
//   6단계(첫인상 UI) 팀원은 기록 완료 시 Submit(...)만 호출하면 된다.
public class ReceiptManager : MonoBehaviour
{
    public FirebaseRest firebase;
    public QRCodeDisplay qr;

    [Tooltip("Firebase Hosting 주소. 끝에 / 없이. 예) https://byeolmalsseum.web.app")]
    public string hostingUrl = "https://YOUR-PROJECT.web.app";

    [Tooltip("느낌 단어 → 행성 색. FirstImpressionUI가 있으면 시작할 때 칩 색으로 자동 덮어씀")]
    public List<FeelingColor> feelingColors = new List<FeelingColor>
    {
        new FeelingColor { feeling = "설렘",     hue = 342 },
        new FeelingColor { feeling = "따뜻함",   hue = 29 },
        new FeelingColor { feeling = "포근함",   hue = 40 },
        new FeelingColor { feeling = "생동감",   hue = 99 },
        new FeelingColor { feeling = "신비로움", hue = 271 },
        new FeelingColor { feeling = "고요함",   hue = 224 },
        new FeelingColor { feeling = "그리움",   hue = 214 },
        new FeelingColor { feeling = "아련함",   hue = 317 },
        new FeelingColor { feeling = "쓸쓸함",   hue = 251 },
        new FeelingColor { feeling = "낯섦",     hue = 169 },
    };

    public UnityEvent<string> onReceiptReady = new UnityEvent<string>();  // QR 주소
    public UnityEvent onReceiptFailed = new UnityEvent();

    [Header("테스트 (인스펙터 ⋮ 메뉴 → Test Submit, 또는 T 키)")]
    public string testWord = "윤슬";
    public string testMeaning = "햇빛이나 달빛에 비치어 반짝이는 잔물결";
    public string testImpression = "물 위에 흩어진 빛 조각 같은 말일 것 같다";
    public string[] testFeelings = { "설렘", "신비로움", "고요함" };

    public bool IsBusy { get; private set; }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T)) TestSubmit();
#endif
    }

    [ContextMenu("Test Submit")]
    public void TestSubmit() => Submit(testWord, testMeaning, testImpression, testFeelings);

    public void Submit(string word, string meaning, string impression, string[] feelings)
    {
        if (IsBusy) return;
        StartCoroutine(SubmitRoutine(word, meaning, impression, feelings));
    }

    IEnumerator SubmitRoutine(string word, string meaning, string impression, string[] feelings)
    {
        IsBusy = true;
        var data = new ReceiptData
        {
            id = NewId(),
            word = word,
            meaning = meaning,
            impression = impression,
            feelings = feelings,
            hueA = HueOf(feelings, 0),
            hueB = HueOf(feelings, 1),
            hueC = HueOf(feelings, 2),
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        string json = JsonUtility.ToJson(data);

        // 1) 영수증 저장 (실패하면 2번 더 시도)
        bool saved = false;
        for (int i = 0; i < 3 && !saved; i++)
            yield return firebase.Put($"receipts/{data.id}", json, ok => saved = ok);

        if (!saved)
        {
            Debug.LogError("[별말씀] 영수증 저장 실패 (인터넷/DB 주소/규칙 확인)");
            IsBusy = false;
            onReceiptFailed?.Invoke();
            yield break;
        }

        // 2) 단어별 첫인상 목록에 추가 (영수증 페이지에 실시간으로 뜸)
        var imp = new ImpressionData { impression = impression, feelings = feelings, createdAt = data.createdAt };
        yield return firebase.Put($"impressions/{word}/{data.id}", JsonUtility.ToJson(imp));

        // 3) QR 표시
        string url = $"{hostingUrl.TrimEnd('/')}/?id={data.id}";
        qr.Show(url);
        Debug.Log($"[별말씀] 영수증 저장 완료 → {url}");
        onReceiptReady?.Invoke(url);

        // 4) 통계 카운터 (QR은 이미 떴으니 뒤에서 천천히)
        yield return firebase.Increment($"stats/{word}/visits");
        foreach (var f in feelings)
            if (!string.IsNullOrEmpty(f))
                yield return firebase.Increment($"stats/{word}/feelings/{f}");

        IsBusy = false;
    }

    float HueOf(string[] feelings, int index)
    {
        if (feelings == null || feelings.Length == 0) return 210f;
        string f = feelings[Mathf.Min(index, feelings.Length - 1)];
        foreach (var fc in feelingColors)
            if (fc.feeling == f) return fc.hue;
        return Mathf.Abs(f.GetHashCode()) % 360; // 목록에 없는 단어면 아무 색
    }

    // 헷갈리는 글자(0/O, 1/l/I) 뺀 8자리
    static string NewId()
    {
        const string chars = "abcdefghjkmnpqrstuvwxyz23456789";
        var c = new char[8];
        for (int i = 0; i < c.Length; i++) c[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(c);
    }
}
