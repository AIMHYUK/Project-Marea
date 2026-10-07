using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Cooking
{
    public enum MinigameStepIndex
    {
        NotStarted,
        Step1,
        Step2,
        Step3,
        Completed
    }

    public abstract class BaseCookingMinigame : MonoBehaviour
    {
        [Header("단계별 UI / 연출 오브젝트 패널")]
        [SerializeField] protected GameObject step1Panel;
        [SerializeField] protected GameObject step2Panel;
        [SerializeField] protected GameObject step3Panel;

        [Header("공통 카메라 시스템")]
        [SerializeField] protected MinigameCameraController cameraController;
        [SerializeField] protected Transform cameraViewPoint;

        [Header("단계별 카메라 시점 (비면 cameraViewPoint)")]
        [Tooltip("KitchenViewPoints 아래 공용 시점(화구·그릴·조리대·싱크대·창고)을 연결한다.")]
        [SerializeField] protected Transform step1ViewPoint;
        [SerializeField] protected Transform step2ViewPoint;
        [SerializeField] protected Transform step3ViewPoint;

        [Header("단계 전환 연출 설정")]
        [SerializeField] protected float stepTransitionDelay = 1.5f; // 단계 넘어갈 때 딜레이 시간 (초)

        [Header("미니게임 진행 중 비활성화할 오브젝트")]
        [SerializeField] private List<GameObject> disableDuringMinigame;
        [Tooltip("씬의 CookingDeco를 찾아 미니게임 동안 숨긴다. 프리팹 외부 오브젝트도 연결할 수 있다.")]
        [SerializeField] private bool hideCookingDeco;
        private GameObject _cookingDeco;
        private bool _cookingDecoHidden;

        public MinigameStepIndex CurrentStepIndex { get; protected set; } = MinigameStepIndex.NotStarted;

        protected float Step1Score = 1.0f;
        protected float Step2Score = 1.0f;
        protected float Step3Score = 1.0f;

        private PhysicsRaycaster _cameraRaycaster;
        private Coroutine _stepTransitionRoutine;

        protected virtual void Awake()
        {
            EnsureDependencies();
            SetPhysicsRaycasterState(false);

            // 조리대를 여러 요리가 같이 쓰므로, 평소에는 모든 단계 패널을 꺼 둔다.
            // 지금 요리의 지금 단계 패널만 StartStepN에서 켠다.
            SetStepPanelState(s1: false, s2: false, s3: false);
        }

        protected virtual void OnDisable()
        {
            if (_cookingDecoHidden) SetDisableDuringMinigameState(true);
            SetPhysicsRaycasterState(false);
            if (_stepTransitionRoutine != null)
            {
                StopCoroutine(_stepTransitionRoutine);
                _stepTransitionRoutine = null;
            }
        }

        protected virtual void ResetMinigame()
        {
            if (_stepTransitionRoutine != null)
            {
                StopCoroutine(_stepTransitionRoutine);
                _stepTransitionRoutine = null;
            }

            CurrentStepIndex = MinigameStepIndex.NotStarted;

            Step1Score = 1.0f;
            Step2Score = 1.0f;
            Step3Score = 1.0f;

            SetPhysicsRaycasterState(false);
            SetStepPanelState(s1: false, s2: false, s3: false);

            SetDisableDuringMinigameState(true);
        }

        protected virtual void EnsureDependencies()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<MinigameCameraController>(FindObjectsInactive.Include);
            }

            if (_cameraRaycaster == null && Camera.main != null)
            {
                _cameraRaycaster = Camera.main.GetComponent<PhysicsRaycaster>();
                if (_cameraRaycaster == null)
                {
                    _cameraRaycaster = Camera.main.gameObject.AddComponent<PhysicsRaycaster>();
                }
            }
        }

        public void SetPhysicsRaycasterState(bool active)
        {
            if (_cameraRaycaster == null && Camera.main != null)
            {
                _cameraRaycaster = Camera.main.GetComponent<PhysicsRaycaster>();
            }

            if (_cameraRaycaster != null)
            {
                _cameraRaycaster.enabled = active;
            }
        }

        protected virtual void MoveCameraToViewPoint(Transform viewPoint)
        {
            if (cameraController != null && viewPoint != null)
            {
                cameraController.MoveToViewPoint(viewPoint);
            }
        }

        /// <summary>단계 시점이 비어 있으면 공통 cameraViewPoint를 쓴다.</summary>
        protected Transform GetStepViewPoint(MinigameStepIndex step)
        {
            Transform stepPoint = step switch
            {
                MinigameStepIndex.Step1 => step1ViewPoint,
                MinigameStepIndex.Step2 => step2ViewPoint,
                MinigameStepIndex.Step3 => step3ViewPoint,
                _ => null
            };
            return stepPoint != null ? stepPoint : cameraViewPoint;
        }

        // 2·3단계: 시점이 앞 단계와 같으면 움직이지 않는다.
        private void MoveCameraForStep(MinigameStepIndex step, MinigameStepIndex previous)
        {
            Transform target = GetStepViewPoint(step);
            if (target != GetStepViewPoint(previous))
            {
                MoveCameraToViewPoint(target);
            }
        }

        protected virtual void ReturnCameraToOriginalPosition()
        {
            if (cameraController != null)
            {
                cameraController.ReturnToOriginalPosition();
            }
        }

        protected virtual void Update()
        {
            switch (CurrentStepIndex)
            {
                case MinigameStepIndex.Step1:
                    OnStep1Update();
                    break;
                case MinigameStepIndex.Step2:
                    OnStep2Update();
                    break;
                case MinigameStepIndex.Step3:
                    OnStep3Update();
                    break;
            }
        }

        // --- 1단계 실행 ---
        public virtual void StartStep1()
        {
            SetDisableDuringMinigameState(false);

            SetPhysicsRaycasterState(true);
            MoveCameraToViewPoint(GetStepViewPoint(MinigameStepIndex.Step1));

            CurrentStepIndex = MinigameStepIndex.Step1;
            SetStepPanelState(s1: true, s2: false, s3: false);
            OnStep1Start();
        }

        protected abstract void OnStep1Start();
        protected abstract void OnStep1Update();

        public virtual void CompleteStep1(float score = 1.0f)
        {
            Step1Score = score;
            TransitionToNextStep(StartStep2);
        }

        // --- 2단계 실행 ---
        public virtual void StartStep2()
        {
            MoveCameraForStep(MinigameStepIndex.Step2, MinigameStepIndex.Step1);
            CurrentStepIndex = MinigameStepIndex.Step2;
            SetStepPanelState(s1: false, s2: true, s3: false);
            OnStep2Start();
        }

        protected abstract void OnStep2Start();
        protected abstract void OnStep2Update();

        public virtual void CompleteStep2(float score = 1.0f)
        {
            Step2Score = score;
            TransitionToNextStep(StartStep3);
        }

        // --- 3단계 실행 ---
        public virtual void StartStep3()
        {
            MoveCameraForStep(MinigameStepIndex.Step3, MinigameStepIndex.Step2);
            CurrentStepIndex = MinigameStepIndex.Step3;
            SetStepPanelState(s1: false, s2: false, s3: true);
            OnStep3Start();
        }

        protected abstract void OnStep3Start();
        protected abstract void OnStep3Update();

        public virtual void CompleteStep3(float score = 1.0f)
        {
            Step3Score = score;
            TransitionToNextStep(FinishMinigame);
        }

        // --- 단계 전이 코루틴 처리 ---
        private void TransitionToNextStep(Action nextStepAction)
        {
            if (_stepTransitionRoutine != null)
            {
                StopCoroutine(_stepTransitionRoutine);
            }

            _stepTransitionRoutine = StartCoroutine(StepTransitionRoutine(nextStepAction));
        }

        private IEnumerator StepTransitionRoutine(Action nextStepAction)
        {
            // 중복 Update 방지를 위해 전환 중 상태로 대기
            CurrentStepIndex = MinigameStepIndex.NotStarted;

            yield return new WaitForSeconds(stepTransitionDelay);

            _stepTransitionRoutine = null;
            nextStepAction?.Invoke();
        }

        // --- 미니게임 완결 ---
        protected virtual void FinishMinigame()
        {
            CurrentStepIndex = MinigameStepIndex.Completed;
            SetPhysicsRaycasterState(false);
            ReturnCameraToOriginalPosition();
            SetStepPanelState(s1: false, s2: false, s3: false);

            SetDisableDuringMinigameState(true);

            float averageScore = (Step1Score + Step2Score + Step3Score) / 3.0f;
            OnMinigameCompleted(averageScore);
        }

        protected abstract void OnMinigameCompleted(float finalScore);

        protected void SetStepPanelState(bool s1, bool s2, bool s3)
        {
            if (step1Panel != null) step1Panel.SetActive(s1);
            if (step2Panel != null) step2Panel.SetActive(s2);
            if (step3Panel != null) step3Panel.SetActive(s3);
        }

        private void SetDisableDuringMinigameState(bool active)
        {
            if (hideCookingDeco)
            {
                if (_cookingDeco == null)
                {
                    // Scene objects cannot be serialized into a prefab asset.
                    // Resolve the closest decoration group, including inactive objects.
                    float nearestDistance = float.PositiveInfinity;
                    foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (candidate.name != "CookingDeco" || candidate.IsChildOf(transform)) continue;
                        float distance = (candidate.position - transform.position).sqrMagnitude;
                        if (distance >= nearestDistance) continue;
                        nearestDistance = distance;
                        _cookingDeco = candidate.gameObject;
                    }
                }
                if (_cookingDeco != null && (!active || _cookingDecoHidden))
                {
                    _cookingDeco.SetActive(active);
                    _cookingDecoHidden = !active;
                }
            }
            if (disableDuringMinigame == null) return;

            for (int i = 0; i < disableDuringMinigame.Count; i++)
            {
                if (disableDuringMinigame[i] != null)
                {
                    disableDuringMinigame[i].SetActive(active);
                }
            }
        }
    }
}
