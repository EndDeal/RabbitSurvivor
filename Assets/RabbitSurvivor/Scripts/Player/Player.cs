using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Goldmetal.UndeadSurvivor
{
    public class Player : MonoBehaviour
    {
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

            if (!lethal)
                return;

            rigid.velocity = Vector2.zero;
            hitbox.enabled = false;
            foreach (Hand hand in hands)
                hand.gameObject.SetActive(false);
            foreach (Weapon weapon in GetComponentsInChildren<Weapon>())
                weapon.gameObject.SetActive(false);

            anim.SetTrigger("Dead");
            StartCoroutine(HideCorpse(game.CorpseLifetime));
            game.GameOver();
        }

        IEnumerator HideCorpse(float lifetime)
        {
            yield return new WaitForSecondsRealtime(lifetime);
            gameObject.SetActive(false);
        }

        void OnMove(InputValue value)
        {
            inputVec = value.Get<Vector2>();
        }
    }
}
