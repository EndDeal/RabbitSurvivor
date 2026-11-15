using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RabbitSurvivor
{
    public sealed class SeedCrops : MonoBehaviour
    {
        internal const int RiceWallLayer = 8;
        const float FirstSpawn = 14f;
        const float SpawnInterval = 24f;
        const float FirstSeedMinDistance = 2.5f;
        const float FirstSeedMaxDistance = 3.8f;
        const float SeedLifetime = 90f;
        const float ActivationSeconds = 1f;
        const float ActivationRadius = .65f;
        const int MaxSeeds = 4;
        const int RingPoints = 32;
        const float SeedSpacing = 7f;
        const float MineTriggerRadius = .48f;
        const float MineFuseSeconds = 1f;
        const float ChainInterval = .085f;
        const float RiceTileLength = .55f;

        enum SeedKind { Rice = 20, Wheat = 21, Potato = 22, Carrot = 23 }
        enum SeedGrade { Normal, Rare, Epic, Unique, Legendary }

        [SerializeField] Sprite[] seedSprites;
        [SerializeField] Sprite guideBubble;
        [SerializeField] Sprite guideTail;
        [SerializeField] Sprite scarecrowSprite;
        [SerializeField] Sprite turretSprite;
        [SerializeField] Sprite pelletSprite;
        [SerializeField] Sprite riceBlockSprite;
        [SerializeField] Material effectMaterial;
        [SerializeField] GameObject projectilePrefab;

        static readonly Color32[] GradeColors = {
            new Color32(255, 250, 230, 255), new Color32(92, 188, 255, 255),
            new Color32(204, 144, 255, 255), new Color32(255, 134, 202, 255),
            new Color32(255, 208, 97, 255)
        };

        static readonly string[] CropNames = { "쌀떡길", "밀짚 허수아비", "감자 지뢰", "당근 포탑" };
        static readonly string[] GradeNames = { "노말", "레어", "에픽", "유니크", "레전드리" };

        sealed class Seed
        {
            public SeedKind kind;
            public SeedGrade grade;
            public Vector2 position;
            public GameObject root;
            public SpriteRenderer sprite;
            public LineRenderer ring;
            public bool activated;
            public float held;
            public float expiresAt;
            public float nextEffectAt;
            public int pulses;
            public Queue<(Vector2 position, int depth)> chain;
            public int queuedBlasts;
            public float nextChainAt;
            public int mineKills;
            public bool turretFlankLeft;
            public BoxCollider2D ricePath;
            public float riceContactDamage;
        }

        struct Burst
        {
            public LineRenderer ring;
            public float radius;
            public float started;
            public float duration;
            public float width;
        }

        readonly List<Seed> seeds = new List<Seed>();
        readonly List<Collider2D> hits = new List<Collider2D>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly Stack<LineRenderer> availableBursts = new Stack<LineRenderer>();
        ContactFilter2D enemies;
        GameManager game;
        Camera sceneCamera;
        int projectileIndex;
        float elapsed;
        float nextSpawn = FirstSpawn;
        Vector2 previousPlayerPosition;
        Vector2? lastSpawnPosition;
        Vector2 lastHeading = Vector2.up;
        bool hasPlayerPosition;
        SeedEdgeGuide seedGuide;

        internal int CurrentMineCombo { get; private set; }
        internal float MineComboAt { get; private set; }

        void Awake()
        {
            game = GetComponent<GameManager>();
            sceneCamera = Camera.main;
            projectileIndex = Array.IndexOf(game.pool.prefabs, projectilePrefab);
            enemies.SetLayerMask(game.player.GetComponent<Scanner>().targetLayer);
            enemies.useTriggers = false;
        }

        void LateUpdate()
        {
            if (!game.isLive || !sceneCamera) {
                seedGuide?.Hide();
                return;
            }

            UpdateBursts();
            if (seeds.Count == 0 || !guideBubble || !guideTail) {
                seedGuide?.Hide();
                return;
            }

            seedGuide ??= new SeedEdgeGuide(guideBubble, guideTail, MaxSeeds);
            seedGuide.Begin(sceneCamera.pixelRect, Screen.safeArea);
            for (int index = seeds.Count - 1; index >= 0; index--) {
                Seed seed = seeds[index];
                if (!seed.activated)
                    seedGuide.Show(sceneCamera.WorldToScreenPoint(seed.position), seed.sprite.sprite, GradeColors[(int)seed.grade]);
            }
            seedGuide.End();
        }

        void OnDisable() => seedGuide?.Hide();

        void OnDestroy() => seedGuide?.Destroy();

        void FixedUpdate()
        {
            if (!game.isLive)
                return;

            elapsed += Time.fixedDeltaTime;
            Vector2 playerPosition = game.player.transform.position;
            Vector2 movement = playerPosition - previousPlayerPosition;
            if (hasPlayerPosition && movement.sqrMagnitude > .0001f)
                lastHeading = movement.normalized;
            previousPlayerPosition = playerPosition;
            hasPlayerPosition = true;

            for (int index = seeds.Count - 1; index >= 0; index--) {
                Seed seed = seeds[index];
                if (elapsed >= seed.expiresAt || game.IsOutsideFinalArena(seed.position)) {
                    RemoveSeed(index);
                    continue;
                }
                if (seed.activated)
                    UpdateCrop(seed);
                else
                    CheckActivation(seed, playerPosition);
                if (!game.isLive)
                    return;
            }

            if (elapsed >= nextSpawn)
                nextSpawn = elapsed + (TrySpawn(playerPosition) ? SpawnInterval : 2f);
        }

        bool TrySpawn(Vector2 playerPosition)
        {
            if (!sceneCamera || seeds.Count >= MaxSeeds)
                return false;

            int blocked = enemies.layerMask.value | (1 << SeafloorRocks.Layer) | (1 << RiceWallLayer);
            if (!lastSpawnPosition.HasValue && !game.HasFinalArena)
                return TrySpawnFirstSeed(playerPosition, blocked);

            bool finalArena = game.HasFinalArena;
            float spacing = finalArena ? SeedSpacing * .55f : SeedSpacing;
            Vector2 best = default;
            float bestSpacing = 0f;
            for (int attempt = 0; attempt < 36; attempt++) {
                Vector2 edge = Random.insideUnitCircle.normalized;
                Vector2 position;
                if (finalArena) {
                    float distanceFromCenter = game.FinalArenaRadius * Random.Range(.74f, .88f);
                    position = game.FinalArenaCenter + edge * distanceFromCenter;
                }
                else {
                    float edgeDistance = Random.Range(.48f, .58f);
                    float viewportScale = edgeDistance / Mathf.Max(.01f, Mathf.Abs(edge.x), Mathf.Abs(edge.y));
                    Vector3 view = new Vector3(.5f + edge.x * viewportScale, .5f + edge.y * viewportScale,
                        -sceneCamera.transform.position.z);
                    position = sceneCamera.ViewportToWorldPoint(view);
                }
                if ((position - playerPosition).sqrMagnitude < spacing * spacing || game.IsOutsideFinalArena(position) ||
                    Physics2D.OverlapCircle(position, 1f, blocked))
                    continue;

                float distance = lastSpawnPosition.HasValue ? (position - lastSpawnPosition.Value).sqrMagnitude :
                    (position - playerPosition).sqrMagnitude;
                foreach (Seed seed in seeds)
                    distance = Mathf.Min(distance, (position - seed.position).sqrMagnitude);
                if (distance < spacing * spacing || distance <= bestSpacing)
                    continue;

                best = position;
                bestSpacing = distance;
            }
            if (bestSpacing == 0f)
                return false;

            SpawnRandomSeed(best);
            return true;
        }

        bool TrySpawnFirstSeed(Vector2 playerPosition, int blocked)
        {
            for (int attempt = 0; attempt < 36; attempt++) {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction == Vector2.zero)
                    continue;
                float distance = Random.Range(FirstSeedMinDistance, FirstSeedMaxDistance);
                Vector2 position = playerPosition + direction * distance;
                Vector3 view = sceneCamera.WorldToViewportPoint(position);
                if (view.z <= 0f || view.x < .12f || view.x > .88f || view.y < .12f || view.y > .88f ||
                    Physics2D.OverlapCircle(position, 1f, blocked) ||
                    Physics2D.CircleCast(playerPosition, .32f, direction, distance, 1 << SeafloorRocks.Layer))
                    continue;

                SpawnRandomSeed(position);
                return true;
            }
            return false;
        }

        void SpawnRandomSeed(Vector2 position)
        {
            SeedKind kind = (SeedKind)Random.Range((int)SeedKind.Rice, (int)SeedKind.Carrot + 1);
            SpawnSeed(position, kind, RollGrade());
            lastSpawnPosition = position;
        }

        static SeedGrade RollGrade()
        {
            int roll = Random.Range(0, 100);
            if (roll < 55) return SeedGrade.Normal;
            if (roll < 80) return SeedGrade.Rare;
            if (roll < 93) return SeedGrade.Epic;
            if (roll < 98) return SeedGrade.Unique;
            return SeedGrade.Legendary;
        }

        void SpawnSeed(Vector2 position, SeedKind kind, SeedGrade grade)
        {
            int crop = (int)kind - (int)SeedKind.Rice;
            var root = new GameObject(CropNames[crop] + " 씨앗");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            var sprite = new GameObject("Crop Sprite").AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(root.transform, false);
            sprite.transform.localScale = Vector3.one * 1.3f;
            sprite.sprite = seedSprites[crop];
            sprite.sortingOrder = 4;
            if (effectMaterial)
                sprite.sharedMaterial = effectMaterial;

            var seed = new Seed {
                kind = kind, grade = grade, position = position, root = root, sprite = sprite,
                ring = CreateRing(root.transform, GradeColors[(int)grade]), expiresAt = elapsed + SeedLifetime
            };
            DrawRing(seed.ring, ActivationRadius, 1f, .65f);
            seeds.Add(seed);
        }

        void CheckActivation(Seed seed, Vector2 playerPosition)
        {
            bool inRange = (playerPosition - seed.position).sqrMagnitude <= ActivationRadius * ActivationRadius;
            seed.held = inRange ? seed.held + Time.fixedDeltaTime : 0f;
            if (seed.held + .0001f >= ActivationSeconds) {
                Activate(seed);
                return;
            }

            float light = .64f + Mathf.Sin(elapsed * 4f) * .16f;
            DrawRing(seed.ring, ActivationRadius, inRange ? seed.held / ActivationSeconds : 1f, inRange ? 1f : light);
        }

        void Activate(Seed seed)
        {
            seed.activated = true;
            int tier = (int)seed.grade;
            Color gradeColor = GradeColors[tier];
            float size = 1f + tier * .14f;
            seed.sprite.transform.localScale = Vector3.one * size;
            seed.sprite.color = Color.Lerp(Color.white, gradeColor, tier * .1f);
            seed.ring.widthMultiplier += tier * .017f;
            if (tier >= (int)SeedGrade.Epic)
                ShowBurst(seed.position, ActivationRadius + tier * .14f, gradeColor, .32f, .09f);
            game.uiLevelUp.Notify($"{GradeNames[tier]} {CropNames[(int)seed.kind - (int)SeedKind.Rice]}");
            switch (seed.kind) {
                case SeedKind.Rice:
                    ActivateWall(seed, tier);
                    break;
                case SeedKind.Wheat:
                    seed.sprite.sprite = scarecrowSprite;
                    seed.expiresAt = elapsed + 4f + tier * .75f;
                    DrawRing(seed.ring, LureRadius(tier), 1f, .4f + tier * .09f);
                    break;
                case SeedKind.Potato:
                    seed.expiresAt = elapsed + SeedLifetime;
                    seed.nextEffectAt = float.PositiveInfinity;
                    seed.chain = new Queue<(Vector2 position, int depth)>();
                    seed.sprite.transform.localScale = new Vector3(.9f, .55f, 1f) * size;
                    DrawRing(seed.ring, MineTriggerRadius, 1f, .65f);
                    break;
                case SeedKind.Carrot:
                    seed.sprite.sprite = turretSprite;
                    seed.expiresAt = elapsed + 5f + tier;
                    DrawRing(seed.ring, TurretRadius(tier), 1f, .25f);
                    break;
            }
        }

        void ActivateWall(Seed seed, int tier)
        {
            seed.expiresAt = elapsed + 8f + tier * 2f;
            seed.riceContactDamage = 3f + tier * 2f;
            Vector2 heading = lastHeading;
            if (game.HasFinalArena) {
                Vector2 inward = (game.FinalArenaCenter - seed.position).normalized;
                if (inward != Vector2.zero && Vector2.Dot(heading, inward) < .35f)
                    heading = inward;
            }

            var path = new GameObject("Rice Cake Path");
            path.layer = RiceWallLayer;
            path.transform.SetParent(seed.root.transform, false);
            path.transform.SetPositionAndRotation(seed.position, Quaternion.FromToRotation(Vector3.right, heading));
            seed.sprite.enabled = false;
            seed.ring.enabled = false;
            Vector2 across = Vector2.Perpendicular(heading);
            float width = 1.15f + tier * .12f;
            int tileCount = 5 + tier * 2;
            Color tint = Color.Lerp(Color.white, GradeColors[tier], .19f + tier * .13f);
            int placed = 0;
            for (; placed < tileCount; placed++) {
                float offset = placed * RiceTileLength;
                Vector2 end = seed.position + heading * (offset + RiceTileLength * .5f);
                if (placed > 0 && (game.IsOutsideFinalArena(end + across * width * .5f) ||
                    game.IsOutsideFinalArena(end - across * width * .5f)))
                    break;
                AddRiceTile(path.transform, offset, width, tint);
            }

            seed.ricePath = path.AddComponent<BoxCollider2D>();
            seed.ricePath.size = new Vector2(placed * RiceTileLength, width);
            seed.ricePath.offset = Vector2.right * ((placed - 1) * RiceTileLength * .5f);
            Physics2D.SyncTransforms();
            game.Rocks?.BreakOverlapping(seed.ricePath);
        }

        void AddRiceTile(Transform parent, float offset, float width, Color tint)
        {
            var renderer = new GameObject("Rice Cake Tile").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = Vector3.right * offset;
            renderer.sprite = riceBlockSprite;
            Vector3 art = riceBlockSprite.bounds.size;
            renderer.transform.localScale = new Vector3(RiceTileLength / art.x, width / art.y, 1f);
            renderer.sortingOrder = 1;
            renderer.color = tint;
            if (effectMaterial)
                renderer.sharedMaterial = effectMaterial;
        }

        internal float RiceContactDamage(Collider2D collider)
        {
            foreach (Seed seed in seeds) {
                if (seed.ricePath == collider)
                    return seed.riceContactDamage;
            }
            return 0f;
        }

        internal Vector2 ChaseTarget(Vector2 enemyPosition, Vector2 playerPosition)
        {
            Vector2 target = playerPosition;
            float closest = float.PositiveInfinity;
            foreach (Seed seed in seeds) {
                if (!seed.activated || seed.kind != SeedKind.Wheat || elapsed >= seed.expiresAt)
                    continue;

                float distance = (seed.position - enemyPosition).sqrMagnitude;
                float radius = LureRadius((int)seed.grade);
                if (distance <= radius * radius && distance < closest) {
                    closest = distance;
                    target = seed.position;
                }
            }
            return target;
        }

        void UpdateCrop(Seed seed)
        {
            int tier = (int)seed.grade;
            if (seed.kind == SeedKind.Potato) {
                UpdateMine(seed, tier);
                return;
            }
            if (seed.kind == SeedKind.Wheat && tier >= (int)SeedGrade.Epic && elapsed >= seed.nextEffectAt) {
                ShowBurst(seed.position, LureRadius(tier), GradeColors[tier], .6f, .09f + tier * .012f);
                seed.nextEffectAt = elapsed + 1.25f;
            }
            if (elapsed < seed.nextEffectAt || seed.kind != SeedKind.Carrot || projectileIndex < 0)
                return;

            Vector2 aim = AimAtClosest(seed.position, TurretRadius(tier));
            if (aim == Vector2.zero)
                return;

            int shots = tier >= (int)SeedGrade.Unique ? tier - 1 : 1;
            Color shotColor = tier == (int)SeedGrade.Normal ? Color.white : GradeColors[tier];
            if (tier >= (int)SeedGrade.Epic)
                ShowBurst(seed.position + aim * .3f, .4f + shots * .13f, shotColor, .19f, .09f);
            for (int index = 0; index < shots; index++) {
                float angle = 0f;
                if (index == 1)
                    angle = seed.turretFlankLeft ? -16f : 16f;
                else if (index == 2)
                    angle = seed.turretFlankLeft ? 16f : -16f;
                Vector2 direction = Quaternion.Euler(0, 0, angle) * aim;
                Bullet bullet = game.pool.Get(projectileIndex).GetComponent<Bullet>();
                bullet.transform.SetPositionAndRotation(seed.position, Quaternion.FromToRotation(Vector3.up, direction));
                bullet.Init(4f + tier * .5f, tier >= (int)SeedGrade.Epic ? 1 : 0, direction, pelletSprite, shotColor);
            }
            seed.turretFlankLeft = !seed.turretFlankLeft;
            seed.nextEffectAt = elapsed + .8f - tier * .06f;
        }

        Vector2 AimAtClosest(Vector2 origin, float radius)
        {
            Physics2D.OverlapCircle(origin, radius, enemies, hits);
            Vector2 aim = Vector2.zero;
            float closest = float.PositiveInfinity;
            foreach (Collider2D hit in hits) {
                if (!hit.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                    continue;

                Vector2 offset = (Vector2)enemy.transform.position - origin;
                if (offset.sqrMagnitude < closest) {
                    closest = offset.sqrMagnitude;
                    aim = offset.sqrMagnitude > .001f ? offset.normalized : Vector2.up;
                }
            }
            return aim;
        }

        void UpdateMine(Seed seed, int tier)
        {
            if (seed.pulses == 0 && float.IsPositiveInfinity(seed.nextEffectAt)) {
                Physics2D.OverlapCircle(seed.position, MineTriggerRadius, enemies, hits);
                foreach (Collider2D hit in hits) {
                    if (!hit.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                        continue;

                    seed.nextEffectAt = elapsed + MineFuseSeconds;
                    seed.nextChainAt = seed.nextEffectAt + ChainInterval;
                    seed.expiresAt = seed.nextEffectAt + .7f;
                    seed.ring.startColor = seed.ring.endColor = new Color(1f, .64f, .25f);
                    DrawRing(seed.ring, MineRadius(tier), 1f, .35f);
                    break;
                }
                return;
            }

            if (elapsed + .0001f >= seed.nextEffectAt)
                Explode(seed, tier);
            else if (seed.chain.Count > 0 && elapsed >= seed.nextChainAt) {
                var blast = seed.chain.Dequeue();
                MineBlast(seed, tier, blast.position, 1.18f + tier * .13f, 18f + tier * 3f, blast.depth);
                seed.nextChainAt = elapsed + ChainInterval;
            }
            else if (seed.pulses == 0) {
                float progress = 1f - (seed.nextEffectAt - elapsed) / MineFuseSeconds;
                DrawRing(seed.ring, MineRadius(tier), 1f, Mathf.Lerp(.35f, 1f, progress));
            }
        }

        void Explode(Seed seed, int tier)
        {
            float radius = MineRadius(tier) + seed.pulses * .55f;
            float damage = (18f + tier * 5f) * (seed.pulses == 0 ? 1f : .55f);
            MineBlast(seed, tier, seed.position, radius, damage, 0);
            seed.sprite.enabled = false;
            seed.ring.enabled = false;
            seed.pulses++;
            seed.nextEffectAt = tier == (int)SeedGrade.Legendary && seed.pulses == 1 ?
                elapsed + .38f : float.PositiveInfinity;
        }

        void MineBlast(Seed seed, int tier, Vector2 origin, float radius, float damage, int depth)
        {
            bool primary = depth == 0;
            int killsBeforeBlast = seed.mineKills;
            Color color = primary ? Color.Lerp(new Color(1f, .69f, .25f), GradeColors[tier], tier * .24f) :
                new Color(1f, .88f, .5f);
            ShowBurst(origin, radius, color, primary ? .3f : .21f, primary ? .17f : .09f);
            if (primary && tier >= (int)SeedGrade.Epic)
                ShowBurst(origin, radius * .66f, Color.white, .22f, .1f);
            AudioManager.instance.PlayFeedback(AudioManager.Sfx.Mine, primary ? .84f : 1.14f + depth * .07f,
                primary ? 1f : .62f);
            Physics2D.OverlapCircle(origin, radius, enemies, hits);
            foreach (Collider2D hit in hits) {
                if (!hit.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                    continue;

                Vector2 position = enemy.transform.position;
                Vector2 push = position - origin;
                if (primary && tier >= (int)SeedGrade.Epic && push.sqrMagnitude > .001f)
                    enemy.TakeFurrowDamage(damage, push.normalized, .65f + tier * .22f);
                else
                    enemy.TakeDamage(damage);
                if (game.IsEnding)
                    return;
                if (enemy.IsAlive || enemy.IsBoss || enemy.IsBarrier)
                    continue;

                seed.mineKills++;
                if (depth < 2 + tier && seed.queuedBlasts < 5 + tier * 2) {
                    seed.chain.Enqueue((position, depth + 1));
                    seed.queuedBlasts++;
                    seed.expiresAt = Mathf.Max(seed.expiresAt, elapsed + .3f + seed.chain.Count * ChainInterval);
                }
            }
            if (seed.mineKills >= 3 && seed.mineKills > killsBeforeBlast) {
                CurrentMineCombo = seed.mineKills;
                MineComboAt = Time.time;
            }
        }

        internal void PoisonImpact(Vector2 position)
        {
            ShowBurst(position, .46f, new Color(.88f, .39f, 1f), .19f, .1f);
            AudioManager.instance.PlayPoisonPop();
        }

        void ShowBurst(Vector2 position, float radius, Color color, float duration, float width)
        {
            if (bursts.Count >= 24)
                return;

            LineRenderer ring = availableBursts.Count > 0 ? availableBursts.Pop() : CreateRing(transform, color);
            ring.transform.position = position;
            ring.sortingOrder = 5;
            ring.startColor = ring.endColor = color;
            ring.widthMultiplier = width;
            DrawRing(ring, radius * .2f, 1f, 1f);
            ring.enabled = true;
            bursts.Add(new Burst { ring = ring, radius = radius, started = Time.time, duration = duration, width = width });
        }

        void UpdateBursts()
        {
            for (int index = bursts.Count - 1; index >= 0; index--) {
                Burst burst = bursts[index];
                float progress = (Time.time - burst.started) / burst.duration;
                if (progress >= 1f) {
                    burst.ring.enabled = false;
                    availableBursts.Push(burst.ring);
                    bursts.RemoveAt(index);
                    continue;
                }

                float remaining = 1f - progress;
                burst.ring.widthMultiplier = burst.width * remaining;
                DrawRing(burst.ring, burst.radius * (1f - .8f * remaining * remaining), 1f, remaining);
            }
        }

        static float LureRadius(int tier) => 2.7f + tier * .4f;
        static float MineRadius(int tier) => 1.65f + tier * .23f;
        static float TurretRadius(int tier) => 3.1f + tier * .16f;

        LineRenderer CreateRing(Transform parent, Color color)
        {
            var ring = new GameObject("Seed Glow").AddComponent<LineRenderer>();
            ring.transform.SetParent(parent, false);
            ring.useWorldSpace = false;
            ring.sortingOrder = 3;
            ring.widthMultiplier = .055f;
            ring.startColor = ring.endColor = color;
            if (effectMaterial)
                ring.sharedMaterial = effectMaterial;
            return ring;
        }

        static void DrawRing(LineRenderer ring, float radius, float fill, float alpha)
        {
            Color color = ring.startColor;
            color.a = alpha;
            ring.startColor = ring.endColor = color;
            int points = Mathf.Max(2, Mathf.CeilToInt(RingPoints * fill) + 1);
            ring.positionCount = points;
            for (int index = 0; index < points; index++) {
                float angle = Mathf.PI * (0.5f - 2f * fill * index / (points - 1));
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        void RemoveSeed(int index)
        {
            seeds[index].root.SetActive(false);
            Destroy(seeds[index].root);
            seeds.RemoveAt(index);
        }

        internal void Clear()
        {
            seedGuide?.Hide();
            for (int index = seeds.Count - 1; index >= 0; index--)
                RemoveSeed(index);
            foreach (Burst burst in bursts) {
                burst.ring.enabled = false;
                availableBursts.Push(burst.ring);
            }
            bursts.Clear();
        }
    }
}
