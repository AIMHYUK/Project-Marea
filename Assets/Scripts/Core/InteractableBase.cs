using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 상호작용 오브젝트의 공통 구현. B는 이걸 상속해서 Interact만 채우면 된다.
    ///
    ///   public class CookingStation : InteractableBase {
    ///       public override void Interact(IInteractor actor) { ... }
    ///   }
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Tooltip("캐릭터가 서는 위치. 비워두면 이 오브젝트 자리로 온다.")]
        [SerializeField] private Transform interactPoint;

        public virtual Vector3 InteractPoint =>
            interactPoint != null ? interactPoint.position : transform.position;

        public virtual bool CanInteract(IInteractor actor) => true;

        /// <summary>
        /// 가까이 갔을 때 뜨는 "[E] ○○"의 ○○. 기본은 "상호작용"이다. (+9/28, 이슈 75)
        /// 상태에 따라 문구가 바뀔 수 있게 속성이 아니라 메서드다 — 빈 밭은 "심기", 다 자란 밭은 "수확".
        /// </summary>
        public virtual string InteractLabel(IInteractor actor) => "상호작용";

        public abstract void Interact(IInteractor actor);

        /// <summary>마우스가 올라왔을 때. 아웃라인 같은 걸 켜고 싶으면 override.</summary>
        public virtual void OnHoverEnter() { }

        public virtual void OnHoverExit() { }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(InteractPoint, 0.25f);
        }
#endif
    }
}
