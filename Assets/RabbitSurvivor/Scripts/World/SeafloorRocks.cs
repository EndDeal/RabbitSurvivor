using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class SeafloorRocks : MonoBehaviour
    {
        internal const int Layer = 7;
        const float CellSize = 8f;
        const int ViewPadding = 2;
        const float SpawnClearance = 4.5f;

        [SerializeField] Sprite[] sprites;
        [SerializeField] Material unlitMaterial;

        readonly Dictionary<Vector2Int, SpriteRenderer> rocks = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly Stack<SpriteRenderer> available = new Stack<SpriteRenderer>();
        readonly List<Vector2Int> expired = new List<Vector2Int>();
        Camera sceneCamera;
        Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int lastRadius;
        uint seed;

        void Awake()
        {
            sceneCamera = Camera.main;
            seed = (uint)System.Environment.TickCount;
        }

        void LateUpdate()
        {
            GameManager game = GameManager.instance;
            if (!game || !game.isLive || game.HasFinalArena || !sceneCamera || sprites == null || sprites.Length == 0)
                return;

            Vector2 cameraPosition = sceneCamera.transform.position;
            var center = new Vector2Int(Mathf.FloorToInt(cameraPosition.x / CellSize), Mathf.FloorToInt(cameraPosition.y / CellSize));
            var radius = new Vector2Int(
                Mathf.CeilToInt(sceneCamera.orthographicSize * sceneCamera.aspect / CellSize) + ViewPadding,
                Mathf.CeilToInt(sceneCamera.orthographicSize / CellSize) + ViewPadding);
            if (center == lastCenter && radius == lastRadius)
                return;
            lastCenter = center;
            lastRadius = radius;

            foreach (var rock in rocks) {
                Vector2Int offset = rock.Key - center;
                if (Mathf.Abs(offset.x) > radius.x || Mathf.Abs(offset.y) > radius.y) {
                    rock.Value.gameObject.SetActive(false);
                    available.Push(rock.Value);
                    expired.Add(rock.Key);
                }
            }
            foreach (Vector2Int cell in expired)
                rocks.Remove(cell);
            expired.Clear();

            for (int y = center.y - radius.y; y <= center.y + radius.y; y++) {
                for (int x = center.x - radius.x; x <= center.x + radius.x; x++) {
                    var cell = new Vector2Int(x, y);
                    if (!rocks.ContainsKey(cell))
                        PlaceRock(cell);
                }
            }
        }

        void PlaceRock(Vector2Int cell)
        {
            uint value = Hash(cell);
            if ((value & 255) >= 103)
                return;

            Vector2 position = new Vector2(
                cell.x + Mathf.Lerp(0.26f, 0.74f, ((value >> 8) & 255) / 255f),
                cell.y + Mathf.Lerp(0.26f, 0.74f, ((value >> 16) & 255) / 255f)) * CellSize;
            if (position.sqrMagnitude < SpawnClearance * SpawnClearance)
                return;

            SpriteRenderer rock = available.Count > 0 ? available.Pop() : CreateRock();
            rock.transform.position = position;
            rock.sprite = sprites[(int)((value >> 24) % (uint)sprites.Length)];
            rock.flipX = (value & 256) != 0;
            float width = Mathf.Lerp(1.3f, 2.1f, ((value >> 26) & 63) / 63f);
            rock.transform.localScale = Vector3.one * (width / rock.sprite.bounds.size.x);
            var footprint = rock.GetComponent<CapsuleCollider2D>();
            footprint.size = new Vector2(rock.sprite.bounds.size.x * 0.82f, rock.sprite.bounds.size.y * 0.62f);
            footprint.offset = new Vector2(0f, -rock.sprite.bounds.size.y * 0.12f);
            rock.gameObject.SetActive(true);
            rocks.Add(cell, rock);
        }

        SpriteRenderer CreateRock()
        {
            var rock = new GameObject("Seafloor Rock Pile");
            rock.layer = Layer;
            rock.transform.SetParent(transform, false);
            var sprite = rock.AddComponent<SpriteRenderer>();
            sprite.sortingOrder = 1;
            if (unlitMaterial)
                sprite.sharedMaterial = unlitMaterial;
            rock.AddComponent<CapsuleCollider2D>().direction = CapsuleDirection2D.Horizontal;
            return sprite;
        }

        uint Hash(Vector2Int cell)
        {
            unchecked {
                uint hash = (uint)cell.x * 0x9e3779b9u ^ (uint)cell.y * 0x85ebca6bu ^ seed;
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                return hash ^ (hash >> 16);
            }
        }

        internal void ClearArena(Vector2 center, float radius)
        {
            float distance = radius + 1.4f;
            foreach (SpriteRenderer rock in rocks.Values) {
                if (((Vector2)rock.transform.position - center).sqrMagnitude <= distance * distance)
                    rock.gameObject.SetActive(false);
            }
        }

        internal static Vector2 FreeSpawnPosition(Vector2 position)
        {
            int mask = 1 << Layer;
            if (!Physics2D.OverlapCircle(position, 0.55f, mask))
                return position;

            for (int index = 0; index < 8; index++) {
                float angle = index * (Mathf.PI * 0.25f);
                Vector2 nearby = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.7f;
                if (!Physics2D.OverlapCircle(nearby, 0.55f, mask))
                    return nearby;
            }
            return position;
        }
    }
}
