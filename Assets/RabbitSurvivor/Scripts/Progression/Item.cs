using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Goldmetal.UndeadSurvivor
{
    public class Item : MonoBehaviour, ISelectHandler
    {
        public ItemData data;
        public int level;
        public Weapon weapon;
        public Gear gear;

        Image icon;
        Text textLevel;
        Text textName;
        Text textDesc;
        string recipe;
        LevelUp menu;

        public bool IsMaxLevel => level >= data.MaxLevel;
        internal Button Button { get; private set; }

        public void Bind(LevelUp menu, string evolutionRecipe)
        {
            this.menu = menu;
            recipe = evolutionRecipe;
            icon = GetComponentsInChildren<Image>(true)[1];
            icon.color = data.iconColor;
            icon.preserveAspect = true;

            Text[] texts = GetComponentsInChildren<Text>(true);
            textLevel = texts[0];
            textName = texts[1];
            textDesc = texts[2];
            FitCardText(textLevel, 0.76f, 1f, 5);
            FitCardText(textName, 0.53f, 0.76f, 6);
            FitCardText(textDesc, 0f, 0.53f, 4);

            Button = GetComponent<Button>();
            Button.onClick = new Button.ButtonClickedEvent();
            Button.onClick.AddListener(() => menu.Choose(this));
        }

        static void FitCardText(Text label, float bottom, float top, int maxFontSize)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.offsetMin = new Vector2(27f, 1f);
            rect.offsetMax = new Vector2(-5f, -1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 2;
            label.resizeTextMaxSize = Mathf.Min(maxFontSize, label.fontSize);
            label.raycastTarget = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            menu?.RememberSelection(this);
        }

        public void ShowOffer(bool evolution)
        {
            bool evolved = weapon && weapon.IsEvolved;
            bool showEvolution = evolution || evolved;
            icon.sprite = showEvolution && data.evolutionIcon ? data.evolutionIcon : data.itemIcon;
            textName.text = showEvolution ? data.evolutionName : data.itemName;

            if (evolution) {
                textLevel.text = "진화";
                textDesc.text = $"피해 {weapon.Damage:0.##} → {weapon.Damage * data.evolutionDamage:0.##}\n{data.evolutionDescription}";
            }
            else if (data.itemType == ItemData.ItemType.Heal) {
                textLevel.text = "회복";
                textDesc.text = data.itemDesc;
            }
            else {
                textLevel.text = level == 0 ? (data.IsWeapon ? "액티브" : "패시브") : $"Lv.{level + 1}/{data.MaxLevel}";
                string effect = weapon ? WeaponUpgradeDescription() :
                    level == 0 && !string.IsNullOrEmpty(data.pickupDescription) ? data.pickupDescription :
                    string.Format(data.itemDesc, Mathf.RoundToInt(data.damages[level] * 100), data.counts[level]);
                if (!string.IsNullOrEmpty(recipe) && !evolved)
                    effect += $"\n진화: {recipe}";
                textDesc.text = effect;
            }

            gameObject.SetActive(true);
        }

        string WeaponUpgradeDescription()
        {
            string effect = $"피해 {weapon.Damage:0.##} → {weapon.DamageAtLevel(level + 1):0.##}";
            int extra = data.counts[level];
            if (extra == 0)
                return effect;

            string quantity = data.itemType switch {
                ItemData.ItemType.Melee => "회전체",
                ItemData.ItemType.Range => "관통",
                ItemData.ItemType.Spread => "물거품",
                ItemData.ItemType.Burst => "연속탄",
                _ => ""
            };
            return string.IsNullOrEmpty(quantity) ? effect : $"{effect}\n{quantity} +{extra}";
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
            menu?.GearChanged();
        }
    }
}
