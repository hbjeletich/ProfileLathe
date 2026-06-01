using UnityEditor;
using UnityEngine;
using ProfileLathe;

namespace ProfileLathe.EditorTools
{
    /// <summary>
    /// An IMGUI control that renders the cross-section profile in a rectangle and
    /// lets the user drag control points, click empty curve space to add a point,
    /// and right-click a point to delete it. Pure view/interaction logic — it
    /// mutates the <see cref="LatheProfile"/> it is handed and reports whether a
    /// change occurred so the window can rebuild the preview.
    /// </summary>
    public class ProfileEditorControl
    {
        const float PAD = 22f;
        const float HIT_RADIUS = 9f;

        int _dragIndex = -1;

        public bool Draw(Rect area, LatheProfile profile)
        {
            bool changed = false;

            // backdrop
            EditorGUI.DrawRect(area, new Color(0.14f, 0.16f, 0.18f));
            DrawGrid(area);
            DrawAxis(area);

            Handles.BeginGUI();

            // silhouette fill toward axis
            DrawSilhouette(area, profile);

            // profile polyline
            Handles.color = new Color(1f, 0.71f, 0.33f);
            for (int i = 0; i < profile.points.Count - 1; i++)
            {
                Handles.DrawAAPolyLine(2.5f,
                    ToPixel(area, profile.points[i]),
                    ToPixel(area, profile.points[i + 1]));
            }

            Event e = Event.current;

            // ── interaction ──
            if (e.type == EventType.MouseDown && area.Contains(e.mousePosition))
            {
                int hit = HitTest(area, profile, e.mousePosition);
                if (e.button == 1) // right-click delete
                {
                    if (hit >= 0 && profile.points.Count > 3)
                    {
                        profile.points.RemoveAt(hit);
                        changed = true;
                    }
                    e.Use();
                }
                else if (e.button == 0)
                {
                    if (hit < 0)
                    {
                        // add a point, ordered by height
                        ProfilePoint np = ToProfile(area, e.mousePosition);
                        int ins = profile.points.FindIndex(p => p.y > np.y);
                        if (ins < 0) ins = profile.points.Count;
                        profile.points.Insert(ins, np);
                        hit = ins;
                        changed = true;
                    }
                    _dragIndex = hit;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && _dragIndex >= 0)
            {
                profile.points[_dragIndex] = ToProfile(area, e.mousePosition);
                changed = true;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _dragIndex = -1;
            }

            // ── points ──
            for (int i = 0; i < profile.points.Count; i++)
            {
                Vector2 px = ToPixel(area, profile.points[i]);
                float radius = (i == _dragIndex) ? 6.5f : 5f;
                Handles.color = (i == _dragIndex)
                    ? new Color(1f, 0.84f, 0.59f)
                    : new Color(1f, 0.71f, 0.33f);
                Handles.DrawSolidDisc(px, Vector3.forward, radius);
                Handles.color = new Color(0.08f, 0.09f, 0.1f);
                Handles.DrawWireDisc(px, Vector3.forward, radius);
            }

            Handles.EndGUI();
            return changed;
        }

        // ── space mapping ──
        Vector2 ToPixel(Rect a, ProfilePoint p)
        {
            return new Vector2(
                a.x + PAD + p.x * (a.width - 2f * PAD),
                a.yMax - PAD - p.y * (a.height - 2f * PAD));
        }

        ProfilePoint ToProfile(Rect a, Vector2 px)
        {
            return new ProfilePoint(
                Mathf.Clamp01((px.x - a.x - PAD) / (a.width - 2f * PAD)),
                Mathf.Clamp01((a.yMax - PAD - px.y) / (a.height - 2f * PAD)));
        }

        int HitTest(Rect a, LatheProfile profile, Vector2 px)
        {
            for (int i = 0; i < profile.points.Count; i++)
            {
                if (Vector2.Distance(ToPixel(a, profile.points[i]), px) < HIT_RADIUS)
                    return i;
            }
            return -1;
        }

        // ── decoration ──
        void DrawGrid(Rect a)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.16f, 0.18f, 0.22f);
            for (int i = 0; i <= 4; i++)
            {
                float gx = a.x + PAD + i / 4f * (a.width - 2f * PAD);
                Handles.DrawLine(new Vector2(gx, a.y + PAD), new Vector2(gx, a.yMax - PAD));
                float gy = a.y + PAD + i / 4f * (a.height - 2f * PAD);
                Handles.DrawLine(new Vector2(a.x + PAD, gy), new Vector2(a.xMax - PAD, gy));
            }
            Handles.EndGUI();
        }

        void DrawAxis(Rect a)
        {
            Handles.BeginGUI();
            float ax = ToPixel(a, new ProfilePoint(0, 0)).x;
            Handles.color = new Color(0.43f, 0.66f, 1f);
            Handles.DrawDottedLine(
                new Vector3(ax, a.y + PAD - 6f),
                new Vector3(ax, a.yMax - PAD + 6f), 4f);
            GUI.color = new Color(0.43f, 0.66f, 1f);
            GUI.Label(new Rect(ax + 4f, a.y + 2f, 60f, 16f), "axis");
            GUI.color = Color.white;
            Handles.EndGUI();
        }

        void DrawSilhouette(Rect a, LatheProfile profile)
        {
            if (profile.points.Count < 2) return;
            float ax = ToPixel(a, new ProfilePoint(0, 0)).x;
            var poly = new System.Collections.Generic.List<Vector3>();
            poly.Add(new Vector2(ax, ToPixel(a, profile.points[0]).y));
            foreach (var p in profile.points) poly.Add(ToPixel(a, p));
            poly.Add(new Vector2(ax, ToPixel(a, profile.points[profile.points.Count - 1]).y));
            Handles.color = new Color(0.43f, 0.66f, 1f, 0.10f);
            Handles.DrawAAConvexPolygon(poly.ToArray());
        }
    }
}
