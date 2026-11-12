using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Goldmetal.UndeadSurvivor
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager instance;
        [Header("# Game Control")]
        public bool isLive;
        public float gameTime;
        public float maxGameTime = 2 * 10f;
        [Header("# Player Info")]
        public int playerId;
        public float health;
        public float maxHealth = 100;
        public int level;
        public int kill;
        public int exp;
        public int[] nextExp = { 3, 5, 10, 100, 150, 210, 280, 360, 450, 600 };
        [Header("# Game Object")]
        public PoolManager pool;
        public Player player;
        public LevelUp uiLevelUp;
        public Result uiResult;
        public Transform uiJoy;
        public GameObject enemyCleaner;
        public Sprite treasureChestSprite;
        [SerializeField] LootDrops lootDrops;

        float baseMaxHealth;

        public int ExperienceToNextLevel => nextExp[Mathf.Min(level, nextExp.Length - 1)];

        void Awake()
        {
            instance = this;
            baseMaxHealth = maxHealth;
            Application.targetFrameRate = 60;
        }

        public void GameStart(int id)
        {
            playerId = id;
            health = maxHealth;

            player.gameObject.SetActive(true);
            uiLevelUp.Select(playerId % 2);
            Resume();

            AudioManager.instance.PlayBgm(true);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Select);
        }

        public void GameOver()
        {
            StartCoroutine(GameOverRoutine());
        }

        IEnumerator GameOverRoutine()
        {
            isLive = false;

            yield return new WaitForSeconds(0.5f);

            uiResult.gameObject.SetActive(true);
            uiResult.Lose();
            Stop();

            AudioManager.instance.PlayBgm(false);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Lose);
        }

        public void GameVictroy()
        {
            StartCoroutine(GameVictroyRoutine());
        }

        IEnumerator GameVictroyRoutine()
        {
            isLive = false;
            enemyCleaner.SetActive(true);

            yield return new WaitForSeconds(0.5f);

            uiResult.gameObject.SetActive(true);
            uiResult.Win();
            Stop();

            AudioManager.instance.PlayBgm(false);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Win);
        }

        public void GameRetry()
        {
            SceneManager.LoadScene("RabbitSurvivor");
        }

        public void GameQuit()
        {
            Application.Quit();
        }

        void Update()
        {
            if (!isLive)
                return;

            gameTime += Time.deltaTime;

            if (gameTime > maxGameTime) {
                gameTime = maxGameTime;
                GameVictroy();
            }
        }

        public void RegisterKill(Vector3 position, bool midBoss)
        {
            kill++;
            if (!isLive && !uiLevelUp.IsOpen)
                return;

            if (lootDrops)
                lootDrops.DropFromEnemy(position, midBoss);
            else
                GainExperience(1);

            if (midBoss && treasureChestSprite)
                TreasureChest.Drop(position, treasureChestSprite);
        }

        public void GainExperience(int amount)
        {
            exp += amount;
            while (exp >= ExperienceToNextLevel) {
                exp -= ExperienceToNextLevel;
                level++;
                uiLevelUp.Show();
            }
        }

        public void SetMaxHealthBonus(float rate)
        {
            float newMax = baseMaxHealth * (1f + rate);
            health = Mathf.Min(health + newMax - maxHealth, newMax);
            maxHealth = newMax;
        }

        public void Stop()
        {
            isLive = false;
            Time.timeScale = 0;
            uiJoy.localScale = Vector3.zero;
        }

        public void Resume()
        {
            isLive = true;
            Time.timeScale = 1;
            uiJoy.localScale = Vector3.one;
        }
    }
}
