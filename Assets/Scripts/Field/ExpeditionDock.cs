using Marea.Core;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 수리된 탐사 선착장의 클릭 창구. ExpeditionManager에 넘기기만 한다. (+9/28, 이슈 73)
    ///
    /// InteractableBase를 상속하는 이유는 클릭 때문뿐이다 (FarmPlotCell과 같다).
    /// 배가 아니라 선착장에 붙이는 이유: 탐사 중엔 배가 꺼져서 콜라이더도 같이 사라진다.
    /// </summary>
    public class ExpeditionDock : InteractableBase
    {
        private ExpeditionManager _manager;

        private void Awake()
        {
            _manager = GetComponentInParent<ExpeditionManager>();
            if (_manager == null)
                Debug.LogError($"{name}: 부모에 ExpeditionManager가 없다. 클릭해도 아무 일도 안 일어난다.", this);
        }

        public override bool CanInteract(IInteractor actor) => _manager != null && _manager.CanInteract;

        // (+9/28, 이슈 75) (+10/2) 탐사 중에도 남은 시간 창을 열 수 있어 라벨이 뜬다.
        public override string InteractLabel(IInteractor actor)
        {
            if (_manager == null) return "파견";
            return _manager.Current switch
            {
                ExpeditionManager.State.Returned => "보상 받기",
                ExpeditionManager.State.Away => "탐사 현황",
                _ => "파견",
            };
        }

        public override void Interact(IInteractor actor)
        {
            if (_manager != null) _manager.Interact();
        }
    }
}
