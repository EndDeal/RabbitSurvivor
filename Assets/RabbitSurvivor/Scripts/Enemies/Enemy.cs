using System.Collections;
using UnityEngine;

namespace RabbitSurvivor
{
    public class Enemy : MonoBehaviour
    {
        const float BarrierLifetime = 24f;
        const float BarrierScale = 1.25f;
        const float ShotRange = 3.8f;
        const float ShotWindup = 0.45f;
        const float ShotCooldown = 2.1f;
        const float FurrowPushSpeed = 7.5f;
        const float ObstacleLookAhead = 1.05f;
        const float BossOrbitHitInterval = .35f;
        const float RiceWallDamageInterval = .8f;
        const float RiceWallContactMargin = .07f;

        static readonly int Hit = Animator.StringToHash("Hit");
        static readonly int Dead = Animator.StringToHash("Dead");

        public float speed;
        public float health;
        public float maxHealth;
        public RuntimeAnimatorController[] animCon;
        public Rigidbody2D target;

        public BossType Boss { get; private set; }
        public bool IsBoss => Boss != BossType.None;
        public bool IsAlive => isLive;
        public int SpriteType { get; private set; }
        public int ExperienceReward { get; private set; }
        public EnemyBehavior Behavior { get; private set; }
        public bool IsBarrier => Behavior == EnemyBehavior.Barrier;
        internal Enemy Summoner { get; set; }
        internal bool IsDashing => bossAttacks && bossAttacks.IsDashing;

        internal float ContactDamage => Boss == BossType.Final ? 20f : Boss == BossType.Mid ? 16f :
            IsBarrier ? 8f : Behavior == EnemyBehavior.Shooter ? 4f : chaseContactDamage;

        bool isLive;
        Rigidbody2D rigid;
        CapsuleCollider2D coll;
        Animator anim;
        SpriteRenderer spriter;
        WaitForFixedUpdate wait;
        Vector3 defaultScale;
        Vector2 defaultColliderSize;
        RigidbodyConstraints2D defaultConstraints;
        Color defaultColor;
        SpriteHitFlash hitFlash;
        float recycleAt;
        float barrierExpiresAt;
        float chaseContactDamage;
        Sprite attackPose;
        Camera sceneCamera;
        int projectileIndex;
        Vector2 shotDirection;
        float windupRemaining;
        float nextShotAt;
        BossAttacks bossAttacks;
        readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[24];
        ContactFilter2D obstacleFilter;
        Vector2 detourDirection;
        Vector2 detourHeading;
        Vector2 pushDirection;
        float pushRemaining;
        float nextRiceWallDamageAt;
        float nextBossOrbitHitAt;

        void Awake()
        {
            rigid = GetComponent<Rigidbody2D>();
            coll = GetComponent<CapsuleCollider2D>();
            anim = GetComponent<Animator>();
            spriter = GetComponent<SpriteRenderer>();
            wait = new WaitForFixedUpdate();
            defaultScale = transform.localScale;
            defaultColliderSize = coll.size;
            defaultConstraints = rigid.constraints;
            defaultColor = spriter.color;
            obstacleFilter.SetLayerMask((1 << SeafloorRocks.Layer) | (1 << SeedCrops.RiceWallLayer) |
                (1 << gameObject.layer));
            obstacleFilter.useTriggers = false;
        }

        void FixedUpdate()
        {
            if (!GameManager.instance.isLive || !isLive)
                return;

            if (IsBarrier) {
                if (Time.time >= barrierExpiresAt)
                    gameObject.SetActive(false);
                return;
            }

            if (pushRemaining > 0f) {
                float step = Mathf.Min(FurrowPushSpeed * Time.fixedDeltaTime, pushRemaining);
                if (FindObstacle(pushDirection, step + 0.025f, out RaycastHit2D obstacle)) {
                    step = Mathf.Min(step, Mathf.Max(0f, obstacle.distance - 0.025f));
                    pushRemaining = step;
                }
                pushRemaining -= step;
                rigid.MovePosition(rigid.position + pushDirection * step);
                return;
            }

            if (!IsBoss && anim.GetCurrentAnimatorStateInfo(0).shortNameHash == Hit)
                return;

            Vector2 direction = target.position - rigid.position;
            if (bossAttacks && bossAttacks.isActiveAndEnabled && bossAttacks.Step(direction))
                return;
            if (Boss == BossType.Final)
                return;
            if (Behavior == EnemyBehavior.Shooter && AimOrShoot(direction))
                return;

            if (!IsBoss && Behavior == EnemyBehavior.Chase && GameManager.instance.Crops)
                direction = GameManager.instance.Crops.ChaseTarget(rigid.position, target.position) - rigid.position;

            Vector2 movement = SteerAroundObstacles(direction) * speed * Time.fixedDeltaTime;
            if (!isLive || !GameManager.instance.isLive)
                return;
            rigid.MovePosition(rigid.position + movement);
            rigid.velocity = Vector2.zero;
        }

        Vector2 SteerAroundObstacles(Vector2 toPlayer)
        {
            Vector2 forward = toPlayer.normalized;
            if (forward == Vector2.zero)
                return forward;

            float clearDistance = detourHeading == Vector2.zero ?
                Mathf.Min(toPlayer.magnitude, ObstacleLookAhead) : toPlayer.magnitude;
            if (!FindObstacle(forward, clearDistance, out RaycastHit2D obstacle)) {
                detourDirection = Vector2.zero;
                detourHeading = Vector2.zero;
                return forward;
            }

            if (obstacle.collider.gameObject.layer == SeedCrops.RiceWallLayer) {
                if (obstacle.distance <= RiceWallContactMargin)
                    TakeRiceWallDamage(obstacle.collider);
                else if (detourHeading == Vector2.zero)
                    return forward;
            }

            Vector2 normal = obstacle.normal;
            if (obstacle.distance <= 0.025f) {
                ColliderDistance2D separation = coll.Distance(obstacle.collider);
                if (separation.isValid && separation.isOverlapped)
                    normal = -separation.normal;
            }
            Vector2 tangent = Vector2.Perpendicular(normal);
            if (detourHeading == Vector2.zero) {
                float alignment = Vector2.Dot(tangent, forward);
                float side = Mathf.Abs(alignment) > 0.05f ? Mathf.Sign(alignment) : rigid.position.y < target.position.y ? -1f : 1f;
                detourHeading = tangent * side;
            }

            if (!FindObstacle(detourHeading, 0.36f, out _)) {
                detourDirection = detourHeading;
                return detourHeading;
            }

            Vector2 reference = detourDirection == Vector2.zero ? detourHeading : detourDirection;
            Vector2 around = Vector2.Dot(tangent, reference) < 0f ? -tangent : tangent;
            for (int angle = 0; angle <= 90; angle += 30) {
                float radians = angle * Mathf.Deg2Rad;
                Vector2 candidate = around * Mathf.Cos(radians) + normal * Mathf.Sin(radians);
                if (!FindObstacle(candidate, 0.36f, out _)) {
                    detourDirection = candidate;
                    return candidate;
                }
            }
            return Vector2.zero;
        }

        bool FindObstacle(Vector2 direction, float distance, out RaycastHit2D nearest)
        {
            int count = coll.Cast(direction, obstacleFilter, obstacleHits, distance);
            nearest = default;
            float closest = float.PositiveInfinity;
            for (int index = 0; index < count; index++) {
                RaycastHit2D hit = obstacleHits[index];
                int layer = hit.collider.gameObject.layer;
                if (layer != SeafloorRocks.Layer && layer != SeedCrops.RiceWallLayer &&
                    (!hit.collider.TryGetComponent(out Enemy enemy) || !enemy.IsAlive || !enemy.IsBarrier))
                    continue;
                if (hit.distance <= 0.025f) {
                    ColliderDistance2D separation = coll.Distance(hit.collider);
                    if (separation.isValid && Vector2.Dot(direction, separation.normal) <= 0.001f)
                        continue;
                }
                if (hit.distance >= closest)
                    continue;
                closest = hit.distance;
                nearest = hit;
            }
            return nearest.collider;
        }

        void LateUpdate()
        {
            if (!isLive && Time.unscaledTime >= recycleAt) {
                gameObject.SetActive(false);
                return;
            }

            GameManager game = GameManager.instance;
            if (game.IsEnding && bossAttacks && bossAttacks.enabled)
                bossAttacks.enabled = false;

            if (game.isLive && isLive && !IsBarrier) {
                bool bossAiming = bossAttacks && bossAttacks.IsAiming;
                if (bossAiming || IsDashing)
                    spriter.flipX = bossAttacks.Aim.x < 0f;
                else
                    spriter.flipX = windupRemaining > 0f ? shotDirection.x < 0f : target.position.x < rigid.position.x;
                if ((windupRemaining > 0f || bossAiming) && attackPose)
                    spriter.sprite = attackPose;
            }

            hitFlash?.Refresh();
        }

        void OnEnable()
        {
            target = GameManager.instance.player.GetComponent<Rigidbody2D>();
            isLive = true;
            hitFlash?.Reset();
            coll.enabled = true;
            rigid.simulated = true;
            spriter.sortingOrder = 2;
            anim.enabled = true;
            anim.SetBool(Dead, false);
            health = maxHealth;
            nextRiceWallDamageAt = 0f;
            nextBossOrbitHitAt = 0f;
        }

        public void Init(SpawnData data, BossType boss = BossType.None, RuntimeAnimatorController animation = null)
        {
            if (bossAttacks)
                bossAttacks.enabled = false;

            anim.runtimeAnimatorController = animation ? animation : animCon[data.spriteType];
            anim.ResetTrigger(Hit);
            anim.Play("Run", 0, 0f);
            speed = data.speed;
            maxHealth = data.health;
            chaseContactDamage = data.contactDamage;
            health = maxHealth;
            Boss = boss;
            Summoner = null;
            SpriteType = data.spriteType;
            Behavior = data.behavior;
            attackPose = data.attackPose;
            projectileIndex = data.projectile ? System.Array.IndexOf(GameManager.instance.pool.prefabs, data.projectile) : -1;
            sceneCamera = IsBoss || Behavior == EnemyBehavior.Shooter ? Camera.main : null;
            windupRemaining = 0f;
            nextShotAt = Time.time;
            barrierExpiresAt = Time.time + BarrierLifetime;
            rigid.velocity = Vector2.zero;
            detourDirection = Vector2.zero;
            detourHeading = Vector2.zero;
            pushRemaining = 0f;
            rigid.constraints = IsBarrier || Boss == BossType.Final ? RigidbodyConstraints2D.FreezeAll : defaultConstraints;
            coll.size = Boss == BossType.Final ? new Vector2(4.5f, 3.6f) :
                Boss == BossType.Mid ? new Vector2(3.6f, 3.4f) :
                IsBarrier ? new Vector2(8.8f, 8.8f) :
                Behavior == EnemyBehavior.Shooter ? new Vector2(5.2f, 4.5f) :
                animation && data.spriteType == 0 ? new Vector2(9.4f, 7.2f) :
                animation && data.spriteType == 1 ? new Vector2(8.5f, 8.2f) : defaultColliderSize;
            spriter.flipX = false;
            ExperienceReward = Mathf.Max(1, data.experience);
            float scale = boss == BossType.Final ? 4.2f : IsBoss ? 3.4f :
                IsBarrier ? BarrierScale : animation && Behavior == EnemyBehavior.Chase ? (data.spriteType == 0 ? 0.60f : 0.70f) : 1f;
            transform.localScale = defaultScale * scale;
            spriter.color = IsBoss && !animation ? new Color(1f, 0.77f, 0.38f) : defaultColor;
        }

        internal void ConfigureBossAttacks(Spawner spawner, Material guideMaterial)
        {
            if (!bossAttacks)
                bossAttacks = gameObject.AddComponent<BossAttacks>();
            bossAttacks.enabled = true;
            bossAttacks.Begin(spawner, projectileIndex, guideMaterial);
        }

        bool AimOrShoot(Vector2 direction)
        {
            if (windupRemaining > 0f) {
                windupRemaining -= Time.fixedDeltaTime;
                if (windupRemaining <= 0f) {
                    var projectile = GameManager.instance.pool.Get(projectileIndex).GetComponent<EnemyProjectile>();
                    projectile.Launch(rigid.position + shotDirection * 0.32f, shotDirection);
                    nextShotAt = Time.time + ShotCooldown;
                }
                return true;
            }

            if (projectileIndex < 0 || direction.sqrMagnitude > ShotRange * ShotRange || !IsInView() ||
                FindObstacle(direction.normalized, direction.magnitude, out _))
                return false;

            rigid.velocity = Vector2.zero;
            if (Time.time >= nextShotAt) {
                shotDirection = direction.normalized;
                windupRemaining = ShotWindup;
            }
            return true;
        }

        internal bool IsInView()
        {
            if (!sceneCamera)
                return true;

            Vector3 view = sceneCamera.WorldToViewportPoint(transform.position);
            return view.z > 0f && view.x > 0.04f && view.x < 0.96f && view.y > 0.04f && view.y < 0.96f;
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Bullet") || !collision.TryGetComponent(out Bullet bullet))
                return;

            if (IsBoss && bullet.per == Bullet.InfinitePierce) {
                if (!isLive || !GameManager.instance.isLive || Time.time < nextBossOrbitHitAt)
                    return;

                nextBossOrbitHitAt = Time.time + BossOrbitHitInterval;
            }
            TakeDamage(bullet.damage);
        }

        public void TakeDamage(float damage) => ApplyDamage(damage, Vector2.zero, 0f);

        internal void TakeJabDamage(float damage) => ApplyDamage(damage, Vector2.zero, 0f, knockBack: false);

        void OnCollisionEnter2D(Collision2D collision) => OnCollisionStay2D(collision);

        void OnCollisionStay2D(Collision2D collision)
        {
            if (collision.gameObject.layer == SeedCrops.RiceWallLayer)
                TakeRiceWallDamage(collision.collider);
        }

        void TakeRiceWallDamage(Collider2D wall)
        {
            if (!isLive || !GameManager.instance.isLive || Time.time < nextRiceWallDamageAt)
                return;

            float damage = GameManager.instance.Crops ? GameManager.instance.Crops.RiceContactDamage(wall) : 0f;
            if (damage <= 0f)
                return;
            nextRiceWallDamageAt = Time.time + RiceWallDamageInterval;
            ApplyDamage(damage, Vector2.zero, 0f, stagger: false);
        }

        internal void TakeFurrowDamage(float damage, Vector2 direction, float distance) =>
            ApplyDamage(damage, direction, distance);

        internal void CrushBarrier() => ApplyDamage(health, Vector2.zero, 0f, awardKill: false);

        void ApplyDamage(float damage, Vector2 direction, float pushDistance, bool stagger = true, bool knockBack = true,
            bool awardKill = true)
        {
            if (!isLive || damage <= 0f)
                return;

            GameManager game = GameManager.instance;
            health -= damage;
            if (windupRemaining > 0f) {
                windupRemaining = 0f;
                nextShotAt = Time.time + ShotCooldown;
            }
            if (game.isLive) {
                hitFlash ??= new SpriteHitFlash(spriter);
                hitFlash.Show(game.HitFlashMaterial, health <= 0f);
            }

            if (health > 0f) {
                if (!stagger)
                    return;
                if (!IsBoss && !IsBarrier) {
                    if (pushDistance > 0f) {
                        pushDirection = direction;
                        pushRemaining = Mathf.Max(pushRemaining, pushDistance);
                        rigid.velocity = Vector2.zero;
                    }
                    else if (knockBack && pushRemaining <= 0f) {
                        StartCoroutine(KnockBack());
                    }
                }
                anim.SetTrigger(Hit);
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Hit);
                return;
            }

            isLive = false;
            if (bossAttacks)
                bossAttacks.enabled = false;
            recycleAt = Time.unscaledTime + game.CorpseLifetime;
            coll.enabled = false;
            rigid.simulated = false;
            spriter.sortingOrder = 1;
            anim.SetBool(Dead, true);

            if (awardKill)
                game.RegisterKill(this);

            if (game.isLive)
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Dead);
        }

        IEnumerator KnockBack()
        {
            yield return wait;
            if (!isLive || pushRemaining > 0f)
                yield break;
            Vector3 playerPosition = GameManager.instance.player.transform.position;
            Vector3 direction = transform.position - playerPosition;
            rigid.AddForce(direction.normalized * 3f, ForceMode2D.Impulse);
        }

    }
}
