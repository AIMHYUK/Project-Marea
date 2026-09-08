using Marea.Core;
using Marea.Data;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 보급품 상자. 클릭하면 정해진 식재료가 창고로 들어간다. (+9/9)
    ///
    /// 기획 「자원 관리 — 임시 식재료 수급」이 근거다. 농사·낚시가 들어오기 전까지
    /// 기본 수급처로 쓰고, 그 뒤에 대체하거나 제거한다. 그래서 겉모습도 기본 큐브다.
    ///
    /// 재료를 플레이어가 들고 오지 않는다 — 상자에서 창고로 바로 간다.
    /// 그래서 actor를 안 본다. 누가 열었는지가 결과에 영향을 주지 않는다.
    /// </summary>
    public class SupplyCrate : InteractableBase
    {
        [Header("내용물")]
        [Tooltip("한 번 열 때 창고로 들어갈 재료와 수량.")]
        [SerializeField] private RecipeEntry[] contents;

        [Header("재충전")]
        [Tooltip("연 뒤 이만큼 지나면 다시 열린다. 0이면 바로 다시 열린다. "
               + "1회성이 아닌 이유는 목적이 '재료 경제 검증'이라서다 — "
               + "한 번씩만 열리면 재료가 금방 말라 요리·영업을 돌려볼 수가 없다.")]
        [SerializeField, Min(0f)] private float refillSeconds = 20f;

        [Header("겉모습")]
        [Tooltip("비어 있는 동안 끄는 것. 비워둬도 동작한다.")]
        [SerializeField] private Renderer fullVisual;

        // 인스턴스별 상태다. SO에 넣으면 상자 전부가 같은 타이머를 공유한다.
        private float _refilledAt;
        private bool _emptied;

        /// <summary>지금 열 수 있는가.</summary>
        public bool IsReady => !_emptied || Time.time >= _refilledAt;

        private void Awake()
        {
            if (contents == null || contents.Length == 0)
                Debug.LogError($"{name}: SupplyCrate.contents가 비어 있다. "
                             + "열어도 아무것도 안 들어온다. 인스펙터에 재료를 넣을 것.", this);

            ShowFull(true);
        }

        private void Update()
        {
            // 비어 있다가 시간이 찼으면 다시 채운다. 켜지는 순간을 잡아야
            // 겉모습을 한 번만 바꾼다.
            if (!_emptied || Time.time < _refilledAt) return;

            _emptied = false;
            ShowFull(true);
        }

        public override bool CanInteract(IInteractor actor) => IsReady;

        public override void Interact(IInteractor actor)
        {
            if (!IsReady) return;

            // 창고는 행위자가 아니라 씬에 있다 (+9/9). 없으면 조용히 사라지는 대신 에러를 낸다 —
            // 증상이 "상자를 열었는데 아무 일도 안 일어난다"라서 원인이 안 보인다.
            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 수급한 재료가 갈 곳이 없다.", this);
                return;
            }

            foreach (var entry in contents)
                warehouse.Add(entry.ingredient, entry.count);

            _emptied = true;
            _refilledAt = Time.time + refillSeconds;
            ShowFull(false);
        }

        private void ShowFull(bool on)
        {
            if (fullVisual != null) fullVisual.enabled = on;
        }
    }
}
