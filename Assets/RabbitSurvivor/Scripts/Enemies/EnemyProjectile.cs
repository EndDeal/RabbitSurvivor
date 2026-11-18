using UnityEngine;

namespace RabbitSurvivor
{
    public class EnemyProjectile : MonoBehaviour
    {
        const float Lifetime = 2.4f;
        const float TrailSeconds = 0.11f;
        const float TrailWidth = 0.12f;

        Rigidbody2D body;
        TrailRenderer trail;
        Vector3 baseScale;
        float hitDamage;
        float remaining;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            baseScale = transform.localScale;

            var shotSprite = GetComponentInChildren<SpriteRenderer>();
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = shotSprite.sharedMaterial;
            trail.sortingLayerID = shotSprite.sortingLayerID;
            trail.sortingOrder = shotSprite.sortingOrder - 1;
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.025f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.numCapVertices = 2;
            trail.startColor = new Color32(255, 211, 255, 235);
            trail.endColor = new Color32(241, 119, 239, 0);
        }

        internal void Launch(Vector2 position, Vector2 direction, float damage = 8f, float speed = 4.1f, float size = 1f)
        {
            body.position = position;
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            body.velocity = direction * speed;
            transform.localScale = baseScale * size;
            trail.Clear();
            trail.widthMultiplier = TrailWidth * size;
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
