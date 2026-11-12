using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class Hand : MonoBehaviour
    {
        public bool isLeft;
        public SpriteRenderer spriter;

        SpriteRenderer player;

        static readonly Vector3 RightPosition = new Vector3(0.35f, -0.15f, 0f);
        static readonly Vector3 RightPositionReversed = new Vector3(-0.15f, -0.15f, 0f);

        void Awake()
        {
            player = GetComponentInParent<Player>().GetComponent<SpriteRenderer>();
        }

        public void Equip(Sprite sprite)
        {
            spriter.sprite = sprite;
            gameObject.SetActive(true);
            if (isLeft && player.sprite)
                transform.localScale = Vector3.one * (player.sprite.bounds.size.y * 0.55f / sprite.bounds.size.y);
        }

        void LateUpdate()
        {
            bool reversed = player.flipX;
            spriter.flipX = reversed;

            if (isLeft) {
                Bounds body = player.sprite.bounds;
                float direction = reversed ? -1f : 1f;
                transform.localPosition = new Vector3(direction * body.size.x * 0.20f, body.min.y + body.size.y * 0.32f, 0f);
                transform.localRotation = Quaternion.Euler(0f, 0f, direction * 30f);
                spriter.flipY = false;
                spriter.sortingOrder = player.sortingOrder + 1;
            }
            else {
                transform.localPosition = reversed ? RightPositionReversed : RightPosition;
                spriter.sortingOrder = reversed ? 6 : 4;
            }
        }
    }
}
