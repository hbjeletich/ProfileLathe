using UnityEngine;

namespace ProfileLathe
{
    /// <summary>
    /// Bakes a tangent-space normal map from a procedural height field, so fine
    /// surface relief (vertical flutes) can be rendered without adding geometry.
    /// The height field is differentiated with a Sobel filter to recover surface
    /// gradients, which are packed into an RGBA normal texture.
    /// </summary>
    public static class SurfaceBaker
    {
        /// <summary>
        /// Generate a tiling normal map of vertical flutes. U wraps around the
        /// revolve, so the cosine pattern repeats <paramref name="fluteCount"/>
        /// times around the surface.
        /// </summary>
        public static Texture2D BakeFlutes(int resolution, int fluteCount, float depth, float strength = 6f)
        {
            resolution = Mathf.Clamp(resolution, 8, 4096);
            float[,] heights = new float[resolution, resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)(resolution - 1);
                    float h = (Mathf.Cos(u * fluteCount * Mathf.PI * 2f) * 0.5f + 0.5f) * depth;
                    heights[x, y] = h;
                }
            }

            return HeightFieldToNormalMap(heights, resolution, strength);
        }

        /// <summary>
        /// Sobel-filter a square height field into a tangent-space normal map.
        /// Public so other relief sources (textures, noise) can reuse it.
        /// </summary>
        public static Texture2D HeightFieldToNormalMap(float[,] heights, int resolution, float strength)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true)
            {
                name = "LatheNormalMap",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float tl = Sample(heights, x - 1, y + 1, resolution);
                    float t = Sample(heights, x, y + 1, resolution);
                    float tr = Sample(heights, x + 1, y + 1, resolution);
                    float l = Sample(heights, x - 1, y, resolution);
                    float r = Sample(heights, x + 1, y, resolution);
                    float bl = Sample(heights, x - 1, y - 1, resolution);
                    float b = Sample(heights, x, y - 1, resolution);
                    float br = Sample(heights, x + 1, y - 1, resolution);

                    float dX = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dY = (tl + 2f * t + tr) - (bl + 2f * b + br);
                    dX *= strength;
                    dY *= strength;

                    Vector3 n = new Vector3(-dX, -dY, 1f).normalized;
                    pixels[y * resolution + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        static float Sample(float[,] h, int x, int y, int res)
        {
            // wrap X (around the revolve), clamp Y (top/bottom of the form)
            x = ((x % res) + res) % res;
            y = Mathf.Clamp(y, 0, res - 1);
            return h[x, y];
        }
    }
}
