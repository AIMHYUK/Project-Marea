using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
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
    ///
    /// (+10/2) 상위 단계는 골드에 더해 특수 자원(강화 금속)을 쓴다 — 창고에서 뺀다.
    /// 순서는 골드 → 자원 → 레벨이고, 뒤가 실패하면 앞을 되돌린다.
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
            Locked,              // (+9/28) 아직 해금 전이라 레벨을 못 올린다
            AlreadyUnlocked,     // (+9/28)
            MissingPrerequisite, // (+9/28) 선행 시설이 아직 잠겨 있다
            NotEnoughResource,   // (+10/2) 특수 자원이 모자란다
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

            if (!levels.IsUnlocked(kind)) return Result.Locked;
            if (!levels.CanRaise(kind)) return Result.MaxLevel;
            data.TryGetStep(levels.LevelOf(kind) + 1, out FacilityUpgradeStep step);
            if (wallet.Gold < step.goldCost) return Result.NotEnoughGold;
            if (!HasResource(step)) return Result.NotEnoughResource;

            return Result.Ok;
        }

        /// <summary>이 단계의 특수 자원을 창고에 가지고 있나. 자원이 없는 단계면 true.</summary>
        public static bool HasResource(FacilityUpgradeStep step)
        {
            if (step.resource == null || step.resourceAmount <= 0) return true;
            return Warehouse.Instance != null && Warehouse.Instance.CountOf(step.resource) >= step.resourceAmount;
        }

        private static IReadOnlyList<RecipeEntry> ResourceBundle(FacilityUpgradeStep step)
            => new[] { new RecipeEntry { ingredient = step.resource, requiredAmount = step.resourceAmount } };

        /// <summary>
        /// 실제로 올린다. <see cref="Result.Ok"/>가 아니면 골드도 레벨도 안 건드린다.
        /// </summary>
        public static Result TryUpgrade(FacilityKind kind)
        {
            Result check = CanUpgrade(kind);
            if (check != Result.Ok) return check;

            FacilityLevels levels = FacilityLevels.Instance;
            levels.DataOf(kind).TryGetStep(levels.LevelOf(kind) + 1, out FacilityUpgradeStep step);
            int cost = step.goldCost;
            bool usesResource = step.resource != null && step.resourceAmount > 0;

            // 차감이 먼저다. 레벨을 먼저 올리면 TrySpend가 false일 때 되돌려야 하는데,
            // 그 되돌리기가 OnLevelChanged를 이미 쏜 뒤라 UI가 한 번 깜빡인다.
            if (cost > 0 && !Wallet.Instance.TrySpend(cost)) return Result.NotEnoughGold;

            if (usesResource && !Warehouse.Instance.Consume(ResourceBundle(step)))
            {
                if (cost > 0) Wallet.Instance.Add(cost);
                return Result.NotEnoughResource;
            }

            if (!levels.TryRaiseLevel(kind))
            {
                // CanUpgrade를 통과했으면 여기 올 수 없다. 왔다면 그 사이에 레벨이
                // 다른 데서 바뀐 것이고, 비용만 빠진 상태라 조용히 넘기면 안 된다.
                Debug.LogError($"FacilityUpgrade: {kind} 차감({cost}G)은 됐는데 레벨이 안 올랐다. "
                             + "비용을 되돌린다.");
                if (cost > 0) Wallet.Instance.Add(cost);
                if (usesResource) Warehouse.Instance.Add(step.resource, step.resourceAmount);
                return Result.Misconfigured;
            }

            return Result.Ok;
        }

        // ── 해금 (+9/28, 이슈 71). 위 둘과 같은 모양이다 — 확인은 부작용 없이, 실행은
        //    차감을 먼저 하고 실패하면 되돌린다.

        /// <summary>지금 이 시설을 해금할 수 있는지. 아무것도 바꾸지 않는다.</summary>
        public static Result CanUnlock(FacilityKind kind)
        {
            FacilityLevels levels = FacilityLevels.Instance;
            Wallet wallet = Wallet.Instance;
            if (levels == null || wallet == null) return Result.Misconfigured;

            var data = levels.DataOf(kind);
            if (data == null) return Result.Misconfigured;

            if (levels.IsUnlocked(kind)) return Result.AlreadyUnlocked;
            if (FirstMissingPrerequisite(kind) != null) return Result.MissingPrerequisite;
            if (wallet.Gold < data.UnlockCost) return Result.NotEnoughGold;

            return Result.Ok;
        }

        /// <summary>실제로 연다. <see cref="Result.Ok"/>가 아니면 골드도 상태도 안 건드린다.</summary>
        public static Result TryUnlock(FacilityKind kind)
        {
            Result check = CanUnlock(kind);
            if (check != Result.Ok) return check;

            int cost = FacilityLevels.Instance.DataOf(kind).UnlockCost;
            if (!Wallet.Instance.TrySpend(cost)) return Result.NotEnoughGold;

            if (!FacilityLevels.Instance.TryUnlock(kind))
            {
                Debug.LogError($"FacilityUpgrade: {kind} 해금 차감({cost}G)은 됐는데 해금이 안 됐다. "
                             + "골드를 되돌린다.");
                Wallet.Instance.Add(cost);
                return Result.Misconfigured;
            }

            return Result.Ok;
        }

        /// <summary>아직 잠긴 선행 시설 중 첫째. 전부 열렸으면 null. UI가 사유 문구에 쓴다.</summary>
        public static FacilityKind? FirstMissingPrerequisite(FacilityKind kind)
        {
            FacilityLevels levels = FacilityLevels.Instance;
            var data = levels != null ? levels.DataOf(kind) : null;
            if (data == null) return null;

            foreach (FacilityKind pre in data.Prerequisites)
                if (!levels.IsUnlocked(pre)) return pre;

            return null;
        }
    }
}
