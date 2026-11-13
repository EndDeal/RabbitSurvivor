using System;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;

namespace Goldmetal.UndeadSurvivor
{
    public class AchiveManager : MonoBehaviour
    {
        public GameObject[] lockCharacter;
        public GameObject[] unlockCharacter;
        public GameObject uiNotice;

        const int PotatoKillRequirement = 20;

        enum Achive { UnlockPotato, UnlockBean }
        Achive[] achives;
        WaitForSecondsRealtime wait;

        internal static bool IsCharacterUnlocked(int index)
        {
            return index < 2 || PlayerPrefs.GetInt(index == 2 ? nameof(Achive.UnlockPotato) : nameof(Achive.UnlockBean)) == 1;
        }

        internal static string CharacterRequirement(int index)
        {
            return index == 2 ? $"적 {PotatoKillRequirement}마리 처치 시 합류" : "용왕 처치 시 합류";
        }

        void Awake()
        {
            achives = (Achive[])Enum.GetValues(typeof(Achive));
            wait = new WaitForSecondsRealtime(5);

            if (!PlayerPrefs.HasKey("MyData")) {
                Init();
            }
        }

        void Init()
        {
            PlayerPrefs.SetInt("MyData", 1);

            foreach (Achive achive in achives) {
                PlayerPrefs.SetInt(achive.ToString(), 0);
            }
        }

        void Start()
        {
            UnlockCharacter();
            Text potatoRequirement = lockCharacter[0].GetComponentInChildren<Text>(true);
            if (potatoRequirement)
                potatoRequirement.text = $"적 {PotatoKillRequirement}마리 처치 시 합류";
        }

        void UnlockCharacter()
        {
            for (int index = 0; index < lockCharacter.Length; index++) {
                string achiveName = achives[index].ToString();
                bool isUnlock = PlayerPrefs.GetInt(achiveName) == 1;
                lockCharacter[index].SetActive(!isUnlock);
                unlockCharacter[index].SetActive(isUnlock);
            }
        }

        void LateUpdate()
        {
            foreach (Achive achive in achives) {
                CheckAchive(achive);
            }
        }

        void CheckAchive(Achive achive)
        {
            bool isAchive = false;

            switch (achive) {
                case Achive.UnlockPotato:
                    isAchive = GameManager.instance.kill >= PotatoKillRequirement;
                    break;
                case Achive.UnlockBean:
                    isAchive = GameManager.instance.HasWon;
                    break;
            }

            if (isAchive && PlayerPrefs.GetInt(achive.ToString()) == 0) {
                PlayerPrefs.SetInt(achive.ToString(), 1);
                PlayerPrefs.Save();

                for (int index = 0; index < uiNotice.transform.childCount; index++) {
                    bool isActive = index == (int)achive;
                    uiNotice.transform.GetChild(index).gameObject.SetActive(isActive);
                }

                StartCoroutine(NoticeRoutine());
            }
        }

        IEnumerator NoticeRoutine()
        {
            uiNotice.SetActive(true);
            AudioManager.instance.PlaySfx(AudioManager.Sfx.LevelUp);

            yield return wait;

            uiNotice.SetActive(false);
        }
    }
}
