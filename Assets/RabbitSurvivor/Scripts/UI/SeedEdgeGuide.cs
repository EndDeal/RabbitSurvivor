using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    internal sealed class SeedEdgeGuide
    {
        const float BubbleSize = 20f;
        const float MarkerSpacing = 28f;
        const float EdgeInset = 17f;

        readonly Canvas canvas;
        readonly RectTransform layer;
        readonly Marker[] markers;
        Rect visibleArea;
        Rect markerArea;
        float scale;
        int shown;

        struct Marker
        {
            public RectTransform root;
            public Image outline;
            public Image icon;
            public RectTransform tail;
            public Vector2 position;
        }

        public SeedEdgeGuide(Sprite bubble, Sprite tail, int capacity)
        {
            canvas = new GameObject("Seed Edge Guides", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            layer = (RectTransform)canvas.transform;

            markers = new Marker[capacity];
            for (int index = 0; index < markers.Length; index++) {
                var root = (RectTransform)new GameObject("Seed Marker", typeof(RectTransform)).transform;
                root.SetParent(layer, false);
                root.anchorMin = root.anchorMax = Vector2.zero;
                root.sizeDelta = Vector2.one * BubbleSize;
                Image pointer = AddImage(root, "Tail", tail, new Vector2(8f, 6f));
                Image outline = AddImage(root, "Rarity", bubble, Vector2.one * (BubbleSize + 2f));
                AddImage(root, "Bubble", bubble, Vector2.one * BubbleSize);
                Image icon = AddImage(root, "Crop", null, Vector2.one * 17f);
                icon.preserveAspect = true;
                markers[index] = new Marker { root = root, tail = pointer.rectTransform, outline = outline, icon = icon };
                root.gameObject.SetActive(false);
            }
        }

        public void Begin(Rect viewport, Rect safeArea)
        {
            shown = 0;
            visibleArea = Rect.MinMaxRect(Mathf.Max(viewport.xMin, safeArea.xMin), Mathf.Max(viewport.yMin, safeArea.yMin),
                Mathf.Min(viewport.xMax, safeArea.xMax), Mathf.Min(viewport.yMax, safeArea.yMax));
            scale = Mathf.Min(visibleArea.width, visibleArea.height) / 180f;
            canvas.scaleFactor = Mathf.Max(.01f, scale);
            float inset = EdgeInset * scale;
            markerArea = Rect.MinMaxRect(visibleArea.xMin + inset, visibleArea.yMin + inset,
                visibleArea.xMax - inset, visibleArea.yMax - inset);
        }

        public void Show(Vector3 screen, Sprite sprite, Color rarity)
        {
            if (scale <= 0f || shown >= markers.Length || screen.z <= 0f || visibleArea.Contains(screen))
                return;

            Vector2 origin = markerArea.center;
            Vector2 offset = (Vector2)screen - origin;
            float across = Mathf.Abs(offset.x) / (markerArea.width * .5f);
            float up = Mathf.Abs(offset.y) / (markerArea.height * .5f);
            Vector2 point = origin + offset / Mathf.Max(across, up);
            bool side = across >= up;
            point = SpaceMarker(point, side);

            Marker marker = markers[shown];
            marker.root.anchoredPosition = point / scale;
            marker.position = point;
            marker.icon.sprite = sprite;
            marker.outline.color = rarity;
            Vector2 direction = ((Vector2)screen - point).normalized;
            marker.tail.anchoredPosition = direction * 10.5f;
            marker.tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg);
            marker.root.gameObject.SetActive(true);
            markers[shown++] = marker;
        }

        Vector2 SpaceMarker(Vector2 point, bool side)
        {
            int axis = side ? 1 : 0;
            float low = side ? markerArea.yMin : markerArea.xMin;
            float high = side ? markerArea.yMax : markerArea.xMax;
            float spacing = MarkerSpacing * scale;
            for (int step = 0; step <= shown * 2; step++) {
                Vector2 candidate = point;
                int slots = (step + 1) / 2 * (step % 2 == 0 ? -1 : 1);
                candidate[axis] = Mathf.Clamp(point[axis] + slots * spacing, low, high);
                bool overlaps = false;
                for (int index = 0; index < shown; index++) {
                    Vector2 separation = candidate - markers[index].position;
                    if (Mathf.Abs(separation.x) < spacing && Mathf.Abs(separation.y) < spacing) {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps)
                    return candidate;
            }
            return point;
        }

        public void End()
        {
            for (int index = shown; index < markers.Length; index++)
                markers[index].root.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (!canvas)
                return;

            shown = 0;
            End();
        }

        public void Destroy()
        {
            if (canvas)
                Object.Destroy(canvas.gameObject);
        }

        static Image AddImage(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.sizeDelta = size;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
