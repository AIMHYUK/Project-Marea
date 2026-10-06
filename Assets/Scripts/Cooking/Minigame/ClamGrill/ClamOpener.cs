using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개가 그릴 위에서 "열리는" 연출의 공통 바탕. 조개 프리팹 루트에 붙는다. (+10/6)
    ///
    /// PopupTouchGame은 슬롯이 열릴 때 모습에 이게 있으면 프리팹을 바로 갈아 끼우지 않고 Open()을 부른다.
    /// ReplaceWhenDone이면 연출이 끝난 뒤 slotOpenPrefabs로 갈아 끼우고(전복 — 뒤집힌 모습), 아니면 그대로 둔다(가리비 — 뚜껑만 열림).
    /// </summary>
    public abstract class ClamOpener : MonoBehaviour
    {
        /// <summary>연출이 끝나면 열린 모습(slotOpenPrefabs)으로 갈아 끼울지.</summary>
        public virtual bool ReplaceWhenDone => false;

        /// <summary>열기 시작한다. 끝나면 onFinished(없어도 된다). 이미 열렸으면 아무것도 안 한다.</summary>
        public abstract void Open(Action onFinished);

        public void Open() => Open(null);
    }
}
