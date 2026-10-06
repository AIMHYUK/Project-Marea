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
        [SerializeField] private int totalNoteCount = 15;      // 생성할 총 노트 개수
        [SerializeField] private float initialDelay = 1.5f;     // 첫 노트가 나오는 대기 시간 (초)
        [SerializeField] private float minInterval = 0.8f;      // 노트 간 최소 간격 (초)
        [SerializeField] private float maxInterval = 1.4f;      // 노트 간 최대 간격 (초)

        public bool IsQTECompleted { get; private set; }
        public int SuccessCount { get; private set; }
        public int MissCount { get; private set; }
        public int TotalNoteCount => totalNoteCount;

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

            float currentSpawnTime =
                initialDelay;

            for (int i = 0; i < totalNoteCount; i++)
            {
                SequenceNoteData noteData =
                    new SequenceNoteData
                    {
                        // 💡 UnityEngine.Random 명시
                        keyType =
                            (UnityEngine.Random.value > 0.5f)
                                ? SeasoningKey.Salt
                                : SeasoningKey.Pepper,

                        spawnTime =
                            currentSpawnTime
                    };

                randomNotes.Add(noteData);

                currentSpawnTime +=
                    UnityEngine.Random.Range(
                        minInterval,
                        maxInterval
                    );
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

                bool clickedSalt = VeggieClickArea.Contains(_mainCamera, salt3DObject,
                    mousePosition, seasoningClickPadding, out float saltScore);
                bool clickedPepper = VeggieClickArea.Contains(_mainCamera, pepper3DObject,
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
