using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Cooking
{
    public class PanStirCookingController : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        [Header("오브젝트 바인딩")]
        [SerializeField] private GameObject clickableVeggieObject; // 클릭할 3D 야채
        [SerializeField] private Transform panInsideTransform;     // 프라이팬 내부 투입 지점
        [SerializeField] private Transform spatulaTransform;      // 3D 주걱 오브젝트

        [Header("수치 설정")]
        [SerializeField] private float requiredCookProgress = 100f; // 목표 조리 진행도
        [SerializeField] private float burnRate = 15f;               // 안 저을 때 초당 타는 게이지 증가량
        [SerializeField] private float cookRate = 25f;               // 저을 때 초당 조리 게이지 증가량

        public bool IsVeggieInPan { get; private set; }
        public bool IsCookCompleted { get; private set; }
        public float CookProgress { get; private set; }
        public float BurnProgress { get; private set; }

        private Vector3 _lastMousePos;
        private bool _isStirringThisFrame;

        public void ResetStep()
        {
            IsVeggieInPan = false;
            IsCookCompleted = false;
            CookProgress = 0f;
            BurnProgress = 0f;
            if (clickableVeggieObject != null) clickableVeggieObject.SetActive(true);
        }

        // 야채 3D 오브젝트 클릭 시 프라이팬 내부로 투입
        public void OnClickVeggieToPan()
        {
            if (IsVeggieInPan) return;

            IsVeggieInPan = true;
            if (clickableVeggieObject != null)
            {
                clickableVeggieObject.transform.position = panInsideTransform.position;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastMousePos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsVeggieInPan || IsCookCompleted) return;

            Vector3 delta = (Vector3)eventData.position - _lastMousePos;
            if (delta.sqrMagnitude > 5f)
            {
                _isStirringThisFrame = true;

                // 주걱 위치 드래그 이동
                if (spatulaTransform != null)
                {
                    spatulaTransform.position += new Vector3(delta.x, 0, delta.y) * 0.005f;
                }
            }
            _lastMousePos = eventData.position;
        }

        private void Update()
        {
            if (!IsVeggieInPan || IsCookCompleted) return;

            if (_isStirringThisFrame)
            {
                CookProgress += cookRate * Time.deltaTime;
                BurnProgress = Mathf.Max(0f, BurnProgress - (burnRate * 0.5f * Time.deltaTime));
            }
            else
            {
                // 주걱을 젓지 않으면 야채가 탐
                BurnProgress += burnRate * Time.deltaTime;
            }

            _isStirringThisFrame = false;

            if (CookProgress >= requiredCookProgress)
            {
                IsCookCompleted = true;
            }
        }
    }
}
