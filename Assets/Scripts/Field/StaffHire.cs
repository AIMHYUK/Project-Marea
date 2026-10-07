using Marea.Core;
using Marea.Data;
using Marea.Economy;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 직원 운영 업그레이드(AUTO_SERVE, 기획 UPG_STAFF_001 "직원 고용 / 서빙 자동화")를 사면 서빙 직원을 내보낸다. (+10/7)
    ///
    /// 직원은 씬에 꺼 둔 채로 둔다 — ServingStaff는 켜질 때 게시판을 구독해 바로 일을 시작하니 "켜기 = 고용"이다.
    /// 시작할 때 이미 고용 레벨이면 조용히 켜고, 플레이 중에 레벨이 오르면 반짝임 · 소리와 함께 켠다.
    /// 해고(레벨이 내려가는 일)는 없으므로 끄지 않는다.
    /// </summary>
    public class StaffHire : MonoBehaviour
    {
        [Tooltip("내보낼 직원. 씬에 꺼 둔 PF_ServingStaff.")]
        [SerializeField] private ServingStaff staff;

        [Header("나타날 때")]
        [SerializeField] private VfxId appearVfx = VfxId.Sparkle;
        [SerializeField, Min(0.1f)] private float appearVfxScale = 1.2f;
        [SerializeField] private AudioClip appearClip;
        [SerializeField, Range(0f, 1f)] private float appearVolume = 0.8f;

        private void Awake()
        {
            if (staff == null)
            {
                Debug.LogError($"{name}: StaffHire.staff가 비어 있다. 직원 운영 업그레이드를 사도 직원이 안 나온다.", this);
                enabled = false;
                return;
            }
            // 씬에서 켜 둔 채 저장했어도 고용 전엔 숨긴다.
            staff.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (FacilityLevels.Instance == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. 직원 고용 여부를 알 수 없다.", this);
                return;
            }
            FacilityLevels.Instance.OnLevelChanged += HandleLevelChanged;
            if (IsHired()) staff.gameObject.SetActive(true);   // 이미 고용돼 있으면 연출 없이
        }

        private void OnDestroy()
        {
            if (FacilityLevels.Instance != null) FacilityLevels.Instance.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(FacilityKind kind, int level)
        {
            if (kind != FacilityKind.Staff || staff.gameObject.activeSelf || !IsHired()) return;

            staff.gameObject.SetActive(true);
            Vector3 at = staff.transform.position;
            Vfx.Play(appearVfx, at + Vector3.up * 0.5f, appearVfxScale);
            SoundManager.PlayAt(appearClip, at, appearVolume);
        }

        // 지금 레벨까지의 누적 값 — Lv1(AUTO_SERVE 1)을 지나면 1 이상.
        private static bool IsHired()
            => FacilityLevels.Instance.EffectValue(FacilityKind.Staff, FacilityEffectType.AutoServe) >= 1f;
    }
}
