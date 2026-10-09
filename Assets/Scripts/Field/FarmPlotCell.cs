using System.Collections.Generic;
using Marea.Core;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 밭 한 칸. 클릭을 받아 FarmManager에 넘기고, 매니저가 시키는 대로 모습만 바꾼다. (+9/28, 이슈 72)
    ///
    /// InteractableBase를 상속하는 이유는 클릭 때문뿐이다 — ClickSelector가
    /// GetComponentInParent&lt;InteractableBase&gt;()로 대상을 찾아서, 상속이 없으면 클릭이 안 잡힌다.
    /// 상태·로직은 여기 없다.
    ///
    /// (+9/30) 작물에 단계 모델(CropData.StagePrefabs)이 있으면 심을 때 전 단계를 한 번에 만들어
    /// 두고 켜고 끄기만 한다. 수확하면 지운다. 단계마다 Instantiate/Destroy하는 것보다 싸고,
    /// 밭이 모든 작물의 모델을 미리 들고 있을 필요도 없다. 단계 모델이 없는 작물은 기존 싹이 자란다.
    /// </summary>
    public class FarmPlotCell : InteractableBase
    {
        [Tooltip("자라는 모습. 성장률만큼 세로로 커진다. 모델을 갈아끼워도 된다.")]
        [SerializeField] private Transform sprout;

        [Tooltip("다 자랐을 때 켜는 표식 전체.")]
        [SerializeField] private GameObject readyMarker;

        [Tooltip("표식 안의 재료 아이콘. 재료에 아이콘이 있으면 이걸 쓴다.")]
        [SerializeField] private SpriteRenderer readyIcon;

        [Tooltip("(+10/9) 수확 아이콘의 월드 크기(m, 긴 변). 말풍선은 이 크기 × bubblePadding. 재료 아이콘은 512px · PPU 100이라 그대로 그리면 5.12m가 된다 — "
               + "스프라이트 해상도와 상관없이 이 크기로 맞춘다.")]
        [SerializeField, Min(0.05f)] private float readyIconSize = 0.75f;

        [Tooltip("(+10/9) 아이콘 뒤 말풍선(손님 주문 말풍선과 같은 balloon_blank). 아이콘이 있을 때만 켠다. 비우면 아이콘만.")]
        [SerializeField] private SpriteRenderer readyBubble;
        [Tooltip("말풍선 폭 = 아이콘 크기 × 이 값.")]
        [SerializeField, Min(1f)] private float bubblePadding = 1.45f;
        [Tooltip("말풍선 아래 꼬리 높이 비율(그림 높이 대비). 아이콘을 꼬리 뺀 몸통 가운데로 올린다.")]
        [SerializeField, Range(0f, 0.5f)] private float bubbleTailRatio = 0.18f;

        [Tooltip("재료 아이콘이 없을 때 대신 보일 임시 표식.")]
        [SerializeField] private GameObject readyFallback;

        [Tooltip("싹이 처음 보일 때 크기 비율 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float minSproutScale = 0.2f;

        [Tooltip("단계 모델을 붙일 자리. 비우면 이 칸 자신에 붙는다. (+9/30)")]
        [SerializeField] private Transform stageAnchor;

        private FarmManager _manager;
        private Vector3 _sproutFullScale = Vector3.one;
        private readonly List<GameObject> _stages = new();   // (+9/30) 지금 심긴 작물의 단계 모델

        private void Awake()
        {
            _manager = GetComponentInParent<FarmManager>();
            if (_manager == null)
                Debug.LogError($"{name}: 부모에 FarmManager가 없다. 클릭해도 아무 일도 안 일어난다.", this);

            if (sprout != null) _sproutFullScale = sprout.localScale;
        }

        public override bool CanInteract(IInteractor actor) => _manager != null && _manager.CanInteract(this);

        // (+9/28, 이슈 75) 성장 중엔 CanInteract가 false라 라벨이 안 뜬다.
        public override string InteractLabel(IInteractor actor)
            => _manager != null && _manager.StateOf(this) == FarmManager.PlotState.Ready ? "수확" : "심기";

        public override void Interact(IInteractor actor)
        {
            if (_manager != null) _manager.Interact(this);
        }

        /// <summary>
        /// 심을 때 한 번 부른다. 이 작물의 단계 모델을 전부 만들어 꺼 둔다. (+9/30)
        /// 비어 있거나 null이면 기존 싹으로 자란다.
        /// </summary>
        public void Plant(IReadOnlyList<GameObject> stagePrefabs)
        {
            ClearStages();
            if (stagePrefabs == null) return;

            Transform parent = stageAnchor != null ? stageAnchor : transform;
            foreach (GameObject prefab in stagePrefabs)
            {
                if (prefab == null)
                {
                    Debug.LogError($"{name}: 작물 단계 모델에 빈 칸이 있다. 그 단계는 안 보인다.", this);
                    continue;
                }
                GameObject stage = Instantiate(prefab, parent, false);
                stage.SetActive(false);
                _stages.Add(stage);
            }
        }

        public void ShowEmpty()
        {
            ClearStages();
            if (sprout != null) sprout.gameObject.SetActive(false);
            if (readyMarker != null) readyMarker.SetActive(false);
        }

        public void ShowGrowing(float t)
        {
            if (_stages.Count > 0)
            {
                // 마지막 단계는 다 자란 모습이라 성장 중엔 그 앞까지만 쓴다. 단계가 하나뿐이면 그걸 쓴다.
                int growingStages = Mathf.Max(1, _stages.Count - 1);
                ShowStage(Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(t) * growingStages), growingStages - 1));
            }
            else if (sprout != null)
            {
                sprout.gameObject.SetActive(true);
                float s = Mathf.Lerp(minSproutScale, 1f, Mathf.Clamp01(t));
                sprout.localScale = new Vector3(_sproutFullScale.x, _sproutFullScale.y * s, _sproutFullScale.z);
            }
            if (readyMarker != null) readyMarker.SetActive(false);
        }

        public void ShowReady(Sprite icon)
        {
            if (_stages.Count > 0)
            {
                ShowStage(_stages.Count - 1);
            }
            else if (sprout != null)
            {
                sprout.gameObject.SetActive(true);
                sprout.localScale = _sproutFullScale;
            }
            if (readyMarker == null) return;

            readyMarker.SetActive(true);
            bool hasIcon = icon != null && readyIcon != null;
            if (readyIcon != null)
            {
                readyIcon.sprite = icon;
                readyIcon.gameObject.SetActive(hasIcon);
                if (hasIcon) FitIcon(icon);
            }
            if (readyBubble != null)
            {
                readyBubble.gameObject.SetActive(hasIcon);
                if (hasIcon) FitBubble();
            }
            if (readyFallback != null) readyFallback.SetActive(!hasIcon);
        }

        /// <summary>(+10/9) 아이콘 긴 변이 readyIconSize(월드 m)가 되게 크기를 맞춘다. 부모 크기는 나눠서 뺀다.</summary>
        private void FitIcon(Sprite icon)
        {
            Vector3 size = icon.bounds.size;   // 스케일 1일 때 월드 크기(= 픽셀 / PPU)
            float longest = Mathf.Max(size.x, size.y);
            if (longest <= 0f) return;
            Transform parent = readyIcon.transform.parent;
            float parentScale = parent != null ? Mathf.Max(parent.lossyScale.x, parent.lossyScale.y) : 1f;
            readyIcon.transform.localScale = Vector3.one * (readyIconSize / longest / Mathf.Max(parentScale, 0.0001f));
        }

        /// <summary>
        /// (+10/9) 말풍선을 아이콘보다 bubblePadding배 크게, 꼬리를 뺀 몸통 가운데에 아이콘이 오게 놓는다.
        /// 말풍선이 아이콘 뒤에 그려지게 정렬 순서를 하나 낮추고 살짝 뒤로 뺀다.
        /// </summary>
        private void FitBubble()
        {
            Sprite bubble = readyBubble.sprite;
            if (bubble == null) return;
            Vector3 size = bubble.bounds.size;
            if (size.x <= 0f) return;
            Transform parent = readyBubble.transform.parent;
            float parentScale = parent != null ? Mathf.Max(parent.lossyScale.x, parent.lossyScale.y) : 1f;
            float scale = readyIconSize * bubblePadding / size.x / Mathf.Max(parentScale, 0.0001f);
            readyBubble.transform.localScale = Vector3.one * scale;

            float worldHeight = size.y * scale * parentScale;
            // 그림 가운데가 원점이라 꼬리 절반만큼 올리면 몸통 가운데가 아이콘 자리(원점)에 온다.
            readyBubble.transform.localPosition = new Vector3(0f, -worldHeight * bubbleTailRatio * 0.5f / parentScale, 0.02f);
            if (readyIcon != null) readyBubble.sortingOrder = readyIcon.sortingOrder - 1;
        }

        // (+10/9) 카메라를 돌릴 수 있게 돼서 고정 방향 그림은 옆에서 얇아진다 — 수확 표시는 늘 카메라를 본다.
        private void LateUpdate()
        {
            if (readyMarker == null || !readyMarker.activeInHierarchy) return;
            Camera cam = Camera.main;
            if (cam != null) readyMarker.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }

        private void ShowStage(int index)
        {
            if (sprout != null) sprout.gameObject.SetActive(false);
            for (int i = 0; i < _stages.Count; i++)
                if (_stages[i] != null) _stages[i].SetActive(i == index);
        }

        private void ClearStages()
        {
            foreach (GameObject stage in _stages)
                if (stage != null) Destroy(stage);
            _stages.Clear();
        }
    }
}
