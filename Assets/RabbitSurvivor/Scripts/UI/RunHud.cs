using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public class RunHud : MonoBehaviour
    {
        const int SlotsPerRow = 3;
        const float HitBorderDuration = 0.24f;
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Color32 Ink = new Color32(26, 46, 58, 255);
        static readonly Color32 Panel = new Color32(37, 73, 83, 255);
        static readonly Color32 Cream = new Color32(255, 244, 218, 255);
        static readonly Color32 Gold = new Color32(255, 207, 112, 255);
        static readonly Color32 OxygenBlue = new Color32(57, 160, 203, 255);
        static readonly Color32 RefillMint = new Color32(60, 188, 161, 255);
        static readonly Color32 DamageRed = new Color32(240, 75, 87, 255);
        static readonly string[] RankLabels = { "", "1", "2", "3", "4", "5" };

        readonly GearSlot[] slots = new GearSlot[SlotsPerRow * 2];
        GameManager game;
        Font font;
        Button settings;
        GameObject overlay;
        Button resume;
        Slider volume;
        GameObject previousSelection;
        int gearVersion = -1;
        FloatingStick stick;
        Image healthFill;
        Text healthLabel;
        Image oxygenFill;
        Text oxygenLabel;
        PlayStatusBorder statusBorder;
        int shownOxygen = -1;
        bool shownHasAir;
        Text gearDetails;
        Text mineCombo;
        float shownMineComboAt = -1f;
        int shownSlot = -1;
        int shownHealth = -1;
        int shownMaxHealth = -1;

        struct GearSlot
        {
            public Image icon;
            public Image background;
            public Text rank;
            public Item item;
            public Button button;
        }

        internal void Initialize(GameManager manager, Font uiFont)
        {
            game = manager;
            font = uiFont;
            RectTransform touchArea = Place(transform, "Touch Movement", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            touchArea.anchorMax = Vector2.one;
            touchArea.SetAsFirstSibling();
            AddImage(touchArea, Color.clear).raycastTarget = true;
            stick = touchArea.gameObject.AddComponent<FloatingStick>();
            stick.Initialize(game.player, (RectTransform)game.uiJoy);
            RectTransform border = Place(transform, "Play Status Border", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero);
            border.anchorMax = Vector2.one;
            border.SetAsFirstSibling();
            statusBorder = border.gameObject.AddComponent<PlayStatusBorder>();
            statusBorder.raycastTarget = false;
            statusBorder.enabled = false;
            BuildMeter("Health", 0f, new Color32(215, 77, 94, 255), out healthFill, out healthLabel);
            if (game.Oxygen) {
                BuildMeter("Oxygen", 10f, OxygenBlue, out oxygenFill, out oxygenLabel);
                RefreshOxygen();
            }
            BuildGearRow("무기", 0, -45f);
            BuildGearRow("패시브", SlotsPerRow, -61f);
            RectTransform detail = Place(transform, "Gear Details", TopLeft, TopLeft, new Vector2(3f, -78f), new Vector2(83f, 19f));
            AddImage(detail, new Color32(Ink.r, Ink.g, Ink.b, 175));
            gearDetails = AddLabel(Place(detail, "Description", Center, Center, Vector2.zero, detail.sizeDelta - new Vector2(6f, 3f)), "", 5);
            gearDetails.resizeTextForBestFit = true;
            gearDetails.resizeTextMinSize = 3;
            gearDetails.resizeTextMaxSize = 5;
            gearDetails.horizontalOverflow = HorizontalWrapMode.Wrap;
            gearDetails.verticalOverflow = VerticalWrapMode.Truncate;
            detail.gameObject.SetActive(false);
            var comboAnchor = new Vector2(.5f, .72f);
            mineCombo = AddLabel(Place(transform, "Potato Combo", comboAnchor, Center, Vector2.zero, new Vector2(115f, 18f)), "", 10);
            mineCombo.color = Gold;
            mineCombo.gameObject.SetActive(false);
            settings = AddButton(transform, "Settings", "설정", TopRight, new Vector2(-3f, -28f), new Vector2(25f, 11f), OpenSettings);
            BuildSettings();
        }

        void LateUpdate()
        {
            if (!game)
                return;

            RefreshHealth();
            if (oxygenFill)
                RefreshOxygen();
            RefreshStatusBorder();
            RefreshMineCombo();
            if (stick.gameObject.activeSelf != game.isLive)
                stick.gameObject.SetActive(game.isLive);
            if (!game.isLive && shownSlot >= 0)
                ShowGear(-1);
            if (gearVersion != game.uiLevelUp.GearVersion) {
                gearVersion = game.uiLevelUp.GearVersion;
                RefreshGear();
            }
            if (game.IsEnding) {
                settings.gameObject.SetActive(false);
                overlay.SetActive(false);
                return;
            }

            settings.gameObject.SetActive(game.isLive);
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
                return;

            if (overlay.activeSelf)
                CloseSettings();
            else if (game.isLive)
                OpenSettings();
        }

        void RefreshMineCombo()
        {
            SeedCrops crops = game.Crops;
            bool visible = game.isLive && crops && crops.CurrentMineCombo > 0 && Time.time - crops.MineComboAt < 1.5f;
            mineCombo.gameObject.SetActive(visible);
            if (!visible)
                return;

            if (shownMineComboAt != crops.MineComboAt) {
                shownMineComboAt = crops.MineComboAt;
                mineCombo.text = $"감자 연쇄 {crops.CurrentMineCombo}마리!";
            }
            float pulse = Mathf.Clamp01(1f - (Time.time - shownMineComboAt) * 6f);
            mineCombo.rectTransform.localScale = Vector3.one * (1f + .22f * pulse);
        }

        void BuildGearRow(string label, int start, float y)
        {
            RectTransform row = Place(transform, label, TopLeft, TopLeft, new Vector2(3f, y), new Vector2(68f, 14f));
            AddLabel(Place(row, "Label", TopLeft, TopLeft, Vector2.zero, new Vector2(17f, 14f)), label, 5);

            for (int index = 0; index < SlotsPerRow; index++) {
                RectTransform tile = Place(row, "Gear Slot", TopLeft, TopLeft, new Vector2(18f + index * 16f, 0f), new Vector2(14f, 14f));
                Image background = AddImage(tile, Ink);
                background.raycastTarget = true;
                Button button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                int slot = start + index;
                button.onClick.AddListener(() => ShowGear(shownSlot == slot ? -1 : slot));
                Image icon = AddImage(Place(tile, "Gear Icon", Center, Center, Vector2.zero, new Vector2(11f, 11f)), Color.white);
                icon.preserveAspect = true;
                Text rank = AddLabel(Place(tile, "Rank", Vector2.right, Vector2.right, new Vector2(-0.4f, 0f), new Vector2(5f, 5f)), "", 4);
                rank.GetComponent<Outline>().effectDistance = new Vector2(0.5f, -0.5f);
                slots[slot] = new GearSlot { background = background, icon = icon, rank = rank, button = button };
            }
        }

        void RefreshGear()
        {
            IReadOnlyList<Item> items = game.uiLevelUp.Items;
            int weaponSlot = 0;
            int passiveSlot = SlotsPerRow;
            foreach (Item item in items) {
                if (item.level == 0 || (!item.data.IsWeapon && !item.data.IsPassive))
                    continue;

                if (item.data.IsWeapon ? weaponSlot == SlotsPerRow : passiveSlot == slots.Length)
                    continue;
                int index = item.data.IsWeapon ? weaponSlot++ : passiveSlot++;
                GearSlot slot = slots[index];
                slots[index].item = item;
                slot.button.interactable = true;
                bool evolved = item.weapon && item.weapon.IsEvolved;
                slot.background.color = evolved ? Gold : Panel;
                slot.icon.sprite = evolved && item.data.evolutionIcon ? item.data.evolutionIcon : item.data.itemIcon;
                slot.icon.enabled = slot.icon.sprite;
                string rank = item.level < RankLabels.Length ? RankLabels[item.level] : item.level.ToString();
                if (slot.rank.text != rank)
                    slot.rank.text = rank;
            }

            for (int index = weaponSlot; index < SlotsPerRow; index++)
                ClearSlot(index);
            for (int index = passiveSlot; index < slots.Length; index++)
                ClearSlot(index);
            if (shownSlot >= 0)
                ShowGear(shownSlot);
        }

        void ClearSlot(int index)
        {
            GearSlot slot = slots[index];
            slots[index].item = null;
            slot.button.interactable = false;
            slot.background.color = Ink;
            slot.icon.enabled = false;
            slot.rank.text = "";
        }

        void ShowGear(int index)
        {
            Item item = index >= 0 ? slots[index].item : null;
            shownSlot = item ? index : -1;
            gearDetails.transform.parent.gameObject.SetActive(item);
            if (!item)
                return;

            ItemData data = item.data;
            bool evolved = item.weapon && item.weapon.IsEvolved;
            string name = evolved ? data.evolutionName : data.itemName;
            string effect = data.IsWeapon
                ? (evolved ? data.evolutionDescription : data.pickupDescription)
                : string.Format(data.itemDesc, Mathf.RoundToInt(data.damages[item.level - 1] * 100f), data.counts[item.level - 1]);
            gearDetails.text = $"{(data.IsWeapon ? "무기" : "패시브")} · {name} Lv.{item.level}\n{effect}";
        }

        void BuildMeter(string name, float bottom, Color color, out Image meterFill, out Text meterLabel)
        {
            RectTransform track = Place(transform, "Player " + name, Vector2.zero, Vector2.zero,
                new Vector2(0f, bottom), new Vector2(0f, 10f));
            track.anchorMax = Vector2.right;
            AddImage(track, Ink);
            RectTransform fillArea = Place(track, "Fill Area", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.one;
            fillArea.offsetMax = -Vector2.one;
            RectTransform fill = Place(fillArea, name + " Fill", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMax = Vector2.one;
            meterFill = AddImage(fill, color);
            RectTransform label = Place(track, name + " Value", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            label.anchorMax = Vector2.one;
            meterLabel = AddLabel(label, "", 6);
        }

        void RefreshHealth()
        {
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(game.health / game.maxHealth), 1f);
            int current = Mathf.CeilToInt(game.health);
            int maximum = Mathf.CeilToInt(game.maxHealth);
            if (current == shownHealth && maximum == shownMaxHealth)
                return;
            shownHealth = current;
            shownMaxHealth = maximum;
            healthLabel.text = $"{current} / {maximum}";
        }

        void RefreshOxygen()
        {
            UnderwaterOxygen oxygen = game.Oxygen;
            float fraction = oxygen.Fraction;
            oxygenFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            Color32 stateColor = OxygenStateColor(oxygen);
            oxygenFill.color = stateColor;

            int current = fraction >= 1f ? 100 : Mathf.Min(99, Mathf.CeilToInt(fraction * 100f));
            if (current == shownOxygen && oxygen.HasAir == shownHasAir)
                return;

            shownOxygen = current;
            shownHasAir = oxygen.HasAir;
            string status = oxygen.HasAir ? (current == 100 ? " · 호흡 가능" : " · 충전 중") : "";
            oxygenLabel.text = $"산소 {current}%{status}";
            oxygenLabel.color = !oxygen.HasAir && fraction <= .25f ? stateColor : Cream;
        }

        static Color32 OxygenStateColor(UnderwaterOxygen oxygen)
        {
            if (oxygen.HasAir)
                return RefillMint;
            if (oxygen.Fraction <= 0f)
                return DamageRed;
            return oxygen.Fraction <= .25f ? Gold : OxygenBlue;
        }

        void RefreshStatusBorder()
        {
            UnderwaterOxygen oxygen = game.Oxygen;
            float hit = Mathf.Clamp01(1f - (Time.unscaledTime - game.player.LastHitAt) / HitBorderDuration);
            statusBorder.enabled = game.isLive && (oxygen || hit > 0f);
            if (!statusBorder.enabled)
                return;

            Color baseColor = oxygen ? OxygenStateColor(oxygen) : Color.clear;
            baseColor.a = oxygen ? oxygen.Fraction <= 0f && !oxygen.HasAir ? .36f : .23f : 0f;
            Color hitColor = DamageRed;
            hitColor.a = .78f;
            statusBorder.color = Color.Lerp(baseColor, hitColor, hit);
        }

        void BuildSettings()
        {
            RectTransform root = Place(transform.parent, "Run Settings", Center, Center, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            Canvas menuCanvas = root.gameObject.AddComponent<Canvas>();
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = 60;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var backdrop = AddImage(root, new Color32(9, 22, 29, 200));
            backdrop.raycastTarget = true;
            overlay = root.gameObject;

            RectTransform card = Place(root, "Settings Panel", Center, Center, Vector2.zero, new Vector2(104f, 108f));
            AddImage(card, Ink);
            AddLabel(Place(card, "Title", Center, Center, new Vector2(0f, 42f), new Vector2(90f, 14f)), "설정", 9);
            Text value = AddLabel(Place(card, "Volume Value", Center, Center, new Vector2(0f, 28f), new Vector2(90f, 9f)), "", 6);
            volume = AddVolumeSlider(card);
            volume.onValueChanged.AddListener(amount => {
                AudioManager.SetMasterVolume(amount);
                value.text = $"전체 볼륨  {amount * 100f:0}%";
            });
            value.text = $"전체 볼륨  {AudioListener.volume * 100f:0}%";

            resume = AddButton(card, "Resume", "계속하기", Center, new Vector2(0f, 0f), new Vector2(76f, 13f), CloseSettings);
            Button restart = AddButton(card, "Restart", "다시 시작", Center, new Vector2(0f, -17f), new Vector2(76f, 13f), Restart);
            Button quit = AddButton(card, "Quit", "게임 종료", Center, new Vector2(0f, -34f), new Vector2(76f, 13f), Quit);
            Link(volume, null, resume);
            Link(resume, volume, restart);
            Link(restart, resume, quit);
            Link(quit, restart, null);
            overlay.SetActive(false);
        }

        Slider AddVolumeSlider(RectTransform card)
        {
            RectTransform track = Place(card, "Master Volume", Center, Center, new Vector2(0f, 16f), new Vector2(76f, 8f));
            Image trackImage = AddImage(track, Panel);
            trackImage.raycastTarget = true;
            Slider slider = track.gameObject.AddComponent<Slider>();
            RectTransform fillArea = Place(track, "Fill Area", Center, Center, Vector2.zero, Vector2.zero);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(1f, 2f);
            fillArea.offsetMax = new Vector2(-1f, -2f);
            RectTransform fill = Place(fillArea, "Fill", Center, Center, Vector2.zero, Vector2.zero);
            AddImage(fill, Gold);

            RectTransform travel = Place(track, "Handle Travel", Center, Center, Vector2.zero, Vector2.zero);
            travel.anchorMin = Vector2.zero;
            travel.anchorMax = Vector2.one;
            travel.offsetMin = new Vector2(4f, 0f);
            travel.offsetMax = new Vector2(-4f, 0f);
            RectTransform handle = Place(travel, "Handle", Center, Center, Vector2.zero, new Vector2(7f, 8f));
            Image knob = AddImage(handle, Cream);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.SetValueWithoutNotify(AudioListener.volume);
            return slider;
        }

        void OpenSettings()
        {
            if (!game.isLive)
                return;

            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            game.Stop();
            overlay.SetActive(true);
            resume.Select();
        }

        void CloseSettings()
        {
            AudioManager.SaveMasterVolume();
            overlay.SetActive(false);
            game.Resume();
            settings.gameObject.SetActive(game.isLive);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        void Restart()
        {
            AudioManager.SaveMasterVolume();
            game.GameRetry();
        }

        void Quit()
        {
            AudioManager.SaveMasterVolume();
            game.GameQuit();
        }

        Button AddButton(Transform parent, string name, string label, Vector2 anchor, Vector2 offset, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Place(parent, name, anchor, anchor, offset, size);
            Image image = AddImage(rect, Panel);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            AddLabel(Place(rect, "Label", Center, Center, Vector2.zero, size), label, 6);
            return button;
        }

        Text AddLabel(RectTransform rect, string value, int size)
        {
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Cream;
            label.text = value;
            label.raycastTarget = false;
            rect.gameObject.AddComponent<Outline>().effectDistance = new Vector2(0.6f, -0.6f);
            return label;
        }

        static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static RectTransform Place(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        static void Link(Selectable item, Selectable up, Selectable down)
        {
            item.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        }
    }
}
