using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 탐사정 하나의 파견 · 타이머 · 귀환 · 보상. (+9/28, 이슈 73)
    ///
    /// 선착장(ExpeditionDock)은 클릭을 받아 여기로 넘기기만 한다.
    /// 기획 8페이지의 첫 구현 그대로다 — 귀환한 탐사정을 클릭하면 보상이 자동으로 들어오고,
    /// 연출은 배가 월드에서 사라졌다가 다시 나타나는 것. 결과창은 없다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 대기 상태다 (보류).
    /// </summary>
    public class ExpeditionManager : MonoBehaviour
    {
        public enum State { Idle, Away, Returned }

        [Tooltip("갈 수 있는 지역. 지역 선택 창에 이 순서대로 뜬다.")]
        [SerializeField] private ExpeditionRegionData[] regions;

        [Tooltip("탐사 중에 사라질 배. 선착장 발판은 여기 넣지 않는다.")]
        [SerializeField] private GameObject boat;

        [Tooltip("탐사 중 남은 시간을 띄울 월드 텍스트. 비워둬도 동작한다.")]
        [SerializeField] private TMP_Text timerLabel;

        [Tooltip("귀환했을 때 켜는 표식(느낌표).")]
        [SerializeField] private GameObject returnedMarker;

        private ExpeditionUI _ui;
        private ExpeditionRegionData _current;
        private float _returnAt;
        private Camera _cam;

        public State Current { get; private set; } = State.Idle;
        public ExpeditionRegionData[] Regions => regions;

        private void Awake()
        {
            _ui = FindAnyObjectByType<ExpeditionUI>(FindObjectsInactive.Include);

            if (regions == null || regions.Length == 0)
                Debug.LogError($"{name}: ExpeditionManager.regions가 비어 있다. 보낼 곳이 없다.", this);
            if (boat == null)
                Debug.LogError($"{name}: ExpeditionManager.boat가 비어 있다. 파견해도 배가 안 사라진다.", this);
            if (_ui == null)
                Debug.LogError($"{name}: 씬에 ExpeditionUI가 없다. 탐사정을 눌러도 지역 창이 안 뜬다.", this);

            Show();
        }

        private void Update()
        {
            if (Current != State.Away) return;

            float remaining = _returnAt - Time.time;
            if (remaining > 0f)
            {
                if (timerLabel != null)
                {
                    int sec = Mathf.CeilToInt(remaining);
                    timerLabel.text = $"{sec / 60}:{sec % 60:00}";
                }
                return;
            }

            Current = State.Returned;
            Show();
        }

        // 월드 텍스트가 옆에서 보면 납작해진다. 카메라와 같은 방향을 보게 한다.
        private void LateUpdate()
        {
            if (timerLabel == null || !timerLabel.gameObject.activeInHierarchy) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) timerLabel.transform.rotation = _cam.transform.rotation;
        }

        /// <summary>탐사 중에는 클릭을 안 받는다 (기획 8 파견 3번).</summary>
        public bool CanInteract => Current != State.Away;

        /// <summary>플레이어가 선착장에 도착했다. 대기면 지역 창, 귀환이면 보상.</summary>
        public void Interact()
        {
            switch (Current)
            {
                case State.Idle:
                    if (_ui != null) _ui.Open(this);
                    break;
                case State.Returned:
                    Collect();
                    break;
            }
        }

        /// <summary>이 지역으로 보낸다. 대기가 아니면 false.</summary>
        public bool TryDispatch(ExpeditionRegionData region)
        {
            if (Current != State.Idle || region == null) return false;

            _current = region;
            _returnAt = Time.time + region.DurationSeconds;
            Current = State.Away;
            Show();
            return true;
        }

        private void Collect()
        {
            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                // 받기 전에 멈춘다. 대기로 돌려버리면 보상이 그냥 사라진다.
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 보상을 받을 곳이 없어 귀환 상태로 둔다.", this);
                return;
            }

            foreach (RecipeEntry reward in _current.Rewards)
                if (reward.ingredient != null) warehouse.Add(reward.ingredient, reward.requiredAmount);

            _current = null;
            Current = State.Idle;
            Show();
        }

        private void Show()
        {
            if (boat != null) boat.SetActive(Current != State.Away);
            if (timerLabel != null) timerLabel.gameObject.SetActive(Current == State.Away);
            if (returnedMarker != null) returnedMarker.SetActive(Current == State.Returned);
        }
    }
}
