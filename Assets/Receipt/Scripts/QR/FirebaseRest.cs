using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// [역할] Firebase Realtime Database에 REST로 읽고 쓰기 (UnityWebRequest만 사용, Firebase SDK 설치 불필요)
//   - Put       : 영수증 저장
//   - Get       : 값 읽기 (8단계 행성 재배치 팀원도 이걸로 stats 읽으면 됨)
//   - Increment : 카운터 +1 (여러 관객이 동시에 눌러도 숫자가 안 꼬이게 ETag 조건부 요청 사용)
public class FirebaseRest : MonoBehaviour
{
    [Tooltip("Realtime Database 주소. 끝에 / 없이. 예) https://byeolmalsseum-default-rtdb.asia-southeast1.firebasedatabase.app")]
    public string databaseUrl = "https://YOUR-PROJECT-default-rtdb.asia-southeast1.firebasedatabase.app";
    public int timeoutSeconds = 10;

    // "stats/윤슬/visits" → ".../stats/%EC%9C%A4%EC%8A%AC/visits.json" (한글 경로 안전하게)
    string Url(string path)
    {
        var parts = path.Trim('/').Split('/');
        for (int i = 0; i < parts.Length; i++) parts[i] = Uri.EscapeDataString(parts[i]);
        return databaseUrl.TrimEnd('/') + "/" + string.Join("/", parts) + ".json";
    }

    UnityWebRequest MakePut(string path, string json)
    {
        var req = new UnityWebRequest(Url(path), "PUT");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.timeout = timeoutSeconds;
        return req;
    }

    public IEnumerator Put(string path, string json, Action<bool> done = null)
    {
        using (var req = MakePut(path, json))
        {
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogWarning($"[Firebase] PUT {path} 실패: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            done?.Invoke(ok);
        }
    }

    public IEnumerator Get(string path, Action<bool, string> done)
    {
        using (var req = UnityWebRequest.Get(Url(path)))
        {
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogWarning($"[Firebase] GET {path} 실패: {req.responseCode} {req.error}");
            done?.Invoke(ok, ok ? req.downloadHandler.text : null);
        }
    }

    // 읽기(ETag 받기) → 'ETag가 그대로일 때만' 쓰기. 그 사이 누가 바꿨으면(412) 다시 시도
    public IEnumerator Increment(string path, int delta = 1, Action<bool> done = null)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            string etag = null;
            long current = 0;

            using (var get = UnityWebRequest.Get(Url(path)))
            {
                get.SetRequestHeader("X-Firebase-ETag", "true");
                get.timeout = timeoutSeconds;
                yield return get.SendWebRequest();
                if (get.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Firebase] 카운터 읽기 실패 {path}: {get.error}");
                    break;
                }
                etag = get.GetResponseHeader("ETag");
                long.TryParse(get.downloadHandler.text.Trim(), out current); // 처음엔 "null" → 0
            }
            if (string.IsNullOrEmpty(etag)) { Debug.LogWarning("[Firebase] ETag를 못 받음"); break; }

            using (var put = MakePut(path, (current + delta).ToString()))
            {
                put.SetRequestHeader("if-match", etag);
                yield return put.SendWebRequest();
                if (put.result == UnityWebRequest.Result.Success) { done?.Invoke(true); yield break; }
                if (put.responseCode != 412)
                {
                    Debug.LogWarning($"[Firebase] 카운터 쓰기 실패 {path}: {put.responseCode} {put.error}");
                    break;
                }
                // 412 = 동시에 다른 관객이 올림 → 반복
            }
        }
        done?.Invoke(false);
    }
}
