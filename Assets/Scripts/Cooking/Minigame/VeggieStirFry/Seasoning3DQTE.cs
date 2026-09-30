using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class Seasoning3DQTE : MonoBehaviour
    {
        [Header("3D 양념통 오브젝트")]
        [SerializeField] private GameObject salt3DObject;   // 3D 소금통
        [SerializeField] private GameObject pepper3DObject; // 3D 후추통

        [Header("리듬 레일 UI 연동")]
        [SerializeField] private SequenceInputRailUI railUI;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            // New Input System 마우스 좌클릭 감지
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (_mainCamera == null) _mainCamera = Camera.main;
                if (_mainCamera == null) return;

                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // 3D 소금통 클릭
                    if (hit.transform.gameObject == salt3DObject)
                    {
                        AnimateSeasoning(salt3DObject);
                        railUI?.CheckInput(SeasoningKey.Salt, null);
                    }
                    // 3D 후추통 클릭
                    else if (hit.transform.gameObject == pepper3DObject)
                    {
                        AnimateSeasoning(pepper3DObject);
                        railUI?.CheckInput(SeasoningKey.Pepper, null);
                    }
                }
            }
        }

        private void AnimateSeasoning(GameObject obj)
        {
            Debug.Log($"[Seasoning3DQTE] {obj.name} 클릭됨 - 양념 투입 연출!");
        }
    }
}
