using Marea.Core;
using Marea.Data;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 부서진 시설을 클릭하면 뜨는 해금 창의 내용. (+9/28, 이슈 71)
    ///
    /// 기획 7-2 「해금 대상 선택」이 근거다. 성장 창에서도 해금할 수 있고 이 창은
    /// 월드에서 들어오는 창구다. 해금 자체는 둘 다 FacilityUpgrade.TryUnlock 한 곳을 탄다.
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「해금 / 바닥 수리」) 화면 가운데 고정 창이었다가
    /// 시설 위에 붙는 작은 확인창(<see cref="WorldConfirmUI"/>)이 됐다. 창 자체는 바닥 수리와
    /// 같이 쓰고, 여기는 "무엇을 보일지 · [해금]이 무엇을 하는지"만 정한다.
    /// </summary>
    public class FacilityUnlockUI : MonoBehaviour
    {
        [Tooltip("비워두면 같은 씬에서 찾는다.")]
        [SerializeField] private WorldConfirmUI confirm;

        [Tooltip("창을 띄울 높이(m). 시설 바닥보다 위에 뜨게.")]
        [SerializeField] private float anchorHeight = 1.5f;

        private void Awake()
        {
            if (confirm == null) confirm = FindAnyObjectByType<WorldConfirmUI>(FindObjectsInactive.Include);
            if (confirm == null)
                Debug.LogError($"{name}: 씬에 WorldConfirmUI가 없다. 해금 창이 안 뜬다.", this);
        }

        /// <summary>이 시설의 해금 창을 anchor 위에 연다.</summary>
        public void Open(FacilityKind kind, Transform anchor)
        {
            if (confirm == null) return;
            confirm.Open(anchor, Vector3.up * anchorHeight, () => BuildView(kind),
                         () => FacilityUpgrade.TryUnlock(kind) == FacilityUpgrade.Result.Ok);
        }

        private static ConfirmView BuildView(FacilityKind kind)
        {
            FacilityLevels levels = FacilityLevels.Instance;
            FacilityData data = levels != null ? levels.DataOf(kind) : null;
            FacilityUpgrade.Result state = FacilityUpgrade.CanUnlock(kind);
            int gold = Wallet.Instance != null ? Wallet.Instance.Gold : 0;

            string status = data == null ? "설정 오류" : $"필요 골드 {data.UnlockCost:N0} G\n보유 골드 {gold:N0} G";
            FacilityKind? missing = FacilityUpgrade.FirstMissingPrerequisite(kind);
            if (missing != null) status += $"\n먼저 해금: {NameOf(missing.Value)}";

            return new ConfirmView
            {
                Title = $"{NameOf(kind)} 해금",
                Body = data != null ? data.UnlockDescription : string.Empty,
                Status = status,
                CanConfirm = state == FacilityUpgrade.Result.Ok,
                ConfirmLabel = state switch
                {
                    FacilityUpgrade.Result.Ok                  => "해금",
                    FacilityUpgrade.Result.NotEnoughGold       => "골드 부족",
                    FacilityUpgrade.Result.MissingPrerequisite => "선행 필요",
                    FacilityUpgrade.Result.AlreadyUnlocked     => "해금됨",
                    _                                          => "설정 오류",
                },
            };
        }

        private static string NameOf(FacilityKind kind)
        {
            FacilityData data = FacilityLevels.Instance != null ? FacilityLevels.Instance.DataOf(kind) : null;
            if (data == null) return kind.ToString();
            return string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;
        }
    }
}
