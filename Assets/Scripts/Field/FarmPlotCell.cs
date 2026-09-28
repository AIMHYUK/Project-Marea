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
    /// </summary>
    public class FarmPlotCell : InteractableBase
    {
        [Tooltip("자라는 모습. 성장률만큼 세로로 커진다. 모델을 갈아끼워도 된다.")]
        [SerializeField] private Transform sprout;

        [Tooltip("다 자랐을 때 켜는 표식 전체.")]
        [SerializeField] private GameObject readyMarker;

        [Tooltip("표식 안의 재료 아이콘. 재료에 아이콘이 있으면 이걸 쓴다.")]
        [SerializeField] private SpriteRenderer readyIcon;

        [Tooltip("재료 아이콘이 없을 때 대신 보일 임시 표식.")]
        [SerializeField] private GameObject readyFallback;

        [Tooltip("싹이 처음 보일 때 크기 비율 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float minSproutScale = 0.2f;

        private FarmManager _manager;
        private Vector3 _sproutFullScale = Vector3.one;

        private void Awake()
        {
            _manager = GetComponentInParent<FarmManager>();
            if (_manager == null)
                Debug.LogError($"{name}: 부모에 FarmManager가 없다. 클릭해도 아무 일도 안 일어난다.", this);

            if (sprout != null) _sproutFullScale = sprout.localScale;
        }

        public override bool CanInteract(IInteractor actor) => _manager != null && _manager.CanInteract(this);

        public override void Interact(IInteractor actor)
        {
            if (_manager != null) _manager.Interact(this);
        }

        public void ShowEmpty()
        {
            if (sprout != null) sprout.gameObject.SetActive(false);
            if (readyMarker != null) readyMarker.SetActive(false);
        }

        public void ShowGrowing(float t)
        {
            if (sprout != null)
            {
                sprout.gameObject.SetActive(true);
                float s = Mathf.Lerp(minSproutScale, 1f, Mathf.Clamp01(t));
                sprout.localScale = new Vector3(_sproutFullScale.x, _sproutFullScale.y * s, _sproutFullScale.z);
            }
            if (readyMarker != null) readyMarker.SetActive(false);
        }

        public void ShowReady(Sprite icon)
        {
            if (sprout != null)
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
            }
            if (readyFallback != null) readyFallback.SetActive(!hasIcon);
        }
    }
}
