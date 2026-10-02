using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Hud
{
    /// <summary>
    /// UI 버튼을 누르면 클릭음. 씬에 하나만 둔다. (+10/2, 이슈 92)
    ///
    /// 버튼마다 소리를 붙이지 않는 이유: 창고 칸 · 작물 줄처럼 런타임에 만들어지는 버튼이
    /// 많아서, 붙이는 쪽을 빠뜨리면 그 버튼만 조용해진다. 여기서는 좌클릭 순간 포인터
    /// 아래의 UI를 직접 보고 판단하므로 언제 만들어진 버튼이든 같다.
    ///
    /// 꺼진 버튼(골드 부족 등)을 누르면 거절음을 낸다 — "왜 안 눌리지"를 소리로 알려준다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class UiClickSound : MonoBehaviour
    {
        [SerializeField] private AudioClip clickClip;
        [Tooltip("꺼진 버튼을 눌렀을 때. 비우면 아무 소리도 안 낸다.")]
        [SerializeField] private AudioClip rejectClip;

        private AudioSource _source;
        private readonly List<RaycastResult> _hits = new();
        private PointerEventData _pointer;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            if (clickClip == null) Debug.LogError($"{name}: UiClickSound.clickClip이 비어 있다. 클릭음이 안 난다.", this);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            EventSystem events = EventSystem.current;
            if (mouse == null || events == null || !mouse.leftButton.wasPressedThisFrame) return;

            _pointer ??= new PointerEventData(events);
            _pointer.position = mouse.position.ReadValue();
            _hits.Clear();
            events.RaycastAll(_pointer, _hits);

            // 맨 위 UI 하나만 본다. 그게 버튼의 자식(글자·아이콘)이어도 부모 버튼을 찾는다.
            // 월드 오브젝트(PhysicsRaycaster 결과)는 UI가 아니므로 건너뛴다.
            foreach (RaycastResult hit in _hits)
            {
                if (hit.module is not GraphicRaycaster) continue;

                Selectable target = hit.gameObject.GetComponentInParent<Selectable>();
                if (target == null) return;

                AudioClip clip = target.IsInteractable() ? clickClip : rejectClip;
                if (clip != null) _source.PlayOneShot(clip);
                return;
            }
        }
    }
}
