using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// [퍼르퍼르] 막대를 따라 천을 한 줄로 자동으로 거는 도구
//  - '공간' 아래에 빈 오브젝트를 만들고 이 스크립트를 붙인다. (자모 아래에 두지 말 것!)
//  - 빈 오브젝트를 막대 바로 아래에 두고, 빨간 화살표(X축)가 막대 방향을 따라가게 회전시킨다.
//  - Inspector 제목 오른쪽 ⋮ → "천 걸기 (다시 만들기)" 를 누르면 천이 줄줄이 생긴다.
//  - 마음에 안 들면 Seed 숫자만 바꾸고 다시 누르면 다른 배치가 나온다.
//  - 이 오브젝트의 Scale은 (1, 1, 1)로 둘 것.
public class ClothLine : MonoBehaviour
{
    [Header("재료")]
    [Tooltip("Prefabs 폴더의 천 프리팹")]
    public GameObject clothPrefab;
    [Tooltip("섞어 쓸 천 재질들 (크림, 살구, 하늘, 분홍, 흰)")]
    public Material[] materials;

    [Header("줄")]
    [Tooltip("천을 거는 줄의 길이 (m). 빨간 화살표 방향으로 깔림")]
    public float lineLength = 10f;
    [Tooltip("천 사이 간격 (m)")]
    public float spacing = 0.7f;
    [Tooltip("중간중간 비워 둘 확률 (0~1)")]
    [Range(0f, 1f)] public float skipChance = 0.15f;

    [Header("천 크기 (m)")]
    public Vector2 widthRange = new Vector2(0.6f, 1.0f);
    public Vector2 lengthRange = new Vector2(2.5f, 5.5f);

    [Header("통로 (문에 걸 때)")]
    [Tooltip("가운데 이 폭(m) 안의 천은 짧게 만들어 사람이 지나가게 함. 0이면 통로 없음")]
    public float passageWidth = 0f;
    public Vector2 passageLengthRange = new Vector2(1.2f, 2.2f);

    [Header("기타")]
    [Tooltip("바꾸면 다른 랜덤 배치")]
    public int seed = 1;
    [Tooltip("천이 거꾸로(고정된 쪽이 아래로) 매달리면 체크")]
    public bool flip = false;

    const float PlaneSize = 10f; // Unity 기본 Plane = 10m x 10m

    [ContextMenu("천 걸기 (다시 만들기)")]
    public void Build()
    {
        Clear();
        if (clothPrefab == null)
        {
            Debug.LogWarning("[ClothLine] Cloth Prefab 칸이 비어 있어요");
            return;
        }

        var rng = new System.Random(seed);
        int count = Mathf.Max(1, Mathf.FloorToInt(lineLength / Mathf.Max(0.1f, spacing)) + 1);
        float start = -lineLength * 0.5f;

        for (int i = 0; i < count; i++)
        {
            if (rng.NextDouble() < skipChance) continue;

            float x = start + i * spacing;
            bool inPassage = Mathf.Abs(x) < passageWidth * 0.5f;
            float len = RandomIn(inPassage ? passageLengthRange : lengthRange, rng);
            float w = RandomIn(widthRange, rng);

            GameObject go = Spawn();
            go.name = "천_" + i.ToString("00");
            Transform t = go.transform;
            t.SetParent(transform, false);
            t.localRotation = Quaternion.Euler(flip ? -90f : 90f, 0f, 0f);
            t.localScale = new Vector3(w / PlaneSize, 1f, len / PlaneSize);
            t.localPosition = new Vector3(x, -len * 0.5f, (float)(rng.NextDouble() - 0.5) * 0.2f);

            if (materials != null && materials.Length > 0)
            {
                Renderer r = go.GetComponentInChildren<Renderer>();
                Material m = materials[rng.Next(materials.Length)];
                if (r != null && m != null) r.sharedMaterial = m;
            }
        }

        PeureuWind wind = GetComponentInParent<PeureuWind>();
        if (wind != null) wind.Refresh();
    }

    [ContextMenu("천 모두 지우기")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(c);
                continue;
            }
#endif
            Destroy(c);
        }
    }

    GameObject Spawn()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject o = (GameObject)PrefabUtility.InstantiatePrefab(clothPrefab);
            Undo.RegisterCreatedObjectUndo(o, "천 걸기");
            return o;
        }
#endif
        return Instantiate(clothPrefab);
    }

    static float RandomIn(Vector2 range, System.Random rng)
    {
        return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.6f, 0.9f);
        Gizmos.DrawLine(new Vector3(-lineLength * 0.5f, 0f, 0f), new Vector3(lineLength * 0.5f, 0f, 0f));
        if (passageWidth > 0f)
        {
            Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, -1f, 0f), new Vector3(passageWidth, 2f, 0.1f));
        }
    }
}
