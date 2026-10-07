using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 걸은 거리만큼 발소리를 낸다. 플레이어(기획 MOV-01 · MOV-04)와 서빙 직원(SRV-01)이 같이 쓴다. (+10/6, 이슈 117)
    ///
    /// 애니메이션 이벤트가 아니라 거리로 잰다 — 클릭 이동 · WASD · 직원 이동이 전부 AgentMover를 타서
    /// 실측 속도 하나로 다 잡힌다. 애니메이션 클립마다 이벤트를 심을 필요가 없다.
    /// 뛰면 보폭이 길어져서 걸음 수가 속도만큼 늘지는 않는다(runStride).
    /// </summary>
    [RequireComponent(typeof(AgentMover))]
    public class Footsteps : MonoBehaviour
    {
        [Tooltip("돌아가며 낼 발소리. 비우면 조용하다.")]
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.08f;
        [Tooltip("켜면 그 자리에서 나는 소리(3D) — 직원. 끄면 화면 소리(2D) — 플레이어.")]
        [SerializeField] private bool spatial;

        [Header("보폭")]
        [Tooltip("걸을 때 한 걸음 거리(m).")]
        [SerializeField, Min(0.1f)] private float walkStride = 0.7f;
        [Tooltip("뛸 때 한 걸음 거리(m).")]
        [SerializeField, Min(0.1f)] private float runStride = 1.1f;
        [Tooltip("이 속도(m/s) 이상을 뛴다고 본다.")]
        [SerializeField, Min(0.1f)] private float runSpeed = 4f;
        [Tooltip("이보다 느리면 서 있다고 본다(m/s).")]
        [SerializeField, Min(0f)] private float minSpeed = 0.3f;

        private AgentMover _mover;
        private float _travel;
        private int _last = -1;

        private void Awake() => _mover = GetComponent<AgentMover>();

        private void Update()
        {
            float speed = _mover.CurrentSpeed;
            if (speed < minSpeed)
            {
                _travel = walkStride * 0.5f;   // 다시 걸으면 반걸음 뒤 첫 소리 — 출발하자마자 툭 나지 않게
                return;
            }

            _travel += speed * Time.deltaTime;
            float stride = Mathf.Lerp(walkStride, runStride, Mathf.InverseLerp(minSpeed, runSpeed, speed));
            if (_travel < stride) return;
            _travel -= stride;

            AudioClip clip = Next();
            if (clip == null) return;
            if (spatial) SoundManager.PlayAt(clip, transform.position, volume, pitchJitter);
            else SoundManager.Play(clip, volume, pitchJitter);
        }

        // 같은 소리가 연달아 안 나게 — 직전 것을 빼고 고른다.
        private AudioClip Next()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            int i;
            if (_last < 0) i = Random.Range(0, clips.Length);
            else if ((i = Random.Range(0, clips.Length - 1)) >= _last) i++;
            _last = i;
            return clips[i];
        }
    }
}
