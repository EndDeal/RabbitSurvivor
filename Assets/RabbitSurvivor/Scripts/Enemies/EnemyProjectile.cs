using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class EnemyProjectile : MonoBehaviour
    {
        const float Speed = 4.5f;
        const float Damage = 8f;
        const float Lifetime = 2.4f;

        Rigidbody2D body;
        float remaining;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        internal void Launch(Vector2 position, Vector2 direction)
        {
            body.position = position;
            body.velocity = direction * Speed;
            remaining = Lifetime;
        }

        void Update()
        {
            if (!GameManager.instance.isLive)
                return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
                gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!gameObject.activeSelf || !GameManager.instance.isLive || !other.TryGetComponent(out Player player))
                return;

            gameObject.SetActive(false);
            player.TakeDamage(Damage);
        }
    }
}
