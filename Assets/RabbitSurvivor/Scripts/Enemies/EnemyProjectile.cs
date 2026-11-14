using UnityEngine;

namespace RabbitSurvivor
{
    public class EnemyProjectile : MonoBehaviour
    {
        const float Lifetime = 2.4f;

        Rigidbody2D body;
        Vector3 baseScale;
        float hitDamage;
        float remaining;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            baseScale = transform.localScale;
        }

        internal void Launch(Vector2 position, Vector2 direction, float damage = 8f, float speed = 4.5f, float size = 1f)
        {
            body.position = position;
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            body.velocity = direction * speed;
            transform.localScale = baseScale * size;
            hitDamage = damage;
            remaining = Lifetime;
        }

        void Update()
        {
            GameManager game = GameManager.instance;
            if (game.IsEnding) {
                gameObject.SetActive(false);
                return;
            }
            if (!game.isLive)
                return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
                gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!gameObject.activeSelf || !GameManager.instance.isLive)
                return;

            int layer = other.gameObject.layer;
            if (layer == SeafloorRocks.Layer || layer == SeedCrops.RiceWallLayer) {
                GameManager.instance.Crops?.PoisonImpact(other.ClosestPoint(body.position));
                gameObject.SetActive(false);
            }
            else if (other.TryGetComponent(out Player player)) {
                gameObject.SetActive(false);
                player.TakeDamage(hitDamage);
            }
        }
    }
}
