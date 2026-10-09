using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 바닥을 찍으면 그 자리에서 번쩍하는 이동 표시. (+10/7) "갑니다" 핑처럼.
    ///
    /// 마커 프리팹(Unique Markers Pointers의 vfx_ClickMovement)은 원이 퍼지고 화살표가 모이는 0.25초짜리 한 번 재생이다.
    /// 하나만 만들어 두고 찍을 때마다 그 자리로 옮겨 처음부터 다시 튼다. ClickSelector가 Show/Hide를 부른다.
    /// </summary>
    public class ClickMarker : MonoBehaviour
    {
        [SerializeField] private GameObject markerPrefab;
        [Tooltip("바닥에서 띄우는 높이 — 바닥에 묻혀 깜빡이지 않게.")]
        [SerializeField] private float heightOffset = 0.03f;
        [Tooltip("재생이 끝나고 끄기까지. 마커 재생 길이보다 길면 된다.")]
        [SerializeField, Min(0.1f)] private float hideAfter = 1f;
        [Tooltip("프리팹 크기에 곱한다. vfx_ClickMovement는 원 지름이 15m라 0.1이면 1.5m.")]
        [SerializeField, Min(0.01f)] private float scale = 0.1f;

        private GameObject _marker;
        private ParticleSystem[] _systems;
        private float _hideAt;

        private void Awake()
        {
            if (markerPrefab == null)
            {
                Debug.LogError($"{name}: ClickMarker.markerPrefab이 비어 있다. 바닥을 찍어도 표시가 안 뜬다.", this);
                enabled = false;
                return;
            }
            _marker = Instantiate(markerPrefab);
            _marker.name = "ClickMarker";
            _marker.transform.localScale = markerPrefab.transform.localScale * scale;
            _systems = _marker.GetComponentsInChildren<ParticleSystem>(true);
            _marker.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker);
        }

        /// <summary>world 자리에서 표시를 한 번 튼다.</summary>
        public void Show(Vector3 world)
        {
            if (_marker == null) return;
            _hideAt = Time.time + hideAfter;
            _marker.transform.position = world + Vector3.up * heightOffset;
            _marker.SetActive(true);
            foreach (ParticleSystem ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        public void Hide()
        {
            if (_marker != null && _marker.activeSelf) _marker.SetActive(false);
        }

        private void Update()
        {
            if (_marker != null && _marker.activeSelf && Time.time >= _hideAt) Hide();
        }
    }
}
