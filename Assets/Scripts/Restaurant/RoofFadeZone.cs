using System.Collections.Generic;
using UnityEngine;

namespace Marea.Environment
{
    [RequireComponent(typeof(Collider))]
    public class RoofFadeZone : MonoBehaviour
    {
        [Header("숨길 지붕/천장 오브젝트 목록")]
        [Tooltip("플레이어가 내부로 진입했을 때 비활성화할 지붕 오브젝트들을 등록합니다.")]
        [SerializeField] private List<GameObject> roofObjects = new();

        [Header("플레이어 감지 태그")]
        [SerializeField] private string playerTag = "Player";

        private int _occupantCount = 0; // 복수 진입(또는 트리거 중복) 방어용 카운터

        private void Awake()
        {
            // 콜라이더 트리거 강제 설정
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // 시작할 때는 지붕을 보이게 설정
            SetRoofVisibility(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _occupantCount++;
                if (_occupantCount == 1)
                {
                    SetRoofVisibility(false); // 지붕 숨기기
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _occupantCount = Mathf.Max(0, _occupantCount - 1);
                if (_occupantCount == 0)
                {
                    SetRoofVisibility(true); // 지붕 보이기
                }
            }
        }

        private void SetRoofVisibility(bool isVisible)
        {
            foreach (var roof in roofObjects)
            {
                if (roof != null)
                {
                    roof.SetActive(isVisible);
                }
            }
        }

        private void OnDrawGizmos()
        {
            // 에디터 씬 뷰에서 존 영역을 시각적으로 확인하기 위함
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
                if (col is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
        }
    }
}
