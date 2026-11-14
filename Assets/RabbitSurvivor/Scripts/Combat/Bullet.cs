using UnityEngine;

namespace RabbitSurvivor
{
    public class Bullet : MonoBehaviour
    {
        public const int InfinitePierce = -100;

        public float damage;
        public int per;

        Rigidbody2D rigid;
        SpriteRenderer spriteRenderer;
        Sprite defaultSprite;

        void Awake()
        {
            rigid = GetComponent<Rigidbody2D>();
            if (TryGetComponent(out spriteRenderer))
                defaultSprite = spriteRenderer.sprite;
        }

        public void Init(float damage, int per, Vector2 direction, Sprite sprite, Color color)
        {
            this.damage = damage;
            this.per = per;
            spriteRenderer.sprite = sprite ? sprite : defaultSprite;
            spriteRenderer.color = color;
            if (per != InfinitePierce)
                rigid.velocity = direction * 15f;
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Enemy") || per == InfinitePierce)
                return;

            per--;
            if (per < 0) {
                rigid.velocity = Vector2.zero;
                gameObject.SetActive(false);
            }
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Area") && per != InfinitePierce)
                gameObject.SetActive(false);
        }
    }
}
