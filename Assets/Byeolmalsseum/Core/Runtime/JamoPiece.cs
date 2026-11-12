using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 자모 조각 하나 (초성 / 중성 / 종성 단위).
    /// ▶ 이 오브젝트(껍데기)의 위치·회전·크기 = 공간 속 최종 자리. 작업자가 옮기는 건 이거.
    /// ▶ 자식 'Visual'(알맹이) = 눈에 보이는 자모. 날아가는 연출은 알맹이만 움직임.
    /// 연출 코드는 껍데기 Transform에 절대 쓰지 않음 → 연출 코드를 고쳐도 배치가 안 날아감.
    /// </summary>
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    public class JamoPiece : MonoBehaviour
    {
        [Header("정보 (자동 생성 — 수정 X)")]
        public string id;
        public int syllableIndex;
        public string syllable;
        public JamoRole role;
        public string jamo;

        // 글자 원형 모양 데이터: 획들의 점을 한 줄로 이어 붙이고, 획마다 점 개수를 따로 저장
        [HideInInspector] public Vector2[] points;
        [HideInInspector] public int[] strokeLengths;
        [HideInInspector] public Vector2 pivot;          // 자모 중심 (글자 원형 좌표)
        [HideInInspector] public Vector3 spawnPosition;  // 처음 생성된 자리 (안 옮긴 자모 찾기용)
        [HideInInspector] public Transform visual;

        Mesh mesh;

        public bool IsPlaced => (transform.position - spawnPosition).sqrMagnitude > 0.0025f;

        public List<Vector2[]> Strokes
        {
            get
            {
                var list = new List<Vector2[]>();
                if (points == null || strokeLengths == null) return list;
                int k = 0;
                foreach (int len in strokeLengths)
                {
                    if (k + len > points.Length) break;
                    var s = new Vector2[len];
                    System.Array.Copy(points, k, s, 0, len);
                    list.Add(s);
                    k += len;
                }
                return list;
            }
        }

        public void SetStrokes(List<Vector2[]> strokes)
        {
            var pts = new List<Vector2>();
            var lens = new int[strokes.Count];
            for (int i = 0; i < strokes.Count; i++) { pts.AddRange(strokes[i]); lens[i] = strokes[i].Length; }
            points = pts.ToArray();
            strokeLengths = lens;
        }

        void OnEnable() => RebuildMesh();

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        public void RebuildMesh()
        {
            if (points == null || points.Length < 2 || visual == null) return;
            var ws = GetComponentInParent<WordSpace>(true);
            float th = ws ? ws.strokeThickness : .07f;
            float dp = ws ? ws.strokeDepth : .2f;

            if (mesh == null) mesh = new Mesh { hideFlags = HideFlags.DontSave }; // 씬에 저장 안 함 (매번 데이터로 다시 만듦)
            StrokeMeshBuilder.Build(Strokes, pivot, th, dp, mesh);
            var mf = visual.GetComponent<MeshFilter>();
            if (mf) mf.sharedMesh = mesh;
        }
    }
}
