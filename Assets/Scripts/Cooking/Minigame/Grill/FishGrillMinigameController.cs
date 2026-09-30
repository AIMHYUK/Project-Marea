using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Player;
using Marea.Restaurant;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class FishGrillMinigameController : BaseCookingMinigame
    {
        [Header("3D 연출 및 UI")]
        [SerializeField] private FishGrillVisual fishVisual;
        [SerializeField] private FishGrillMinigameUI minigameUI;

        [Header("1단계: 칼집내기 가이드")]
        [SerializeField] private List<KnifeCutGuide> cutGuides;

        [Header("2단계: 굽기 타이머 및 판정 구간")]
        [SerializeField] private float totalBakeDuration = 6.5f;
        [SerializeField] private float perfectMin = 0.65f;
        [SerializeField] private float perfectMax = 0.80f;
        [SerializeField] private float goodMin = 0.45f;
        [SerializeField] private float goodMax = 0.90f;
        [SerializeField] private float badMin = 0.30f;
        [SerializeField] private float badMax = 0.97f;

        [Header("3단계: 플레이팅 항목")]
        [SerializeField] private List<MinigameDraggable> platingItems;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private float _elapsedTime;
        private bool _isPlaying;
        private bool _hasFlipped;
        private HitGrade _finalHitGrade = HitGrade.Miss;

        protected override void EnsureDependencies()
        {
            base.EnsureDependencies(); // 부모(BaseCookingMinigame)의 카메라 및 Raycaster 수집

            if (fishVisual == null)
            {
                fishVisual = FindFirstObjectByType<FishGrillVisual>(FindObjectsInactive.Include);
            }

            // MinigameUI가 연결되어 있지 않은 경우 자동 바인딩
            if (minigameUI == null)
            {
                // 1순위: 내 오브젝트 또는 하위(자식) 패널에서 우선 검색
                minigameUI = GetComponentInChildren<FishGrillMinigameUI>(true);

                // 2순위: 씬 전체(비활성화 상태 포함)에서 검색
                if (minigameUI == null)
                {
                    minigameUI = FindFirstObjectByType<FishGrillMinigameUI>(FindObjectsInactive.Include);
                }

                if (minigameUI != null)
                {
                    Debug.Log($"[FishGrillMinigameController] '{minigameUI.gameObject.name}'을(를) 자동으로 바인딩했습니다.");
                }
                else
                {
                    Debug.LogWarning("[FishGrillMinigameController] 씬에서 FishGrillMinigameUI를 찾을 수 없습니다.");
                }
            }

            if (cameraViewPoint == null)
            {
                GameObject viewPointObj = GameObject.Find("FishGrillCameraViewPoint");
                if (viewPointObj != null) cameraViewPoint = viewPointObj.transform;
            }
        
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(true);

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _elapsedTime = 0f;
            _hasFlipped = false;
            _isPlaying = true;

            if (fishVisual != null)
            {
                fishVisual.InitializeVisual();
            }

            if (minigameUI != null)
            {
                minigameUI.Open();
            }

            // 1단계 시작 (내부에서 카메라 이동도 함께 실행됨)
            StartStep1();
        }

        // ==========================================
        // 1단계: 칼집내기 (Score Fish)
        // ==========================================
        protected override void OnStep1Start()
        {
            // 칼집 초기화 로직
        }

        protected override void OnStep1Update()
        {
            if (!_isPlaying || cutGuides == null || cutGuides.Count == 0) return;

            int completedCount = 0;
            for (int i = 0; i < cutGuides.Count; i++)
            {
                if (cutGuides[i] != null && cutGuides[i].IsCompleted) completedCount++;
            }

            if (completedCount >= cutGuides.Count)
            {
                CompleteStep1(1.0f);
            }
        }

        // ==========================================
        // 2단계: 굽기 (Grill Fish)
        // ==========================================
        protected override void OnStep2Start()
        {
            _elapsedTime = 0f;
            _hasFlipped = false;
        }

        protected override void OnStep2Update()
        {
            if (!_isPlaying || _hasFlipped) return;

            _elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsedTime / totalBakeDuration);

            if (fishVisual != null)
            {
                fishVisual.UpdateGrillColor(progress);
            }

            bool isTriggered = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                               || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

            if (isTriggered)
            {
                OnFlipInput(progress);
            }
            else if (_elapsedTime >= totalBakeDuration)
            {
                OnTimeExpired();
            }
        }

        private void OnFlipInput(float progress)
        {
            _hasFlipped = true;

            if (fishVisual != null)
            {
                fishVisual.PlayFlipAnimation();
            }

            float multiplier;

            if (progress >= perfectMin && progress <= perfectMax)
            {
                _finalHitGrade = HitGrade.Perfect;
                multiplier = 1.2f;
            }
            else if (progress >= goodMin && progress <= goodMax)
            {
                _finalHitGrade = HitGrade.Good;
                multiplier = 1.0f;
            }
            else if (progress >= badMin && progress <= badMax)
            {
                _finalHitGrade = HitGrade.Bad;
                multiplier = 0.8f;
            }
            else
            {
                _finalHitGrade = HitGrade.Miss;
                multiplier = 0f;
            }

            CompleteStep2(multiplier);
        }

        private void OnTimeExpired()
        {
            _hasFlipped = true;
            _finalHitGrade = HitGrade.Miss;
            CompleteStep2(0f);
        }

        // ==========================================
        // 3단계: 플레이팅 (Plating)
        // ==========================================
        protected override void OnStep3Start()
        {
            // 플레이팅 초기화 로직
        }

        protected override void OnStep3Update()
        {
            if (!_isPlaying || platingItems == null || platingItems.Count == 0) return;

            int placedCount = 0;
            for (int i = 0; i < platingItems.Count; i++)
            {
                if (platingItems[i] != null && platingItems[i].IsPlaced) placedCount++;
            }

            if (placedCount >= platingItems.Count)
            {
                CompleteStep3(1.0f);
            }
        }

        // ==========================================
        // 최종 완결 연출 및 결과 반환
        // ==========================================
        protected override void OnMinigameCompleted(float finalScore)
        {
            _isPlaying = false;

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(false);

            int basePrice = _currentMenu != null ? _currentMenu.BasePrice : 0;
            int finalPrice = Mathf.RoundToInt(basePrice * Step2Score);

            CookingResult result = new CookingResult
            {
                isSuccess = (_finalHitGrade != HitGrade.Miss),
                finalPrice = finalPrice,
                bestGrade = _finalHitGrade,
                menuData = _currentMenu
            };

            StartCoroutine(FinishRoutine(result));
        }

        private IEnumerator FinishRoutine(CookingResult result)
        {
            if (minigameUI != null) minigameUI.ShowResult(result.bestGrade);

            yield return new WaitForSeconds(1.2f);

            if (minigameUI != null) minigameUI.Close();

            // 카메라 원복은 Base.FinishMinigame()에서 자동 제어됨
            ReturnCameraToOriginalPosition();

            if (fishVisual != null) fishVisual.ResetVisual();

            SetPhysicsRaycasterState(false);

            if (result.isSuccess) DispatchCookedFood(result);

            _onCompleteCallback?.Invoke(result);
        }

        private void DispatchCookedFood(CookingResult result)
        {
            Sprite icon = _currentMenu != null ? _currentMenu.Icon : null;
            ServeBoard board = FindFirstObjectByType<ServeBoard>();
            ServingStaff[] staffs = FindObjectsByType<ServingStaff>(FindObjectsSortMode.None);

            if (board == null || staffs.Length == 0)
            {
                GiveFoodToPlayer(result);
                return;
            }

            CustomerController target = PickTarget(board, staffs);
            if (target == null)
            {
                GiveFoodToPlayer(result);
                return;
            }

            board.Post(target.transform, icon, () =>
            {
                if (target != null) target.ServeFood(result);
            });
        }

        private CustomerController PickTarget(ServeBoard board, ServingStaff[] staffs)
        {
            CustomerManager manager = FindFirstObjectByType<CustomerManager>();
            if (manager == null) return null;

            foreach (CustomerController c in manager.GetWaitingCustomers())
            {
                if (board.IsTargeted(c.transform)) continue;

                bool taken = false;
                foreach (ServingStaff s in staffs)
                {
                    if (s.DeliverTarget == c.transform) { taken = true; break; }
                }

                if (!taken) return c;
            }

            return null;
        }

        private void GiveFoodToPlayer(CookingResult result) { }
    }
}
