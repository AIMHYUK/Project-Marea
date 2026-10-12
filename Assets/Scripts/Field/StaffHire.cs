using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using Marea.Player;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 직원 운영 업그레이드(AUTO_SERVE, 기획 UPG_STAFF_001 "직원 고용 / 서빙 자동화")를 사면 서빙 직원을 내보낸다. (+10/7)
    ///
    /// 직원은 씬에 꺼 둔 채로 둔다 — ServingStaff는 켜질 때 게시판을 구독해 바로 일을 시작하니 "켜기 = 고용"이다.
    /// 시작할 때 이미 고용 레벨이면 조용히 켜고, 플레이 중에 레벨이 오르면 반짝임 · 소리와 함께 켠다.
    /// 해고(레벨이 내려가는 일)는 없으므로 끄지 않는다.
    ///
    /// (+10/13) 플레이 중 고용되면 해금 연출(FacilitySite)처럼 카메라가 직원 자리로 가서 나타나는 걸 보여 주고 돌아온다.
    /// 그동안 열린 창은 가리고 플레이어 입력은 막는다.
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

        [Header("카메라 연출 (+10/13)")]
        [Tooltip("카메라가 볼 높이 — 직원 발밑에서 위로(m).")]
        [SerializeField, Min(0f)] private float lookHeight = 1f;
        [Tooltip("카메라 거리. 0이면 카메라 기본 연출 거리.")]
        [SerializeField, Min(0f)] private float focusDistance;
        [Tooltip("카메라가 도착한 뒤 직원이 나타나기까지(초).")]
        [SerializeField, Min(0f)] private float beforeAppear = 0.3f;
        [Tooltip("나타난 뒤 돌아가기까지(초).")]
        [SerializeField, Min(0f)] private float afterAppear = 1.2f;
        [Tooltip("카메라가 못 가도 이 시간(초)이 지나면 진행한다.")]
        [SerializeField, Min(0.1f)] private float arriveTimeout = 2f;

        private readonly List<UiPanel> _hiddenPanels = new();
        private bool _revealing;

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

            if (_revealing) return;
            StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            _revealing = true;
            CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam == null) Debug.LogError($"{name}: MainCamera에 CameraFollow가 없다. 카메라 연출 없이 직원만 내보낸다.", this);
            PlayerController player = FindAnyObjectByType<PlayerController>();

            // 창이 화면을 가리면 연출이 안 보인다 — 닫지 않고 가렸다가 되돌린다(FacilitySite와 같다).
            foreach (UiPanel panel in FindObjectsByType<UiPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.IsVisible) { panel.Suspend(); _hiddenPanels.Add(panel); }
            if (player != null) player.BeginBusy();

            Vector3 at = staff.transform.position;
            if (cam != null)
            {
                cam.FocusOn(at + Vector3.up * lookHeight, focusDistance);
                for (float t = 0f; !cam.IsAtFocus && t < arriveTimeout; t += Time.deltaTime)
                    yield return null;
                yield return new WaitForSeconds(beforeAppear);
            }

            staff.gameObject.SetActive(true);
            Vfx.Play(appearVfx, at + Vector3.up * 0.5f, appearVfxScale);
            SoundManager.PlayAt(appearClip, at, appearVolume);

            if (cam != null)
            {
                yield return new WaitForSeconds(afterAppear);
                cam.ClearFocus();
            }
            if (player != null) player.EndBusy();
            foreach (UiPanel panel in _hiddenPanels)
                if (panel != null) panel.Resume();
            _hiddenPanels.Clear();
            _revealing = false;
        }

        // 지금 레벨까지의 누적 값 — Lv1(AUTO_SERVE 1)을 지나면 1 이상.
        private static bool IsHired()
            => FacilityLevels.Instance.EffectValue(FacilityKind.Staff, FacilityEffectType.AutoServe) >= 1f;
    }
}
