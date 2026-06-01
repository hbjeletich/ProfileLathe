using System.Collections.Generic;
using UnityEngine;

namespace ProfileLathe
{
    /// <summary>
    /// A single control point of the cross-section, expressed in profile space.
    /// X is the radius out from the lathe axis (>= 0); Y is the height up the axis.
    /// Stored in normalized 0..1 space so the editor and the mesh builder agree
    /// on a common coordinate system; world scale is applied at build time.
    /// </summary>
    [System.Serializable]
    public struct ProfilePoint
    {
        public float x;   // radius from axis
        public float y;   // height

        public ProfilePoint(float x, float y) { this.x = x; this.y = y; }
        public Vector2 ToVector2() => new Vector2(x, y);
    }

    /// <summary>
    /// How adjacent control points are joined when the profile is sampled.
    /// Linear connects them with straight segments; CatmullRom fits a smooth
    /// spline through them for organic shapes (vases, goblets).
    /// </summary>
    public enum ProfileInterpolation { Linear, CatmullRom }

    /// <summary>
    /// All data needed to produce a solid of revolution: the cross-section
    /// points plus the revolve and surface-relief settings. This is pure data —
    /// no Unity mesh logic lives here, so it serializes cleanly and can be reused
    /// outside the editor.
    /// </summary>
    [System.Serializable]
    public class LatheProfile
    {
        // ── cross-section ──
        public List<ProfilePoint> points = new List<ProfilePoint>();
        public ProfileInterpolation interpolation = ProfileInterpolation.CatmullRom;

        [Tooltip("Samples taken along the profile between control points. " +
                 "Higher values follow a Catmull-Rom curve more closely.")]
        [Range(2, 64)] public int curveSamples = 12;

        // ── world scale ──
        [Tooltip("World-space radius the profile's X = 1 maps to.")]
        public float worldRadius = 0.5f;
        [Tooltip("World-space height the profile's Y = 1 maps to.")]
        public float worldHeight = 1f;

        // ── revolve ──
        [Range(3, 256)] public int segments = 48;
        [Range(1f, 360f)] public float sweepDegrees = 360f;
        public bool capEnds = true;
        public bool smoothShading = true;

        // ── surface relief (normal-map bake) ──
        public bool enableFlutes = false;
        [Range(2, 64)] public int fluteCount = 16;
        [Range(0f, 1f)] public float fluteDepth = 0.4f;
        [Range(64, 2048)] public int normalMapResolution = 256;

        public LatheProfile() { LoadPreset(LathePreset.Vase); }

        public bool IsClosed => sweepDegrees >= 359.5f;

        // ── presets ──
        public enum LathePreset { Vase, Goblet, Bottle, Pawn }

        public void LoadPreset(LathePreset preset)
        {
            points.Clear();
            float[,] src = preset switch
            {
                LathePreset.Goblet => new float[,]
                    { {0.12f,0f},{0.20f,0.04f},{0.14f,0.60f},{0.14f,1.04f},{0.60f,1.20f},{0.80f,1.56f},{0.76f,2.00f} },
                LathePreset.Bottle => new float[,]
                    { {0.20f,0f},{0.64f,0.04f},{0.68f,0.80f},{0.66f,1.12f},{0.28f,1.32f},{0.24f,1.84f},{0.32f,2.00f} },
                LathePreset.Pawn => new float[,]
                    { {0.16f,0f},{0.52f,0.06f},{0.36f,0.28f},{0.20f,0.52f},{0.40f,0.80f},{0.24f,1.04f},{0.32f,1.32f},{0.44f,1.48f},{0.28f,1.64f},{0.36f,2.00f} },
                _ => new float[,]   // Vase
                    { {0.20f,0f},{0.68f,0.08f},{0.80f,0.44f},{0.52f,0.92f},{0.40f,1.32f},{0.60f,1.72f},{0.52f,2.00f} },
            };
            for (int i = 0; i < src.GetLength(0); i++)
                points.Add(new ProfilePoint(src[i, 0], src[i, 1] * 0.5f));
            // (preset Y values authored on a 0..2 scale; halved into 0..1 profile space)
        }

        /// <summary>
        /// Produce the final list of cross-section points the mesh builder revolves,
        /// applying interpolation and world scale. The result is in world units in
        /// the XY plane (X = radius, Y = height).
        /// </summary>
        public List<Vector2> SampleCrossSection()
        {
            var raw = new List<Vector2>(points.Count);
            foreach (var p in points)
                raw.Add(new Vector2(p.x * worldRadius, p.y * worldHeight));

            if (interpolation == ProfileInterpolation.Linear || raw.Count < 3)
                return raw;

            // Catmull-Rom through the control points, clamped endpoints.
            var outPts = new List<Vector2>();
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector2 p0 = raw[Mathf.Max(i - 1, 0)];
                Vector2 p1 = raw[i];
                Vector2 p2 = raw[i + 1];
                Vector2 p3 = raw[Mathf.Min(i + 2, raw.Count - 1)];

                int steps = (i == raw.Count - 2) ? curveSamples : curveSamples - 1;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / curveSamples;
                    if (i < raw.Count - 2 && s == curveSamples) continue; // avoid dupes at joins
                    outPts.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            return outPts;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public LatheProfile Clone()
        {
            var c = new LatheProfile
            {
                interpolation = interpolation,
                curveSamples = curveSamples,
                worldRadius = worldRadius,
                worldHeight = worldHeight,
                segments = segments,
                sweepDegrees = sweepDegrees,
                capEnds = capEnds,
                smoothShading = smoothShading,
                enableFlutes = enableFlutes,
                fluteCount = fluteCount,
                fluteDepth = fluteDepth,
                normalMapResolution = normalMapResolution,
            };
            c.points = new List<ProfilePoint>(points);
            return c;
        }
    }
}
