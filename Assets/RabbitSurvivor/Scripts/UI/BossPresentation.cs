using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public class BossPresentation : MonoBehaviour
    {
        const string CaptainName = "대장 거북";
        const string DragonName = "용왕";
        const float BannerDuration = 1.85f;
        const float CelebrationDuration = 0.8f;
        const float GoldOpacity = 0.05f;
        const int PiecesPerCorner = 4;

        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        static readonly Color Gold = new Color32(255, 206, 104, 255);
        static readonly Color Ink = new Color32(41, 32, 53, 247);
        static readonly Color Cream = new Color32(255, 244, 216, 255);
        static readonly Color[] ConfettiColors = {
            Gold, Cream, new Color32(247, 162, 136, 255), new Color32(152, 222, 205, 255)
        };

        [SerializeField] Font font;
        [SerializeField] Sprite arrivalPortrait;
        [SerializeField] Sprite defeatedPortrait;
        [SerializeField] Sprite finalArrivalPortrait;
        [SerializeField] Sprite finalDefeatedPortrait;

        public float BannerSeconds => BannerDuration;

        readonly List<Enemy> bosses = new List<Enemy>(3);
        readonly List<ConfettiPiece> confetti = new List<ConfettiPiece>(4 * PiecesPerCorner);
        Enemy focusedBoss;
        Enemy queuedArrival;
        Canvas canvas;
        RectTransform healthPanel;
        RectTransform healthFill;
        RectTransform healthTrail;
        Text healthPercent;
        Text healthName;
        int lastHealthPercent = -1;
        float trail;

        RectTransform banner;
        CanvasGroup bannerGroup;
        Image bannerBorder;
        Image bannerPortrait;
        Text bannerTitle;
        float bannerStarted;
        bool showingDefeat;

        Image goldFlash;
        RectTransform confettiLayer;
        float celebrationStarted;

        struct ConfettiPiece
        {
            public Image image;
            public Vector2 direction;
            public float speed;
            public float spin;
            public Color color;
        }

        void Awake()
        {
            var overlay = new GameObject("Boss Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(135f, 270f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)overlay.transform;
            goldFlash = AddImage(Stretch(root, "Boss Defeat Gold"), Color.clear);
            goldFlash.gameObject.SetActive(false);

            confettiLayer = Stretch(root, "Boss Defeat Confetti");
            BuildConfetti();
            confettiLayer.gameObject.SetActive(false);

            BuildHealthPanel(root);
            BuildBanner(root);
        }

        void OnDestroy()
        {
            if (canvas)
                Destroy(canvas.gameObject);
        }

        public void ShowArrival(Enemy boss)
        {
            if (!bosses.Contains(boss))
                bosses.Add(boss);
            Focus(boss);
            if (banner.gameObject.activeSelf && showingDefeat)
                queuedArrival = boss;
            else
                ShowBanner(boss.Boss, false);
        }

        public void ShowDefeat(Enemy boss)
        {
            if (!bosses.Remove(boss))
                return;

            if (focusedBoss == boss)
                FocusLastBoss();
            ShowBanner(boss.Boss, true);
            celebrationStarted = Time.unscaledTime;
            goldFlash.color = new Color(Gold.r, Gold.g, Gold.b, GoldOpacity);
            goldFlash.gameObject.SetActive(true);
            confettiLayer.gameObject.SetActive(true);
            AnimateCelebration(0f);
        }

        public void Clear()
        {
            bosses.Clear();
            queuedArrival = null;
            Focus(null);
            banner.gameObject.SetActive(false);
            goldFlash.gameObject.SetActive(false);
            confettiLayer.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (focusedBoss && (!focusedBoss.gameObject.activeInHierarchy || focusedBoss.health <= 0f))
                FocusLastBoss();
            if (focusedBoss)
                UpdateHealth(false);
            else if (healthPanel.gameObject.activeSelf)
                FocusLastBoss();

            if (banner.gameObject.activeSelf)
                AnimateBanner(Time.unscaledTime - bannerStarted);
            if (goldFlash.gameObject.activeSelf)
                AnimateCelebration(Time.unscaledTime - celebrationStarted);
        }

        void FocusLastBoss()
        {
            while (bosses.Count > 0) {
                int index = bosses.Count - 1;
                Enemy candidate = bosses[index];
                if (candidate && candidate.gameObject.activeInHierarchy && candidate.health > 0f) {
                    Focus(candidate);
                    return;
                }
                bosses.RemoveAt(index);
            }
            Focus(null);
        }

        void Focus(Enemy boss)
        {
            focusedBoss = boss;
            healthPanel.gameObject.SetActive(boss != null);
            lastHealthPercent = -1;
            if (boss) {
                healthName.text = boss.Boss == BossType.Final ? DragonName : CaptainName;
                UpdateHealth(true);
            }
        }

        void UpdateHealth(bool resetTrail)
        {
            float ratio = Mathf.Clamp01(focusedBoss.health / focusedBoss.maxHealth);
            trail = resetTrail ? ratio : Mathf.MoveTowards(trail, ratio, Time.unscaledDeltaTime * 0.9f);
            healthFill.anchorMax = new Vector2(ratio, 1f);
            healthTrail.anchorMax = new Vector2(trail, 1f);
            int percent = Mathf.RoundToInt(ratio * 100f);
            if (ratio > 0f && ratio < 1f)
                percent = Mathf.Clamp(percent, 1, 99);
            if (percent == lastHealthPercent)
                return;
            lastHealthPercent = percent;
            healthPercent.text = $"{percent}%";
        }

        void ShowBanner(BossType boss, bool defeated)
        {
            showingDefeat = defeated;
            Sprite portrait = defeated ? defeatedPortrait : arrivalPortrait;
            if (boss == BossType.Final) {
                Sprite finalPortrait = defeated ? finalDefeatedPortrait : finalArrivalPortrait;
                if (finalPortrait)
                    portrait = finalPortrait;
            }
            bannerPortrait.sprite = portrait;
            bannerTitle.text = defeated ? "보스 처치!" : "보스 등장!";
            bannerTitle.color = defeated ? Gold : Cream;
            bannerBorder.color = defeated ? Gold : new Color32(187, 159, 213, 255);
            bannerStarted = Time.unscaledTime;
            banner.gameObject.SetActive(true);
            AnimateBanner(0f);
        }

        void AnimateBanner(float elapsed)
        {
            if (elapsed >= BannerDuration) {
                banner.gameObject.SetActive(false);
                Enemy arrival = queuedArrival;
                queuedArrival = null;
                if (arrival && bosses.Contains(arrival))
                    ShowBanner(arrival.Boss, false);
                return;
            }

            float enter = Mathf.Clamp01(elapsed / 0.18f);
            float exit = Mathf.Clamp01((BannerDuration - elapsed) / 0.26f);
            float slide = 1f - enter;
            float height = ((RectTransform)canvas.transform).rect.height;
            float portraitLayout = Mathf.InverseLerp(145f, 270f, height);
            float size = Mathf.Lerp(0.72f, 1f, portraitLayout);
            banner.localScale = Vector3.one * size;
            banner.anchoredPosition = new Vector2(-14f * slide * slide, -Mathf.Lerp(57f, 66f, portraitLayout));
            bannerGroup.alpha = Mathf.Min(enter, exit);
        }

        void AnimateCelebration(float elapsed)
        {
            if (elapsed >= CelebrationDuration) {
                goldFlash.gameObject.SetActive(false);
                confettiLayer.gameObject.SetActive(false);
                return;
            }

            float progress = Mathf.Clamp01(elapsed / CelebrationDuration);
            goldFlash.color = new Color(Gold.r, Gold.g, Gold.b, GoldOpacity * (1f - progress));
            foreach (ConfettiPiece piece in confetti) {
                RectTransform rect = piece.image.rectTransform;
                rect.anchoredPosition = piece.direction * (6f + piece.speed * elapsed) +
                    Vector2.down * (34f * elapsed * elapsed);
                rect.localRotation = Quaternion.Euler(0f, 0f, piece.spin * elapsed);
                Color color = piece.color;
                color.a = Mathf.Min(1f, (1f - progress) * 2f) * 0.7f;
                piece.image.color = color;
            }
        }

        void BuildHealthPanel(RectTransform root)
        {
            healthPanel = Place(root, "Boss Health", TopCenter, TopCenter, new Vector2(0f, -28f), new Vector2(78f, 13f));
            AddImage(healthPanel, Gold);
            RectTransform face = Stretch(healthPanel, "Health Panel Face", 0.7f);
            AddImage(face, Ink);
            healthName = AddText(Place(face, "Boss Name", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(3f, -1f), new Vector2(34f, 6f)), 6, TextAnchor.MiddleLeft);
            healthPercent = AddText(Place(face, "Boss Health Percent", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-3f, -1f), new Vector2(33f, 6f)), 6, TextAnchor.MiddleRight);

            RectTransform track = Stretch(face, "Boss Health Track");
            track.anchorMax = new Vector2(1f, 0f);
            track.offsetMin = new Vector2(3f, 2f);
            track.offsetMax = new Vector2(-3f, 4.8f);
            AddImage(track, new Color32(81, 54, 76, 255));
            healthTrail = Stretch(track, "Boss Health Damage Trail");
            AddImage(healthTrail, Gold);
            healthFill = Stretch(track, "Boss Health Fill");
            AddImage(healthFill, new Color32(241, 100, 126, 255));
            healthPanel.gameObject.SetActive(false);
        }

        void BuildBanner(RectTransform root)
        {
            banner = Place(root, "Boss Portrait Banner", TopCenter, TopCenter, new Vector2(0f, -66f), new Vector2(93f, 28f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.interactable = false;
            bannerGroup.blocksRaycasts = false;
            bannerBorder = AddImage(banner, Gold);

            RectTransform face = Stretch(banner, "Banner Face", 1f);
            AddImage(face, Ink);
            RectTransform portrait = Place(face, "Boss Portrait", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), new Vector2(24f, 24f));
            bannerPortrait = AddImage(portrait, Color.white);
            bannerPortrait.preserveAspect = true;
            bannerTitle = AddText(Place(face, "Boss Announcement", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(59f, 15f)), 9, TextAnchor.MiddleLeft);
            banner.gameObject.SetActive(false);
        }

        void BuildConfetti()
        {
            for (int corner = 0; corner < 4; corner++) {
                Vector2 anchor = new Vector2(corner % 2, corner / 2);
                Vector2 inward = Vector2.one - 2f * anchor;
                for (int index = 0; index < PiecesPerCorner; index++) {
                    Vector2 direction = new Vector2(inward.x * (0.5f + index * 0.17f),
                        inward.y * (1f - index * 0.16f)).normalized;
                    RectTransform rect = Place(confettiLayer, "Confetti", anchor, Vector2.one * 0.5f,
                        Vector2.zero, new Vector2(index % 2 == 0 ? 2.1f : 1.2f, 3f));
                    Color color = ConfettiColors[index];
                    confetti.Add(new ConfettiPiece {
                        image = AddImage(rect, color),
                        direction = direction,
                        speed = 26f + index * 8f,
                        spin = inward.x * (145f + index * 35f),
                        color = color
                    });
                }
            }
        }

        static RectTransform Place(RectTransform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 offset, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Stretch(RectTransform parent, string name, float inset = 0f)
        {
            RectTransform rect = Place(parent, name, Vector2.zero, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
            return rect;
        }

        static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text AddText(RectTransform rect, int size, TextAnchor alignment)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Cream;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }
    }
}
