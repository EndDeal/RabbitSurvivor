using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Goldmetal.UndeadSurvivor
{
    public class RunHud : MonoBehaviour
    {
        const int SlotsPerRow = 3;
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Color32 Ink = new Color32(26, 46, 58, 255);
        static readonly Color32 Panel = new Color32(37, 73, 83, 255);
        static readonly Color32 Cream = new Color32(255, 244, 218, 255);
        static readonly Color32 Gold = new Color32(255, 207, 112, 255);
        static readonly string[] RankLabels = { "", "1", "2", "3", "4", "5" };

        readonly GearSlot[] slots = new GearSlot[SlotsPerRow * 2];
        GameManager game;
        Font font;
        Button settings;
        GameObject overlay;
        Button resume;
        Slider volume;
        GameObject previousSelection;

        struct GearSlot
        {
            public Image icon;
            public Image background;
            public Text rank;
        }

        internal void Initialize(GameManager manager, Font uiFont)
        {
            game = manager;
            font = uiFont;
            BuildGearRow("무기", 0, -45f);
            BuildGearRow("패시브", SlotsPerRow, -61f);
            settings = AddButton(transform, "Settings", "설정", TopRight, new Vector2(-3f, -28f), new Vector2(25f, 11f), OpenSettings);
            BuildSettings();
        }

        void LateUpdate()
        {
            if (!game)
                return;

            RefreshGear();
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

        void BuildGearRow(string label, int start, float y)
        {
            RectTransform row = Place(transform, label, TopLeft, TopLeft, new Vector2(3f, y), new Vector2(68f, 14f));
            AddLabel(Place(row, "Label", TopLeft, TopLeft, Vector2.zero, new Vector2(17f, 14f)), label, 5);

            for (int index = 0; index < SlotsPerRow; index++) {
                RectTransform tile = Place(row, "Gear Slot", TopLeft, TopLeft, new Vector2(18f + index * 16f, 0f), new Vector2(14f, 14f));
                Image background = AddImage(tile, Ink);
                Image icon = AddImage(Place(tile, "Gear Icon", Center, Center, Vector2.zero, new Vector2(11f, 11f)), Color.white);
                icon.preserveAspect = true;
                Text rank = AddLabel(Place(tile, "Rank", Vector2.right, Vector2.right, new Vector2(-0.4f, 0f), new Vector2(5f, 5f)), "", 4);
                rank.GetComponent<Outline>().effectDistance = new Vector2(0.5f, -0.5f);
                slots[start + index] = new GearSlot { background = background, icon = icon, rank = rank };
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
                bool evolved = item.weapon && item.weapon.IsEvolved;
                slot.background.color = evolved ? Gold : Panel;
                slot.icon.sprite = evolved && item.data.evolutionIcon ? item.data.evolutionIcon : item.data.itemIcon;
                slot.icon.enabled = slot.icon.sprite;
                string rank = item.level < RankLabels.Length ? RankLabels[item.level] : item.level.ToString();
                if (slot.rank.text != rank)
                    slot.rank.text = rank;
            }

            for (int index = weaponSlot; index < SlotsPerRow; index++)
                ClearSlot(slots[index]);
            for (int index = passiveSlot; index < slots.Length; index++)
                ClearSlot(slots[index]);
        }

        static void ClearSlot(GearSlot slot)
        {
            slot.background.color = Ink;
            slot.icon.enabled = false;
            slot.rank.text = "";
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

        static RectTransform Place(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
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
