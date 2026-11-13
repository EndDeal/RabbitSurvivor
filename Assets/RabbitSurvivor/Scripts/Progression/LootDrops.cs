using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class LootDrops : MonoBehaviour
    {
        const int FirstMagnetKills = 100;
        const float CellSize = 8f;
        const float ExperiencePickupRadius = 0.35f;
        const float ExperienceAttractRadius = 1.2f;
        const float ExperienceSpeed = 12f;
        const float ItemPickupRadius = 0.7f;
        const int MagnetFlights = 32;
        const float MagnetSpacing = 0.065f;

        [SerializeField] Spawner spawner;
        [SerializeField] Sprite experienceSprite;
        [SerializeField] Sprite magnetSprite;
        [SerializeField] Sprite potionSprite;
        [SerializeField, Range(0f, 1f)] float potionDropChance = 0.003f;
        [SerializeField, Range(0f, 1f)] float potionHealFraction = 0.25f;
        [SerializeField, Min(0f)] float potionCooldown = 18f;

        enum Kind { Experience, Magnet, Potion }

        struct Pickup
        {
            public Kind kind;
            public Vector2 position;
            public SpriteRenderer renderer;
            public int experience;
        }

        readonly Dictionary<Vector2Int, List<Pickup>> cells = new Dictionary<Vector2Int, List<Pickup>>();
        readonly Stack<List<Pickup>> availableCells = new Stack<List<Pickup>>();
        readonly Stack<SpriteRenderer> availableSprites = new Stack<SpriteRenderer>();
        readonly List<Pickup> attracted = new List<Pickup>();
        readonly List<Pickup> magnetQueue = new List<Pickup>();
        readonly List<Vector2Int> emptyCells = new List<Vector2Int>();
        GameManager game;
        Camera sceneCamera;
        RectInt visibleCells;
        int killsSinceMagnet;
        bool hasDroppedMagnet;
        float nextPotionAt;
        float nextFlightAt;

        void Awake()
        {
            game = GetComponent<GameManager>();
            sceneCamera = Camera.main;
        }

        public void DropFromEnemy(Vector3 position, bool boss, int experience)
        {
            Drop(Kind.Experience, position, experience);
            if (boss)
                return;

            int magnetInterval = hasDroppedMagnet ?
                Mathf.Max(FirstMagnetKills, spawner.CurrentWave.killsPerMagnet) : FirstMagnetKills;
            killsSinceMagnet++;
            if (killsSinceMagnet >= magnetInterval) {
                killsSinceMagnet = 0;
                hasDroppedMagnet = true;
                Drop(Kind.Magnet, position + Vector3.left * 0.45f);
                game.uiLevelUp.Notify("경험치 자석 등장!");
            }

            float potionChance = potionDropChance * Mathf.Min(1f, spawner.CurrentWave.spawnTime / 0.48f);
            if (Time.time >= nextPotionAt && Random.value < potionChance) {
                Drop(Kind.Potion, position + Vector3.right * 0.45f);
                nextPotionAt = Time.time + potionCooldown;
            }
        }

        void Drop(Kind kind, Vector2 position, int experience = 0)
        {
            bool outside = game.IsOutsideFinalArena(position);
            if (outside && kind != Kind.Experience)
                return;

            Vector2Int cell = CellAt(position);
            if (!cells.TryGetValue(cell, out List<Pickup> pickups)) {
                pickups = availableCells.Count > 0 ? availableCells.Pop() : new List<Pickup>();
                cells.Add(cell, pickups);
            }

            var pickup = new Pickup { kind = kind, position = position, experience = experience };
            if (!outside && visibleCells.Contains(cell))
                Show(ref pickup);
            pickups.Add(pickup);
        }

        internal void RecycleOutsideFinalArena()
        {
            foreach (var entry in cells) {
                List<Pickup> pickups = entry.Value;
                for (int index = pickups.Count - 1; index >= 0; index--) {
                    Pickup pickup = pickups[index];
                    if (!game.IsOutsideFinalArena(pickup.position))
                        continue;

                    Hide(ref pickup);
                    if (pickup.kind == Kind.Experience)
                        pickups[index] = pickup;
                    else
                        RemoveAt(pickups, index);
                }
                if (pickups.Count == 0)
                    emptyCells.Add(entry.Key);
            }
            RecycleEmptyCells();

            // Keep XP that was already flying when the arena closed.
            for (int index = 0; index < attracted.Count; index++) {
                Pickup pickup = attracted[index];
                if (game.IsOutsideFinalArena(pickup.position))
                    Hide(ref pickup);
                attracted[index] = pickup;
            }
        }

        void Update()
        {
            if (!game.isLive)
                return;

            Vector2 playerPosition = game.player.transform.position;
            UpdateVisibility(playerPosition);

            float multiplier = 1f + game.player.PickupRange;
            float attractionRadius = ExperienceAttractRadius * multiplier;
            float itemRadius = ItemPickupRadius * multiplier;
            Vector2 reach = Vector2.one * Mathf.Max(attractionRadius, itemRadius);
            Vector2Int minimum = CellAt(playerPosition - reach);
            Vector2Int maximum = CellAt(playerPosition + reach);
            bool magnetCollected = false;

            for (int y = minimum.y; y <= maximum.y; y++) {
                for (int x = minimum.x; x <= maximum.x; x++) {
                    var cell = new Vector2Int(x, y);
                    if (cells.TryGetValue(cell, out List<Pickup> pickups)) {
                        CheckNearby(pickups, playerPosition, attractionRadius * attractionRadius,
                            itemRadius * itemRadius, ref magnetCollected);
                        if (pickups.Count == 0)
                            RecycleCell(cell);
                    }
                }
            }

            if (magnetCollected)
                CollectMagnet();
            if (magnetQueue.Count > 0 && Time.time >= nextFlightAt) {
                Pickup pickup = magnetQueue[magnetQueue.Count - 1];
                magnetQueue.RemoveAt(magnetQueue.Count - 1);
                Vector2 offset = pickup.position - playerPosition;
                if (offset.sqrMagnitude < 2.25f) {
                    float angle = magnetQueue.Count * 2.39996f;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
                }
                pickup.position = playerPosition + Vector2.ClampMagnitude(offset, 5f);
                attracted.Add(pickup);
                nextFlightAt = Time.time + MagnetSpacing;
            }
            int experience = MoveAttracted(playerPosition);
            if (experience > 0)
                game.GainExperience(experience);
        }

        void CheckNearby(List<Pickup> pickups, Vector2 playerPosition, float attractionRangeSquared,
            float itemRangeSquared, ref bool magnetCollected)
        {
            for (int index = pickups.Count - 1; index >= 0; index--) {
                Pickup pickup = pickups[index];
                float distance = (pickup.position - playerPosition).sqrMagnitude;
                if (pickup.kind == Kind.Experience) {
                    if (distance <= attractionRangeSquared && !game.IsOutsideFinalArena(pickup.position)) {
                        Show(ref pickup);
                        attracted.Add(pickup);
                        RemoveAt(pickups, index);
                    }
                    continue;
                }

                if (distance > itemRangeSquared)
                    continue;

                if (pickup.kind == Kind.Magnet)
                    magnetCollected = true;
                else if (game.health < game.maxHealth) {
                    game.health = Mathf.Min(game.health + game.maxHealth * potionHealFraction, game.maxHealth);
                    game.uiLevelUp.Notify("체력 회복");
                }
                else
                    continue;

                Hide(ref pickup);
                RemoveAt(pickups, index);
            }
        }

        int MoveAttracted(Vector2 playerPosition)
        {
            int experience = 0;
            float step = ExperienceSpeed * Time.deltaTime;
            for (int index = attracted.Count - 1; index >= 0; index--) {
                Pickup pickup = attracted[index];
                pickup.position = Vector2.MoveTowards(pickup.position, playerPosition, step);
                if ((pickup.position - playerPosition).sqrMagnitude <= ExperiencePickupRadius * ExperiencePickupRadius) {
                    experience += pickup.experience;
                    Hide(ref pickup);
                    RemoveAt(attracted, index);
                    continue;
                }

                if (!game.IsOutsideFinalArena(pickup.position) && visibleCells.Contains(CellAt(pickup.position))) {
                    Show(ref pickup);
                    pickup.renderer.transform.position = pickup.position;
                }
                else
                    Hide(ref pickup);
                attracted[index] = pickup;
            }
            return experience;
        }

        void CollectMagnet()
        {
            int total = 0;
            foreach (List<Pickup> cell in cells.Values)
                foreach (Pickup pickup in cell)
                    if (pickup.kind == Kind.Experience)
                        total++;

            int perFlight = Mathf.Max(1, Mathf.CeilToInt(total / (float)MagnetFlights));
            int groupCount = 0;
            Pickup flight = default;
            foreach (var entry in cells) {
                List<Pickup> pickups = entry.Value;
                for (int index = pickups.Count - 1; index >= 0; index--) {
                    Pickup pickup = pickups[index];
                    if (pickup.kind != Kind.Experience)
                        continue;
                    if (groupCount == 0) {
                        flight.position = pickup.position;
                        flight.experience = 0;
                    }
                    flight.experience += pickup.experience;
                    if (++groupCount == perFlight) {
                        magnetQueue.Add(flight);
                        groupCount = 0;
                    }
                    Hide(ref pickup);
                    RemoveAt(pickups, index);
                }
                if (pickups.Count == 0)
                    emptyCells.Add(entry.Key);
            }
            if (groupCount != 0)
                magnetQueue.Add(flight);
            RecycleEmptyCells();
            game.uiLevelUp.Notify("경험치 자석 발동!");
        }

        void UpdateVisibility(Vector2 playerPosition)
        {
            Vector2 center = sceneCamera ? (Vector2)sceneCamera.transform.position : playerPosition;
            float height = sceneCamera ? sceneCamera.orthographicSize : 16f;
            float width = sceneCamera ? height * sceneCamera.aspect : height;
            Vector2 extents = new Vector2(width + 1f, height + 1f);
            Vector2Int minimum = CellAt(center - extents);
            Vector2Int maximum = CellAt(center + extents);
            var next = new RectInt(minimum, maximum - minimum + Vector2Int.one);
            if (next.Equals(visibleCells))
                return;

            UpdateVisibleCells(visibleCells, next, false);
            UpdateVisibleCells(next, visibleCells, true);
            visibleCells = next;
        }

        void UpdateVisibleCells(RectInt window, RectInt unchanged, bool show)
        {
            for (int y = window.yMin; y < window.yMax; y++) {
                for (int x = window.xMin; x < window.xMax; x++) {
                    var cell = new Vector2Int(x, y);
                    if (unchanged.Contains(cell) || !cells.TryGetValue(cell, out List<Pickup> pickups))
                        continue;

                    for (int index = 0; index < pickups.Count; index++) {
                        Pickup pickup = pickups[index];
                        if (show && !game.IsOutsideFinalArena(pickup.position))
                            Show(ref pickup);
                        else
                            Hide(ref pickup);
                        pickups[index] = pickup;
                    }
                }
            }
        }

        static Vector2Int CellAt(Vector2 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize));
        }

        void Show(ref Pickup pickup)
        {
            if (pickup.renderer)
                return;

            SpriteRenderer renderer;
            if (availableSprites.Count > 0)
                renderer = availableSprites.Pop();
            else {
                renderer = new GameObject("Loot Pickup").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(transform, false);
            }

            renderer.sprite = pickup.kind == Kind.Experience ? experienceSprite :
                pickup.kind == Kind.Magnet ? magnetSprite : potionSprite;
            renderer.transform.localScale = Vector3.one * (pickup.kind == Kind.Experience ? 0.56f : 1f);
            renderer.sortingOrder = pickup.kind == Kind.Experience ? 1 : 4;
            renderer.transform.position = pickup.position;
            renderer.gameObject.SetActive(true);
            pickup.renderer = renderer;
        }

        void Hide(ref Pickup pickup)
        {
            if (!pickup.renderer)
                return;

            pickup.renderer.gameObject.SetActive(false);
            availableSprites.Push(pickup.renderer);
            pickup.renderer = null;
        }

        void RecycleEmptyCells()
        {
            foreach (Vector2Int cell in emptyCells)
                RecycleCell(cell);
            emptyCells.Clear();
        }

        void RecycleCell(Vector2Int cell)
        {
            availableCells.Push(cells[cell]);
            cells.Remove(cell);
        }

        static void RemoveAt(List<Pickup> pickups, int index)
        {
            int last = pickups.Count - 1;
            pickups[index] = pickups[last];
            pickups.RemoveAt(last);
        }
    }
}
