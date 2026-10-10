using Marea.Core;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// (+10/10) 다 자란 밭 위 수확 마커(아이콘 말풍선). 누르면 걸어가지 않고 그 자리에서 수확한다.
    /// FarmManager.harvestByMarkerClick이 꺼져 있으면 아무것도 안 받는다 — 그땐 예전처럼 밭을 눌러 걸어가 수확.
    ///
    /// FarmPlotCell이 Awake에서 자기 readyMarker에 붙인다(트리거 구체 콜라이더와 같이). E 근접 경로는 밭 칸이 이미 받으니 여기선 끈다.
    /// </summary>
    public class FarmReadyMarker : InteractableBase
    {
        private FarmPlotCell _cell;
        private FarmManager _manager;

        public void Init(FarmPlotCell cell, FarmManager manager)
        {
            _cell = cell;
            _manager = manager;
        }

        public override bool AllowKey => false;
        public override bool ClickFromAfar => true;

        public override bool CanInteract(IInteractor actor)
            => _manager != null && _manager.HarvestByMarkerClick && _manager.StateOf(_cell) == FarmManager.PlotState.Ready;

        public override string InteractLabel(IInteractor actor) => "수확";

        public override void Interact(IInteractor actor)
        {
            if (CanInteract(actor)) _manager.Interact(_cell);
        }
    }
}
