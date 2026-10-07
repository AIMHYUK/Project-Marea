using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class Seasoning3DQTE : MonoBehaviour
    {
        [Header("3D 양념통 오브젝트")]
        [SerializeField] private GameObject salt3DObject;   // 3D 소금통
        [SerializeField] private GameObject pepper3DObject; // 3D 후추통

        [Header("클릭 영역 (1920×1080 기준 픽셀 여유)")]
        [SerializeField, Min(0f)] private float seasoningClickPadding = 24f;

        [Header("3D 연출 요소 (Particle & Sound)")]
        [SerializeField] private ParticleSystem saltParticle;   // 소금 흩날림 파티클
        [SerializeField] private ParticleSystem pepperParticle; // 후추 흩날림 파티클
        [SerializeField] private AudioSource shakeAudioSource;  // 쌕쌕/톡톡 소리
        [SerializeField] private AudioClip pepperShakeClip;

        [Header("리듬 레일 UI 연동")]
        [SerializeField] private SequenceInputRailUI railUI;

        [Header("랜덤 패턴 생성 설정")]
        [SerializeField, Min(1)] private int totalNoteCount = 15; // 생성할 총 노트 개수
        [SerializeField] private float initialDelay = 1.5f;     // 첫 노트가 나오는 대기 시간 (초)
        [SerializeField] private float minInterval = 0.8f;      // 노트 간 최소 간격 (초)
        [SerializeField] private float maxInterval = 1.4f;      // 노트 간 최대 간격 (초)
        [Tooltip("일반 노트 간격에 추가할 무작위 편차 비율.")]
        [InspectorName("일반 간격 랜덤 편차")]
        [SerializeField, Range(0f, 0.5f)] private float intervalVariation = 0.25f;
        [Tooltip("같은 양념통을 빠르게 두 번 클릭하는 패턴의 발생 확률. 0보다 크면 한 판에 최소 한 쌍이 등장합니다.")]
        [InspectorName("연속 두 번 클릭 확률")]
        [SerializeField, Range(0f, 1f)] private float doubleClickChance = 0.35f;
        [InspectorName("연속 클릭 간격 (최소/최대 초)")]
        [SerializeField] private Vector2 doubleClickInterval = new Vector2(0.28f, 0.42f);

        public bool IsQTECompleted { get; private set; }
        public int SuccessCount { get; private set; }
        public int MissCount { get; private set; }
        public int TotalNoteCount => Mathf.Max(1, totalNoteCount);

        private Camera _mainCamera;
        private Action _onQTEFinishedCallback;
        private readonly Dictionary<GameObject, Quaternion> _initialRotations = new();

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (salt3DObject != null)
            {
                _initialRotations[salt3DObject] =
                    salt3DObject.transform.localRotation;
            }

            if (pepper3DObject != null)
            {
                _initialRotations[pepper3DObject] =
                    pepper3DObject.transform.localRotation;
            }
        }

        public void ResetQTE()
        {
            StopAllCoroutines();

            IsQTECompleted = false;
            SuccessCount = 0;
            MissCount = 0;

            _onQTEFinishedCallback = null;

            if (railUI != null)
            {
                railUI.ResetRail();
            }

            if (salt3DObject != null &&
                _initialRotations.TryGetValue(
                    salt3DObject,
                    out Quaternion saltRotation))
            {
                salt3DObject.transform.localRotation =
                    saltRotation;
            }

            if (pepper3DObject != null &&
                _initialRotations.TryGetValue(
                    pepper3DObject,
                    out Quaternion pepperRotation))
            {
                pepper3DObject.transform.localRotation =
                    pepperRotation;
            }

            if (saltParticle != null)
            {
                saltParticle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            if (pepperParticle != null)
            {
                pepperParticle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            if (shakeAudioSource != null)
            {
                shakeAudioSource.Stop();
            }
        }

        public void StartQTEGame(Action onQTEComplete = null)
        {
            ResetQTE();

            IsQTECompleted = false;
            SuccessCount = 0;
            MissCount = 0;
            _onQTEFinishedCallback = onQTEComplete;

            List<SequenceNoteData> generatedNotes =
                GenerateRandomNotes();

            if (railUI != null &&
                generatedNotes.Count > 0)
            {
                railUI.StartRail(
                    generatedNotes,
                    OnHitResultReceived,
                    OnQTEFinished
                );
            }
            else
            {
                Debug.LogWarning(
                    "[Seasoning3DQTE] Rail UI 연결 상태를 확인해주세요."
                );
            }
        }

        private List<SequenceNoteData> GenerateRandomNotes()
        {
            List<SequenceNoteData> randomNotes =
                new List<SequenceNoteData>();

            float currentSpawnTime = Mathf.Max(0f, initialDelay);
            bool hasDoubleClick = false;
            float regularMin = Mathf.Max(0.2f, Mathf.Min(minInterval, maxInterval));
            float regularMax = Mathf.Max(regularMin, Mathf.Max(minInterval, maxInterval));
            float doubleMin = Mathf.Max(0.2f, Mathf.Min(doubleClickInterval.x, doubleClickInterval.y));
            float doubleMax = Mathf.Max(doubleMin, Mathf.Max(doubleClickInterval.x, doubleClickInterval.y));

            while (randomNotes.Count < TotalNoteCount)
            {
                SeasoningKey key = UnityEngine.Random.value > 0.5f ? SeasoningKey.Salt : SeasoningKey.Pepper;
                randomNotes.Add(new SequenceNoteData { keyType = key, spawnTime = currentSpawnTime });

                bool canAddPair = randomNotes.Count < TotalNoteCount;
                bool ensurePair = !hasDoubleClick && randomNotes.Count == TotalNoteCount - 1;
                if (canAddPair && doubleClickChance > 0f &&
                    (ensurePair || UnityEngine.Random.value < doubleClickChance))
                {
                    currentSpawnTime += UnityEngine.Random.Range(doubleMin, doubleMax);
                    randomNotes.Add(new SequenceNoteData { keyType = key, spawnTime = currentSpawnTime });
                    hasDoubleClick = true;
                }

                // Resume a normal interval after each pair, preventing accidental runs of three or more.
                float interval = UnityEngine.Random.Range(regularMin, regularMax)
                    * UnityEngine.Random.Range(1f - intervalVariation, 1f + intervalVariation);
                currentSpawnTime += Mathf.Max(0.2f, interval);
            }

            return randomNotes;
        }

        private void Update()
        {
            if (IsQTECompleted)
            {
                return;
            }

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                }

                if (_mainCamera == null)
                {
                    return;
                }

                Vector2 mousePosition =
                    Mouse.current.position.ReadValue();

                bool clickedSalt = CookingClickArea.Contains(_mainCamera, salt3DObject,
                    mousePosition, seasoningClickPadding, out float saltScore);
                bool clickedPepper = CookingClickArea.Contains(_mainCamera, pepper3DObject,
                    mousePosition, seasoningClickPadding, out float pepperScore);
                if (clickedSalt || clickedPepper)
                {
                    if (clickedSalt && (!clickedPepper || saltScore <= pepperScore))
                    {
                        AnimateSeasoning(
                            salt3DObject,
                            saltParticle
                        );

                        railUI?.CheckInput(
                            SeasoningKey.Salt,
                            OnHitResultReceived
                        );
                    }
                    else
                    {
                        AnimateSeasoning(
                            pepper3DObject,
                            pepperParticle
                        );

                        railUI?.CheckInput(
                            SeasoningKey.Pepper,
                            OnHitResultReceived
                        );
                    }
                }
            }
        }

        private void AnimateSeasoning(
            GameObject obj,
            ParticleSystem particle)
        {
            if (obj == null)
            {
                return;
            }

            StartCoroutine(
                CoShakeAnimation(obj)
            );

            if (particle != null)
            {
                particle.Play();
            }

            if (shakeAudioSource != null)
            {
                AudioClip clip = obj == pepper3DObject && pepperShakeClip != null
                    ? pepperShakeClip : shakeAudioSource.clip;
                if (clip != null) shakeAudioSource.PlayOneShot(clip);
            }
        }

        private IEnumerator CoShakeAnimation(
            GameObject obj)
        {
            Transform targetTransform =
                obj.transform;

            Quaternion originalRot =
                _initialRotations.ContainsKey(obj)
                    ? _initialRotations[obj]
                    : targetTransform.localRotation;

            Quaternion tiltRot =
                originalRot *
                Quaternion.Euler(
                    0f,
                    0f,
                    -30f
                );

            float elapsed = 0f;
            float duration = 0.08f;

            while (elapsed < duration)
            {
                targetTransform.localRotation =
                    Quaternion.Slerp(
                        originalRot,
                        tiltRot,
                        elapsed / duration
                    );

                elapsed +=
                    Time.deltaTime;

                yield return null;
            }

            elapsed = 0f;

            while (elapsed < duration)
            {
                targetTransform.localRotation =
                    Quaternion.Slerp(
                        tiltRot,
                        originalRot,
                        elapsed / duration
                    );

                elapsed +=
                    Time.deltaTime;

                yield return null;
            }

            targetTransform.localRotation =
                originalRot;
        }

        private void OnHitResultReceived(
            HitGrade grade)
        {
            switch (grade)
            {
                case HitGrade.Perfect:
                case HitGrade.Good:
                    SuccessCount++;

                    Debug.Log(
                        $"[SeasoningQTE] {grade}! (성공: {SuccessCount})"
                    );

                    break;

                case HitGrade.Miss:
                    MissCount++;

                    Debug.Log(
                        $"[SeasoningQTE] Miss! (실패: {MissCount})"
                    );

                    break;
            }
        }

        private void OnQTEFinished()
        {
            if (IsQTECompleted)
            {
                return;
            }

            IsQTECompleted = true;

            Debug.Log(
                $"[SeasoningQTE] 3단계 양념 미니게임 완료! 성공: {SuccessCount}, 실패: {MissCount}"
            );

            _onQTEFinishedCallback?.Invoke();
        }
    }
}
