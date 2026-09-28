using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Marea.Field
{
    /// <summary>
    /// 월드 위치를 따라다니는 화면 라벨 — 탐사 남은 시간, 밭 남은 시간 같은 것. (+9/28)
    ///
    /// 월드 공간 텍스트를 쓰지 않는 이유: junhyuk_main 에서 탐사 타이머가 아트 천막(y 12.5)
    /// 아래 들어가 안 보였다. 화면 위에 그리면 아트가 뭘 덮어도 가려지지 않는다.
    ///
    /// 쓰는 쪽은 보이고 싶은 프레임마다 Set을 부른다. 그 프레임에 Set이 안 온 라벨은
    /// LateUpdate에서 꺼진다 — 끄는 걸 잊어서 라벨이 남는 일이 없다.
    /// </summary>
    public class WorldLabelUI : MonoBehaviour
    {
        [Tooltip("복제할 라벨. 오버레이 Canvas 안에 두고 꺼둔다. 자식에 TMP_Text가 있어야 한다.")]
        [SerializeField] private RectTransform template;

        private sealed class Entry
        {
            public RectTransform Rect;
            public TMP_Text Text;
            public Vector3 World;
            public int Frame;
        }

        private readonly Dictionary<Object, Entry> _entries = new();
        private readonly Stack<Entry> _pool = new();
        private readonly List<Object> _stale = new();
        private Camera _cam;

        private void Awake()
        {
            if (template == null)
                Debug.LogError($"{name}: WorldLabelUI.template이 비어 있다. 남은 시간 라벨이 안 뜬다.", this);
            else
                template.gameObject.SetActive(false);
        }

        /// <summary>이번 프레임에 key의 라벨을 world 위치에 text로 띄운다.</summary>
        public void Set(Object key, Vector3 world, string text)
        {
            if (template == null || key == null) return;

            if (!_entries.TryGetValue(key, out Entry e))
            {
                e = _pool.Count > 0 ? _pool.Pop() : Create();
                _entries[key] = e;
            }

            e.World = world;
            e.Frame = Time.frameCount;
            if (e.Text != null && e.Text.text != text) e.Text.text = text;
        }

        private Entry Create()
        {
            var rect = Instantiate(template, template.parent);
            return new Entry { Rect = rect, Text = rect.GetComponentInChildren<TMP_Text>(true) };
        }

        // 카메라가 따라 움직인 뒤에 위치를 잡는다 (ProximityInteractor와 같은 이유).
        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;

            _stale.Clear();
            foreach (var pair in _entries)
            {
                Entry e = pair.Value;
                if (e.Frame != Time.frameCount || _cam == null)
                {
                    _stale.Add(pair.Key);
                    continue;
                }

                Vector3 screen = _cam.WorldToScreenPoint(e.World);
                bool visible = screen.z > 0f;   // 카메라 뒤면 숨긴다
                if (visible) e.Rect.position = screen;
                if (e.Rect.gameObject.activeSelf != visible) e.Rect.gameObject.SetActive(visible);
            }

            // 이번 프레임에 안 불린 라벨은 끄고 풀에 돌려준다. key가 파괴된 경우도 여기서 정리된다.
            foreach (Object key in _stale)
            {
                Entry e = _entries[key];
                e.Rect.gameObject.SetActive(false);
                _entries.Remove(key);
                _pool.Push(e);
            }
        }
    }
}
