using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Debug = UnityEngine.Debug;
using Rect = UnityEngine.Rect;

// 손 인식만 하는 가벼운 버전: 웹캠 → MediaPipe Hand Landmarker → StarGrab.SetHand
// MediaPipe 샘플 폴더(Assets/MediaPipeUnity) 없이 플러그인 패키지 + 모델 파일만 있으면 동작
public class HandTracker : MonoBehaviour
{
    [Header("모델 (Assets/StreamingAssets 안)")]
    public string modelFile = "hand_landmarker.bytes";

    [Header("웹캠")]
    public int deviceIndex = 0;
    public int width = 640;
    public int height = 480;
    public int fps = 30;
    [Tooltip("거울 모드: 손을 오른쪽으로 움직이면 화면에서도 오른쪽")]
    public bool mirror = true;

    [Header("확인용")]
    [Tooltip("왼쪽 위에 작은 카메라 화면과 손 점을 보여줌 (전시 땐 끄기)")]
    public bool showPreview = true;

    WebCamTexture webcam;
    Texture2D frame;
    Color32[] raw, flipped;
    HandLandmarker landmarker;
    HandLandmarkerResult result;
    Stopwatch clock;
    bool ready;
    Vector3[] lastHand;

    IEnumerator Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, modelFile);
        if (!File.Exists(path))
        {
            Debug.LogError($"[손 인식] 모델 파일이 없어요: {path}\nAssets/StreamingAssets 폴더에 {modelFile} 을 넣어 주세요.");
            yield break;
        }

        var options = new HandLandmarkerOptions(
            new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: File.ReadAllBytes(path)),
            runningMode: RunningMode.VIDEO,
            numHands: 1);
        landmarker = HandLandmarker.CreateFromOptions(options);

        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("[손 인식] 웹캠을 찾을 수 없어요.");
            yield break;
        }
        int idx = Mathf.Clamp(deviceIndex, 0, WebCamTexture.devices.Length - 1);
        webcam = new WebCamTexture(WebCamTexture.devices[idx].name, width, height, fps);
        webcam.Play();
        yield return new WaitUntil(() => webcam.width > 16);

        frame = new Texture2D(webcam.width, webcam.height, TextureFormat.RGBA32, false);
        raw = new Color32[webcam.width * webcam.height];
        flipped = new Color32[raw.Length];
        result = HandLandmarkerResult.Alloc(1);
        clock = Stopwatch.StartNew();
        ready = true;
        Debug.Log($"[손 인식] 시작: {WebCamTexture.devices[idx].name} {webcam.width}x{webcam.height}");
    }

    void Update()
    {
        if (!ready || !webcam.didUpdateThisFrame) return;
        int w = webcam.width, h = webcam.height;
        if (w != frame.width || h != frame.height) return;

        // 유니티 텍스처는 아래→위 순서라서, MediaPipe가 원하는 위→아래로 뒤집기 (+ 거울 모드면 좌우도)
        webcam.GetPixels32(raw);
        for (int y = 0; y < h; y++)
        {
            int src = (h - 1 - y) * w, dst = y * w;
            if (!mirror) System.Array.Copy(raw, src, flipped, dst, w);
            else for (int x = 0; x < w; x++) flipped[dst + x] = raw[src + w - 1 - x];
        }
        frame.SetPixels32(flipped);

        using (var image = new Image(ImageFormat.Types.Format.Srgba, frame))
        {
            if (landmarker.TryDetectForVideo(image, clock.ElapsedMilliseconds, null, ref result)
                && result.handLandmarks != null && result.handLandmarks.Count > 0)
            {
                var lm = result.handLandmarks[0].landmarks;
                var pts = new Vector3[lm.Count];
                for (int i = 0; i < lm.Count; i++) pts[i] = new Vector3(lm[i].x, lm[i].y, lm[i].z);
                lastHand = pts;
                StarGrab.SetHand(pts);
            }
            else
            {
                lastHand = null;
                StarGrab.SetHand(null);
            }
        }

        if (showPreview) frame.Apply(false);
    }

    void OnGUI()
    {
        if (!showPreview || !ready) return;
        var r = new Rect(10, 10, 240, 240f * frame.height / frame.width);
        GUI.DrawTextureWithTexCoords(r, frame, new Rect(0, 1, 1, -1));   // 뒤집어 둔 걸 다시 바로 보이게
        if (lastHand == null) return;
        foreach (var p in lastHand)
            GUI.DrawTexture(new Rect(r.x + p.x * r.width - 2, r.y + p.y * r.height - 2, 4, 4), Texture2D.whiteTexture);
    }

    void OnDestroy()
    {
        if (webcam != null) webcam.Stop();
        landmarker?.Close();
        if (frame) Destroy(frame);
    }
}
