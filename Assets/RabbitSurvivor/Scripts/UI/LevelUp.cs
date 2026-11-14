using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace RabbitSurvivor
{
    public class LevelUp : MonoBehaviour
    {
        const int WeaponSlots = 3;
        const int PassiveSlots = 3;
        const int OfferCount = 3;
        const float TicketRowHeight = 15f;

        enum EvolutionMenu { None, Chest, Ticket }

        [SerializeField] ItemData[] additionalItems = Array.Empty<ItemData>();
        [SerializeField] Text title;

        RectTransform rect;
        Text notice;
        float noticeTime;
        List<Item> items;
        Item recovery;
        readonly List<Item> eligible = new List<Item>();
        readonly List<Item> offered = new List<Item>();
        readonly List<Item> evolutionOffers = new List<Item>();
        RectTransform itemGroup;
        Vector2 itemGroupBottom;
        Button ticketButton;
        Text ticketLabel;
        Item lastLevelSelection;
        string levelHeading;
        int pendingLevels;
        EvolutionMenu evolutionMenu;

        public bool IsOpen { get; private set; }
        public bool HasEvolved { get; private set; }
        public bool HasEvolutionTicket { get; private set; }
        internal IReadOnlyList<Item> Items => items;
        internal int GearVersion { get; private set; }

        internal void GearChanged()
        {
            GearVersion++;
        }

        internal void RememberSelection(Item item)
        {
            if (evolutionMenu == EvolutionMenu.None && offered.Contains(item))
                lastLevelSelection = item;
        }

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            items = new List<Item>(GetComponentsInChildren<Item>(true));
            foreach (Item item in items) {
                item.gameObject.SetActive(false);
            }

            Item template = items[0];
            itemGroup = (RectTransform)template.transform.parent;
            itemGroupBottom = itemGroup.offsetMin;
            foreach (ItemData data in additionalItems) {
                Item card = Instantiate(template, template.transform.parent);
                card.data = data;
                card.name = data.itemName;
                items.Add(card);
            }

            foreach (Item item in items) {
                ItemData passive = item.data.requiredPassive;
                string recipe = passive ? $"무기 Lv.{item.data.MaxLevel} + {passive.itemName}" : "";
                if (item.data.IsPassive) {
                    var weapons = items.FindAll(candidate => candidate.data.requiredPassive == item.data);
                    recipe = string.Join(" · ", weapons.ConvertAll(candidate => candidate.data.itemName));
                }
                if (item.data.itemType == ItemData.ItemType.Heal)
                    recovery = item;
                item.Bind(this, recipe);
            }

            if (title) {
                CreateNotice();
                CreateTicketButton();
            }
        }

        void Update()
        {
            if (!notice || !notice.gameObject.activeSelf)
                return;

            noticeTime -= Time.unscaledDeltaTime;
            if (noticeTime <= 0f)
                notice.gameObject.SetActive(false);
        }

        public void Select(int id)
        {
            Item starter = items.Find(item => item.data.itemId == id && item.data.IsWeapon);
            starter?.Upgrade();
        }

        public void Show()
        {
            pendingLevels++;
            if (!IsOpen)
                ShowNextLevel();
        }

        internal void CollectTreasure()
        {
            if (HasEvolved || HasEvolutionTicket)
                return;

            FindEvolutions();
            if (evolutionOffers.Count > 0) {
                ShowEvolutionOffers(EvolutionMenu.Chest);
                Open();
                return;
            }

            HasEvolutionTicket = true;
            eligible.Clear();
            foreach (Item item in items) {
                if (item != recovery && item.level > 0 && !item.IsMaxLevel)
                    eligible.Add(item);
            }

            if (eligible.Count == 0) {
                Notify("장비 강화권 획득!");
                return;
            }

            Item upgrade = eligible[Random.Range(0, eligible.Count)];
            upgrade.Upgrade();
            Notify($"장비 강화권 획득!\n{upgrade.data.itemName} Lv.{upgrade.level}");
        }

        void FindEvolutions()
        {
            evolutionOffers.Clear();
            foreach (Item item in items) {
                if (CanEvolve(item))
                    evolutionOffers.Add(item);
            }
        }

        bool CanEvolve(Item item)
        {
            if (!item.weapon || !item.IsMaxLevel || item.weapon.IsEvolved || !item.data.requiredPassive)
                return false;

            Item passive = items.Find(candidate => candidate.data == item.data.requiredPassive);
            return passive && passive.level > 0;
        }

        public void Choose(Item item)
        {
            if (!IsOpen)
                return;

            if (evolutionMenu != EvolutionMenu.None) {
                if (!evolutionOffers.Contains(item))
                    return;

                Evolve(item);
                if (evolutionMenu == EvolutionMenu.Ticket) {
                    HideOffers(evolutionOffers);
                    evolutionOffers.Clear();
                    ShowLevelOffers();
                    return;
                }
            }
            else {
                if (!offered.Contains(item))
                    return;
                item.Upgrade();
            }

            ClearOffers();
            if (pendingLevels > 0) {
                ShowNextLevel();
                return;
            }

            IsOpen = false;
            rect.localScale = Vector3.zero;
            GameManager.instance.Resume();
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Select);
            AudioManager.instance.EffectBgm(false);
        }

        public void Dismiss()
        {
            if (!IsOpen)
                return;

            ClearOffers();
            pendingLevels = 0;
            IsOpen = false;
            rect.localScale = Vector3.zero;
            AudioManager.instance.EffectBgm(false);
        }

        void ShowNextLevel()
        {
            pendingLevels--;
            lastLevelSelection = null;
            int weaponCount = items.FindAll(item => item.level > 0 && item.data.IsWeapon).Count;
            int passiveCount = items.FindAll(item => item.level > 0 && item.data.IsPassive).Count;

            eligible.Clear();
            foreach (Item item in items) {
                if (item == recovery || item.IsMaxLevel)
                    continue;

                int owned = item.data.IsWeapon ? weaponCount : passiveCount;
                int slots = item.data.IsWeapon ? WeaponSlots : PassiveSlots;
                if (item.level > 0 || owned < slots)
                    eligible.Add(item);
            }

            // Reserve one choice for a passive upgrade when the player can progress toward a recipe.
            List<Item> passiveOffers = eligible.FindAll(item => item.data.IsPassive && item.level > 0);
            if (passiveOffers.Count == 0 && passiveCount == 0)
                passiveOffers = eligible.FindAll(item => item.data.IsPassive);
            if (passiveOffers.Count > 0) {
                Item passive = passiveOffers[Random.Range(0, passiveOffers.Count)];
                eligible.Remove(passive);
                offered.Add(passive);
            }

            while (offered.Count < OfferCount && eligible.Count > 0) {
                int index = Random.Range(0, eligible.Count);
                offered.Add(eligible[index]);
                eligible.RemoveAt(index);
            }

            if (offered.Count == 0)
                offered.Add(recovery);

            if (HasEvolutionTicket)
                FindEvolutions();
            levelHeading = $"레벨 업\n액티브 {weaponCount}/{WeaponSlots} · 패시브 {passiveCount}/{PassiveSlots}";
            ShowLevelOffers();
            Open();
        }

        void Evolve(Item item)
        {
            item.weapon.Evolve();
            GearChanged();
            HasEvolved = true;
            HasEvolutionTicket = false;
            Notify($"{item.data.evolutionName} 진화!");
        }

        void ShowLevelOffers()
        {
            evolutionMenu = EvolutionMenu.None;
            if (title)
                title.text = levelHeading;
            ShowOffers(offered, false);
            bool ready = evolutionOffers.Count > 0;
            SetTicketButton(HasEvolutionTicket, ready ? "강화권 사용 · 무기 진화" : "강화권 보유 · 조건 미충족", ready);
            FocusChoices(offered, lastLevelSelection);
        }

        void ShowEvolutionOffers(EvolutionMenu menu)
        {
            evolutionMenu = menu;
            if (title)
                title.text = menu == EvolutionMenu.Ticket ? "장비 강화권\n진화할 무기 선택" : "보물상자\n진화할 무기 선택";
            HideOffers(offered);
            ShowOffers(evolutionOffers, true);
            SetTicketButton(menu == EvolutionMenu.Ticket, "레벨업 보상으로 돌아가기", true);
            FocusChoices(evolutionOffers, null);
        }

        void FocusChoices(List<Item> choices, Item preferred)
        {
            bool ticketSelectable = ticketButton && ticketButton.isActiveAndEnabled && ticketButton.interactable;
            for (int index = 0; index < choices.Count; index++) {
                Button previous = index > 0 ? choices[index - 1].Button : null;
                Button next = index + 1 < choices.Count ? choices[index + 1].Button :
                    ticketSelectable ? ticketButton : null;
                SetVerticalNavigation(choices[index].Button, previous, next);
            }

            if (ticketSelectable)
                SetVerticalNavigation(ticketButton, choices[choices.Count - 1].Button, null);

            if (EventSystem.current)
                (preferred ? preferred : choices[0]).Button.Select();
        }

        static void SetVerticalNavigation(Button button, Button previous, Button next)
        {
            button.navigation = new Navigation {
                mode = Navigation.Mode.Explicit,
                selectOnUp = previous,
                selectOnDown = next
            };
        }

        static void ShowOffers(List<Item> offers, bool evolution)
        {
            foreach (Item item in offers) {
                item.transform.SetAsLastSibling();
                item.ShowOffer(evolution);
            }
        }

        static void HideOffers(List<Item> offers)
        {
            foreach (Item item in offers)
                item.gameObject.SetActive(false);
        }

        void ClearOffers()
        {
            EventSystem events = EventSystem.current;
            if (events && events.currentSelectedGameObject &&
                events.currentSelectedGameObject.transform.IsChildOf(transform))
                events.SetSelectedGameObject(null);

            HideOffers(offered);
            HideOffers(evolutionOffers);
            offered.Clear();
            evolutionOffers.Clear();
            evolutionMenu = EvolutionMenu.None;
            SetTicketButton(false, "", false);
        }

        void Open()
        {
            IsOpen = true;
            rect.localScale = Vector3.one;
            GameManager.instance.Stop();
            AudioManager.instance.PlaySfx(AudioManager.Sfx.LevelUp);
            AudioManager.instance.EffectBgm(true);
        }

        void CreateTicketButton()
        {
            var button = new GameObject("Evolution Ticket", typeof(RectTransform), typeof(Image), typeof(Button));
            var placement = (RectTransform)button.transform;
            placement.SetParent(itemGroup.parent, false);
            placement.anchorMin = Vector2.zero;
            placement.anchorMax = Vector2.right;
            placement.offsetMin = new Vector2(5f, itemGroupBottom.y);
            placement.offsetMax = new Vector2(-5f, itemGroupBottom.y + TicketRowHeight - 2f);
            var background = button.GetComponent<Image>();
            background.color = new Color32(44, 64, 55, 255);
            ticketButton = button.GetComponent<Button>();
            ticketButton.targetGraphic = background;
            ticketButton.onClick.AddListener(ToggleTicketMenu);

            ticketLabel = Instantiate(title, placement);
            ticketLabel.name = "Ticket Label";
            ticketLabel.fontSize = 7;
            ticketLabel.raycastTarget = false;
            RectTransform labelRect = ticketLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            button.SetActive(false);
        }

        void SetTicketButton(bool visible, string label, bool interactable)
        {
            if (!ticketButton)
                return;

            ticketLabel.text = label;
            ticketLabel.color = interactable ? new Color32(255, 228, 156, 255) : Color.white;
            ticketButton.interactable = interactable;
            ticketButton.gameObject.SetActive(visible);
            itemGroup.offsetMin = itemGroupBottom + (visible ? Vector2.up * TicketRowHeight : Vector2.zero);
        }

        void ToggleTicketMenu()
        {
            if (evolutionMenu == EvolutionMenu.Ticket) {
                HideOffers(evolutionOffers);
                ShowLevelOffers();
            }
            else if (HasEvolutionTicket && evolutionOffers.Count > 0) {
                ShowEvolutionOffers(EvolutionMenu.Ticket);
            }
        }

        void CreateNotice()
        {
            notice = Instantiate(title, rect.parent);
            notice.name = "Battle Notice";
            notice.raycastTarget = false;
            notice.color = new Color(1f, 0.86f, 0.49f);
            RectTransform placement = notice.rectTransform;
            placement.anchoredPosition = new Vector2(0f, -44f);
            notice.gameObject.SetActive(false);
        }

        public void Notify(string message)
        {
            if (!notice)
                return;

            notice.text = message;
            noticeTime = 2.4f;
            notice.gameObject.SetActive(true);
        }
    }
}
