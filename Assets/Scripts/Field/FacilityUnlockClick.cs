using Marea.Core;
using Marea.Economy;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 부서진 시설의 클릭 창구. 걸어가서 해금 창을 연다. (+9/28, 이슈 71)
    ///
    /// FacilitySite의 Broken 오브젝트에 붙인다. 해금되면 Broken이 꺼지면서 이것도 같이
    /// 꺼지므로, 수리된 시설의 클릭(밭·선착장)과 겹치지 않는다.
    /// </summary>
    public class FacilityUnlockClick : InteractableBase
    {
        private FacilitySite _site;
        private FacilityUnlockUI _ui;

        private void Awake()
        {
            _site = GetComponentInParent<FacilitySite>();
            _ui = FindAnyObjectByType<FacilityUnlockUI>(FindObjectsInactive.Include);

            if (_site == null)
                Debug.LogError($"{name}: 부모에 FacilitySite가 없다. 무엇을 해금할지 모른다.", this);
            if (_ui == null)
                Debug.LogError($"{name}: 씬에 FacilityUnlockUI가 없다. 클릭해도 해금 창이 안 뜬다.", this);
        }

        public override bool CanInteract(IInteractor actor)
            => _site != null && _ui != null
            && FacilityLevels.Instance != null && !FacilityLevels.Instance.IsUnlocked(_site.Kind);

        public override string InteractLabel(IInteractor actor) => "해금";   // (+9/28, 이슈 75)

        public override void Interact(IInteractor actor)
        {
            if (CanInteract(actor)) _ui.Open(_site.Kind);
        }
    }
}
