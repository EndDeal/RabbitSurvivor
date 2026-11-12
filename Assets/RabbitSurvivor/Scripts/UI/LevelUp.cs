using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Goldmetal.UndeadSurvivor
{
    public class LevelUp : MonoBehaviour
    {
        const int WeaponSlots = 3;
        const int PassiveSlots = 3;
        const int OfferCount = 3;

        [SerializeField] ItemData[] additionalItems = Array.Empty<ItemData>();
        [SerializeField] Text title;

        RectTransform rect;
        Text notice;
        float noticeTime;
        List<Item> items;
        Item recovery;
        readonly List<Item> eligible = new List<Item>();
        readonly List<Item> offered = new List<Item>();
        int pendingLevels;
        bool evolution;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            items = new List<Item>(GetComponentsInChildren<Item>(true));
            foreach (Item item in items) {
                item.gameObject.SetActive(false);
            }

            Item template = items[0];
            foreach (ItemData data in additionalItems) {
                Item card = Instantiate(template, template.transform.parent);
                card.data = data;
                card.name = data.itemName;
                items.Add(card);
            }

            foreach (Item item in items) {
                ItemData partner = item.data.requiredPassive;
                if (item.data.IsPassive) {
                    partner = items.Find(candidate => candidate.data.requiredPassive == item.data)?.data;
                }
                if (item.data.itemType == ItemData.ItemType.Heal)
                    recovery = item;
                item.Bind(this, partner);
            }

            if (title)
                CreateNotice();
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

        public bool TryShowEvolution()
        {
            if (IsOpen)
                return false;

            eligible.Clear();
            foreach (Item item in items) {
                if (CanEvolve(item))
                    eligible.Add(item);
            }
            if (eligible.Count == 0)
                return false;

            evolution = true;
            foreach (Item item in eligible) {
                AddOffer(item);
            }
            Open("보물상자\n진화할 무기 선택");
            return true;
        }

        bool CanEvolve(Item item)
        {
            if (!item.weapon || item.weapon.IsEvolved || !item.data.requiredPassive)
                return false;

            Item passive = items.Find(candidate => candidate.data == item.data.requiredPassive);
            return passive && passive.IsMaxLevel;
        }

        public void Choose(Item item)
        {
            if (!IsOpen || !offered.Contains(item))
                return;

            if (evolution) {
                item.weapon.Evolve();
                Notify($"{item.data.evolutionName} 진화!");
            }
            else {
                item.Upgrade();
            }

            foreach (Item offer in offered) {
                offer.gameObject.SetActive(false);
            }
            offered.Clear();

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

        void ShowNextLevel()
        {
            pendingLevels--;
            evolution = false;
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
                AddOffer(passive);
            }

            while (offered.Count < OfferCount && eligible.Count > 0) {
                int index = Random.Range(0, eligible.Count);
                AddOffer(eligible[index]);
                eligible.RemoveAt(index);
            }

            if (offered.Count == 0)
                AddOffer(recovery);

            Open($"레벨 업\n액티브 {weaponCount}/{WeaponSlots} · 패시브 {passiveCount}/{PassiveSlots}");
        }

        void AddOffer(Item item)
        {
            offered.Add(item);
            item.transform.SetAsLastSibling();
            item.ShowOffer(evolution);
        }

        void Open(string heading)
        {
            IsOpen = true;
            if (title)
                title.text = heading;

            rect.localScale = Vector3.one;
            GameManager.instance.Stop();
            AudioManager.instance.PlaySfx(AudioManager.Sfx.LevelUp);
            AudioManager.instance.EffectBgm(true);
        }

        void CreateNotice()
        {
            notice = Instantiate(title, rect.parent);
            notice.name = "Battle Notice";
            notice.raycastTarget = false;
            notice.color = new Color(1f, 0.86f, 0.49f);
            RectTransform placement = notice.rectTransform;
            placement.anchoredPosition = new Vector2(0f, -34f);
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
