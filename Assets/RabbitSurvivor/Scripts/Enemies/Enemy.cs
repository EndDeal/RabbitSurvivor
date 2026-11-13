using System.Collections;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class Enemy : MonoBehaviour
    {
        const float BarrierLifetime = 24f;
        const float ShotRange = 3.8f;
        const float ShotWindup = 0.45f;
        const float ShotCooldown = 2.1f;

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

        internal float ContactDamage => Boss == BossType.Final ? 24f : Boss == BossType.Mid ? 16f :
            IsBarrier ? 8f : Behavior == EnemyBehavior.Shooter ? 4f : 10f;

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
        Sprite attackPose;
        Camera sceneCamera;
        int projectileIndex;
        Vector2 shotDirection;
        float windupRemaining;
        float nextShotAt;
        BossAttacks bossAttacks;

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

            if (!IsBoss && anim.GetCurrentAnimatorStateInfo(0).IsName("Hit"))
                return;

            Vector2 direction = target.position - rigid.position;
            if (bossAttacks && bossAttacks.isActiveAndEnabled && bossAttacks.Step(direction))
                return;
            if (Boss == BossType.Final)
                return;
            if (Behavior == EnemyBehavior.Shooter && AimOrShoot(direction))
                return;

            Vector2 movement = direction.normalized * speed * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + movement);
            rigid.velocity = Vector2.zero;
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
            anim.SetBool("Dead", false);
            health = maxHealth;
        }

        public void Init(SpawnData data, BossType boss = BossType.None, RuntimeAnimatorController animation = null)
        {
            if (bossAttacks)
                bossAttacks.enabled = false;

            anim.runtimeAnimatorController = animation ? animation : animCon[data.spriteType];
            anim.ResetTrigger("Hit");
            anim.Play("Run", 0, 0f);
            speed = data.speed;
            maxHealth = data.health;
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
            rigid.constraints = IsBarrier || Boss == BossType.Final ? RigidbodyConstraints2D.FreezeAll : defaultConstraints;
            coll.size = Boss == BossType.Final ? new Vector2(4.5f, 3.6f) :
                Boss == BossType.Mid ? new Vector2(3.6f, 3.4f) :
                IsBarrier ? new Vector2(8.5f, 8.5f) :
                Behavior == EnemyBehavior.Shooter ? new Vector2(5.2f, 4.5f) :
                animation && data.spriteType == 0 ? new Vector2(9.4f, 7.2f) :
                animation && data.spriteType == 1 ? new Vector2(8.5f, 8.2f) : defaultColliderSize;
            spriter.flipX = false;
            ExperienceReward = Mathf.Max(1, data.experience);
            float scale = boss == BossType.Final ? 2.1f : IsBoss ? 1.7f :
                animation && Behavior == EnemyBehavior.Chase ? 0.7f : 1f;
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

            if (projectileIndex < 0 || direction.sqrMagnitude > ShotRange * ShotRange || !IsInView())
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
            if (collision.CompareTag("Bullet") && collision.TryGetComponent(out Bullet bullet))
                TakeDamage(bullet.damage);
        }

        public void TakeDamage(float damage)
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
                if (!IsBoss && !IsBarrier)
                    StartCoroutine(KnockBack());
                anim.SetTrigger("Hit");
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
            anim.SetBool("Dead", true);

            game.RegisterKill(this);

            if (game.isLive)
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Dead);
        }

        IEnumerator KnockBack()
        {
            yield return wait;
            Vector3 playerPosition = GameManager.instance.player.transform.position;
            Vector3 direction = transform.position - playerPosition;
            rigid.AddForce(direction.normalized * 3f, ForceMode2D.Impulse);
        }

    }
}
