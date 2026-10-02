using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Marea.Hud
{
    /// <summary>
    /// 메인 화면 좌측의 알람 한 칸. "!" 와 한 줄 문구. (+10/2, 이슈 92)
    ///
    /// 알람을 내는 쪽이 이 컴포넌트를 모르게 하려고, 각 시스템은 이벤트만 내고
    /// <see cref="MainHud"/> 가 그걸 듣고 여기에 올린다.
    ///
    /// 여러 개가 겹치면 가장 나중에 올라온 것을 보여준다. 키(누가 올렸나)로 지우므로
    /// 탐사정 알람을 내리면서 다른 알람까지 지우는 일은 없다.
    /// </summary>
    public class HudAlarm : MonoBehaviour
    {
        [Tooltip("알람이 없을 때 통째로 끈다.")]
        [SerializeField] private GameObject root;

        [SerializeField] private TextMeshProUGUI label;

        private readonly List<(object key, string text)> _alarms = new();

        private void Awake()
        {
            if (root == null) Debug.LogError($"{name}: HudAlarm.root가 비어 있다. 알람을 켜고 끌 수 없다.", this);
            if (label == null) Debug.LogError($"{name}: HudAlarm.label이 비어 있다. 알람 문구가 안 뜬다.", this);
            Refresh();
        }

        /// <summary>같은 키로 다시 올리면 문구만 바꾸고 맨 위로 올린다.</summary>
        public void Post(object key, string text)
        {
            _alarms.RemoveAll(a => a.key == key);
            _alarms.Add((key, text));
            Refresh();
        }

        public void Clear(object key)
        {
            if (_alarms.RemoveAll(a => a.key == key) > 0) Refresh();
        }

        private void Refresh()
        {
            bool any = _alarms.Count > 0;
            if (root != null) root.SetActive(any);
            if (any && label != null) label.text = _alarms[^1].text;
        }
    }
}
