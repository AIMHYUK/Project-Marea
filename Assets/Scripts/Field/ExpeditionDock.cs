using Marea.Core;
using Marea.Player;
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
        [Tooltip("(+10/9) 귀환 보상은 InteractPoint(배 앞)에서 이 거리(m) 안에 있어야 받는다. 멀면 배 앞으로 걸어간다.")]
        [SerializeField, Min(0.5f)] private float collectReach = 2.5f;

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
            if (_manager == null) return;

            // (+10/9, 이슈 128 「자원 수령」) 귀환한 배의 보상은 배 앞(이 선착장의 InteractPoint)까지 와야 받는다.
            // E는 선착장 근처 판정 범위 어디서든 눌리고 HUD 버튼은 어디서든 눌리니, 멀면 창 대신 배 앞으로 걸어가게 한다.
            if (_manager.Current == ExpeditionManager.State.Returned && actor != null && !IsAtBoat(actor.Transform.position))
            {
                if (actor is PlayerController player) player.GoInteract(this);
                return;
            }
            _manager.Interact();
        }

        /// <summary>(+10/9) 배 앞에 서 있는가 — InteractPoint에서 수평으로 collectReach 안.</summary>
        public bool IsAtBoat(Vector3 position)
        {
            Vector3 d = position - InteractPoint;
            d.y = 0f;
            return d.sqrMagnitude <= collectReach * collectReach;
        }
    }
}
