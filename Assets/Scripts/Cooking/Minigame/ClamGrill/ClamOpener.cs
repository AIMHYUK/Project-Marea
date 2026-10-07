using System;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개가 그릴 위에서 "열리는" 연출의 공통 바탕. 조개 프리팹 루트에 붙는다. (+10/6)
    ///
    /// PopupTouchGame은 슬롯이 열릴 때 모습에 이게 있으면 프리팹을 바로 갈아 끼우지 않고 연출을 맡긴다.
    ///   SwapBeforeOpen  — 열린 모습(slotOpenPrefabs)으로 먼저 갈아 끼우고, 그 새 모습에 PlayOnSwapped로 연출(전복 — 살 있는 모델이 뒤집히며 내려앉음)
    ///   ReplaceWhenDone — 이 모습에서 Open 연출 뒤 열린 모습으로 갈아 끼움
    ///   둘 다 아님     — 이 모습 그대로 Open만(가리비 — 뚜껑만 열림)
    /// </summary>
    public abstract class ClamOpener : MonoBehaviour
    {
        /// <summary>연출이 끝나면 열린 모습(slotOpenPrefabs)으로 갈아 끼울지.</summary>
        public virtual bool ReplaceWhenDone => false;

        /// <summary>연출 전에 열린 모습으로 먼저 갈아 끼울지. 그러면 Open 대신 PlayOnSwapped가 불린다.</summary>
        public virtual bool SwapBeforeOpen => false;

        /// <summary>열기 시작한다. 끝나면 onFinished(없어도 된다). 이미 열렸으면 아무것도 안 한다.</summary>
        public abstract void Open(Action onFinished);

        public void Open() => Open(null);

        /// <summary>SwapBeforeOpen일 때 — 방금 갈아 끼운 새 모습에서 연출한다. 같은 프레임에 불린다(이 모습은 곧 지워진다).</summary>
        public virtual void PlayOnSwapped(GameObject swapped, Action onFinished) => onFinished?.Invoke();
    }
}
