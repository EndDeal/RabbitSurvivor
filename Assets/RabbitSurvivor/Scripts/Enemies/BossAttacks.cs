using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class BossAttacks : MonoBehaviour
    {
        const float CaptainWindup = 0.65f;
        const float CaptainDashDistance = 3.8f;
        const float CaptainDashSpeed = 8f;
        const float CaptainDashDamage = 14f;
        const float CaptainRecovery = 0.4f;
        const float CaptainCooldown = 6f;

        const int BubbleCount = 7;
        const float BubbleWindup = 0.55f;
        const float BubbleSpacing = 0.12f;
        const float VolleyCooldown = 4f;
        const float SummonCooldown = 9f;
        const float DragonRange = 8f;
        const int GuardsPerSummon = 3;
        const int MaxGuards = 6;

        enum DashPhase { Chasing, Aiming, Dashing, Recovering }

        Enemy owner;
        Rigidbody2D body;
        Spawner spawner;
        LineRenderer dashGuide;
        readonly List<Enemy> guards = new List<Enemy>(MaxGuards);
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

        internal bool IsDashing => dashPhase == DashPhase.Dashing;
        internal bool IsAiming => dashPhase == DashPhase.Aiming || bubblesRemaining > 0;
        internal Vector2 Aim => aim;

        void Awake()
        {
            owner = GetComponent<Enemy>();
            body = GetComponent<Rigidbody2D>();
        }

        internal void Begin(Spawner source, int shotIndex, Material guideMaterial)
        {
            spawner = source;
            projectileIndex = shotIndex;
            dashReadyAt = Time.time + 2.2f;
            volleyReadyAt = Time.time + 2.2f;
            summonReadyAt = Time.time + 5f;
            if (owner.Boss == BossType.Mid && !dashGuide)
                CreateDashGuide(guideMaterial);
        }

        void OnDisable()
        {
            dashPhase = DashPhase.Chasing;
            bubblesRemaining = 0;
            guards.Clear();
            if (dashGuide)
                dashGuide.enabled = false;
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

                    if (dashGuide)
                        dashGuide.enabled = false;
                    dashPhase = DashPhase.Dashing;
                    dashRemaining = CaptainDashDistance;
                    hitDuringDash = false;
                    goto case DashPhase.Dashing;

                case DashPhase.Dashing:
                    if (dashRemaining > 0f) {
                        float distance = Mathf.Min(CaptainDashSpeed * Time.fixedDeltaTime, dashRemaining);
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

        void StepDragon(Vector2 toPlayer)
        {
            float now = Time.time;
            if (bubblesRemaining > 0) {
                if (now >= nextBubbleAt)
                    FireBubble(now);
            }
            else if (projectileIndex >= 0 && now >= volleyReadyAt && toPlayer.sqrMagnitude <= DragonRange * DragonRange) {
                aim = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector2.down;
                bubblesRemaining = BubbleCount;
                nextBubbleAt = now + BubbleWindup;
            }

            if (now >= summonReadyAt && toPlayer.sqrMagnitude <= DragonRange * DragonRange) {
                SummonGuards(toPlayer.sqrMagnitude > 0.001f ? -toPlayer.normalized : Vector2.up);
                summonReadyAt = now + SummonCooldown;
            }
        }

        void FireBubble(float now)
        {
            float angle = ((BubbleCount + 1) * 0.5f - bubblesRemaining) * 8f;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            EnemyProjectile shot = GameManager.instance.pool.Get(projectileIndex).GetComponent<EnemyProjectile>();
            shot.Launch(body.position + direction * 0.6f, direction, damage: 9f, speed: 4.2f, size: 1.25f);
            bubblesRemaining--;
            nextBubbleAt = now + BubbleSpacing;
            if (bubblesRemaining == 0)
                volleyReadyAt = now + VolleyCooldown;
        }

        void SummonGuards(Vector2 awayFromPlayer)
        {
            guards.RemoveAll(guard => !guard || !guard.isActiveAndEnabled || !guard.IsAlive || guard.Summoner != owner);
            int count = Mathf.Min(GuardsPerSummon, MaxGuards - guards.Count);
            for (int index = 0; index < count; index++) {
                Vector2 direction = Quaternion.Euler(0f, 0f, (index - 1) * 80f) * awayFromPlayer;
                guards.Add(spawner.SpawnDragonGuard(body.position + direction * 1.45f, owner));
            }
        }

        void CreateDashGuide(Material material)
        {
            if (!material)
                return;

            dashGuide = new GameObject("Captain Dash Guide").AddComponent<LineRenderer>();
            dashGuide.transform.SetParent(transform, false);
            dashGuide.sharedMaterial = material;
            dashGuide.useWorldSpace = true;
            dashGuide.sortingOrder = 1;
            dashGuide.positionCount = 2;
            dashGuide.numCapVertices = 4;
            dashGuide.startWidth = 0.5f;
            dashGuide.endWidth = 0.28f;
            dashGuide.startColor = new Color32(246, 127, 82, 194);
            dashGuide.endColor = new Color32(255, 206, 118, 175);
            dashGuide.enabled = false;
        }

        void DrawDashGuide()
        {
            if (!dashGuide)
                return;

            Vector2 position = body.position;
            dashGuide.SetPosition(0, position + aim * 0.3f);
            dashGuide.SetPosition(1, position + aim * CaptainDashDistance);
            dashGuide.enabled = true;
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
