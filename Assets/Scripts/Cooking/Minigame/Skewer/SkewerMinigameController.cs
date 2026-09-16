using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Restaurant;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Cooking
{
    public class SkewerMinigameController : MonoBehaviour
    {
        [Header("카메라 연출")]
        [SerializeField] private MinigameCameraController cameraController;
        [SerializeField] private Transform cameraViewPoint;

        [Header("3D 연출 및 UI")]
        [SerializeField] private IngredientController ingredientController;
        [SerializeField] private SkewerMinigameUI minigameUI;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private int _currentIngredientIndex;
        private readonly List<HitGrade> _hitHistory = new();
        private readonly List<IngredientData> _targetIngredients = new();
        private bool _isPlaying;

        // 메뉴 레시피에 정의된 총 개수를 반환
        public int TotalIngredients => _targetIngredients.Count;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void EnsureDependencies()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<MinigameCameraController>(FindObjectsInactive.Include);
            }

            if (minigameUI == null)
            {
                minigameUI = FindFirstObjectByType<SkewerMinigameUI>(FindObjectsInactive.Include);
            }

            if (ingredientController == null)
            {
                ingredientController = FindFirstObjectByType<IngredientController>(FindObjectsInactive.Include);
            }

            if (cameraViewPoint == null)
            {
                // 씬 내 활성/비활성 오브젝트를 포함하여 이름으로 자동 탐색
                GameObject viewPointObj = GameObject.Find("SkewerCameraViewPoint");
                if (viewPointObj == null)
                {
                    viewPointObj = GameObject.Find("CameraViewPoint");
                }

                // 부모가 비활성화 상태여서 Find로 못 잡을 경우를 대비한 트랜스폼 전체 탐색
                if (viewPointObj == null)
                {
                    Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t.gameObject.scene.isLoaded &&
                           (t.name == "Skewer_CameraTarget_Point"))
                        {
                            viewPointObj = t.gameObject;
                            break;
                        }
                    }
                }

                if (viewPointObj != null)
                {
                    cameraViewPoint = viewPointObj.transform;
                }
            }
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _currentIngredientIndex = 0;
            _hitHistory.Clear();
            _targetIngredients.Clear();

            // MenuData의 Recipe에서 재료와 개수를 전개하여 순서 리스트 생성.
            // 전에는 인스펙터 배열 순서를 그대로 썼다 — 행을 위아래로 옮기면 조리 순서가
            // 조용히 바뀌었다. 이제 기획 RecipeData.InputOrder가 정한다. (+9/16, #47)
            BuildTargetIngredients(_currentMenu, _targetIngredients);

            _isPlaying = true;

            if (cameraController != null && cameraViewPoint != null)
            {
                cameraController.MoveToViewPoint(cameraViewPoint);
            }

            if (ingredientController != null)
            {
                ingredientController.InitializeMinigame3D(_targetIngredients);
            }

            if (minigameUI != null)
            {
                minigameUI.Setup(this);
                minigameUI.Open();
            }
        }

        /// <summary>
        /// 레시피를 투입 순서대로 펼쳐 재료 한 개씩의 목록으로 만든다.
        ///
        /// inputOrder가 같은 줄끼리는 인스펙터에 적힌 순서를 유지한다 — List.Sort는
        /// 불안정 정렬이라 그냥 쓰면 값이 같은 줄의 앞뒤가 실행마다 달라질 수 있다.
        /// </summary>
        private static void BuildTargetIngredients(MenuData menu, List<IngredientData> result)
        {
            result.Clear();
            if (menu == null || menu.Recipe == null) return;

            IReadOnlyList<RecipeEntry> recipe = menu.Recipe;

            var order = new List<int>(recipe.Count);
            for (int i = 0; i < recipe.Count; i++)
            {
                if (recipe[i].ingredient == null || recipe[i].requiredAmount <= 0) continue;
                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int byOrder = recipe[a].inputOrder.CompareTo(recipe[b].inputOrder);
                return byOrder != 0 ? byOrder : a.CompareTo(b);
            });

            for (int i = 0; i < order.Count; i++)
            {
                RecipeEntry entry = recipe[order[i]];
                for (int n = 0; n < entry.requiredAmount; n++)
                {
                    result.Add(entry.ingredient);
                }
            }
        }

        public void ProcessHit(HitGrade grade)
        {
            if (!_isPlaying) return;

            _hitHistory.Add(grade);

            if (grade != HitGrade.Miss)
            {
                if (ingredientController != null)
                {
                    ingredientController.AttachIngredient(_currentIngredientIndex);
                }

                _currentIngredientIndex++;

                if (_currentIngredientIndex >= TotalIngredients)
                {
                    FinishGame(true);
                }
            }
        }

        private void FinishGame(bool isSuccess)
        {
            _isPlaying = false;

            if (ingredientController != null)
            {
                ingredientController.StopMoving();
            }

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            float multiplier = 1.0f;
            if (_hitHistory.Contains(HitGrade.Perfect)) multiplier += 0.1f;
            if (!isSuccess) multiplier -= 0.2f;

            int finalPrice = _currentMenu != null ? Mathf.RoundToInt(_currentMenu.BasePrice * multiplier) : 0;

            CookingResult result = new CookingResult
            {
                isSuccess = isSuccess,
                finalPrice = finalPrice,
                bestGrade = _hitHistory.Contains(HitGrade.Perfect) ? HitGrade.Perfect : HitGrade.Good,
                menuData = _currentMenu
            };

            StartCoroutine(FinishRoutine(result));
        }

        private IEnumerator FinishRoutine(CookingResult result)
        {
            yield return new WaitForSeconds(0.8f);

            if (minigameUI != null)
            {
                minigameUI.Close();
            }

            if (ingredientController != null)
            {
                ingredientController.HideAll();
            }

            if (cameraController != null)
            {
                cameraController.ReturnToOriginalPosition();
            }

            if (result.isSuccess)
            {
                DispatchCookedFood(result);
            }

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

        private void GiveFoodToPlayer(CookingResult result)
        {
            // 플레이어 손에 직접 넣지 않고 CookingMenuUI의 _onCompleteCallback으로 넘겨 조리대에 거치하도록 위임
        }
    }
}
