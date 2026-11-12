using System.Collections;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class Enemy : MonoBehaviour
    {
        public float speed;
        public float health;
        public float maxHealth;
        public RuntimeAnimatorController[] animCon;
        public Rigidbody2D target;

        public bool IsMidBoss { get; private set; }

        bool isLive;
        Rigidbody2D rigid;
        Collider2D coll;
        Animator anim;
        SpriteRenderer spriter;
        WaitForFixedUpdate wait;
        Vector3 defaultScale;
        Color defaultColor;

        void Awake()
        {
            rigid = GetComponent<Rigidbody2D>();
            coll = GetComponent<Collider2D>();
            anim = GetComponent<Animator>();
            spriter = GetComponent<SpriteRenderer>();
            wait = new WaitForFixedUpdate();
            defaultScale = transform.localScale;
            defaultColor = spriter.color;
        }

        void FixedUpdate()
        {
            if (!GameManager.instance.isLive || !isLive)
                return;

            if (!IsMidBoss && anim.GetCurrentAnimatorStateInfo(0).IsName("Hit"))
                return;

            Vector2 direction = target.position - rigid.position;
            Vector2 movement = direction.normalized * speed * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + movement);
            rigid.velocity = Vector2.zero;
        }

        void LateUpdate()
        {
            if (GameManager.instance.isLive && isLive)
                spriter.flipX = target.position.x < rigid.position.x;
        }

        void OnEnable()
        {
            target = GameManager.instance.player.GetComponent<Rigidbody2D>();
            isLive = true;
            coll.enabled = true;
            rigid.simulated = true;
            spriter.sortingOrder = 2;
            anim.SetBool("Dead", false);
            health = maxHealth;
        }

        public void Init(SpawnData data, bool midBoss = false)
        {
            anim.runtimeAnimatorController = animCon[data.spriteType];
            speed = data.speed;
            maxHealth = data.health;
            health = maxHealth;
            IsMidBoss = midBoss;
            transform.localScale = midBoss ? defaultScale * 1.7f : defaultScale;
            spriter.color = midBoss ? new Color(1f, 0.77f, 0.38f) : defaultColor;
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Bullet") && collision.TryGetComponent(out Bullet bullet))
                TakeDamage(bullet.damage);
        }

        public void TakeDamage(float damage)
        {
            if (!isLive)
                return;

            health -= damage;
            if (health > 0f) {
                if (!IsMidBoss)
                    StartCoroutine(KnockBack());
                anim.SetTrigger("Hit");
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Hit);
                return;
            }

            isLive = false;
            coll.enabled = false;
            rigid.simulated = false;
            spriter.sortingOrder = 1;
            anim.SetBool("Dead", true);

            GameManager game = GameManager.instance;
            game.RegisterKill(transform.position, IsMidBoss);

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

        void Dead()
        {
            gameObject.SetActive(false);
        }
    }
}
