using Marea.Core;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 식당 간판. 넓은 맵에서 클릭하면 그 앞까지 걸어가서 영업을 시작한다. (+9/10)
    ///
    /// 영업 개시 경로가 이미 둘 있는데(존 트리거로 뜨는 UI 버튼) 클릭 경로가 없었다.
    /// 맵이 넓어지면 "식당까지 걸어가서 연다"가 자연스러운데, 존에 발을 들여야만
    /// 버튼이 뜨는 구조라 멀리서는 영업을 열 수단이 아예 없다.
    ///
    /// <see cref="BusinessManager"/>는 B 소유다. 여기서는 <c>StartBusiness()</c>만
    /// 부른다 — 영업 상태를 여기서 들고 있지 않는다. 상태는 하나여야 한다.
    ///
    /// 기존 UI 버튼을 지우지 않았다. 둘 다 같은 <c>StartBusiness()</c>로 가고
    /// 그쪽이 Ready가 아니면 그냥 빠지므로, 두 번 열려도 두 번 시작되지 않는다.
    /// </summary>
    public class RestaurantSign : InteractableBase
    {
        [Header("겉모습")]
        [Tooltip("영업 중·정산 중처럼 지금 열 수 없을 때 끄는 것. 비워둬도 동작한다.")]
        [SerializeField] private Renderer readyVisual;

        private void Start()
        {
            // Awake에서 보면 안 된다 — BusinessManager도 Awake에서 Instance를 잡아서
            // 둘 중 누가 먼저인지가 정해져 있지 않다.
            //
            // 조용히 넘어가면 증상이 "간판을 클릭했는데 아무 일도 안 일어난다"인데,
            // CanInteract가 false를 주면 커서 반응도 없어서 클릭이 먹었는지조차 모른다.
            if (BusinessManager.Instance == null)
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 간판을 눌러도 영업이 시작되지 않는다.", this);
        }

        private void Update()
        {
            ShowReady(IsReady);
        }

        /// <summary>지금 영업을 열 수 있는가. 준비 단계에서만 연다.</summary>
        private bool IsReady =>
            BusinessManager.Instance != null &&
            BusinessManager.Instance.CurrentState == BusinessState.Ready;

        // 영업 중에 또 클릭해서 걸어가 봐야 할 게 없다. 여기서 막으면
        // ClickSelector가 바닥 이동으로도 흘려보내지 않는다(설계상 클릭 하나에 뜻 하나).
        public override bool CanInteract(IInteractor actor) => IsReady;

        public override void Interact(IInteractor actor)
        {
            BusinessManager manager = BusinessManager.Instance;
            if (manager == null)
            {
                Debug.LogError($"{name}: 씬에 BusinessManager가 없다. 영업을 시작할 수 없다.", this);
                return;
            }

            // 여러 프레임 이어지는 상호작용이 아니라 BeginBusy를 하지 않는다.
            // 감싸면 EndBusy를 부를 자리가 없어서 플레이어가 그대로 잠긴다.
            manager.StartBusiness();
        }

        private void ShowReady(bool on)
        {
            if (readyVisual != null && readyVisual.enabled != on) readyVisual.enabled = on;
        }
    }
}
