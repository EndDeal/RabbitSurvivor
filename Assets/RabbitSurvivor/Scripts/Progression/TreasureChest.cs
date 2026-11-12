using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class TreasureChest : MonoBehaviour
    {
        const float PickupDistance = 1.15f;

        Transform spriteTransform;
        bool waitingForPassive;

        public static void Drop(Vector3 position, Sprite sprite)
        {
            TreasureChest chest = new GameObject("Treasure Chest").AddComponent<TreasureChest>();
            chest.transform.position = position;

            SpriteRenderer renderer = new GameObject("Chest Sprite").AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            chest.spriteTransform = renderer.transform;
            renderer.transform.SetParent(chest.transform, false);
            renderer.transform.localScale = Vector3.one * 1.25f;

            GameManager.instance.uiLevelUp.Notify("보물상자 등장!");
        }

        void Update()
        {
            GameManager game = GameManager.instance;
            if (!game.isLive)
                return;

            spriteTransform.localPosition = Vector3.up * (0.15f + Mathf.Sin(Time.time * 4f) * 0.12f);
            Vector2 offset = game.player.transform.position - transform.position;
            if (offset.sqrMagnitude > PickupDistance * PickupDistance) {
                waitingForPassive = false;
                return;
            }

            if (game.uiLevelUp.TryShowEvolution()) {
                Destroy(gameObject);
                return;
            }

            if (!waitingForPassive) {
                waitingForPassive = true;
                game.uiLevelUp.Notify("보유 무기 + 대응 패시브 Lv.5 필요");
            }
        }
    }
}
