// ⚠️ 임시 파일이다. 씬에서 상호작용 뼈대가 도는지 확인하려고 만든 비계다.
//    A의 첫 실제 구현체인 FarmPlot이 생기면 이 파일과 씬의 오브젝트를 같이 지운다.
//    B 몫인 CookingStation·Table을 대신 만들지 않기 위한 것이기도 하다.
using Marea.Core;
using UnityEngine;

namespace Marea.Player
{
    /// <summary>
    /// 클릭 → 이동 → 도착 → Interact 흐름을 로그로만 확인하는 더미.
    /// InteractableBase가 abstract라 그대로는 씬에 못 붙어서 이게 필요하다.
    /// </summary>
    public class TestInteractable : InteractableBase
    {
        [Tooltip("끄면 CanInteract가 false가 된다. hover가 같이 죽는지 확인용.")]
        [SerializeField] private bool interactable = true;

        public override bool CanInteract(IInteractor actor) => interactable;

        public override void Interact(IInteractor actor)
            => Debug.Log($"[Test] Interact — {name}", this);

        public override void OnHoverEnter() => Debug.Log($"[Test] hover 들어옴 — {name}", this);
        public override void OnHoverExit()  => Debug.Log($"[Test] hover 나감 — {name}", this);
    }
}
