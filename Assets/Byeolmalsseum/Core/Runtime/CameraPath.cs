using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>
    /// 체험 카메라가 반복해서 도는 경로.
    /// 이 오브젝트의 '자식 오브젝트'들이 경로 점 (위에서부터 순서대로).
    /// 점을 씬에서 그냥 옮기면 됨. 추가는 인스펙터 버튼 / 삭제는 Delete 키.
    /// </summary>
    public class CameraPath : MonoBehaviour
    {
        [Tooltip("켜면 마지막 점 → 첫 점으로 이어지는 고리 경로")]
        public bool closed = true;
        [Range(4, 64)] public int samplesPerSegment = 24;
        public Color gizmoColor = new Color(.3f, 1f, .8f);

        readonly List<Vector3> samples = new List<Vector3>();
        readonly List<float> cumulative = new List<float>();

        public int PointCount => transform.childCount;
        public bool IsValid => transform.childCount >= 2;
        public float Length { get { Rebuild(); return cumulative.Count > 0 ? cumulative[cumulative.Count - 1] : 0f; } }

        Vector3 P(int i)
        {
            int n = transform.childCount;
            if (closed) i = ((i % n) + n) % n;
            else i = Mathf.Clamp(i, 0, n - 1);
            return transform.GetChild(i).position;
        }

        // Catmull-Rom
        static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>점 위치로 곡선 샘플 + 누적 거리 계산</summary>
        public void Rebuild()
        {
            samples.Clear(); cumulative.Clear();
            int n = transform.childCount;
            if (n < 2) return;
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
                for (int k = 0; k < samplesPerSegment; k++)
                    samples.Add(CR(P(s - 1), P(s), P(s + 1), P(s + 2), k / (float)samplesPerSegment));
            samples.Add(closed ? P(0) : P(n - 1));

            cumulative.Add(0);
            for (int i = 1; i < samples.Count; i++)
                cumulative.Add(cumulative[i - 1] + Vector3.Distance(samples[i - 1], samples[i]));
        }

        /// <summary>경로 시작점에서 distance 만큼 간 위치 (속도 일정). 끝을 넘으면 처음부터 반복.</summary>
        public Vector3 EvaluateByDistance(float distance)
        {
            if (samples.Count == 0) Rebuild();
            if (samples.Count == 0) return transform.position;
            float total = cumulative[cumulative.Count - 1];
            if (total <= 1e-4f) return samples[0];
            distance = Mathf.Repeat(distance, total);

            int lo = 0, hi = cumulative.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cumulative[mid] < distance) lo = mid; else hi = mid;
            }
            float segLen = cumulative[hi] - cumulative[lo];
            float t = segLen > 1e-6f ? (distance - cumulative[lo]) / segLen : 0f;
            return Vector3.Lerp(samples[lo], samples[hi], t);
        }

        void OnDrawGizmos()
        {
            Rebuild();
            if (samples.Count < 2) return;
            Gizmos.color = gizmoColor;
            for (int i = 1; i < samples.Count; i++) Gizmos.DrawLine(samples[i - 1], samples[i]);
            for (int i = 0; i < transform.childCount; i++)
            {
                Gizmos.color = i == 0 ? Color.yellow : gizmoColor;
                Gizmos.DrawSphere(transform.GetChild(i).position, i == 0 ? .25f : .15f);
            }
            // 진행 방향 화살표
            Vector3 a = EvaluateByDistance(0.5f), b = EvaluateByDistance(1.2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(b, .1f);
        }
    }
}
