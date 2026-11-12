using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>자모 하나의 획들(꺾은선 여러 개) → 하나로 합쳐진 입체 메시</summary>
    public static class StrokeMeshBuilder
    {
        public static Mesh Build(IList<Vector2[]> strokes, Vector2 pivot, float thickness, float depth, Mesh reuse = null)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var pts in strokes)
            {
                if (pts == null) continue;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i] - pivot, b = pts[i + 1] - pivot;
                    Vector2 d = b - a;
                    float len = d.magnitude;
                    if (len < 1e-5f) continue;
                    AddBox(verts, normals, uvs, tris, (a + b) * .5f, d / len, len + thickness, thickness, depth);
                }
            }

            var mesh = reuse != null ? reuse : new Mesh();
            mesh.Clear();
            mesh.name = "자모 메시";
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                           Vector2 center, Vector2 dir, float length, float width, float depth)
        {
            Vector3 c = center;
            Vector3 u = new Vector3(dir.x, dir.y, 0) * (length * .5f);
            Vector3 w = new Vector3(-dir.y, dir.x, 0) * (width * .5f);
            Vector3 z = new Vector3(0, 0, depth * .5f);

            AddQuad(v, n, uv, t, c + z, u, w, z.normalized);
            AddQuad(v, n, uv, t, c - z, u, w, -z.normalized);
            AddQuad(v, n, uv, t, c + w, u, z, w.normalized);
            AddQuad(v, n, uv, t, c - w, u, z, -w.normalized);
            AddQuad(v, n, uv, t, c + u, w, z, u.normalized);
            AddQuad(v, n, uv, t, c - u, w, z, -u.normalized);
        }

        static void AddQuad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 center, Vector3 ax, Vector3 ay, Vector3 normal)
        {
            int s = v.Count;
            Vector3 p0 = center - ax - ay, p1 = center + ax - ay, p2 = center + ax + ay, p3 = center - ax + ay;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            for (int i = 0; i < 4; i++) n.Add(normal);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));

            // Unity 앞면 = 법선 쪽에서 봤을 때 시계방향. 방향이 반대면 뒤집음.
            bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0;
            if (!flip) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            else { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
        }
    }
}
