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

        public float AttackHaste { get; internal set; }
        public float AttackPower { get; internal set; }
        public float AttackArea { get; internal set; }

        void Awake()
        {
            rigid = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<Collider2D>();
            spriter = GetComponent<SpriteRenderer>();
            anim = GetComponent<Animator>();
            scanner = GetComponent<Scanner>();
            hands = GetComponentsInChildren<Hand>(true);
            baseMoveSpeed = speed;
        }

        void OnEnable()
        {
            SetMoveSpeedBonus(0);
            hitbox.enabled = true;
            hitFlash?.Reset();
            anim.runtimeAnimatorController = animCon[GameManager.instance.playerId];
        }

        public void SetMoveSpeedBonus(float rate)
        {
            speed = baseMoveSpeed * Character.Speed * (1f + rate);
        }

        void Update()
        {
            if (!GameManager.instance.isLive)
                return;

            //inputVec.x = Input.GetAxisRaw("Horizontal");
            //inputVec.y = Input.GetAxisRaw("Vertical");
        }

        void FixedUpdate()
        {
            if (!GameManager.instance.isLive)
                return;

            Vector2 nextVec = inputVec.normalized * speed * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + nextVec);
        }

        void LateUpdate()
        {
            if (GameManager.instance.isLive) {
                anim.SetFloat("Speed", inputVec.magnitude);
                if (inputVec.x != 0)
                    spriter.flipX = inputVec.x < 0;
            }

            hitFlash?.Refresh();
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            GameManager game = GameManager.instance;
            if (!game.isLive)
                return;

            float damage = 10f;
            if (collision.gameObject.TryGetComponent(out Enemy enemy) && enemy.IsBoss)
                damage = enemy.Boss == BossType.Final ? 24f : 16f;
            game.health = Mathf.Max(0f, game.health - Time.deltaTime * damage);
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
