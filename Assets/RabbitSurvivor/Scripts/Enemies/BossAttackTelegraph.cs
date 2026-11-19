using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    internal sealed class BossAttackTelegraph
    {
        const int ArcSegments = 24;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly LineRenderer outline;
        readonly Color tint;
        readonly Vector3[] boundary = new Vector3[ArcSegments + 2];
        readonly List<Vector3> vertices = new List<Vector3>(ArcSegments + 3);
        readonly List<Color> colors = new List<Color>(ArcSegments + 3);
        readonly List<Vector2> uv = new List<Vector2>(ArcSegments + 3);
        readonly List<int> triangles = new List<int>((ArcSegments + 2) * 3);

        internal BossAttackTelegraph(Transform parent, Material material, string name, Color color)
        {
            root = new GameObject(name);
            root.transform.SetParent(parent, false);
            tint = color;
            mesh = new Mesh { name = name + " Fill" };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var fill = root.AddComponent<MeshRenderer>();
            fill.sharedMaterial = material;
            fill.sortingOrder = 1;
            fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fill.receiveShadows = false;

            outline = new GameObject("Warning Outline").AddComponent<LineRenderer>();
            outline.transform.SetParent(root.transform, false);
            outline.sharedMaterial = material;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.sortingOrder = 1;
            outline.widthMultiplier = .055f;
            outline.numCapVertices = 0;
            outline.numCornerVertices = 0;
            root.SetActive(false);
        }

        internal void Rectangle(Vector2 center, Vector2 direction, float length, float width, float progress)
        {
            boundary[0] = new Vector3(-length * .5f, -width * .5f);
            boundary[1] = new Vector3(-length * .5f, width * .5f);
            boundary[2] = new Vector3(length * .5f, width * .5f);
            boundary[3] = new Vector3(length * .5f, -width * .5f);
            Draw(center, direction, 4, progress);
        }

        internal void Sector(Vector2 origin, Vector2 direction, float radius, float degrees, float progress)
        {
            boundary[0] = Vector3.zero;
            for (int index = 0; index <= ArcSegments; index++) {
                float angle = Mathf.Lerp(-degrees * .5f, degrees * .5f, index / (float)ArcSegments) * Mathf.Deg2Rad;
                boundary[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Draw(origin, direction, ArcSegments + 2, progress);
        }

        internal void Circle(Vector2 center, float radius, float progress)
        {
            for (int index = 0; index < ArcSegments; index++) {
                float angle = index * 2f * Mathf.PI / ArcSegments;
                boundary[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Draw(center, Vector2.right, ArcSegments, progress);
        }

        void Draw(Vector2 center, Vector2 direction, int count, float progress)
        {
            progress = Mathf.Clamp01(progress);
            Vector3 parentScale = root.transform.parent.lossyScale;
            root.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            root.transform.SetPositionAndRotation(new Vector3(center.x, center.y, -.02f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));

            vertices.Clear();
            colors.Clear();
            uv.Clear();
            triangles.Clear();
            Vector3 middle = Vector3.zero;
            for (int index = 0; index < count; index++)
                middle += boundary[index];
            middle /= count;
            Color fill = tint;
            fill.a = Mathf.Lerp(.14f, .32f, progress);
            vertices.Add(middle);
            colors.Add(fill);
            uv.Add(Vector2.one * .5f);
            outline.positionCount = count;
            for (int index = 0; index < count; index++) {
                vertices.Add(boundary[index]);
                colors.Add(fill);
                uv.Add(Vector2.one * .5f);
                outline.SetPosition(index, boundary[index]);
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add((index + 1) % count + 1);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            Color edge = tint;
            edge.a = Mathf.Lerp(.65f, 1f, progress);
            outline.startColor = outline.endColor = edge;
            root.SetActive(true);
        }

        internal void Hide() => root.SetActive(false);

        internal void Dispose()
        {
            Object.Destroy(mesh);
            Object.Destroy(root);
        }
    }
}
