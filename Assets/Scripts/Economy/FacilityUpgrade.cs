using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 「비용 확인 → 골드 차감 → 레벨 +1」을 한 덩어리로 묶는다.
    ///
    /// 이 순서가 코드 두 군데에 흩어지면 한쪽이 실패했을 때 다른 쪽이 이미 지나가 있다 —
    /// 골드는 빠졌는데 레벨이 안 오르는 상태가 그것이다. 그래서 UI는 이 함수만 부른다.
    ///
    /// MonoBehaviour가 아니다. 씬에 놓을 상태가 없고, 두 싱글턴을 잇는 절차뿐이다.
    /// </summary>
    public static class FacilityUpgrade
    {
        /// <summary>업그레이드가 왜 안 되는지. UI가 버튼을 끄고 사유를 보여주는 데 쓴다.</summary>
        public enum Result
        {
            Ok,
            MaxLevel,
            NotEnoughGold,
            Misconfigured,   // 씬에 Wallet/FacilityLevels가 없거나 에셋이 빠졌다
        }

        /// <summary>
        /// 지금 이 시설을 올릴 수 있는지. 아무것도 바꾸지 않는다.
        /// UI가 매 갱신마다 부르는 자리라 부작용이 있으면 안 된다.
        /// </summary>
        public static Result CanUpgrade(FacilityKind kind)
        {
            FacilityLevels levels = FacilityLevels.Instance;
            Wallet wallet = Wallet.Instance;

            // 둘 중 하나라도 없으면 각자의 Awake가 이미 에러를 냈다. 여기서 또 찍으면
            // UI가 매 프레임 부르는 만큼 콘솔이 덮인다.
            if (levels == null || wallet == null) return Result.Misconfigured;

            var data = levels.DataOf(kind);
            if (data == null) return Result.Misconfigured;

            if (!levels.CanRaise(kind)) return Result.MaxLevel;
            if (wallet.Gold < data.CostToNext(levels.LevelOf(kind))) return Result.NotEnoughGold;

            return Result.Ok;
        }

        /// <summary>
        /// 실제로 올린다. <see cref="Result.Ok"/>가 아니면 골드도 레벨도 안 건드린다.
        /// </summary>
        public static Result TryUpgrade(FacilityKind kind)
        {
            Result check = CanUpgrade(kind);
            if (check != Result.Ok) return check;

            FacilityLevels levels = FacilityLevels.Instance;
            int cost = levels.DataOf(kind).CostToNext(levels.LevelOf(kind));

            // 차감이 먼저다. 레벨을 먼저 올리면 TrySpend가 false일 때 되돌려야 하는데,
            // 그 되돌리기가 OnLevelChanged를 이미 쏜 뒤라 UI가 한 번 깜빡인다.
            if (!Wallet.Instance.TrySpend(cost)) return Result.NotEnoughGold;

            if (!levels.TryRaiseLevel(kind))
            {
                // CanUpgrade를 통과했으면 여기 올 수 없다. 왔다면 그 사이에 레벨이
                // 다른 데서 바뀐 것이고, 골드만 빠진 상태라 조용히 넘기면 안 된다.
                Debug.LogError($"FacilityUpgrade: {kind} 차감({cost}G)은 됐는데 레벨이 안 올랐다. "
                             + "골드를 되돌린다.");
                Wallet.Instance.Add(cost);
                return Result.Misconfigured;
            }

            return Result.Ok;
        }
    }
}
