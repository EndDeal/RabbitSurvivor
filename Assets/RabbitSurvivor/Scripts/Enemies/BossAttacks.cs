using System.Collections.Generic;
using UnityEngine;

namespace RabbitSurvivor
{
    public class BossAttacks : MonoBehaviour
    {
        const float CaptainWindup = 0.65f;
        const float CaptainDashDistance = 3.8f;
        const float CaptainDashSpeed = 8f;
        const float CaptainDashDamage = 12f;
        const float CaptainRecovery = 0.4f;
        const float CaptainCooldown = 6f;

        const int BubbleCount = 7;
        const float BubbleWindup = 0.55f;
        const float BubbleSpacing = 0.12f;
        const float BubbleAngleStep = 8f;
        const float BubbleSpeed = 3.8f;
        const float BubbleSpawnOffset = .6f;
        const float VolleyCooldown = 4f;
        const float SummonWindup = .8f;
        const float SummonMarkerRadius = .48f;
        const float SummonCooldown = 9f;
        const float DragonRange = 8f;
        const int GuardsPerSummon = 3;
        const int MaxGuards = 6;

        enum DashPhase { Chasing, Aiming, Dashing, Recovering }

        Enemy owner;
        Rigidbody2D body;
        Collider2D footprint;
        SpriteRenderer appearance;
        Spawner spawner;
        BossAttackTelegraph dashGuide;
        BossAttackTelegraph volleyGuide;
        readonly BossAttackTelegraph[] summonGuides = new BossAttackTelegraph[GuardsPerSummon];
        readonly Vector2[] summonPositions = new Vector2[GuardsPerSummon];
        readonly List<Enemy> guards = new List<Enemy>(MaxGuards);
        readonly List<RaycastHit2D> dashHits = new List<RaycastHit2D>(8);
        ContactFilter2D dashFilter;
        DashPhase dashPhase;
        Vector2 aim;
        float phaseUntil;
        float dashRemaining;
        float dashReadyAt;
        bool hitDuringDash;
        int projectileIndex;
        int bubblesRemaining;
        float nextBubbleAt;
        float volleyReadyAt;
        float summonReadyAt;
        float summonAt;
        int pendingGuards;

        internal bool IsDashing => dashPhase == DashPhase.Dashing;
        internal bool IsAiming => dashPhase == DashPhase.Aiming || bubblesRemaining > 0 || pendingGuards > 0;
        internal Vector2 Aim => aim;

        void Awake()
        {
            owner = GetComponent<Enemy>();
            body = GetComponent<Rigidbody2D>();
            footprint = GetComponent<Collider2D>();
            appearance = GetComponent<SpriteRenderer>();
            dashFilter.SetLayerMask((1 << SeafloorRocks.Layer) | (1 << gameObject.layer));
            dashFilter.useTriggers = false;
        }

        internal void Begin(Spawner source, int shotIndex, Material guideMaterial)
        {
            spawner = source;
            projectileIndex = shotIndex;
            dashReadyAt = Time.time + 2.2f;
            volleyReadyAt = Time.time + 2.2f;
            summonReadyAt = Time.time + 5f;
            if (!guideMaterial)
                return;
            if (owner.Boss == BossType.Mid)
                dashGuide ??= new BossAttackTelegraph(transform, guideMaterial, "Captain Dash Warning", new Color32(245, 61, 70, 255));
            else {
                volleyGuide ??= new BossAttackTelegraph(transform, guideMaterial, "Dragon Volley Warning", new Color32(231, 75, 152, 255));
                for (int index = 0; index < summonGuides.Length; index++)
                    summonGuides[index] ??= new BossAttackTelegraph(transform, guideMaterial, "Dragon Summon Warning", new Color32(255, 126, 73, 255));
            }
        }

        void OnDisable()
        {
            dashPhase = DashPhase.Chasing;
            bubblesRemaining = 0;
            pendingGuards = 0;
            guards.Clear();
            dashGuide?.Hide();
            volleyGuide?.Hide();
            foreach (BossAttackTelegraph guide in summonGuides)
                guide?.Hide();
        }

        void OnDestroy()
        {
            dashGuide?.Dispose();
            volleyGuide?.Dispose();
            foreach (BossAttackTelegraph guide in summonGuides)
                guide?.Dispose();
        }

        internal bool Step(Vector2 toPlayer)
        {
            if (owner.Boss == BossType.Mid)
                return StepCaptain(toPlayer);

            StepDragon(toPlayer);
            return true;
        }

        bool StepCaptain(Vector2 toPlayer)
        {
            float now = Time.time;
            switch (dashPhase) {
                case DashPhase.Aiming:
                    if (now < phaseUntil) {
                        DrawDashGuide();
                        return true;
                    }

                    dashGuide?.Hide();
                    dashPhase = DashPhase.Dashing;
                    dashRemaining = CaptainDashDistance;
                    hitDuringDash = false;
                    goto case DashPhase.Dashing;

                case DashPhase.Dashing:
                    if (dashRemaining > 0f) {
                        float distance = Mathf.Min(CaptainDashSpeed * Time.fixedDeltaTime, dashRemaining);
                        CrushDashObstacles(distance);
                        body.MovePosition(body.position + aim * distance);
                        dashRemaining -= distance;
                        return true;
                    }

                    body.velocity = Vector2.zero;
                    dashPhase = DashPhase.Recovering;
                    phaseUntil = now + CaptainRecovery;
                    dashReadyAt = now + CaptainCooldown;
                    return true;

                case DashPhase.Recovering:
                    if (now < phaseUntil)
                        return true;
                    dashPhase = DashPhase.Chasing;
                    return false;
            }

            if (now < dashReadyAt || toPlayer.sqrMagnitude > CaptainDashDistance * CaptainDashDistance ||
                toPlayer.sqrMagnitude < 0.36f || !owner.IsInView())
                return false;

            aim = toPlayer.normalized;
            body.velocity = Vector2.zero;
            dashPhase = DashPhase.Aiming;
            phaseUntil = now + CaptainWindup;
            DrawDashGuide();
            return true;
        }

        void CrushDashObstacles(float distance)
        {
            var rocks = GameManager.instance.Rocks;
            int count = footprint.Cast(aim, dashFilter, dashHits, distance);
            bool brokeRock = false;
            for (int index = 0; index < count; index++) {
                Collider2D obstacle = dashHits[index].collider;
                if (obstacle.gameObject.layer == SeafloorRocks.Layer)
                    brokeRock |= rocks && rocks.BreakRock(obstacle);
                else if (obstacle.TryGetComponent(out Enemy enemy) && enemy.IsBarrier)
                    enemy.CrushBarrier();
            }
            if (brokeRock)
                AudioManager.instance.PlayFeedback(AudioManager.Sfx.Hit, .7f, .8f);
        }

        void StepDragon(Vector2 toPlayer)
        {
            float now = Time.time;
            if (bubblesRemaining > 0) {
                if (now >= nextBubbleAt) {
                    volleyGuide?.Hide();
                    FireBubble(now);
                }
                else if (bubblesRemaining == BubbleCount)
                    DrawVolleyGuide(now);
            }
            else if (projectileIndex >= 0 && now >= volleyReadyAt && toPlayer.sqrMagnitude <= DragonRange * DragonRange) {
                aim = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.down;
                bubblesRemaining = BubbleCount;
                nextBubbleAt = now + BubbleWindup;
                DrawVolleyGuide(now);
            }

            if (pendingGuards > 0) {
                if (now >= summonAt) {
                    for (int index = 0; index < pendingGuards; index++) {
                        summonGuides[index]?.Hide();
                        guards.Add(spawner.SpawnDragonGuard(summonPositions[index], owner));
                    }
                    pendingGuards = 0;
                    summonReadyAt = now + SummonCooldown;
                }
                else
                    DrawSummonGuides(now);
            }
            else if (now >= summonReadyAt && toPlayer.sqrMagnitude <= DragonRange * DragonRange) {
                PrepareSummon(toPlayer, now);
            }
        }

        void FireBubble(float now)
        {
            float angle = ((BubbleCount + 1) * 0.5f - bubblesRemaining) * BubbleAngleStep;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            EnemyProjectile shot = GameManager.instance.pool.Get(projectileIndex).GetComponent<EnemyProjectile>();
            shot.Launch(body.position + direction * BubbleSpawnOffset, direction, damage: 9f, speed: BubbleSpeed, size: 1.25f);
            bubblesRemaining--;
            nextBubbleAt = now + BubbleSpacing;
            if (bubblesRemaining == 0)
                volleyReadyAt = now + VolleyCooldown;
        }

        void PrepareSummon(Vector2 toPlayer, float now)
        {
            guards.RemoveAll(guard => !guard || !guard.isActiveAndEnabled || !guard.IsAlive || guard.Summoner != owner);
            pendingGuards = Mathf.Min(GuardsPerSummon, MaxGuards - guards.Count);
            if (pendingGuards == 0) {
                summonReadyAt = now + SummonCooldown;
                return;
            }
            Vector2 awayFromPlayer = toPlayer.sqrMagnitude > .001f ? -toPlayer.normalized : Vector2.up;
            if (bubblesRemaining == 0)
                aim = -awayFromPlayer;
            for (int index = 0; index < pendingGuards; index++) {
                Vector2 direction = Quaternion.Euler(0f, 0f, (index - 1) * 80f) * awayFromPlayer;
                Vector2 extent = appearance.bounds.extents;
                float distance = Mathf.Abs(direction.x) * extent.x + Mathf.Abs(direction.y) * extent.y + SummonMarkerRadius;
                summonPositions[index] = SeafloorRocks.FreeSpawnPosition(body.position + direction * distance);
            }
            summonAt = now + SummonWindup;
            DrawSummonGuides(now);
        }

        void DrawVolleyGuide(float now)
        {
            float progress = 1f - (nextBubbleAt - now) / BubbleWindup;
            float radius = BubbleSpawnOffset + BubbleSpeed * EnemyProjectile.Lifetime;
            volleyGuide?.Sector(body.position, aim, radius, (BubbleCount - 1) * BubbleAngleStep, progress);
        }

        void DrawSummonGuides(float now)
        {
            float progress = 1f - (summonAt - now) / SummonWindup;
            for (int index = 0; index < pendingGuards; index++)
                summonGuides[index]?.Circle(summonPositions[index], SummonMarkerRadius, progress);
        }

        void DrawDashGuide()
        {
            Vector2 size = footprint.bounds.size;
            Vector2 side = new Vector2(-aim.y, aim.x);
            float width = Mathf.Abs(side.x) * size.x + Mathf.Abs(side.y) * size.y;
            float depth = Mathf.Abs(aim.x) * size.x + Mathf.Abs(aim.y) * size.y;
            Vector2 center = (Vector2)footprint.bounds.center + aim * (CaptainDashDistance * .5f);
            float progress = 1f - (phaseUntil - Time.time) / CaptainWindup;
            dashGuide?.Rectangle(center, aim, CaptainDashDistance + depth, width, progress);
        }

        void OnCollisionEnter2D(Collision2D collision) => HitDuringDash(collision);
        void OnCollisionStay2D(Collision2D collision) => HitDuringDash(collision);

        void HitDuringDash(Collision2D collision)
        {
            if (!IsDashing || hitDuringDash || !GameManager.instance.isLive ||
                !collision.gameObject.TryGetComponent(out Player player))
                return;

            hitDuringDash = true;
            player.TakeDamage(CaptainDashDamage);
        }
    }
}
