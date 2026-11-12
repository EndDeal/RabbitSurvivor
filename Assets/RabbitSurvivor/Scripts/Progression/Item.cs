using UnityEngine;
using UnityEngine.UI;

namespace Goldmetal.UndeadSurvivor
{
    public class Item : MonoBehaviour
    {
        public ItemData data;
        public int level;
        public Weapon weapon;
        public Gear gear;

        Image icon;
        Text textLevel;
        Text textName;
        Text textDesc;
        ItemData partner;

        public bool IsMaxLevel => level >= data.MaxLevel;

        public void Bind(LevelUp menu, ItemData evolutionPartner)
        {
            partner = evolutionPartner;
            icon = GetComponentsInChildren<Image>(true)[1];
            icon.color = data.iconColor;
            icon.preserveAspect = true;

            Text[] texts = GetComponentsInChildren<Text>(true);
            textLevel = texts[0];
            textName = texts[1];
            textDesc = texts[2];

            Button button = GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => menu.Choose(this));
        }

        public void ShowOffer(bool evolution)
        {
            bool evolved = weapon && weapon.IsEvolved;
            bool showEvolution = evolution || evolved;
            icon.sprite = showEvolution && data.evolutionIcon ? data.evolutionIcon : data.itemIcon;
            textName.text = showEvolution ? data.evolutionName : data.itemName;

            if (evolution) {
                textLevel.text = "진화";
                textDesc.text = data.evolutionDescription;
            }
            else if (data.itemType == ItemData.ItemType.Heal) {
                textLevel.text = "회복";
                textDesc.text = data.itemDesc;
            }
            else {
                textLevel.text = level == 0 ? (data.IsWeapon ? "액티브" : "패시브") : $"Lv.{level + 1}/{data.MaxLevel}";
                string effect = level == 0 && !string.IsNullOrEmpty(data.pickupDescription) ?
                    data.pickupDescription : string.Format(data.itemDesc, Mathf.RoundToInt(data.damages[level] * 100), data.counts[level]);
                if (partner && !evolved) {
                    string recipe = data.IsWeapon ? $"{partner.itemName} Lv.{partner.MaxLevel}" : partner.itemName;
                    effect += $"\n진화: {recipe}";
                }
                textDesc.text = effect;
            }

            gameObject.SetActive(true);
        }

        public void Upgrade()
        {
            if (data.itemType == ItemData.ItemType.Heal) {
                GameManager game = GameManager.instance;
                game.health = game.maxHealth;
                return;
            }

            if (IsMaxLevel)
                return;

            level++;
            if (data.IsWeapon) {
                if (level == 1) {
                    weapon = new GameObject(data.itemName).AddComponent<Weapon>();
                    weapon.Init(data);
                }
                else {
                    weapon.LevelUp(level);
                }
            }
            else if (level == 1) {
                gear = new GameObject(data.itemName).AddComponent<Gear>();
                gear.Init(data);
            }
            else {
                gear.LevelUp(data.damages[level - 1]);
            }
        }
    }
}
