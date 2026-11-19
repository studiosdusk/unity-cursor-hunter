using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Soft radial wedges rendered behind the relic discovery UI.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RelicDiscoveryRays : MaskableGraphic
    {
        private const int RayCount = 12;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 center = rect.center;
            Color32 inner = color;
            Color32 middle = color;
            middle.a = (byte)(middle.a * 0.45f);
            Color32 outer = color;
            outer.a = 0;

            for (int i = 0; i < RayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / RayCount;
                float halfWidth = Mathf.PI / RayCount * (i % 2 == 0 ? 0.42f : 0.27f);
                Vector2 left = new Vector2(Mathf.Cos(angle - halfWidth), Mathf.Sin(angle - halfWidth));
                Vector2 right = new Vector2(Mathf.Cos(angle + halfWidth), Mathf.Sin(angle + halfWidth));
                Vector2 midLeft = center + left * radius * 0.35f;
                Vector2 midRight = center + right * radius * 0.35f;
                AddQuad(mesh, center, midLeft, midRight, center, inner, middle, middle, inner);
                AddQuad(mesh, midLeft, center + left * radius, center + right * radius,
                    midRight, middle, outer, outer, middle);
            }
        }

        private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, ca, Vector2.zero);
            mesh.AddVert(b, cb, Vector2.zero);
            mesh.AddVert(c, cc, Vector2.zero);
            mesh.AddVert(d, cd, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
