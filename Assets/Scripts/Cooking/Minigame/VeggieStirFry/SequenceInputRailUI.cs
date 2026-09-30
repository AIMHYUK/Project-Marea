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
        [SerializeField] private RectTransform railContainer;
        [SerializeField] private RectTransform hitZoneTransform; // 판정선 위치
        [SerializeField] private SequenceNoteUI notePrefab;

        [Header("노트 이동 속도")]
        [SerializeField] private float noteSpeed = 300f;
        [SerializeField] private float hitTolerance = 40f; // 판정 허용 오차 (픽셀)

        private readonly List<SequenceNoteUI> _activeNotes = new();
        private bool _isRunning;

        public void StartRail(List<SequenceNoteData> notes, Action<HitGrade> onHitResult)
        {
            _isRunning = true;
            // 노트 생성 및 레일 구동 실행
        }

        public void CheckInput(SeasoningKey inputKey, Action<HitGrade> onHitResult)
        {
            if (!_isRunning || _activeNotes.Count == 0) return;

            SequenceNoteUI targetNote = _activeNotes[0];
            float distance = Mathf.Abs(targetNote.Rect.anchoredPosition.x - hitZoneTransform.anchoredPosition.x);

            if (targetNote.KeyType == inputKey && distance <= hitTolerance)
            {
                onHitResult?.Invoke(HitGrade.Perfect);
                DestroyNote(targetNote);
            }
            else
            {
                onHitResult?.Invoke(HitGrade.Miss);
            }
        }

        private void DestroyNote(SequenceNoteUI note)
        {
            _activeNotes.Remove(note);
            if (note != null) Destroy(note.gameObject);
        }
    }
}
