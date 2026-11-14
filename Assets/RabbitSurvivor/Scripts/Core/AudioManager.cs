using System.Collections;
using UnityEngine;

namespace RabbitSurvivor
{
    public class AudioManager : MonoBehaviour
    {
        const string MasterVolumeKey = "MasterVolume";
        static readonly int[] GemScale = { 0, 2, 4, 7, 9, 12 };
        public static AudioManager instance;

        [Header("#BGM")]
        public AudioClip bgmClip;
        public float bgmVolume;
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip bossMusic;
        AudioSource bgmPlayer;
        AudioHighPassFilter bgmEffect;
        Coroutine musicFade;
        AudioClip requestedMusic;

        [Header("#SFX")]
        public AudioClip[] sfxClips;
        public float sfxVolume;
        public int channels;
        AudioSource[] sfxPlayers;
        AudioSource[] feedbackPlayers;
        int channelIndex;
        int feedbackIndex;
        float lastGemAt = float.NegativeInfinity;
        float nextGemNote;
        float nextPoisonPop;
        int gemNotes;

        public enum Sfx { Dead, Hit, LevelUp = 3, Lose, Melee, Range = 7, Select, Win, Gem, Mine, PoisonPop }

        void Awake()
        {
            instance = this;
            LoadMasterVolume();

            GameObject bgmObject = new GameObject("BgmPlayer");
            bgmObject.transform.SetParent(transform, false);
            bgmPlayer = bgmObject.AddComponent<AudioSource>();
            bgmPlayer.playOnAwake = false;
            bgmPlayer.loop = true;
            bgmEffect = Camera.main.GetComponent<AudioHighPassFilter>();

            GameObject sfxObject = new GameObject("SfxPlayer");
            sfxObject.transform.SetParent(transform, false);
            sfxPlayers = CreateSfxChannels(sfxObject, channels);
            feedbackPlayers = CreateSfxChannels(sfxObject, 4);
            SetMusic(menuMusic);
        }

        static AudioSource[] CreateSfxChannels(GameObject owner, int count)
        {
            var sources = new AudioSource[count];
            for (int index = 0; index < count; index++) {
                sources[index] = owner.AddComponent<AudioSource>();
                sources[index].playOnAwake = false;
                sources[index].bypassListenerEffects = true;
            }
            return sources;
        }

        public static void SetMasterVolume(float volume) => AudioListener.volume = Mathf.Clamp01(volume);

        internal static void LoadMasterVolume() => SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));

        public static void SaveMasterVolume()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
            PlayerPrefs.Save();
        }

        public void PlayBgm(bool isPlay) => SetMusic(isPlay ? bgmClip : null);

        internal void PlayBossMusic() => SetMusic(bossMusic ? bossMusic : bgmClip);

        void SetMusic(AudioClip clip)
        {
            if (requestedMusic == clip)
                return;

            requestedMusic = clip;
            if (musicFade != null)
                StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeToMusic(clip));
        }

        IEnumerator FadeToMusic(AudioClip clip)
        {
            const float fadeSeconds = .24f;
            float fadeSpeed = bgmVolume / fadeSeconds;
            if (bgmPlayer.isPlaying && bgmPlayer.clip != clip) {
                while (bgmPlayer.volume > 0f) {
                    bgmPlayer.volume = Mathf.MoveTowards(bgmPlayer.volume, 0f, fadeSpeed * Time.unscaledDeltaTime);
                    yield return null;
                }
                bgmPlayer.Stop();
            }

            if (clip && !bgmPlayer.isPlaying) {
                bgmPlayer.clip = clip;
                bgmPlayer.volume = 0f;
                bgmPlayer.Play();
            }
            while (clip && bgmPlayer.volume < bgmVolume) {
                bgmPlayer.volume = Mathf.MoveTowards(bgmPlayer.volume, bgmVolume, fadeSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
            musicFade = null;
        }

        public void EffectBgm(bool isPlay)
        {
            if (bgmEffect)
                bgmEffect.enabled = isPlay;
        }

        internal void PlayGem(int highestValue)
        {
            float now = Time.time;
            if (now - lastGemAt > .42f)
                gemNotes = 0;
            lastGemAt = now;
            if (now < nextGemNote && highestValue < 9)
                return;

            nextGemNote = now + .055f;
            float pitch = Mathf.Pow(2f, GemScale[gemNotes] / 12f);
            gemNotes = Mathf.Min(gemNotes + 1, GemScale.Length - 1);
            bool heavy = highestValue >= 9;
            PlayFeedback(Sfx.Gem, heavy ? pitch * .72f : pitch, heavy ? .75f : .49f);
        }

        internal void PlayPoisonPop()
        {
            if (Time.time < nextPoisonPop)
                return;

            nextPoisonPop = Time.time + .05f;
            PlayFeedback(Sfx.PoisonPop, 1f, .85f);
        }

        public void PlaySfx(Sfx sfx)
        {
            int clipIndex = (int)sfx;
            if (sfx == Sfx.Hit || sfx == Sfx.Melee)
                clipIndex += Random.Range(0, 2);
            PlayClip(clipIndex, sfxPlayers, ref channelIndex, 1f, 1f);
        }

        internal void PlayFeedback(Sfx sfx, float pitch, float volume) =>
            PlayClip((int)sfx, feedbackPlayers, ref feedbackIndex, pitch, volume);

        void PlayClip(int clipIndex, AudioSource[] players, ref int startIndex, float pitch, float volume)
        {
            if (clipIndex >= sfxClips.Length || !sfxClips[clipIndex])
                return;

            for (int index = 0; index < players.Length; index++) {
                int channel = (index + startIndex) % players.Length;
                AudioSource source = players[channel];
                if (source.isPlaying)
                    continue;

                startIndex = (channel + 1) % players.Length;
                source.clip = sfxClips[clipIndex];
                source.pitch = pitch;
                source.volume = sfxVolume * volume;
                source.Play();
                break;
            }
        }
    }
}
