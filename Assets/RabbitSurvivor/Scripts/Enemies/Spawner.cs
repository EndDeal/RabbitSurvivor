using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class Spawner : MonoBehaviour
    {
        public Transform[] spawnPoint;
        public SpawnData[] spawnData;
        public float levelTime;

        [SerializeField] float[] midBossTimes = System.Array.Empty<float>();

        int nextMidBoss;
        float timer;

        public SpawnData CurrentWave => spawnData[Mathf.Min(
            Mathf.FloorToInt(GameManager.instance.gameTime / levelTime), spawnData.Length - 1)];

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
            float gameTime = GameManager.instance.gameTime;
            SpawnData wave = CurrentWave;

            if (nextMidBoss < midBossTimes.Length && gameTime >= midBossTimes[nextMidBoss]) {
                SpawnMidBoss(wave);
                nextMidBoss++;
            }

            if (timer > wave.spawnTime) {
                timer = 0;
                Spawn(wave, false);
            }
        }

        void SpawnMidBoss(SpawnData wave)
        {
            SpawnData boss = new SpawnData {
                spriteType = spawnData[spawnData.Length - 1].spriteType,
                health = wave.health * (5 + nextMidBoss),
                speed = wave.speed * 0.8f
            };
            Spawn(boss, true);
            GameManager.instance.uiLevelUp.Notify("중간 보스 출현!");
        }

        void Spawn(SpawnData data, bool midBoss)
        {
            GameObject enemy = GameManager.instance.pool.Get(0);
            enemy.transform.position = spawnPoint[Random.Range(1, spawnPoint.Length)].position;
            enemy.GetComponent<Enemy>().Init(data, midBoss);
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
    }
}
