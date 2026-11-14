using UnityEngine;
using UnityEngine.UI;

namespace RabbitSurvivor
{
    public class HUD : MonoBehaviour
    {
        public enum InfoType { Exp, Level, Kill, Time, Health }
        public InfoType type;

        Text myText;
        Slider mySlider;
        int displayedNumber = -1;
        bool displayedBossClock;

        void Awake()
        {
            myText = GetComponent<Text>();
            mySlider = GetComponent<Slider>();
        }

        void OnEnable()
        {
            displayedNumber = -1;
        }

        void LateUpdate()
        {
            GameManager game = GameManager.instance;
            switch (type) {
                case InfoType.Exp:
                    mySlider.value = (float)game.exp / game.ExperienceToNextLevel;
                    break;
                case InfoType.Level:
                    if (displayedNumber != game.level) {
                        displayedNumber = game.level;
                        myText.text = $"Lv.{displayedNumber:F0}";
                    }
                    break;
                case InfoType.Kill:
                    if (displayedNumber != game.kill) {
                        displayedNumber = game.kill;
                        myText.text = displayedNumber.ToString("F0");
                    }
                    break;
                case InfoType.Time:
                    bool bossClock = game.IsBossClockHeld;
                    if (displayedNumber < 0 || displayedBossClock != bossClock) {
                        displayedBossClock = bossClock;
                        myText.color = bossClock ? new Color32(255, 206, 104, 255) : Color.white;
                    }
                    int seconds = Mathf.FloorToInt(game.gameTime);
                    if (displayedNumber != seconds) {
                        displayedNumber = seconds;
                        myText.text = $"{seconds / 60:D2}:{seconds % 60:D2}";
                    }
                    break;
                case InfoType.Health:
                    mySlider.value = game.health / game.maxHealth;
                    break;
            }
        }
    }
}
