// 개발용 — 스튜 증기 · 화구 효과를 고르고 나면 지운다. (+10/6, 이슈 117)
using System;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    /// <summary>
    /// 스튜 미니게임 중에 증기 · 화구 효과를 키로 바로 갈아 끼운다. StewMinigameController와 같은 오브젝트에 붙인다.
    ///
    /// VfxLibrary 칸을 바꾸면 매번 플레이를 다시 켜고 수프를 처음부터 만들어야 했다. 여기선 도는 중에 바뀐다.
    ///   F5 / F6 — 증기 이전 / 다음   (공용 Steam + 테스트 연기 201~299)
    ///   F7 / F8 — 화구 이전 / 다음   (공용 Flame + 테스트 불 301~399)
    /// 지금 고른 이름은 화면 왼쪽 위에 뜬다. 프리팹 값은 안 바뀐다 — 고른 이름을 VfxLibrary 공용 ID에 옮기는 건 손으로.
    /// 릴리스 빌드(Debug.isDebugBuild = false)에선 꺼진다.
    /// </summary>
    [RequireComponent(typeof(StewMinigameController))]
    public class StewVfxDebug : MonoBehaviour
    {
        [SerializeField] private Key steamPrev = Key.F5;
        [SerializeField] private Key steamNext = Key.F6;
        [SerializeField] private Key burnerPrev = Key.F7;
        [SerializeField] private Key burnerNext = Key.F8;

        private StewMinigameController _stew;
        private readonly List<VfxId> _steams = new();
        private readonly List<VfxId> _burners = new();
        private int _steamIndex, _burnerIndex;
        private GUIStyle _style;

        private void Awake()
        {
            _stew = GetComponent<StewMinigameController>();
            if (!Debug.isDebugBuild) { enabled = false; return; }

            // 후보는 enum에서 번호대로 뽑는다 — 테스트 ID를 더하거나 지워도 여기는 안 고친다.
            _steams.Add(VfxId.Steam);
            _burners.Add(VfxId.Flame);
            foreach (VfxId id in (VfxId[])Enum.GetValues(typeof(VfxId)))
            {
                int n = (int)id;
                if (n >= 200 && n < 300) _steams.Add(id);
                else if (n >= 300 && n < 400) _burners.Add(id);
            }
        }

        private void Start()
        {
            // 처음 칸은 지금 프리팹에 꽂힌 값 — 목록에 없으면 0번(공용)부터.
            _steamIndex = Mathf.Max(0, _steams.IndexOf(_stew.SteamVfx));
            _burnerIndex = Mathf.Max(0, _burners.IndexOf(_stew.BurnerVfx));
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            bool changed = false;
            if (kb[steamPrev].wasPressedThisFrame) { _steamIndex = Wrap(_steamIndex - 1, _steams.Count); changed = true; }
            if (kb[steamNext].wasPressedThisFrame) { _steamIndex = Wrap(_steamIndex + 1, _steams.Count); changed = true; }
            if (kb[burnerPrev].wasPressedThisFrame) { _burnerIndex = Wrap(_burnerIndex - 1, _burners.Count); changed = true; }
            if (kb[burnerNext].wasPressedThisFrame) { _burnerIndex = Wrap(_burnerIndex + 1, _burners.Count); changed = true; }
            if (!changed) return;

            _stew.DebugSetAmbience(_steams[_steamIndex], _burners[_burnerIndex]);
            Debug.Log($"[StewVfxDebug] 증기 {_steams[_steamIndex]} · 화구 {_burners[_burnerIndex]}");
        }

        private static int Wrap(int i, int count) => count == 0 ? 0 : (i % count + count) % count;

        // 미니게임 중에만 띄운다 — 평소 화면을 가리지 않게.
        private void OnGUI()
        {
            if (!_stew.AmbienceOn) return;
            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            string text = $"증기 [{steamPrev}/{steamNext}]  {_steams[_steamIndex]}  ({_steamIndex + 1}/{_steams.Count})\n"
                        + $"화구 [{burnerPrev}/{burnerNext}]  {_burners[_burnerIndex]}  ({_burnerIndex + 1}/{_burners.Count})";
            GUI.Box(new Rect(10, 10, 520, 60), text, _style);
        }
    }
}
