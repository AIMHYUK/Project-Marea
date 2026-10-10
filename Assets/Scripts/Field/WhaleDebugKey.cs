using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Field
{
    /// <summary>
    /// 개발용 — 플레이 중 F9를 누르면 고래가 바로 나타나고, 헤엄치는 동안 카메라가 옆에서 따라가며 보여준다. (+10/5)
    /// WhaleSwimmer와 같은 오브젝트에 붙인다. 고래가 잠수를 마치거나 F9를 다시 누르면 원래 카메라로 돌아간다.
    ///
    /// 평소 등장은 20초 뒤 · 60~150초 간격 · 경로가 화면에 들어올 때만이라 움직임을 확인하려면 오래 기다려야 했다.
    /// 카메라는 그동안 CameraFollow를 꺼서 뺏는다 — 끝나면 다시 켜고 CameraFollow가 플레이어 쪽으로 부드럽게 돌아간다.
    /// 릴리스 빌드(Debug.isDebugBuild = false)에선 꺼진다.
    /// (+10/10) 헤엄치는 중 F10 = 높은 점프, F11 = 낮은 점프(헤엄 클립이 한 바퀴 도는 순간에 뛴다).
    /// </summary>
    [DefaultExecutionOrder(1000)] // 다른 카메라 스크립트 뒤에 카메라를 놓는다
    public class WhaleDebugKey : MonoBehaviour
    {
        [SerializeField] private Key key = Key.F9;
        [Tooltip("(+10/10) 헤엄치는 중 누르면 높은 점프 · 낮은 점프.")]
        [SerializeField] private Key jumpKey = Key.F10;
        [SerializeField] private Key lowJumpKey = Key.F11;

        [Header("따라가는 카메라")]
        [Tooltip("고래 옆으로 떨어진 거리(m). 고래 길이가 약 12m.")]
        [SerializeField, Min(1f)] private float sideDistance = 28f;
        [Tooltip("해수면 기준 카메라 높이(m).")]
        [SerializeField] private float height = 10f;
        [Tooltip("고래 기준점보다 이만큼 위를 본다 — 기준점은 물속이라 등 · 나무 쪽을 보게. 해수면보다 아래는 안 본다.")]
        [SerializeField] private float lookUp = 3f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.4f;

        private WhaleSwimmer _whale;
        private Camera _cam;
        private Behaviour _follow;     // 잠시 끈 카메라 스크립트
        private bool _watching;
        private bool _started;         // 고래가 실제로 헤엄치기 시작했나 — 시작 전 프레임에 바로 끝나지 않게
        private Vector3 _velocity;

        private void Awake()
        {
            _whale = GetComponent<WhaleSwimmer>();
            if (_whale == null)
            {
                Debug.LogError($"{name}: WhaleDebugKey는 WhaleSwimmer와 같은 오브젝트에 붙여야 한다.", this);
                enabled = false;
                return;
            }
            if (!Debug.isDebugBuild) enabled = false;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb[jumpKey].wasPressedThisFrame) _whale.JumpNow(false);     // (+10/10)
            if (kb[lowJumpKey].wasPressedThisFrame) _whale.JumpNow(true);
            if (!kb[key].wasPressedThisFrame) return;

            if (_watching) { StopWatching(); return; }
            _whale.AppearNearest();
            StartWatching();
        }

        private void StartWatching()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogWarning($"{name}: Camera.main이 없어 고래만 불렀다. 카메라는 안 옮긴다.", this);
                return;
            }
            _follow = _cam.GetComponent<Marea.Player.CameraFollow>();
            if (_follow != null) _follow.enabled = false;
            _watching = true;
            _started = false;
            _velocity = Vector3.zero;
        }

        private void StopWatching()
        {
            if (_follow != null) _follow.enabled = true;
            _follow = null;
            _watching = false;
        }

        private void LateUpdate()
        {
            if (!_watching) return;
            if (_cam == null) { StopWatching(); return; }
            if (_whale.IsSwimming) _started = true;
            else if (_started) { StopWatching(); return; }

            // 고래 진행 방향의 오른쪽 옆, 위에서 비스듬히 내려다본다.
            Transform w = _whale.transform;
            Vector3 forward = Vector3.ProjectOnPlane(w.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            // 떠오르기 전엔 14m 물속이라 높이는 해수면 기준으로 — 카메라가 물에 잠기지 않게.
            // (+10/10) 점프하면 몸이 오브젝트보다 한참 위로 간다 — 몸 중심을 본다.
            Vector3 look = _whale.VisualCenter;
            look.y = Mathf.Max(look.y + lookUp, _whale.SeaLevel);
            Vector3 goal = new Vector3(w.position.x, _whale.SeaLevel + height, w.position.z) + side * sideDistance;

            _cam.transform.position = Vector3.SmoothDamp(_cam.transform.position, goal, ref _velocity, smoothTime);
            _cam.transform.rotation = Quaternion.LookRotation(look - _cam.transform.position, Vector3.up);
        }

        private void OnDisable()
        {
            if (_watching) StopWatching();
        }
    }
}
