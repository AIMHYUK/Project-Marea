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

        [Header("동작 효과음")]
        [Tooltip("1단계 칼집 하나를 완성할 때 재생합니다. 비워두면 재생하지 않습니다.")]
        [SerializeField] private AudioClip cutAudioClip;
        [SerializeField] private AudioSource cutAudioSource;
        [Tooltip("2단계 생선을 뒤집기 시작할 때 재생합니다. 비워두면 재생하지 않습니다.")]
        [SerializeField] private AudioClip flipAudioClip;
        [SerializeField] private AudioSource flipAudioSource;

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
        [Tooltip("추가 연결할 플레이팅 재료. Step3Panel 아래의 MinigameDraggable 재료는 자동으로 포함됩니다.")]
        [SerializeField] private List<MinigameDraggable> platingItems;
        [Tooltip("재료를 자유롭게 놓을 접시 모델. 비워두면 Step3Panel의 SM_Salmon_Dish에서 찾습니다.")]
        [SerializeField] private Renderer platingSurface;
        private readonly List<MinigameDraggable> _requiredPlatingItems = new();
        private int _lastPlacedCount = -1;

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

            ResetMinigame();

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

        protected override void ResetMinigame()
        {
            base.ResetMinigame();
            CollectPlatingItems();
            _lastPlacedCount = -1;

            _elapsedTime = 0f;
            _isPlaying = false;
            _hasFlipped = false;
            _finalHitGrade = HitGrade.Miss;

            if (cutGuides != null)
            {
                for (int i = 0; i < cutGuides.Count; i++)
                {
                    if (cutGuides[i] != null)
                    {
                        cutGuides[i].ResetGuide();
                    }
                }
            }

            foreach (MinigameDraggable item in _requiredPlatingItems)
            {
                if (item != null) item.ResetObject();
            }
        }

        // ==========================================
        // 1단계: 칼집내기 (Score Fish)
        // ==========================================
        protected override void OnStep1Start()
        {
            minigameUI?.SetGuide("화살표를 따라 위에서 아래로 드래그해 칼집 3개를 내세요!");
        }

        public void PlayCutSound()
        {
            if (!_isPlaying || CurrentStepIndex != MinigameStepIndex.Step1) return;
            if (cutAudioClip != null && cutAudioSource != null)
                cutAudioSource.PlayOneShot(cutAudioClip);
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
            minigameUI?.SetGuide("알맞게 익었을 때 클릭하여 뒤집으세요!");
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

            if (flipAudioClip != null && flipAudioSource != null)
                flipAudioSource.PlayOneShot(flipAudioClip);

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
            CollectPlatingItems();
            _lastPlacedCount = -1;
            foreach (MinigameDraggable item in _requiredPlatingItems)
            {
                if (item != null) item.ResetObject();
            }
            UpdatePlatingGuide();
        }

        protected override void OnStep3Update()
        {
            if (!_isPlaying) return;
            UpdatePlatingGuide();
            if (AreAllPlatingItemsPlaced()) CompleteStep3(1.0f);
        }

        private void CollectPlatingItems()
        {
            _requiredPlatingItems.Clear();
            if (step3Panel != null)
            {
                foreach (MinigameDraggable item in step3Panel.GetComponentsInChildren<MinigameDraggable>(true))
                    AddPlatingItem(item);
            }
            if (platingItems != null)
                foreach (MinigameDraggable item in platingItems) AddPlatingItem(item);

            // Newly added ingredients may have a draggable component but no placement bindings yet.
            MinigameDraggable template = _requiredPlatingItems.Find(item => item != null && item.HasPlacementTargets);
            if (template != null)
                foreach (MinigameDraggable item in _requiredPlatingItems) item.UsePlacementDefaults(template);
            if (platingSurface == null && step3Panel != null)
            {
                Transform dish = step3Panel.transform.Find("SM_Salmon_Dish");
                if (dish != null) platingSurface = dish.GetComponentInChildren<Renderer>(true);
            }
            foreach (MinigameDraggable item in _requiredPlatingItems) item.UseFreePlateArea(platingSurface);
        }

        private void AddPlatingItem(MinigameDraggable item)
        {
            if (item != null && !_requiredPlatingItems.Contains(item)) _requiredPlatingItems.Add(item);
        }

        private bool AreAllPlatingItemsPlaced()
        {
            int validCount = 0;
            foreach (MinigameDraggable item in _requiredPlatingItems)
            {
                if (item == null) continue;
                validCount++;
                if (!item.IsPlaced) return false;
            }
            return validCount > 0;
        }

        private void UpdatePlatingGuide()
        {
            int placedCount = 0;
            int totalCount = 0;
            foreach (MinigameDraggable item in _requiredPlatingItems)
            {
                if (item == null) continue;
                totalCount++;
                if (item.IsPlaced) placedCount++;
            }
            // Several ingredients can share the same guide objects. Hide only after everyone is placed.
            foreach (MinigameDraggable item in _requiredPlatingItems)
                if (item != null) item.SetPlacementGuidesVisible(placedCount < totalCount);
            if (placedCount == _lastPlacedCount) return;
            _lastPlacedCount = placedCount;
            minigameUI?.SetGuide($"모든 재료를 접시 안 원하는 곳에 놓으세요! ({placedCount}/{totalCount})");
        }

        public override void CompleteStep3(float score = 1.0f)
        {
            // Also guard completion triggered by external scripts or Inspector events.
            if (!_isPlaying || CurrentStepIndex != MinigameStepIndex.Step3) return;
            CollectPlatingItems();
            if (!AreAllPlatingItemsPlaced()) return;
            base.CompleteStep3(score);
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

            ResetMinigame();
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
