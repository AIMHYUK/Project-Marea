using System.Collections;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 배경음악 · 앰비언스. 씬에 하나만 둔다. (+10/6, 이슈 117)
    ///
    /// 기본 BGM(기획 SYS-01, 영업 밖 시간)은 defaultTracks를 차례로 돈다. 바다 앰비언스(SYS-04 파도 · 갈매기)는
    /// ambienceLoops가 각자 계속 돈다 — BGM이 바뀌어도 안 끊긴다.
    ///
    /// 영업 중 BGM(SYS-02) · 리듬게임 BGM은 이 파일 몫이 아니다(B). 그쪽은 Override로 덮고 ClearOverride로 푼다:
    ///   BgmPlayer.Override(clip);   // 영업 시작 · 미니게임 진입
    ///   BgmPlayer.ClearOverride();  // 끝나면 기본 BGM으로 돌아온다
    /// 바뀔 때는 fadeSeconds 동안 줄였다가 키운다.
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        [Header("기본 BGM (SYS-01)")]
        [Tooltip("영업 밖 시간에 차례로 트는 곡들. 끝나면 다음 곡, 마지막 다음은 처음.")]
        [SerializeField] private AudioClip[] defaultTracks;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
        [Tooltip("곡이 바뀔 때 줄였다 키우는 시간(초).")]
        [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;

        [Header("앰비언스 (SYS-04)")]
        [Tooltip("계속 도는 소리들 — 파도, 갈매기. 각자 따로 돈다.")]
        [SerializeField] private AudioClip[] ambienceLoops;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;

        private AudioSource _bgm;
        private AudioClip _override;
        private int _track = -1;
        private Coroutine _switching;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[BgmPlayer] 씬에 둘 이상 있다 — '{name}'은 끈다. 하나만 남길 것.", this);
                enabled = false;
                return;
            }
            Instance = this;

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.spatialBlend = 0f;

            if (ambienceLoops == null) return;
            foreach (AudioClip clip in ambienceLoops)
            {
                if (clip == null) continue;
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.clip = clip;
                s.volume = ambienceVolume;
                // 갈매기 · 파도가 늘 같은 자리에서 겹치지 않게 시작점을 흩는다.
                s.time = Random.Range(0f, clip.length * 0.9f);
                s.Play();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (defaultTracks == null || defaultTracks.Length == 0)
                Debug.LogWarning($"{name}: BgmPlayer.defaultTracks가 비어 있다. 기본 BGM이 안 난다.", this);
            else
                SwitchTo(NextDefault(), loop: false);
        }

        // 기본 곡이 끝나면 다음 곡. 덮어쓴 곡은 loop라 안 끝난다.
        private void Update()
        {
            if (_override != null || _switching != null || _bgm.isPlaying) return;
            AudioClip next = NextDefault();
            if (next != null) SwitchTo(next, loop: false);
        }

        /// <summary>기본 BGM 대신 이 곡을 반복한다. null이면 ClearOverride와 같다.</summary>
        public static void Override(AudioClip clip)
        {
            if (!Ready()) return;
            if (clip == null) { ClearOverride(); return; }
            if (Instance._override == clip) return;
            Instance._override = clip;
            Instance.SwitchTo(clip, loop: true);
        }

        /// <summary>덮어쓴 곡을 풀고 기본 BGM으로 돌아온다.</summary>
        public static void ClearOverride()
        {
            if (!Ready() || Instance._override == null) return;
            Instance._override = null;
            AudioClip next = Instance.NextDefault();
            if (next != null) Instance.SwitchTo(next, loop: false);
            else Instance.SwitchTo(null, loop: false);
        }

        private static bool Ready()
        {
            if (Instance != null && Instance.enabled) return true;
            Debug.LogError("[BgmPlayer] 씬에 BgmPlayer가 없다. BGM을 바꿀 수 없다.");
            return false;
        }

        private AudioClip NextDefault()
        {
            if (defaultTracks == null || defaultTracks.Length == 0) return null;
            for (int k = 0; k < defaultTracks.Length; k++)
            {
                _track = (_track + 1) % defaultTracks.Length;
                if (defaultTracks[_track] != null) return defaultTracks[_track];
            }
            return null;
        }

        private void SwitchTo(AudioClip clip, bool loop)
        {
            if (_switching != null) StopCoroutine(_switching);
            _switching = StartCoroutine(Fade(clip, loop));
        }

        private IEnumerator Fade(AudioClip clip, bool loop)
        {
            if (_bgm.isPlaying && fadeSeconds > 0f)
            {
                float from = _bgm.volume;
                for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
                {
                    _bgm.volume = Mathf.Lerp(from, 0f, t / fadeSeconds);
                    yield return null;
                }
            }
            _bgm.Stop();
            _bgm.clip = clip;
            _bgm.loop = loop;
            if (clip != null)
            {
                _bgm.volume = fadeSeconds > 0f ? 0f : bgmVolume;
                _bgm.Play();
                for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
                {
                    _bgm.volume = Mathf.Lerp(0f, bgmVolume, t / fadeSeconds);
                    yield return null;
                }
                _bgm.volume = bgmVolume;
            }
            _switching = null;
        }
    }
}
