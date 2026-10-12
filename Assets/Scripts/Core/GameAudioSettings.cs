using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Marea.Core
{
    /// <summary>씬과 개별 AudioSource의 음량/페이드에 영향을 주지 않는 사용자 음량 설정.</summary>
    public sealed class GameAudioSettings : MonoBehaviour
    {
        private const string MusicKey = "Audio.MusicVolume";
        private const string EffectsKey = "Audio.EffectsVolume";
        private static GameAudioSettings instance;
        private static AudioMixer mixer;
        private static AudioMixerGroup musicGroup;
        private static AudioMixerGroup effectsGroup;
        private float nextScan;

        public static float MusicVolume => PlayerPrefs.GetFloat(MusicKey, PlayerPrefs.GetFloat("Title.MasterVolume", 1f));
        public static float EffectsVolume => PlayerPrefs.GetFloat(EffectsKey, PlayerPrefs.GetFloat("Title.MasterVolume", 1f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            mixer = null;
            musicGroup = effectsGroup = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            LoadMixer();
            instance = new GameObject("GameAudioSettings").AddComponent<GameAudioSettings>();
            DontDestroyOnLoad(instance.gameObject);
            AudioListener.volume = 1f;
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
        }

        private static void LoadMixer()
        {
            if (mixer != null) return;
            mixer = Resources.Load<AudioMixer>("MareaAudio");
            if (mixer == null) return;
            musicGroup = mixer.FindMatchingGroups("Music")[0];
            effectsGroup = mixer.FindMatchingGroups("Sfx")[0];
        }

        public static void RouteMusic(AudioSource source)
        {
            LoadMixer();
            if (source != null && musicGroup != null) source.outputAudioMixerGroup = musicGroup;
        }

        public static void RouteEffects(AudioSource source)
        {
            LoadMixer();
            if (source != null && effectsGroup != null && source.outputAudioMixerGroup == null)
                source.outputAudioMixerGroup = effectsGroup;
        }

        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
            ApplyVolumes();
            PlayerPrefs.Save();
        }

        public static void PlayEffectAtPoint(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;
            var go = new GameObject("OneShotEffect");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            RouteEffects(source);
            source.Play();
            Object.Destroy(go, clip.length);
        }

        public static void SetEffectsVolume(float value)
        {
            PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
            ApplyVolumes();
            PlayerPrefs.Save();
        }

        private static void ApplyVolumes()
        {
            LoadMixer();
            if (mixer == null) return;
            mixer.SetFloat("MusicVolume", ToDecibels(MusicVolume));
            mixer.SetFloat("SfxVolume", ToDecibels(EffectsVolume));
        }

        private static float ToDecibels(float value) => value <= 0f ? -80f : Mathf.Log10(Mathf.Clamp01(value)) * 20f;
        private void Start() => ApplyVolumes();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RouteRemainingSources();
            ApplyVolumes();
        }

        private void Update()
        {
            // 기존 스크립트가 나중에 생성한 효과음도 설정을 따른다.
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            RouteRemainingSources();
        }

        private static void RouteRemainingSources()
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                RouteEffects(source);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }
    }
}
