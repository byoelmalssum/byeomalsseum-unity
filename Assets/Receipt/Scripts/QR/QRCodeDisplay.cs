using System;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;

// [역할] 문자열(영수증 주소) → QR 코드 이미지 → RawImage에 표시
// 필요: Assets/Plugins/zxing.unity.dll  (ZXing.Net 릴리스 zip 안의 unity 폴더)
public class QRCodeDisplay : MonoBehaviour
{
    public RawImage target;
    [Tooltip("QR 텍스처 해상도. 256 권장 (화면에서 크게 보이는 건 RawImage 크기로 조절)")]
    public int size = 256;

    Texture2D _tex;

    public void Show(string text)
    {
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = size,
                Height = size,
                Margin = 1,
                CharacterSet = "UTF-8",
            }
        };
        Color32[] px = writer.Write(text);
        int w = size;
        int h = px.Length / w;

        // ZXing은 위→아래 순서, Unity Texture2D는 아래→위 순서라 세로로 뒤집는다
        var flipped = new Color32[px.Length];
        for (int y = 0; y < h; y++)
            Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);

        if (_tex == null || _tex.width != w || _tex.height != h)
        {
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // 흐릿해지면 폰이 못 읽으니 Point
                wrapMode = TextureWrapMode.Clamp
            };
        }
        _tex.SetPixels32(flipped);
        _tex.Apply();

        target.texture = _tex;
        target.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (target) target.gameObject.SetActive(false);
    }
}
