using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 클릭해서 다가가 상호작용할 수 있는 것. 밭·요리대·테이블이 전부 이걸 구현한다.
    ///
    /// ⚠️ 이 인터페이스를 직접 구현하지 말고 <b>반드시 InteractableBase를 상속한다.</b>
    /// ClickSelector가 GetComponentInParent&lt;InteractableBase&gt;() 로 대상을 찾기 때문에,
    /// IInteractable만 직접 구현한 오브젝트는 <b>클릭이 아예 안 잡힌다.</b>
    /// 에러도 경고도 안 나고 그냥 반응이 없어서 원인을 찾기 어렵다.
    ///
    /// hover(OnHoverEnter/Exit)가 InteractableBase에만 있어서 생긴 제약이다.
    /// 1차에서는 밭·요리대·테이블이 전부 base를 상속하므로 이대로 둔다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>캐릭터가 실제로 가서 설 위치. 오브젝트 중심이 아니라 그 앞이다.</summary>
        Vector3 InteractPoint { get; }

        /// <summary>지금 상호작용할 수 있는가. false면 클릭해도 안 간다(예: 아직 안 자란 밭).</summary>
        bool CanInteract(IInteractor actor);

        /// <summary>도착한 뒤에 호출된다. 이 시점에 캐릭터는 이미 InteractPoint에 서 있다.</summary>
        void Interact(IInteractor actor);
    }
}
