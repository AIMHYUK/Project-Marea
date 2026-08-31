using Marea.Core;
using UnityEngine;

namespace Marea.Cooking
{
    public class CookingStation : InteractableBase
    {
        [Header("UI 연결")]
        [Tooltip("상호작용 시 띄울 메뉴 선택 UI")]
        [SerializeField] private CookingMenuUI menuUI;

        [Header("호버 연출 (선택 사항)")]
        [SerializeField] private GameObject highlightEffect;

        public override void Interact(IInteractor actor)
        {
            if (menuUI == null)
            {
                Debug.LogWarning("[CookingStation] MenuUI가 연결되지 않았습니다.");
                return;
            }

            // 플레이어 조작 잠금
            actor.BeginBusy();

            // 메뉴 선택 팝업 열기
            menuUI.Open(actor);
        }

        public override void OnHoverEnter()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }

        public override void OnHoverExit()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }
    }
}