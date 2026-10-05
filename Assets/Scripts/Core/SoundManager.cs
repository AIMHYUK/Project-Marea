using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 효과음을 내는 공용 창구. 씬에 하나만 둔다. (+10/6, 이슈 117)
    ///
    /// 왜 — 지금까지는 소리가 필요한 스크립트마다 AudioSource를 따로 붙였다(UiClickSound · 손님 동전 · 미니게임들).
    /// 그러면 소리를 내려는 쪽마다 컴포넌트 · 설정을 챙겨야 하고, 전체 효과음 크기를 한 곳에서 못 바꾼다.
    /// 여기서는 AudioSource 몇 개를 미리 만들어 돌려 쓴다 — 부르는 쪽은 클립만 넘긴다.
    ///
    ///   SoundManager.Play(clip);                 // 화면 소리(2D) — UI · 해금 연출
    ///   SoundManager.PlayAt(clip, position);     // 그 자리에서 나는 소리(3D) — 월드 오브젝트
    ///
    /// BGM은 안 맡는다. 기존 AudioSource 쓰는 스크립트들은 그대로 두었다 — 옮길 때 이걸 쓰면 된다.
    /// 동시에 나는 소리가 sourceCount를 넘으면 가장 오래 울린 것을 끊고 새 소리를 낸다.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Tooltip("동시에 낼 수 있는 효과음 개수.")]
        [SerializeField, Range(1, 32)] private int sourceCount = 10;

        [Tooltip("모든 효과음에 곱하는 크기. 나중에 설정 창이 바꾼다.")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("3D 소리")]
        [Tooltip("PlayAt 소리가 이 거리(m)까지는 다 들린다.")]
        [SerializeField, Min(0.1f)] private float minDistance = 3f;
        [Tooltip("PlayAt 소리가 이 거리(m)에서 거의 안 들린다.")]
        [SerializeField, Min(0.1f)] private float maxDistance = 40f;

        private AudioSource[] _sources;
        private float[] _startedAt;
        private int _next;

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[SoundManager] 씬에 둘 이상 있다 — '{name}'은 끈다. 하나만 남길 것.", this);
                enabled = false;
                return;
            }
            Instance = this;

            _sources = new AudioSource[sourceCount];
            _startedAt = new float[sourceCount];
            for (int i = 0; i < sourceCount; i++)
            {
                var go = new GameObject($"Sfx{i}");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = minDistance;
                s.maxDistance = maxDistance;
                _sources[i] = s;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>화면 소리(2D). 위치와 상관없이 같은 크기로 들린다.</summary>
        /// <param name="pitchJitter">음높이를 ±이만큼 무작위로 흔든다. 같은 소리가 연달아 날 때 덜 기계적이다.</param>
        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            if (Ready(clip)) Instance.Emit(clip, volume, pitchJitter, null);
        }

        /// <summary>그 자리에서 나는 소리(3D). 카메라(AudioListener)에서 멀수록 작다.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0f)
        {
            if (Ready(clip)) Instance.Emit(clip, volume, pitchJitter, position);
        }

        // 클립이 비면 조용히 넘긴다(소리는 선택 사항이라 부르는 쪽 필드가 빌 수 있다).
        // 관리자가 없으면 설정 누락이라 알린다.
        private static bool Ready(AudioClip clip)
        {
            if (clip == null) return false;
            if (Instance != null && Instance.enabled) return true;
            Debug.LogError($"[SoundManager] 씬에 SoundManager가 없다 — '{clip.name}'을 못 낸다. 씬에 하나 둘 것.");
            return false;
        }

        private void Emit(AudioClip clip, float volume, float pitchJitter, Vector3? position)
        {
            AudioSource s = Pick();
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume) * sfxVolume;
            s.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
            s.spatialBlend = position.HasValue ? 1f : 0f;
            s.transform.position = position ?? transform.position;
            s.Play();
            _startedAt[System.Array.IndexOf(_sources, s)] = Time.unscaledTime;
        }

        /// <summary>쉬고 있는 소스, 없으면 가장 오래 울린 소스.</summary>
        private AudioSource Pick()
        {
            for (int k = 0; k < _sources.Length; k++)
            {
                int i = (_next + k) % _sources.Length;
                if (_sources[i].isPlaying) continue;
                _next = (i + 1) % _sources.Length;
                return _sources[i];
            }
            int oldest = 0;
            for (int i = 1; i < _sources.Length; i++)
                if (_startedAt[i] < _startedAt[oldest]) oldest = i;
            return _sources[oldest];
        }
    }
}
