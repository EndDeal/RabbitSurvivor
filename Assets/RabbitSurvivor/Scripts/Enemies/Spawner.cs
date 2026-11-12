using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public enum BossType { None, Mid, Final }

    public class Spawner : MonoBehaviour
    {
        public Transform[] spawnPoint;
        public SpawnData[] spawnData;
        public float levelTime;

        [SerializeField] RuntimeAnimatorController[] enemyAnimations = System.Array.Empty<RuntimeAnimatorController>();
        [SerializeField] RuntimeAnimatorController midBossAnimation;
        [SerializeField] RuntimeAnimatorController finalBossAnimation;
        [SerializeField] SpawnData midBoss = new SpawnData { spriteType = 1, health = 145, speed = 1.3f, experience = 24 };
        [SerializeField] SpawnData finalBoss = new SpawnData { spriteType = 1, health = 525, speed = 1.7f };

        float timer;

        public SpawnData CurrentWave => spawnData[Mathf.Clamp(
            Mathf.FloorToInt(GameManager.instance.gameTime / levelTime), 0, spawnData.Length - 1)];

        void Awake()
        {
            spawnPoint = GetComponentsInChildren<Transform>();
            levelTime = GameManager.instance.maxGameTime / spawnData.Length;
        }

        void Update()
        {
            if (!GameManager.instance.isLive)
                return;

            timer += Time.deltaTime;
            SpawnData wave = CurrentWave;
            if (timer >= wave.spawnTime) {
                timer -= wave.spawnTime;
                Spawn(wave, BossType.None);
            }
        }

        public Enemy SpawnBoss(BossType type)
        {
            Enemy boss = Spawn(type == BossType.Final ? finalBoss : midBoss, type);
            GameManager game = GameManager.instance;
            if (game.BossPresentation)
                game.BossPresentation.ShowArrival(boss);
            else
                game.uiLevelUp.Notify("보스 등장!");
            return boss;
        }

        Enemy Spawn(SpawnData data, BossType bossType)
        {
            GameObject enemy = GameManager.instance.pool.Get(0);
            enemy.transform.position = spawnPoint[Random.Range(1, spawnPoint.Length)].position;
            RuntimeAnimatorController controller = data.spriteType < enemyAnimations.Length ?
                enemyAnimations[data.spriteType] : null;
            if (bossType == BossType.Mid)
                controller = midBossAnimation;
            else if (bossType == BossType.Final)
                controller = finalBossAnimation ? finalBossAnimation : midBossAnimation;

            Enemy spawned = enemy.GetComponent<Enemy>();
            spawned.Init(data, bossType, controller);
            return spawned;
        }
    }

    [System.Serializable]
    public class SpawnData
    {
        public float spawnTime;
        public int spriteType;
        public int health;
        public float speed;
        [Min(1)] public int killsPerMagnet = 100;
        [Min(1)] public int experience = 1;
    }
}
