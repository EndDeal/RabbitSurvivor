using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class LootDrops : MonoBehaviour
    {
        const int FirstMagnetKills = 100;
        const float ExperiencePickupRadius = 0.35f;
        const float ExperienceAttractRadius = 1.2f;
        const float ExperienceSpeed = 12f;
        const float ItemPickupRadius = 0.7f;

        [SerializeField] Spawner spawner;
        [SerializeField] Sprite experienceSprite;
        [SerializeField] Sprite magnetSprite;
        [SerializeField] Sprite potionSprite;
        [SerializeField, Range(0f, 1f)] float potionDropChance = 0.02f;
        [SerializeField, Range(0f, 1f)] float potionHealFraction = 0.25f;

        enum Kind { Experience, Magnet, Potion }

        struct Pickup
        {
            public Kind kind;
            public SpriteRenderer sprite;
            public bool attracted;
            public int experience;
        }

        readonly List<Pickup> pickups = new List<Pickup>();
        readonly Stack<SpriteRenderer> availableSprites = new Stack<SpriteRenderer>();
        GameManager game;
        int killsSinceMagnet;
        bool hasDroppedMagnet;

        void Awake()
        {
            game = GetComponent<GameManager>();
        }

        public void DropFromEnemy(Vector3 position, bool boss, int experience)
        {
            Drop(Kind.Experience, experienceSprite, position, experience);
            if (boss)
                return;

            int magnetInterval = hasDroppedMagnet ?
                Mathf.Max(FirstMagnetKills, spawner.CurrentWave.killsPerMagnet) : FirstMagnetKills;
            killsSinceMagnet++;
            if (killsSinceMagnet >= magnetInterval) {
                killsSinceMagnet = 0;
                hasDroppedMagnet = true;
                Drop(Kind.Magnet, magnetSprite, position + Vector3.left * 0.45f);
                game.uiLevelUp.Notify("경험치 자석 등장!");
            }

            if (Random.value < potionDropChance)
                Drop(Kind.Potion, potionSprite, position + Vector3.right * 0.45f);
        }

        void Drop(Kind kind, Sprite sprite, Vector3 position, int experience = 0)
        {
            SpriteRenderer renderer;
            if (availableSprites.Count > 0) {
                renderer = availableSprites.Pop();
            }
            else {
                renderer = new GameObject("Loot Pickup").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(transform, true);
            }

            renderer.sprite = sprite;
            renderer.transform.localScale = Vector3.one * (kind == Kind.Experience ? 0.56f : 1f);
            renderer.sortingOrder = kind == Kind.Experience ? 1 : 4;
            renderer.transform.position = position;
            renderer.gameObject.SetActive(true);
            pickups.Add(new Pickup { kind = kind, sprite = renderer, experience = experience });
        }

        void Update()
        {
            if (!game.isLive)
                return;

            Vector2 playerPosition = game.player.transform.position;
            int collectedExperience = 0;
            for (int index = pickups.Count - 1; index >= 0; index--) {
                Pickup pickup = pickups[index];
                Vector2 position = pickup.sprite.transform.position;
                float distance = (position - playerPosition).sqrMagnitude;

                if (pickup.kind == Kind.Experience) {
                    if (pickup.attracted || distance <= ExperienceAttractRadius * ExperienceAttractRadius) {
                        position = Vector2.MoveTowards(position, playerPosition, ExperienceSpeed * Time.deltaTime);
                        pickup.sprite.transform.position = position;
                        distance = (position - playerPosition).sqrMagnitude;
                    }

                    if (distance <= ExperiencePickupRadius * ExperiencePickupRadius) {
                        collectedExperience += pickup.experience;
                        RemoveAt(index);
                    }
                    continue;
                }

                if (distance > ItemPickupRadius * ItemPickupRadius)
                    continue;

                if (pickup.kind == Kind.Magnet) {
                    RemoveAt(index);
                    AttractExperience();
                    game.uiLevelUp.Notify("경험치 자석 발동!");
                }
                else if (game.health < game.maxHealth) {
                    RemoveAt(index);
                    game.health = Mathf.Min(game.health + game.maxHealth * potionHealFraction, game.maxHealth);
                    game.uiLevelUp.Notify("체력 회복");
                }
            }

            if (collectedExperience > 0)
                game.GainExperience(collectedExperience);
        }

        void AttractExperience()
        {
            for (int index = 0; index < pickups.Count; index++) {
                Pickup pickup = pickups[index];
                if (pickup.kind == Kind.Experience) {
                    pickup.attracted = true;
                    pickups[index] = pickup;
                }
            }
        }

        void RemoveAt(int index)
        {
            SpriteRenderer sprite = pickups[index].sprite;
            sprite.gameObject.SetActive(false);
            availableSprites.Push(sprite);

            int last = pickups.Count - 1;
            pickups[index] = pickups[last];
            pickups.RemoveAt(last);
        }
    }
}
