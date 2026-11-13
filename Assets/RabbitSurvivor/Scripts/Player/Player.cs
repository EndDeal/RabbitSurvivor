using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Goldmetal.UndeadSurvivor
{
    public class Player : MonoBehaviour
    {
        static readonly int DeadState = Animator.StringToHash("Dead");

        public Vector2 inputVec;
        public float speed;
        public Scanner scanner;
        public Hand[] hands;
        public RuntimeAnimatorController[] animCon;

        Rigidbody2D rigid;
        Collider2D hitbox;
        SpriteRenderer spriter;
        Animator anim;
        float baseMoveSpeed;
        SpriteHitFlash hitFlash;
        readonly RaycastHit2D[] movementHits = new RaycastHit2D[12];
        ContactFilter2D obstacleFilter;
        float touchDamage;
        float nextContactAt;

        public float AttackHaste { get; internal set; }
        public float AttackPower { get; internal set; }
        public float AttackArea { get; internal set; }
        public float DamageReduction { get; internal set; }
        public float PickupRange { get; internal set; }

        void Awake()
        {
            rigid = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            spriter = GetComponent<SpriteRenderer>();
            anim = GetComponent<Animator>();
            scanner = GetComponent<Scanner>();
            hands = GetComponentsInChildren<Hand>(true);
            baseMoveSpeed = speed;
            obstacleFilter.SetLayerMask(scanner.targetLayer | (1 << SeafloorRocks.Layer));
            obstacleFilter.useTriggers = false;
        }

        void OnEnable()
        {
            SetMoveSpeedBonus(0);
            hitbox.enabled = true;
            nextContactAt = 0f;
            hitFlash?.Reset();
            anim.runtimeAnimatorController = animCon[GameManager.instance.playerId];
        }

        public void SetMoveSpeedBonus(float rate)
        {
            speed = baseMoveSpeed * Character.Speed * (1f + rate);
        }

        void FixedUpdate()
        {
            GameManager game = GameManager.instance;
            if (!game.isLive)
                return;

            touchDamage = 0f;
            Vector2 movement = Vector2.ClampMagnitude(inputVec, 1f) * speed * Time.fixedDeltaTime;
            Vector2 destination = game.ConstrainPlayerPosition(rigid.position + movement);
            Vector2 step = destination - rigid.position;
            rigid.MovePosition(rigid.position + LimitMovement(step, true));
            if (touchDamage > 0f)
                TakeContactDamage(touchDamage);
        }

        Vector2 LimitMovement(Vector2 step, bool slide)
        {
            const float skin = 0.025f;
            float length = step.magnitude;
            if (length <= 0.0001f)
                return Vector2.zero;

            Vector2 direction = step / length;
            int count = hitbox.Cast(direction, obstacleFilter, movementHits, length + skin);
            float allowed = length;
            Vector2 normal = Vector2.zero;
            float contact = 0f;
            for (int index = 0; index < count; index++) {
                RaycastHit2D hit = movementHits[index];
                bool isEnemy = hit.collider.TryGetComponent(out Enemy enemy);
                if (isEnemy) {
                    if (!enemy.IsAlive)
                        continue;
                    if (hit.distance <= skin &&
                        Vector2.Dot(direction, rigid.position - (Vector2)enemy.transform.position) > 0f)
                        continue;
                }
                if (Vector2.Dot(direction, hit.normal) >= -0.001f || hit.distance >= allowed + skin)
                    continue;

                allowed = Mathf.Max(0f, hit.distance - skin);
                normal = hit.normal;
                contact = isEnemy && !enemy.IsDashing ? enemy.ContactDamage : 0f;
            }

            touchDamage = Mathf.Max(touchDamage, contact);
            if (!slide || normal == Vector2.zero)
                return direction * allowed;

            Vector2 tangent = step - normal * Vector2.Dot(step, normal);
            return LimitMovement(tangent, false);
        }

        void LateUpdate()
        {
            GameManager game = GameManager.instance;
            if (game.isLive) {
                KeepInsideArena(game);
                anim.SetFloat("Speed", inputVec.magnitude);
                if (inputVec.x != 0)
                    spriter.flipX = inputVec.x < 0;
            }

            hitFlash?.Refresh();
        }

        void KeepInsideArena(GameManager game)
        {
            Vector2 position = rigid.position;
            Vector2 constrained = game.ConstrainPlayerPosition(position);
            Vector2 overshoot = position - constrained;
            if (overshoot.sqrMagnitude <= 0.000001f)
                return;

            Vector2 outward = overshoot.normalized;
            rigid.velocity -= outward * Mathf.Max(0f, Vector2.Dot(rigid.velocity, outward));
            rigid.position = constrained;
            transform.position = new Vector3(constrained.x, constrained.y, transform.position.z);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (collision.gameObject.layer == SeafloorRocks.Layer)
                return;

            if (collision.gameObject.TryGetComponent(out Enemy enemy)) {
                if (!enemy.IsDashing && touchDamage == 0f)
                    TakeContactDamage(enemy.ContactDamage);
                return;
            }
            TakeContactDamage(10f);
        }

        void TakeContactDamage(float damagePerSecond)
        {
            const float interval = 0.28f;
            if (Time.time < nextContactAt)
                return;

            nextContactAt = Time.time + interval;
            TakeDamage(damagePerSecond * interval);
        }

        internal void TakeDamage(float amount)
        {
            GameManager game = GameManager.instance;
            if (!game.isLive)
                return;

            game.health = Mathf.Max(0f, game.health - amount * (1f - DamageReduction));
            bool lethal = game.health <= 0f;
            hitFlash ??= new SpriteHitFlash(spriter);
            hitFlash.Show(game.HitFlashMaterial, lethal);

            if (lethal)
                game.GameOver();
        }

        internal IEnumerator PlayDeathAnimation()
        {
            if (!gameObject.activeInHierarchy)
                yield break;

            rigid.velocity = Vector2.zero;
            hitbox.enabled = false;
            foreach (Hand hand in hands)
                hand.gameObject.SetActive(false);
            foreach (Weapon weapon in GetComponentsInChildren<Weapon>())
                weapon.gameObject.SetActive(false);

            if (!anim.isActiveAndEnabled)
                yield break;

            AnimatorUpdateMode previousMode = anim.updateMode;
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.SetTrigger(DeadState);

            float deadline = Time.unscaledTime + 2f;
            while (Time.unscaledTime < deadline) {
                AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == DeadState && state.normalizedTime >= 1f)
                    break;
                yield return null;
            }

            anim.updateMode = previousMode;
        }

        void OnMove(InputValue value)
        {
            inputVec = value.Get<Vector2>();
        }
    }
}
