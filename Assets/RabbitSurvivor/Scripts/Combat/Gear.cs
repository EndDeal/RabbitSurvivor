using UnityEngine;

namespace RabbitSurvivor
{
    public class Gear : MonoBehaviour
    {
        ItemData.ItemType type;
        Player player;

        public void Init(ItemData data)
        {
            player = GameManager.instance.player;
            transform.SetParent(player.transform, false);
            type = data.itemType;
            LevelUp(data.damages[0]);
        }

        public void LevelUp(float rate)
        {
            switch (type) {
                case ItemData.ItemType.Glove:
                    player.AttackHaste = rate;
                    break;
                case ItemData.ItemType.Shoe:
                    player.SetMoveSpeedBonus(rate);
                    return;
                case ItemData.ItemType.Power:
                    player.AttackPower = rate;
                    break;
                case ItemData.ItemType.Area:
                    player.AttackArea = rate;
                    break;
                case ItemData.ItemType.Vitality:
                    GameManager.instance.SetMaxHealthBonus(rate);
                    return;
                case ItemData.ItemType.Padding:
                    player.DamageReduction = rate;
                    return;
                case ItemData.ItemType.Harvest:
                    player.PickupRange = rate;
                    return;
            }

            foreach (Weapon weapon in player.GetComponentsInChildren<Weapon>()) {
                weapon.RefreshStats();
            }
        }
    }
}
