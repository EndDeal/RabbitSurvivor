using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class Weapon : MonoBehaviour
    {
        const float BurstShotInterval = 0.12f;
        const float PulseDuration = 0.25f;
        const int PulseSegments = 40;

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
        Vector3 pulseCenter;
        readonly List<Collider2D> pulseHits = new List<Collider2D>();

        public void Init(ItemData item)
        {
            data = item;
            player = GameManager.instance.player;
            transform.SetParent(player.transform, true);
            transform.localPosition = Vector3.zero;

            if (data.projectile) {
                prefabId = System.Array.IndexOf(GameManager.instance.pool.prefabs, data.projectile);
            }

            if (data.hand) {
                Hand hand = player.hands[data.itemType == ItemData.ItemType.Melee ? 0 : 1];
                hand.Equip(data.hand);
            }

            if (data.itemType == ItemData.ItemType.Pulse)
                CreatePulse();

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
            RefreshStats();
        }

        public void RefreshStats()
        {
            Damage = DamageAtLevel(rank);
            Count = data.WeaponCountAt(rank);
            if (data.itemType != ItemData.ItemType.Pulse)
                Count += Character.Count;
            attackInterval = data.attackInterval * Character.WeaponRate * (1f - player.AttackHaste);
            attackAreaMultiplier = (1f + player.AttackArea) * (IsEvolved ? data.evolutionArea : 1f);
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
            for (int index = 0; index < Count; index++) {
                Transform blade = index < transform.childCount ? transform.GetChild(index) :
                    GameManager.instance.pool.Get(prefabId).transform;
                blade.SetParent(transform, true);
                blade.localRotation = Quaternion.Euler(0f, 0f, 360f * index / Count);
                blade.position = transform.position + blade.up * attackRadius;
                blade.GetComponent<Bullet>().Init(Damage, Bullet.InfinitePierce, Vector2.zero, data.projectileSprite, attackColor);
                if (data.projectileSprite && blade.TryGetComponent(out BoxCollider2D hitbox)) {
                    Bounds art = data.projectileSprite.bounds;
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
            pulse = new GameObject("Coral Pulse").AddComponent<LineRenderer>();
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

        bool EmitPulse()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(player.scanner.targetLayer);
            Physics2D.OverlapCircle(transform.position, attackRadius, filter, pulseHits);
            if (pulseHits.Count == 0)
                return false;

            pulseCenter = transform.position;
            pulseTime = 0f;
            pulse.enabled = true;
            UpdatePulse();

            foreach (Collider2D hit in pulseHits) {
                if (hit.TryGetComponent(out Enemy enemy))
                    enemy.TakeDamage(Damage);
            }
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Melee);
            return true;
        }

        void UpdatePulse()
        {
            pulseTime += Time.deltaTime;
            float progress = Mathf.Clamp01(pulseTime / PulseDuration);
            pulse.transform.position = pulseCenter;
            pulse.transform.localScale = Vector3.one * attackRadius * Mathf.Lerp(0.25f, 1f, progress);
            Color color = attackColor;
            color.a = 1f - progress;
            pulse.startColor = pulse.endColor = color;
            pulse.enabled = progress < 1f;
        }
    }
}
