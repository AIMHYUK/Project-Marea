using Marea.Core;
using TMPro;
using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 가까운 상호작용 대상 위에 "[E] ○○"를 띄우고, E를 누르면 걷지 않고 바로 상호작용한다. (+9/28, 이슈 75)
    ///
    /// 클릭 경로(ClickSelector → PlayerController.GoInteract)와 나란히 있는 두 번째 경로다.
    /// 클릭은 걸어가서, E는 이미 가까이 있을 때 그 자리에서.
    ///
    /// PF_AUI에 들어 있다 — 플레이어 프리팹이 아니라. 플레이어에 붙이면 PF_Player를 쓰는
    /// 모든 씬(다른 사람 씬 포함)에서 E가 갑자기 켜진다. B의 CustomerInteractable ·
    /// CookingCounter도 E를 직접 읽어서, 준비 안 된 씬에서 둘이 같이 반응할 수 있다.
    /// </summary>
    public class ProximityInteractor : MonoBehaviour
    {
        [Header("범위")]
        [Tooltip("플레이어에서 대상 콜라이더까지 이 거리 안이면 가깝다고 본다.")]
        [SerializeField, Min(0.1f)] private float range = 2.5f;

        [Tooltip("대상 콜라이더가 있는 레이어.")]
        [SerializeField] private LayerMask mask = ~0;

        [Header("표시")]
        [Tooltip("켜고 끄며 대상 머리 위로 옮기는 라벨 루트. 오버레이 Canvas 안에 있어야 한다.")]
        [SerializeField] private RectTransform prompt;

        [SerializeField] private TMP_Text label;

        [Tooltip("대상 콜라이더 윗면에서 이만큼 위에 띄운다(월드 단위).")]
        [SerializeField] private float heightOffset = 0.4f;

        [Header("소리 (+10/6, 이슈 117) — 기획 MOV-02 ui_interact_hover")]
        [Tooltip("상호작용할 대상이 새로 잡힐 때 아주 작게. 비우면 조용하다. 실행음은 PlayerController.")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.3f;

        private readonly Collider[] _hits = new Collider[32];
        private PlayerController _player;
        private PlayerInputReader _input;
        private Camera _cam;
        private InteractableBase _target;
        private bool _suppressHover;   // (+10/6) E 직후 대상을 비운 프레임 — 다시 잡혀도 다가감 소리를 안 낸다
        private Bounds _targetBounds;

        private void Awake()
        {
            if (prompt == null || label == null)
                Debug.LogError($"{name}: ProximityInteractor의 prompt·label이 비어 있다. [E] 표시가 안 뜬다.", this);
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        // 플레이어는 씬 오브젝트라 프리팹이 못 든다. 한 번 찾는다 (UpgradeUI.input과 같은 방식).
        private void Start()
        {
            _player = FindAnyObjectByType<PlayerController>();
            _input = _player != null ? _player.GetComponent<PlayerInputReader>() : null;
            _cam = Camera.main;

            if (_player == null)
                Debug.LogError($"{name}: 씬에 PlayerController가 없다. E 상호작용이 동작하지 않는다.", this);
            else if (_input == null)
                Debug.LogError($"{name}: 플레이어에 PlayerInputReader가 없다. E를 읽을 수 없다.", this);
        }

        private void Update()
        {
            if (_player == null || _input == null) return;

            // 미니게임 중이거나 클릭으로 걸어가는 중이면 받지 않는다. 걸어가는 중에 E로 다른 걸
            // 열면 도착했을 때 원래 대상의 Interact가 또 불린다.
            InteractableBase was = _target;
            _target = _player.IsBusy ? null : FindNearest(out _targetBounds);
            if (_target != null && was == null && !_suppressHover) SoundManager.Play(hoverClip, hoverVolume);   // (+10/6) 없다가 생길 때만
            _suppressHover = false;

            if (_target != null && _input.InteractPressed)
            {
                _player.PlayInteractSound();   // (+10/6)
                _target.Interact(_player);
                // 상호작용 뒤 상태가 바뀌었을 수 있다(심었으면 성장 중). 다음 프레임에 다시 고른다.
                // (+10/6) 여기서 비우면 다음 프레임에 "없다가 생김"이 돼서 다가감 소리가 또 난다 — 한 프레임 막는다.
                _target = null;
                _suppressHover = true;
            }
        }

        // 카메라가 따라 움직인 뒤에 위치를 잡아야 라벨이 한 프레임 늦게 끌려오지 않는다.
        private void LateUpdate()
        {
            if (prompt == null) return;

            if (_target == null || _cam == null)
            {
                prompt.gameObject.SetActive(false);
                return;
            }

            Vector3 world = new Vector3(_targetBounds.center.x, _targetBounds.max.y + heightOffset, _targetBounds.center.z);
            Vector3 screen = _cam.WorldToScreenPoint(world);
            if (screen.z < 0f)   // 카메라 뒤
            {
                prompt.gameObject.SetActive(false);
                return;
            }

            if (label != null) label.text = $"[E] {_target.InteractLabel(_player)}";
            prompt.position = screen;   // 오버레이 Canvas라 스크린 좌표가 곧 위치다
            prompt.gameObject.SetActive(true);
        }

        private InteractableBase FindNearest(out Bounds bounds)
        {
            bounds = default;
            Vector3 origin = _player.transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, range, _hits, mask, QueryTriggerInteraction.Collide);

            InteractableBase best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hits[i];
                // ClickSelector와 같은 규칙 — 콜라이더에서 위로 올라가며 찾는다.
                var candidate = col.GetComponentInParent<InteractableBase>();
                if (candidate == null || !candidate.CanInteract(_player)) continue;

                float dist = Vector3.Distance(origin, ClosestPoint(col, origin));
                if (dist >= bestDist) continue;

                best = candidate;
                bestDist = dist;
                bounds = col.bounds;
            }
            return best;
        }

        // Collider.ClosestPoint는 볼록하지 않은 MeshCollider에서 에러를 낸다. 그땐 바운드로 대신한다.
        private static Vector3 ClosestPoint(Collider col, Vector3 point)
        {
            if (col is MeshCollider mesh && !mesh.convex) return col.bounds.ClosestPoint(point);
            return col.ClosestPoint(point);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_player == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_player.transform.position, range);
        }
#endif
    }
}
