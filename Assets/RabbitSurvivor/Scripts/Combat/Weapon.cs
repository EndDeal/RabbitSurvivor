using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    public class Weapon : MonoBehaviour
    {
        const float BurstShotInterval = 0.12f;
        const float PulseDuration = 0.25f;
        const int PulseSegments = 40;
        const int ScytheSegments = 16;
        const float ScytheSlashDuration = 0.18f;

        public float Damage { get; private set; }
        public int Count { get; private set; }
        public bool IsEvolved { get; private set; }

        ItemData data;
        Player player;
        int rank;
        int prefabId;
        float cooldown;
        float attackInterval;
        float attackRadius;
        float attackAreaMultiplier;
        Color attackColor;
        int burstShots;
        float burstCooldown;
        Vector2 burstDirection;
        LineRenderer pulse;
        float pulseTime;
        float pulseRadius;
        Vector3 pulseCenter;
        readonly List<Collider2D> pulseHits = new List<Collider2D>();
        readonly List<Collider2D> groundTargets = new List<Collider2D>();
        ContactFilter2D enemyFilter;
        SpriteRenderer swing;
        float swingRemaining;
        LineRenderer scytheSlash;
        float scytheSlashRemaining;
        float scytheSlashRadius;
        Vector2 scytheOrigin;
        float scytheHeading;
        LineRenderer furrowTrail;
        float furrowRemaining;

        public void Init(ItemData item)
        {
            data = item;
            player = GameManager.instance.player;
            transform.SetParent(player.transform, true);
            transform.localPosition = Vector3.zero;
            enemyFilter.SetLayerMask(player.scanner.targetLayer);
            enemyFilter.useTriggers = false;

            if (data.projectile) {
                prefabId = System.Array.IndexOf(GameManager.instance.pool.prefabs, data.projectile);
            }

            if (data.itemType == ItemData.ItemType.Melee && data.hand) {
                player.hands[0].Equip(data.hand);
            }

            if (data.itemType == ItemData.ItemType.Pulse || data.itemType == ItemData.ItemType.GroundStrike)
                CreatePulse();
            if (data.itemType == ItemData.ItemType.Sweep)
                CreateScytheSlash();
            if (data.itemType == ItemData.ItemType.Furrow)
                CreateFurrowTrail();
            if ((data.IsToolStrike || data.itemType == ItemData.ItemType.Pulse) && data.projectileSprite) {
                swing = new GameObject("Tool Swing").AddComponent<SpriteRenderer>();
                swing.transform.SetParent(transform, false);
                swing.sortingOrder = player.GetComponent<SpriteRenderer>().sortingOrder + 1;
                if (data.pulseMaterial)
                    swing.sharedMaterial = data.pulseMaterial;
                swing.sprite = data.projectileSprite;
                swing.enabled = false;
            }

            LevelUp(1);
        }

        public void LevelUp(int level)
        {
            rank = level;
            RefreshStats();
        }

        public void Evolve()
        {
            if (IsEvolved)
                return;

            IsEvolved = true;
            name = data.evolutionName;
            if (swing && data.evolutionIcon)
                swing.sprite = data.evolutionIcon;
            RefreshStats();
        }

        public void RefreshStats()
        {
            Damage = DamageAtLevel(rank);
            Count = data.WeaponCountAt(rank);
            if (data.itemType == ItemData.ItemType.Range || data.itemType == ItemData.ItemType.Spread ||
                data.itemType == ItemData.ItemType.Burst)
                Count += Character.Count;
            attackInterval = data.attackInterval * Character.WeaponRate * (1f - player.AttackHaste);
            attackAreaMultiplier = (1f + player.AttackArea) * (IsEvolved ? data.evolutionArea : 1f);
            float rankProgress = (rank - 1f) / Mathf.Max(1, data.MaxLevel - 1);
            attackAreaMultiplier *= Mathf.Lerp(data.startingAttackArea, 1f, rankProgress);
            attackRadius = data.attackRadius * attackAreaMultiplier;
            attackColor = data.attackColor;

            if (IsEvolved) {
                Count += data.evolutionCount;
                attackInterval *= data.evolutionInterval;
                attackColor = Color.Lerp(attackColor, new Color(1f, 0.77f, 0.29f), 0.65f);
            }

            if (data.itemType == ItemData.ItemType.Melee)
                ArrangeOrbit();
        }

        internal float DamageAtLevel(int level)
        {
            return data.WeaponDamageAt(level) * Character.Damage * (1f + player.AttackPower) *
                (IsEvolved ? data.evolutionDamage : 1f);
        }

        void Update()
        {
            if (!GameManager.instance.isLive)
                return;

            if (data.itemType == ItemData.ItemType.Melee) {
                float rotation = 150f * Character.WeaponSpeed / (1f - player.AttackHaste);
                if (IsEvolved)
                    rotation /= data.evolutionInterval;
                transform.Rotate(Vector3.back * rotation * Time.deltaTime);
                return;
            }

            if (pulse && pulse.enabled)
                UpdatePulse();
            if (swing && swing.enabled)
                UpdateSwing();
            if (scytheSlash && scytheSlash.enabled)
                UpdateScytheSlash(Time.deltaTime);
            if (furrowTrail && furrowTrail.enabled)
                UpdateFurrowTrail();

            if (burstShots > 0) {
                burstCooldown -= Time.deltaTime;
                if (burstCooldown <= 0f) {
                    Transform target = player.scanner.nearestTarget;
                    if (target && target.gameObject.activeInHierarchy)
                        burstDirection = AimAt(target);
                    FireProjectile(burstDirection, 0);
                    burstCooldown = BurstShotInterval;
                    burstShots--;
                }
                return;
            }

            cooldown -= Time.deltaTime;
            if (cooldown > 0f)
                return;

            if (data.itemType == ItemData.ItemType.Pulse) {
                if (EmitPulse())
                    cooldown = attackInterval;
                return;
            }

            Transform nearest = player.scanner.nearestTarget;
            if (!nearest || !nearest.gameObject.activeInHierarchy)
                return;

            if (data.IsToolStrike) {
                if (Strike(nearest))
                    cooldown = attackInterval;
                return;
            }

            Vector2 direction = AimAt(nearest);
            cooldown = attackInterval;
            switch (data.itemType) {
                case ItemData.ItemType.Range:
                    FireProjectile(direction, Count);
                    break;
                case ItemData.ItemType.Spread:
                    for (int index = 0; index < Count; index++) {
                        float angle = (index - (Count - 1) * 0.5f) * 13f;
                        FireProjectile(Quaternion.Euler(0, 0, angle) * direction, IsEvolved ? 1 : 0);
                    }
                    break;
                case ItemData.ItemType.Burst:
                    FireProjectile(direction, 0);
                    burstDirection = direction;
                    burstShots = Count - 1;
                    burstCooldown = BurstShotInterval;
                    break;
            }
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Range);
        }

        Vector2 AimAt(Transform target)
        {
            return (target.position - transform.position).normalized;
        }

        void ArrangeOrbit()
        {
            Sprite sprite = IsEvolved && data.evolutionIcon ? data.evolutionIcon : data.projectileSprite;
            for (int index = 0; index < Count; index++) {
                Transform blade = index < transform.childCount ? transform.GetChild(index) :
                    GameManager.instance.pool.Get(prefabId).transform;
                blade.SetParent(transform, true);
                blade.localRotation = Quaternion.Euler(0f, 0f, 360f * index / Count);
                blade.position = transform.position + blade.up * attackRadius;
                blade.GetComponent<Bullet>().Init(Damage, Bullet.InfinitePierce, Vector2.zero, sprite, attackColor);
                if (sprite && blade.TryGetComponent(out BoxCollider2D hitbox)) {
                    Bounds art = sprite.bounds;
                    // Scale the visible blade and its attached hitbox along with the orbit.
                    blade.localScale = Vector3.one * (attackAreaMultiplier / (art.size.y * transform.lossyScale.y));
                    hitbox.size = art.size;
                    hitbox.offset = art.center;
                    blade.GetComponent<SpriteRenderer>().flipY = true;
                }
            }
        }

        void FireProjectile(Vector2 direction, int pierce)
        {
            Bullet bullet = GameManager.instance.pool.Get(prefabId).GetComponent<Bullet>();
            bullet.transform.SetPositionAndRotation(transform.position, Quaternion.FromToRotation(Vector3.up, direction));
            bullet.Init(Damage, pierce, direction, data.projectileSprite, attackColor);
        }

        void CreatePulse()
        {
            pulse = new GameObject("Weapon Pulse").AddComponent<LineRenderer>();
            pulse.transform.SetParent(transform, false);
            pulse.useWorldSpace = false;
            pulse.loop = true;
            pulse.positionCount = PulseSegments;
            pulse.widthMultiplier = 0.06f;
            pulse.sortingOrder = 4;
            pulse.sharedMaterial = data.pulseMaterial;
            pulse.enabled = false;

            for (int index = 0; index < PulseSegments; index++) {
                float angle = index * 2f * Mathf.PI / PulseSegments;
                pulse.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }

        bool Strike(Transform nearest)
        {
            Vector2 origin = transform.position;
            float length = attackRadius;
            float width = data.strikeWidth * attackAreaMultiplier;
            Vector2 direction = AimAt(nearest);
            if (direction == Vector2.zero)
                direction = player.LastMoveDirection == Vector2.zero ? Vector2.right : player.LastMoveDirection;
            Vector2 impact;
            Enemy jabTarget = null;
            switch (data.itemType) {
                case ItemData.ItemType.Sweep:
                    Vector2 input = player.inputVec;
                    if (input.sqrMagnitude > .01f)
                        direction = input.normalized;
                    else if (player.LastMoveDirection != Vector2.zero)
                        direction = player.LastMoveDirection;
                    impact = origin + direction * (length * .55f);
                    Physics2D.OverlapCircle(origin, length, enemyFilter, pulseHits);
                    break;
                case ItemData.ItemType.GroundStrike:
                    if (!FindGroundImpact(origin, length, width, out impact))
                        return false;
                    if ((impact - origin).sqrMagnitude > .0001f)
                        direction = (impact - origin).normalized;
                    Physics2D.OverlapCircle(impact, width, enemyFilter, pulseHits);
                    break;
                case ItemData.ItemType.Jab:
                    Physics2D.OverlapCircle(origin, length, enemyFilter, pulseHits);
                    jabTarget = FindJabTarget(origin);
                    if (!jabTarget)
                        return false;
                    impact = jabTarget.transform.position;
                    direction = AimAt(jabTarget.transform);
                    break;
                case ItemData.ItemType.Furrow:
                    impact = origin + direction * (length * .5f);
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    Physics2D.OverlapBox(impact, new Vector2(length, width), angle, enemyFilter, pulseHits);
                    break;
                default:
                    return false;
            }

            bool hitAny = false;
            foreach (Collider2D hit in pulseHits) {
                if (!hit.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                    continue;
                if (data.itemType == ItemData.ItemType.Jab && enemy != jabTarget)
                    continue;
                if (data.itemType == ItemData.ItemType.Sweep &&
                    Vector2.Dot(direction, ((Vector2)enemy.transform.position - origin).normalized) < 0.15f)
                    continue;
                if (data.itemType == ItemData.ItemType.Furrow)
                    enemy.TakeFurrowDamage(Damage, direction, IsEvolved ? 2.35f : 1.8f);
                else if (enemy == jabTarget)
                    enemy.TakeJabDamage(Damage);
                else
                    enemy.TakeDamage(Damage);
                hitAny = true;
            }

            if (hitAny) {
                if (data.itemType == ItemData.ItemType.Sweep) {
                    scytheOrigin = origin;
                    scytheSlashRadius = length * 0.82f;
                    scytheHeading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    scytheSlashRemaining = ScytheSlashDuration;
                    scytheSlash.enabled = true;
                    ShowSwing(impact, direction, 0.95f * attackAreaMultiplier);
                    UpdateScytheSlash(0f);
                }
                else if (data.itemType == ItemData.ItemType.Furrow) {
                    ShowFurrowTrail(origin, direction, length, width);
                    ShowSwing(origin + direction * 0.55f, direction, Mathf.Min(1.25f, 0.95f * attackAreaMultiplier));
                }
                else {
                    if (data.itemType == ItemData.ItemType.GroundStrike)
                        ShowPulse(impact, width);
                    ShowSwing(impact, direction, 1.3f * attackAreaMultiplier);
                }
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Melee);
            }
            return hitAny;
        }

        bool FindGroundImpact(Vector2 origin, float reach, float radius, out Vector2 impact)
        {
            Physics2D.OverlapCircle(origin, reach + radius, enemyFilter, groundTargets);
            impact = default;
            int bestCount = 0;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider2D candidate in groundTargets) {
                if (!candidate.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                    continue;

                Vector2 center = candidate.bounds.center;
                if ((center - origin).sqrMagnitude > reach * reach)
                    center = candidate.ClosestPoint(origin);
                float distance = (center - origin).sqrMagnitude;
                if (distance > reach * reach)
                    continue;

                int count = 0;
                foreach (Collider2D nearby in groundTargets) {
                    if (nearby.bounds.SqrDistance(center) <= radius * radius &&
                        nearby.TryGetComponent(out Enemy grouped) && grouped.IsAlive &&
                        (nearby.ClosestPoint(center) - center).sqrMagnitude <= radius * radius)
                        count++;
                }
                if (count > bestCount || (count == bestCount && distance < bestDistance)) {
                    bestCount = count;
                    bestDistance = distance;
                    impact = center;
                }
            }
            return bestCount > 0;
        }

        Enemy FindJabTarget(Vector2 origin)
        {
            Enemy best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in pulseHits) {
                if (!hit.TryGetComponent(out Enemy enemy) || !enemy.IsAlive)
                    continue;

                float distance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (best && (enemy.IsBoss != best.IsBoss ? !enemy.IsBoss :
                    enemy.maxHealth < best.maxHealth || (enemy.maxHealth == best.maxHealth && distance >= bestDistance)))
                    continue;

                best = enemy;
                bestDistance = distance;
            }
            return best;
        }

        bool EmitPulse()
        {
            Physics2D.OverlapCircle(transform.position, attackRadius, enemyFilter, pulseHits);
            if (pulseHits.Count == 0)
                return false;

            ShowPulse(transform.position, attackRadius);
            ShowSwing(pulseCenter + Vector3.up * 0.65f, Vector2.right, 1.45f * attackAreaMultiplier);

            foreach (Collider2D hit in pulseHits) {
                if (hit.TryGetComponent(out Enemy enemy))
                    enemy.TakeDamage(Damage);
            }
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Melee);
            return true;
        }

        void ShowPulse(Vector2 center, float radius)
        {
            pulseCenter = center;
            pulseRadius = radius;
            pulseTime = 0f;
            pulse.enabled = true;
            UpdatePulse();
        }

        void ShowSwing(Vector2 position, Vector2 direction, float length)
        {
            if (!swing)
                return;

            swing.transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, direction));
            swing.transform.localScale = Vector3.one * (length / (swing.sprite.bounds.size.y * transform.lossyScale.y));
            swingRemaining = PulseDuration;
            swing.color = attackColor;
            swing.enabled = true;
        }

        void UpdateSwing()
        {
            swingRemaining -= Time.deltaTime;
            Color color = attackColor;
            color.a *= Mathf.Clamp01(swingRemaining / (PulseDuration * 0.5f));
            swing.color = color;
            swing.enabled = swingRemaining > 0f;
        }

        void CreateScytheSlash()
        {
            scytheSlash = new GameObject("Scythe Slash").AddComponent<LineRenderer>();
            scytheSlash.transform.SetParent(transform, false);
            scytheSlash.sharedMaterial = data.pulseMaterial;
            scytheSlash.sortingOrder = 7;
            scytheSlash.positionCount = ScytheSegments + 1;
            scytheSlash.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f),
                new Keyframe(0.52f, 1f), new Keyframe(1f, 0.12f));
            scytheSlash.enabled = false;
        }

        void CreateFurrowTrail()
        {
            furrowTrail = new GameObject("Furrow Trail").AddComponent<LineRenderer>();
            furrowTrail.transform.SetParent(transform, false);
            furrowTrail.sharedMaterial = data.pulseMaterial;
            furrowTrail.sortingOrder = 4;
            furrowTrail.loop = true;
            furrowTrail.positionCount = 4;
            furrowTrail.widthMultiplier = .045f;
            furrowTrail.enabled = false;
        }

        void ShowFurrowTrail(Vector2 origin, Vector2 direction, float length, float width)
        {
            Vector2 side = Vector2.Perpendicular(direction) * (width * .5f);
            Vector2 end = origin + direction * length;
            furrowTrail.SetPosition(0, origin + side);
            furrowTrail.SetPosition(1, end + side);
            furrowTrail.SetPosition(2, end - side);
            furrowTrail.SetPosition(3, origin - side);
            furrowRemaining = PulseDuration;
            furrowTrail.startColor = furrowTrail.endColor = attackColor;
            furrowTrail.enabled = true;
        }

        void UpdateFurrowTrail()
        {
            furrowRemaining -= Time.deltaTime;
            Color color = attackColor;
            color.a *= Mathf.Clamp01(furrowRemaining / PulseDuration);
            furrowTrail.startColor = furrowTrail.endColor = color;
            furrowTrail.enabled = furrowRemaining > 0f;
        }

        void UpdateScytheSlash(float deltaTime)
        {
            scytheSlashRemaining -= deltaTime;
            float progress = 1f - Mathf.Clamp01(scytheSlashRemaining / ScytheSlashDuration);
            if (progress >= 1f) {
                scytheSlash.enabled = false;
                return;
            }

            float edge = Mathf.Lerp(-76f, 76f, Mathf.Clamp01(progress * 2f + 0.24f));
            float start = Mathf.Max(-76f, edge - 112f);
            for (int index = 0; index <= ScytheSegments; index++) {
                float angle = (scytheHeading + Mathf.Lerp(start, edge, index / (float)ScytheSegments)) * Mathf.Deg2Rad;
                scytheSlash.SetPosition(index, scytheOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scytheSlashRadius);
            }
            Color color = IsEvolved ? new Color(1f, 0.84f, 0.38f) : new Color(0.82f, 1f, 0.94f);
            color.a = Mathf.Lerp(0.9f, 0f, progress);
            scytheSlash.startColor = scytheSlash.endColor = color;
            scytheSlash.widthMultiplier = Mathf.Lerp(0.14f, 0.03f, progress) * attackAreaMultiplier;
            if (swing && swing.enabled) {
                swing.transform.position = scytheOrigin +
                    (Vector2)(Quaternion.Euler(0f, 0f, scytheHeading + edge) * Vector3.right) * (scytheSlashRadius * 0.55f);
                swing.transform.rotation = Quaternion.Euler(0f, 0f, scytheHeading + edge - 72f);
            }
        }

        void UpdatePulse()
        {
            pulseTime += Time.deltaTime;
            float progress = Mathf.Clamp01(pulseTime / PulseDuration);
            pulse.transform.position = pulseCenter;
            pulse.transform.localScale = Vector3.one * pulseRadius * Mathf.Lerp(0.25f, 1f, progress);
            Color color = attackColor;
            color.a = 1f - progress;
            pulse.startColor = pulse.endColor = color;
            pulse.enabled = progress < 1f;
        }
    }
}
