using System;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 가게 지갑. 누적 보유 골드 하나만 안다. 매출 내역도 지출 내역도 여기 없다.
    ///
    /// 계약 5다 (2차 분담). B는 정산이 확정될 때 <see cref="Add"/> 한 번만 부른다 —
    /// 음식 하나 팔 때마다가 아니다. 중간에 영업이 끊기면 어디까지 들어갔는지가 애매해진다.
    /// <see cref="TrySpend"/>는 A만 쓴다 (업그레이드).
    ///
    /// 씬에 하나뿐인 물건이라 Instance로 노출한다. Warehouse·WorldClock과 같은 방식이다.
    ///
    /// 금액이 int인 이유: uint면 잔액보다 큰 금액을 빼는 순간 음수가 아니라 42억으로
    /// 감기고, 컴파일도 예외도 안 걸린다. 「골드 부족」이 이 시스템에서 제일 자주 밟는
    /// 경로라 하필 거기서 조용히 터진다. 음수 방지는 타입이 아니라 TrySpend가 한다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 startingGold로 돌아간다 (이슈 23 범위 밖).
    /// </summary>
    public class Wallet : MonoBehaviour
    {
        public static Wallet Instance { get; private set; }

        [Tooltip("시작할 때 들고 있을 골드. 테스트용.")]
        [SerializeField, Min(0)] private int startingGold;

        /// <summary>지금 가진 골드. 음수가 되지 않는다.</summary>
        public int Gold { get; private set; }

        /// <summary>바뀐 뒤 잔액. UI가 구독한다.</summary>
        public event Action<int> OnGoldChanged;

        private void Awake()
        {
            // 조용히 덮어쓰면 어느 쪽이 살아남았는지가 안 보인다. 씬에 둘을 놓는 건
            // 대개 실수이고, 그 실수의 증상은 "골드를 벌었는데 UI가 안 바뀐다"이다.
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{name}: 씬에 Wallet이 둘 이상이다. '{Instance.name}'이 먼저 잡혔고 "
                             + "이 오브젝트는 무시된다. 하나만 남길 것.", this);
                return;
            }

            Instance = this;
            Gold = startingGold;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 아래 둘이 계약 5다. Add의 호출처가 지금 0개인 건 B가 아직 정산에
        //    붙이지 않았기 때문이지 안 쓰는 게 아니다. 시그니처를 바꿀 땐 B에게 먼저 말한다.

        /// <summary>
        /// 번 돈을 넣는다. B가 정산 확정 시점에 부른다.
        /// 매출이 0인 날은 정상이라 0은 그냥 통과한다. 음수는 부르는 쪽의 버그다.
        /// </summary>
        public void Add(int amount)
        {
            if (amount < 0)
            {
                // 차감하려고 Add에 음수를 넣은 경우다. 조용히 빼주면 TrySpend의
                // 잔액 검사를 우회하는 두 번째 경로가 생긴다.
                Debug.LogError($"{name}: Wallet.Add에 음수({amount})가 들어왔다. "
                             + "차감은 TrySpend를 쓸 것.", this);
                return;
            }

            if (amount == 0) return;

            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        /// <summary>
        /// 쓸 수 있으면 쓰고 true. 부족하면 아무것도 안 하고 false —
        /// 던지지 않는다. 버튼 비활성화가 UI 몫이라 실패가 예외 상황이 아니다.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"{name}: Wallet.TrySpend에 {amount}가 들어왔다. "
                             + "비용은 1 이상이어야 한다.", this);
                return false;
            }

            if (Gold < amount) return false;

            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        // ── 여기부터 테스트용. 골드를 버는 진짜 경로(B의 정산 → Add)가 아직 없어서,
        //    이게 없으면 업그레이드를 한 번도 돌려볼 수가 없다.
        //    B가 Add를 붙이면 지워도 된다.

        [ContextMenu("테스트: +1000G")]
        private void DebugAddGold() => Add(1000);

        [ContextMenu("테스트: 0G로 비우기")]
        private void DebugClearGold()
        {
            if (Gold <= 0) return;

            Gold = 0;
            OnGoldChanged?.Invoke(Gold);
        }
    }
}
