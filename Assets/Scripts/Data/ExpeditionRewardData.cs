using System;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 기획 ExpeditionRewardData 한 줄. 보상 그룹 안의 후보 하나다. (+9/28, 이슈 73)
    /// </summary>
    [Serializable]
    public struct ExpeditionRewardEntry
    {
        [Tooltip("기획 ExpeditionRewardData.ItemID. 재료 또는 특수 자원.")]
        public IngredientData item;

        [Tooltip("기획 ExpeditionRewardData.Weight. 추첨 비중. 0이면 안 뽑힌다.")]
        [Min(0f)] public float weight;

        [Tooltip("기획 ExpeditionRewardData.AmountMin.")]
        [Min(1)] public int amountMin;

        [Tooltip("기획 ExpeditionRewardData.AmountMax. AmountMin보다 작으면 AmountMin을 쓴다.")]
        [Min(1)] public int amountMax;
    }

    /// <summary>
    /// 보상 그룹 하나 — 기획 ExpeditionRewardData에서 RewardGroupID가 같은 줄들을 한 에셋에 모았다. (+9/28, 이슈 73)
    ///
    /// 기획 표는 줄마다 RewardGroupID를 반복한다. 여기서는 그룹이 에셋 하나이고, 해역이
    /// 이 에셋을 참조한다. 같은 그룹을 두 해역이 나눠 쓸 수 있다.
    ///
    /// 한 번 귀환할 때 몇 번 뽑는지는 기획에 없다. 지금은 가중치로 한 줄을 뽑고,
    /// 수량은 AmountMin~AmountMax 사이에서 고른다 (ExpeditionManager).
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Expedition Reward Group", fileName = "RewardGroup_")]
    public class ExpeditionRewardData : ScriptableObject
    {
        [Tooltip("기획 ExpeditionRewardData.RewardGroupID. 중복 불가.")]
        [SerializeField] private int id;

        [Tooltip("이 그룹에서 뽑힐 수 있는 후보.")]
        [SerializeField] private ExpeditionRewardEntry[] entries;

        public int Id => id;
        public ExpeditionRewardEntry[] Entries => entries ?? Array.Empty<ExpeditionRewardEntry>();

        /// <summary>
        /// 가중치로 한 줄을 뽑는다. 후보가 없거나 가중치 합이 0이면 false.
        /// roll은 0~1 난수 — 테스트에서 고정값을 넣을 수 있게 밖에서 받는다.
        /// </summary>
        public bool TryPick(float roll, out ExpeditionRewardEntry picked)
        {
            picked = default;
            float total = 0f;
            foreach (ExpeditionRewardEntry e in Entries)
                if (e.item != null && e.weight > 0f) total += e.weight;
            if (total <= 0f) return false;

            float target = Mathf.Clamp01(roll) * total;
            foreach (ExpeditionRewardEntry e in Entries)
            {
                if (e.item == null || e.weight <= 0f) continue;
                picked = e;
                target -= e.weight;
                if (target <= 0f) return true;
            }
            return true;   // 부동소수 오차로 끝까지 온 경우 마지막 후보
        }
    }
}
