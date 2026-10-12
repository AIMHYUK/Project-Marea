using System;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 도움말 내용. (+10/12) 분류 · 항목 · 페이지(그림 + 글). 글은 TMP 서식(&lt;color&gt; 등)을 쓴다.
    /// 안 변하는 값만 든다 — 지금 몇 번째 항목을 보는지는 HelpWindow가 든다.
    /// 항목을 늘리거나 고칠 땐 이 에셋만 고치면 된다(코드는 안 바뀐다).
    /// </summary>
    [CreateAssetMenu(fileName = "HelpBook", menuName = "Marea/도움말")]
    public class HelpBook : ScriptableObject
    {
        [Serializable]
        public class Page
        {
            [Tooltip("위쪽 그림. 비우면 그림 칸을 숨긴다.")]
            public Sprite image;
            [TextArea(3, 8)] public string text;
        }

        [Serializable]
        public class Topic
        {
            public string title;
            [Tooltip("categories 중 하나. 탭으로 거른다.")]
            public string category;
            public Page[] pages;
        }

        [Tooltip("탭 순서. 맨 앞에 '전체' 탭이 따로 붙는다.")]
        public string[] categories;
        public Topic[] topics;
    }
}
