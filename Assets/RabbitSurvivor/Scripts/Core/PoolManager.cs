using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class PoolManager : MonoBehaviour
    {
        public GameObject[] prefabs;

        List<GameObject>[] pools;

        void Awake()
        {
            pools = new List<GameObject>[prefabs.Length];

            for (int index = 0; index < pools.Length; index++) {
                pools[index] = new List<GameObject>();
            }
        }

        public GameObject Get(int index)
        {
            GameObject select = null;

            foreach (GameObject item in pools[index]) {
                if (!item.activeSelf) {
                    select = item;
                    select.SetActive(true);
                    break;
                }
            }

            if (!select) {
                select = Instantiate(prefabs[index], transform);
                pools[index].Add(select);
            }

            return select;
        }

        internal void RecycleOutsideFinalArena(GameManager game)
        {
            foreach (List<GameObject> pool in pools) {
                foreach (GameObject item in pool) {
                    if (!item.activeInHierarchy || !game.IsOutsideFinalArena(item.transform.position))
                        continue;

                    if (item.TryGetComponent(out Enemy enemy)) {
                        if (!enemy.IsBoss)
                            item.SetActive(false);
                    }
                    else if (item.TryGetComponent(out Bullet bullet)) {
                        if (bullet.per != Bullet.InfinitePierce)
                            item.SetActive(false);
                    }
                    else if (item.TryGetComponent<EnemyProjectile>(out _)) {
                        item.SetActive(false);
                    }
                }
            }
        }
    }
}
