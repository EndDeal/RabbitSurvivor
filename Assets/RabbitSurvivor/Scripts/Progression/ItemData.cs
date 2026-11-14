using UnityEngine;

namespace RabbitSurvivor
{
    [CreateAssetMenu(fileName = "Item", menuName = "Scriptble Object/ItemData")]
    public class ItemData : ScriptableObject
    {
        internal const int ShovelId = 1;
        internal const int ShotgunId = 2;

        public enum ItemType { Melee, Range, Glove, Shoe, Heal, Spread, Pulse, Burst, Power, Area, Vitality,
                               Sweep, GroundStrike, Jab, Furrow, Padding, Harvest }

        [Header("# Main Info")]
        public ItemType itemType;
        public int itemId;
        public string itemName;
        [TextArea]
        public string itemDesc;
        public Sprite itemIcon;
        public Color iconColor = Color.white;
        public string pickupDescription;

        [Header("# Level Data")]
        public float baseDamage;
        public int baseCount;
        public float[] damages;
        public int[] counts;

        [Header("# Weapon")]
        public GameObject projectile;
        public Sprite hand;
        public Sprite projectileSprite;
        public Material pulseMaterial;
        public Color attackColor = Color.white;
        public float attackInterval = 0.5f;
        public float attackRadius = 1.5f;
        public float strikeWidth = 1f;
        [Range(0.1f, 1f)] public float startingAttackArea = 1f;

        [Header("# Evolution")]
        public ItemData requiredPassive;
        public string evolutionName;
        public Sprite evolutionIcon;
        public string evolutionDescription;
        public float evolutionDamage = 1f;
        public int evolutionCount;
        public float evolutionInterval = 1f;
        public float evolutionArea = 1f;

        public int MaxLevel => damages.Length;
        public bool IsWeapon => itemType == ItemType.Melee || itemType == ItemType.Range ||
                                itemType == ItemType.Spread || itemType == ItemType.Pulse ||
                                itemType == ItemType.Burst || IsToolStrike;
        internal bool IsToolStrike => itemType == ItemType.Sweep || itemType == ItemType.GroundStrike ||
                                      itemType == ItemType.Jab || itemType == ItemType.Furrow;
        public bool IsPassive => !IsWeapon && itemType != ItemType.Heal;

        public float WeaponDamageAt(int level)
        {
            return baseDamage * (1f + damages[level - 1]);
        }

        public int WeaponCountAt(int level)
        {
            int count = baseCount;
            for (int index = 1; index < level; index++) {
                count += counts[index];
            }
            return count;
        }
    }
}
