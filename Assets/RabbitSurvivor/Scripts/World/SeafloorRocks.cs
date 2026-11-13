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
        const float DebrisLifetime = 0.32f;

        [SerializeField] Sprite[] sprites;
        [SerializeField] Material unlitMaterial;

        readonly Dictionary<Vector2Int, SpriteRenderer> rocks = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly HashSet<Vector2Int> broken = new HashSet<Vector2Int>();
        readonly Stack<SpriteRenderer> available = new Stack<SpriteRenderer>();
        readonly List<Vector2Int> expired = new List<Vector2Int>();
        readonly List<Collider2D> overlaps = new List<Collider2D>(2);
        readonly List<Debris> debris = new List<Debris>(12);
        readonly Stack<SpriteRenderer> availableDebris = new Stack<SpriteRenderer>();
        ContactFilter2D rockFilter;
        Camera sceneCamera;
        Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int lastRadius;
        uint seed;

        struct Debris
        {
            public SpriteRenderer renderer;
            public Vector2 start;
            public Vector2 velocity;
            public float until;
        }

        void Awake()
        {
            sceneCamera = Camera.main;
            seed = (uint)System.Environment.TickCount;
            rockFilter.SetLayerMask(1 << Layer);
            rockFilter.useTriggers = false;
        }

        void LateUpdate()
        {
            UpdateDebris();
            GameManager game = GameManager.instance;
            if (!game || !game.isLive)
                return;

            if (game.HasFinalArena || !sceneCamera || sprites == null || sprites.Length == 0)
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
                    if (!rocks.ContainsKey(cell) && !broken.Contains(cell))
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
            float width = Mathf.Lerp(1.1f, 1.75f, ((value >> 26) & 63) / 63f);
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

        internal void BreakOverlapping(Collider2D[] footprints)
        {
            Physics2D.SyncTransforms();
            foreach (Collider2D footprint in footprints) {
                int count = footprint.OverlapCollider(rockFilter, overlaps);
                for (int index = 0; index < count; index++) {
                    Vector3 position = overlaps[index].transform.position;
                    var cell = new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize));
                    if (!rocks.TryGetValue(cell, out SpriteRenderer rock))
                        continue;

                    SpawnDebris(rock);
                    rocks.Remove(cell);
                    broken.Add(cell);
                    rock.gameObject.SetActive(false);
                    available.Push(rock);
                }
            }
        }

        void SpawnDebris(SpriteRenderer rock)
        {
            for (int index = 0; index < 4; index++) {
                SpriteRenderer chip = availableDebris.Count > 0 ? availableDebris.Pop() :
                    new GameObject("Rock Debris").AddComponent<SpriteRenderer>();
                chip.transform.SetParent(transform, false);
                chip.transform.position = rock.transform.position;
                chip.transform.localScale = rock.transform.localScale * (index % 2 == 0 ? 0.14f : 0.20f);
                chip.sprite = rock.sprite;
                chip.sharedMaterial = rock.sharedMaterial;
                chip.sortingOrder = rock.sortingOrder + 1;
                chip.color = Color.white;
                chip.gameObject.SetActive(true);
                debris.Add(new Debris {
                    renderer = chip,
                    start = rock.transform.position,
                    velocity = new Vector2((index - 1.5f) * 2.1f, 1.8f + index % 2),
                    until = Time.unscaledTime + DebrisLifetime
                });
            }
        }

        void UpdateDebris()
        {
            float now = Time.unscaledTime;
            for (int index = debris.Count - 1; index >= 0; index--) {
                Debris chip = debris[index];
                float remaining = chip.until - now;
                if (remaining <= 0f) {
                    chip.renderer.gameObject.SetActive(false);
                    availableDebris.Push(chip.renderer);
                    debris.RemoveAt(index);
                    continue;
                }

                float age = DebrisLifetime - remaining;
                chip.renderer.transform.position = chip.start + chip.velocity * age + Vector2.down * (8f * age * age);
                chip.renderer.color = new Color(1f, 1f, 1f, remaining / DebrisLifetime);
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
