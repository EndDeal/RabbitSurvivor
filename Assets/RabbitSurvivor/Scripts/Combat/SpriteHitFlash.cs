using UnityEngine;

namespace RabbitSurvivor
{
    sealed class SpriteHitFlash
    {
        const float Duration = 0.10f;
        const float RepeatInterval = 0.25f;

        readonly SpriteRenderer body;
        SpriteRenderer overlay;
        float visibleUntil;
        float nextFlashAt;

        public SpriteHitFlash(SpriteRenderer body)
        {
            this.body = body;
        }

        public void Show(Material material, bool force = false)
        {
            float now = Time.unscaledTime;
            if (!material || (!force && now < nextFlashAt))
                return;

            if (!overlay) {
                overlay = new GameObject("Hit Flash").AddComponent<SpriteRenderer>();
                overlay.transform.SetParent(body.transform, false);
                overlay.sharedMaterial = material;
            }

            visibleUntil = now + Duration;
            nextFlashAt = now + RepeatInterval;
            overlay.enabled = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!overlay || !overlay.enabled)
                return;

            if (!body.enabled || Time.unscaledTime >= visibleUntil) {
                overlay.enabled = false;
                return;
            }

            overlay.sprite = body.sprite;
            overlay.flipX = body.flipX;
            overlay.flipY = body.flipY;
            overlay.sortingLayerID = body.sortingLayerID;
            overlay.sortingOrder = body.sortingOrder + 2;
        }

        public void Reset()
        {
            nextFlashAt = 0f;
            if (overlay)
                overlay.enabled = false;
        }
    }
}
