using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Cooking
{
    public enum SeasoningKey { Salt, Pepper, Sauce }

    [Serializable]
    public struct SequenceNoteData
    {
        public SeasoningKey keyType;
        public float spawnTime; // 등장 타임라인 (초)
    }

    public class SequenceInputRailUI : MonoBehaviour
    {
        [Header("레일 UI 참고 요소")]
        [SerializeField] private RectTransform railContainer;      // 노트가 스폰되어 담길 부모 패널
        [SerializeField] private RectTransform spawnZoneTransform; // 노트 생성 위치 (우측)
        [SerializeField] private RectTransform hitZoneTransform;   // 판정선 위치 (좌측)
        [SerializeField] private SequenceNoteUI notePrefab;        // 노트 프리팹

        [Header("노트 이동 속도 및 판정")]
        [SerializeField] private float noteSpeed = 300f;        // 노트 이동 속도 (픽셀/초)
        [SerializeField] private float perfectTolerance = 35f;  // Perfect 허용 오차 (픽셀)
        [SerializeField] private float goodTolerance = 75f;     // Good 허용 오차 (픽셀)

        private readonly List<SequenceNoteUI> _activeNotes = new();
        private Action<HitGrade> _onHitCallback;
        private Action _onCompleteCallback;
        private bool _isRunning;
        private float _elapsedTime;
        private bool _isAllNotesSpawned;

        public bool IsRunning => _isRunning;

        public void StartRail(List<SequenceNoteData> notes, Action<HitGrade> onHitResult, Action onComplete = null)
        {
            ResetRail();
            _onHitCallback = onHitResult;
            _onCompleteCallback = onComplete;
            _isRunning = true;
            _elapsedTime = 0f;
            _isAllNotesSpawned = false;

            StartCoroutine(CoRunRailPattern(notes));
        }

        public void ResetRail()
        {
            StopAllCoroutines();
            _isRunning = false;
            _isAllNotesSpawned = false;

            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                if (_activeNotes[i] != null) Destroy(_activeNotes[i].gameObject);
            }
            _activeNotes.Clear();
        }

        private IEnumerator CoRunRailPattern(List<SequenceNoteData> notes)
        {
            int noteIndex = 0;

            while (_isRunning)
            {
                _elapsedTime += Time.deltaTime;

                // 1. 타임라인에 따른 노트 스폰
                while (noteIndex < notes.Count && _elapsedTime >= notes[noteIndex].spawnTime)
                {
                    SpawnNote(notes[noteIndex]);
                    noteIndex++;
                }

                if (noteIndex >= notes.Count)
                {
                    _isAllNotesSpawned = true;
                }

                // 2. 노트 이동 및 지나침(Miss) 검사
                UpdateNotesPosition();

                // 3. 모든 노트 스폰 및 처리 완료 시 미니게임 종료
                if (_isAllNotesSpawned && _activeNotes.Count == 0)
                {
                    _isRunning = false;
                    Debug.Log("[SequenceInputRailUI] 모든 노트 처리 완료! QTE 미니게임 종료.");
                    _onCompleteCallback?.Invoke();
                    yield break;
                }

                yield return null;
            }
        }

        private void SpawnNote(SequenceNoteData data)
        {
            if (notePrefab == null || railContainer == null) return;

            SequenceNoteUI noteInstance = Instantiate(notePrefab, railContainer);

            // SpawnZone의 위치 지정 (없으면 기본 우측 배치)
            Vector2 spawnPos = spawnZoneTransform != null
                ? spawnZoneTransform.anchoredPosition
                : new Vector2(250f, 0f);

            noteInstance.SetupNote(data.keyType, spawnPos);
            _activeNotes.Add(noteInstance);
        }

        private void UpdateNotesPosition()
        {
            float hitZoneX = hitZoneTransform != null ? hitZoneTransform.anchoredPosition.x : -200f;

            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                SequenceNoteUI note = _activeNotes[i];
                if (note == null) continue;

                // 좌측 방향(X축 감소)으로 이동
                Vector2 pos = note.Rect.anchoredPosition;
                pos.x -= noteSpeed * Time.deltaTime;
                note.Rect.anchoredPosition = pos;

                // 노트가 HitZone을 거쳐 좌측(goodTolerance 넘어)으로 완전히 빠져나갔을 때만 Miss 처리
                if (pos.x < (hitZoneX - goodTolerance))
                {
                    _onHitCallback?.Invoke(HitGrade.Miss);
                    DestroyNote(note);
                }
            }
        }

        public void CheckInput(SeasoningKey inputKey, Action<HitGrade> onHitResult = null)
        {
            if (!_isRunning || _activeNotes.Count == 0) return;

            Action<HitGrade> callback = onHitResult ?? _onHitCallback;

            // 가장 먼저 판정선에 가까워진 노트(첫번째 노트) 선택
            SequenceNoteUI targetNote = _activeNotes[0];
            float hitZoneX = hitZoneTransform != null ? hitZoneTransform.anchoredPosition.x : -200f;

            // 절대 거리 오차 계산
            float distance = Mathf.Abs(targetNote.Rect.anchoredPosition.x - hitZoneX);

            if (targetNote.KeyType == inputKey)
            {
                if (distance <= perfectTolerance)
                {
                    callback?.Invoke(HitGrade.Perfect);
                    DestroyNote(targetNote);
                }
                else if (distance <= goodTolerance)
                {
                    callback?.Invoke(HitGrade.Good);
                    DestroyNote(targetNote);
                }
                else
                {
                    // 판정선과 너무 먼 거리에서 잘못 누른 경우
                    callback?.Invoke(HitGrade.Miss);
                }
            }
            else
            {
                // 소금/후추 키를 다르게 누른 경우 Miss
                callback?.Invoke(HitGrade.Miss);
            }
        }

        private void DestroyNote(SequenceNoteUI note)
        {
            _activeNotes.Remove(note);
            if (note != null) Destroy(note.gameObject);
        }
    }
}
