using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Cooking
{
    /// <summary>조개 한 개의 드래그 받이. 이동량만 <see cref="ClamScrubGame"/>에 넘긴다. (+10/2)</summary>
    public class ClamScrubTarget : MonoBehaviour, IDragHandler
    {
        internal ClamScrubGame Game;

        public void OnDrag(PointerEventData eventData)
        {
            if (Game != null) Game.AddScrub(this, eventData.delta.magnitude);
        }
    }
}
