using System.Collections.Generic;
using UnityEngine;

namespace ProfileLathe
{
    /// <summary>
    /// Builds a solid of revolution from a <see cref="LatheProfile"/>.
    /// The cross-section (X = radius, Y = height) is swept around the Y axis,
    /// emitting a ring of vertices per angular step and stitching adjacent rings
    /// into quads. Optionally seals the open ends with triangle fans.
    ///
    /// Vertex layout is ring-major: vertex(ring, p) = ring * profileLen + p,
    /// which keeps the quad indexing simple and cache-friendly.
    /// </summary>
    public static class LatheMeshBuilder
    {
        public static Mesh Build(LatheProfile profile, string meshName = "LatheMesh")
        {
            List<Vector2> section = profile.SampleCrossSection();
            int profLen = section.Count;
            if (profLen < 2)
                return new Mesh { name = meshName };

            int segments = Mathf.Max(3, profile.segments);
            float sweep = Mathf.Deg2Rad * profile.sweepDegrees;
            bool closed = profile.IsClosed;

            // When closed, the last ring coincides with the first, so we don't
            // duplicate it; an open sweep needs the extra terminating ring.
            int ringCount = closed ? segments : segments + 1;

            var verts = new List<Vector3>(ringCount * profLen);
            var uvs = new List<Vector2>(ringCount * profLen);
            var tris = new List<int>();

            // ── side vertices ──
            for (int r = 0; r < ringCount; r++)
            {
                float t = (r / (float)segments) * sweep;
                float cos = Mathf.Cos(t), sin = Mathf.Sin(t);
                for (int p = 0; p < profLen; p++)
                {
                    float radius = section[p].x;
                    float height = section[p].y;
                    verts.Add(new Vector3(radius * cos, height, radius * sin));
                    uvs.Add(new Vector2(r / (float)segments, p / (float)(profLen - 1)));
                }
            }

            // ── side quads ──
            int stride = profLen;
            for (int r = 0; r < segments; r++)
            {
                int r0 = r * stride;
                int r1 = (closed ? ((r + 1) % segments) : (r + 1)) * stride;
                for (int p = 0; p < profLen - 1; p++)
                {
                    int a = r0 + p, b = r0 + p + 1;
                    int c = r1 + p, d = r1 + p + 1;
                    // Clockwise winding so normals face outward (Unity is
                    // left-handed; CW front faces, unlike WebGL's CCW).
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }

            // ── caps ──
            if (profile.capEnds)
            {
                float baseY = section[0].y;
                float topY = section[profLen - 1].y;

                int bottomCenter = verts.Count;
                verts.Add(new Vector3(0f, baseY, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));

                int topCenter = verts.Count;
                verts.Add(new Vector3(0f, topY, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));

                int lastP = profLen - 1;
                for (int r = 0; r < segments; r++)
                {
                    int r0 = r * stride;
                    int r1 = (closed ? ((r + 1) % segments) : (r + 1)) * stride;
                    // bottom fan (faces down)
                    tris.Add(bottomCenter); tris.Add(r1); tris.Add(r0);
                    // top fan (faces up)
                    tris.Add(topCenter); tris.Add(r0 + lastP); tris.Add(r1 + lastP);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);

            if (profile.smoothShading)
            {
                mesh.RecalculateNormals();
            }
            else
            {
                // Flat shading needs per-face vertices; split then recalc.
                Flatten(mesh);
            }

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Convert an indexed mesh into one with unique vertices per triangle so
        /// each face gets a hard normal (flat shading).
        /// </summary>
        static void Flatten(Mesh mesh)
        {
            int[] srcTris = mesh.triangles;
            Vector3[] srcVerts = mesh.vertices;
            Vector2[] srcUV = mesh.uv;

            var verts = new Vector3[srcTris.Length];
            var uvs = new Vector2[srcTris.Length];
            var tris = new int[srcTris.Length];
            for (int i = 0; i < srcTris.Length; i++)
            {
                verts[i] = srcVerts[srcTris[i]];
                uvs[i] = srcUV.Length > 0 ? srcUV[srcTris[i]] : Vector2.zero;
                tris[i] = i;
            }
            mesh.Clear();
            mesh.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }
    }
}