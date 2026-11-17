using UnityEngine;

namespace RabbitSurvivor
{
    internal sealed class OxygenZoneVisual
    {
        static readonly Color32 SandGold = new Color32(230, 190, 113, 236);
        static readonly Color32 SandLight = new Color32(255, 225, 150, 244);
        static readonly Color32 SandGrain = new Color32(199, 148, 87, 218);

        readonly Transform root;
        readonly SpriteRenderer sand;
        readonly SpriteRenderer[] bubbles;
        readonly bool isArena;
        float radius;

        public OxygenZoneVisual(Transform parent, string name, Sprite sandSprite, Sprite bubbleSprite,
            Material material, Sprite anemoneSprite, bool arena = false)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            sand = AddSprite("Yellow Sand", sandSprite, material, 1);
            isArena = arena;
            if (anemoneSprite) {
                SpriteRenderer anemone = AddSprite("Anemone", anemoneSprite, material, 2);
                Vector2 size = anemoneSprite.bounds.size;
                anemone.transform.localScale = Vector3.one * (1.06f / Mathf.Max(size.x, size.y));
                anemone.transform.localPosition = Vector2.down * .12f;
            }

            bubbles = new SpriteRenderer[isArena ? 24 : 6];
            for (int index = 0; index < bubbles.Length; index++)
                bubbles[index] = AddSprite("Air Bubble", bubbleSprite, material, 3);
        }

        public void SetActive(bool active) => root.gameObject.SetActive(active);

        public void Place(Vector2 center, float zoneRadius)
        {
            radius = zoneRadius;
            root.position = center;
            sand.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            SetActive(true);
        }

        public void Animate(float time)
        {
            float phaseOffset = (root.position.x + root.position.y) * .061f;
            for (int index = 0; index < bubbles.Length; index++) {
                float phase = Mathf.Repeat(time * .74f + index * .618f + phaseOffset, 1f);
                float angle = (index * 2.39996f) + phaseOffset;
                float spread = isArena ? (index % 4 == 0 ? .49f : .89f) : .16f + .28f * phase;
                Vector2 origin = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * spread);
                float riseHeight = isArena ? (phase - .5f) * .85f : phase * .68f - .08f;
                Vector2 rise = new Vector2(.07f * Mathf.Sin(phase * 6.28f + angle), riseHeight);
                float size = Mathf.Lerp(.16f, isArena ? .52f : .30f, (index % 4) / 3f);
                SpriteRenderer sprite = bubbles[index];
                sprite.transform.localPosition = origin + rise;
                sprite.transform.localScale = Vector3.one * (size * (.7f + .3f * phase));
                float opacity = Mathf.Clamp01(Mathf.Min(phase * 5f, (1f - phase) * 4f));
                sprite.color = new Color(1f, 1f, 1f, opacity);
            }
        }

        SpriteRenderer AddSprite(string name, Sprite sprite, Material material, int sortingOrder)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (material)
                renderer.sharedMaterial = material;
            return renderer;
        }

        internal static Sprite CreateSand(int pixels)
        {
            var colors = new Color32[pixels * pixels];
            for (int y = 0; y < pixels; y++) {
                for (int x = 0; x < pixels; x++) {
                    Vector2 point = new Vector2(x + .5f, y + .5f) * (2f / pixels) - Vector2.one;
                    float distance = point.magnitude;
                    if (distance >= 1f)
                        continue;

                    int grain = (x * 83 ^ y * 173 ^ x * y * 13) & 63;
                    Color32 color = SandGold;
                    if (distance > .88f || grain < 5)
                        color = SandLight;
                    else if (grain < 10)
                        color = SandGrain;
                    color.a = (byte)(color.a * Mathf.Clamp01((1f - distance) * pixels * .6f));
                    colors[y * pixels + x] = color;
                }
            }
            return MakeSprite("Air Zone Sand", colors, pixels);
        }

        internal static Sprite CreateBubble()
        {
            const int pixels = 12;
            var colors = new Color32[pixels * pixels];
            for (int y = 0; y < pixels; y++) {
                for (int x = 0; x < pixels; x++) {
                    float distance = new Vector2(x - 5.5f, y - 5.5f).magnitude;
                    if (distance > 5.6f)
                        continue;

                    Color32 color = new Color32(125, 225, 245, 65);
                    if (distance >= 4.7f)
                        color = new Color32(56, 118, 144, 205);
                    else if (distance >= 3.7f)
                        color = new Color32(197, 250, 255, 235);
                    else if (x >= 3 && x <= 5 && y >= 7 && y <= 8)
                        color = new Color32(249, 255, 255, 255);
                    colors[y * pixels + x] = color;
                }
            }
            return MakeSprite("Clear Air Bubble", colors, pixels);
        }

        static Sprite MakeSprite(string name, Color32[] colors, int pixels)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, pixels, pixels), Vector2.one * .5f, pixels,
                0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
