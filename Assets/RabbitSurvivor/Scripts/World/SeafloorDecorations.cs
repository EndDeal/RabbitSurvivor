using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    public sealed class SeafloorDecorations : MonoBehaviour
    {
        const float CellSize = 3.6f;
        const int ViewPadding = 1;

        [SerializeField] Sprite[] sprites;
        [SerializeField] Material unlitMaterial;

        readonly Dictionary<Vector2Int, SpriteRenderer> visible = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly Stack<SpriteRenderer> available = new Stack<SpriteRenderer>();
        readonly List<Vector2Int> expired = new List<Vector2Int>();
        Camera sceneCamera;
        Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int lastRadius;

        void Awake()
        {
            sceneCamera = Camera.main;
        }

        void LateUpdate()
        {
            GameManager game = GameManager.instance;
            if (!game || !game.isLive || !sceneCamera || sprites == null || sprites.Length == 0)
                return;

            Vector2 position = sceneCamera.transform.position;
            var center = new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize));
            var radius = new Vector2Int(
                Mathf.CeilToInt(sceneCamera.orthographicSize * sceneCamera.aspect / CellSize) + ViewPadding,
                Mathf.CeilToInt(sceneCamera.orthographicSize / CellSize) + ViewPadding);
            if (center == lastCenter && radius == lastRadius)
                return;
            lastCenter = center;
            lastRadius = radius;

            foreach (var decoration in visible) {
                Vector2Int offset = decoration.Key - center;
                if (Mathf.Abs(offset.x) <= radius.x && Mathf.Abs(offset.y) <= radius.y)
                    continue;

                decoration.Value.gameObject.SetActive(false);
                available.Push(decoration.Value);
                expired.Add(decoration.Key);
            }
            foreach (Vector2Int cell in expired)
                visible.Remove(cell);
            expired.Clear();

            for (int y = center.y - radius.y; y <= center.y + radius.y; y++) {
                for (int x = center.x - radius.x; x <= center.x + radius.x; x++) {
                    var cell = new Vector2Int(x, y);
                    if (!visible.ContainsKey(cell))
                        Place(cell, game);
                }
            }
        }

        void Place(Vector2Int cell, GameManager game)
        {
            uint value = Hash(cell);
            if ((value & 255) >= 224)
                return;

            Vector2 position = new Vector2(
                cell.x + Mathf.Lerp(.2f, .8f, ((value >> 8) & 255) / 255f),
                cell.y + Mathf.Lerp(.2f, .8f, ((value >> 16) & 255) / 255f)) * CellSize;
            if ((game.Oxygen && UnderwaterOxygen.NeedsRockClearance(position)) ||
                Physics2D.OverlapCircle(position, .6f, 1 << SeafloorRocks.Layer))
                return;

            SpriteRenderer decoration = available.Count > 0 ? available.Pop() : CreateDecoration();
            decoration.sprite = sprites[(int)((value >> 24) % (uint)sprites.Length)];
            decoration.transform.position = position;
            float width = Mathf.Lerp(.38f, .72f, ((value >> 27) & 31) / 31f);
            decoration.transform.localScale = Vector3.one * (width / decoration.sprite.bounds.size.x);
            decoration.flipX = (value & 256) != 0;
            decoration.color = new Color(.77f, .87f, .85f, .82f);
            decoration.gameObject.SetActive(true);
            visible.Add(cell, decoration);
        }

        SpriteRenderer CreateDecoration()
        {
            var renderer = new GameObject("Seafloor Decoration").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sortingOrder = 1;
            if (unlitMaterial)
                renderer.sharedMaterial = unlitMaterial;
            return renderer;
        }

        static uint Hash(Vector2Int cell)
        {
            unchecked {
                uint value = (uint)cell.x * 0x9e3779b9u ^ (uint)cell.y * 0x85ebca6bu ^ 0xb7e15162u;
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }
}
