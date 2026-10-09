using System;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 일차별 손님 배 일정 — 배가 몇 초마다 오고, 어느 크기 배가 얼마나 자주 오는가. (+10/9, 이슈 128 「손님 등장 빈도」)
    ///
    /// 초반엔 작은 배가 드문드문, 일차가 오를수록 간격이 줄고 큰 배가 섞인다.
    /// 숫자는 기획 확정 전 임시값이다 — 기획 표가 오면 이 에셋만 고친다.
    /// 안 변하는 값만 있다(설계: ScriptableObject엔 인스턴스별 값을 안 넣는다). 지금 몇 일차인지는 BusinessManager.Day.
    /// </summary>
    [CreateAssetMenu(fileName = "GuestWaves", menuName = "Marea/Guest Waves")]
    public class GuestWaveData : ScriptableObject
    {
        [Serializable]
        public struct Stage
        {
            [Tooltip("이 일차부터 적용. 다음 단계의 fromDay 전날까지.")]
            [Min(1)] public int fromDay;

            [Tooltip("앞 배가 떠나기 시작한 뒤 다음 배가 출발할 때까지 초(최소~최대 사이 랜덤).")]
            public Vector2 intervalSeconds;

            [Tooltip("배 종류별 가중치. 순서는 GuestBoatArrival.boatTypes와 같다(작은 · 중간 · 큰). 0이면 그 배는 안 온다.")]
            public float[] boatWeights;
        }

        [Tooltip("fromDay 오름차순. 오늘 일차보다 작거나 같은 것 중 마지막이 쓰인다.")]
        [SerializeField] private Stage[] stages;

        /// <summary>이 일차에 쓸 단계. 단계가 하나도 없으면 false.</summary>
        public bool TryGetStage(int day, out Stage stage)
        {
            stage = default;
            if (stages == null || stages.Length == 0) return false;

            stage = stages[0];
            foreach (Stage s in stages)
                if (s.fromDay <= day) stage = s;
            return true;
        }
    }
}
