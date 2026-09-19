using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Marea.Data
{
    /// <summary>
    /// 게임에 존재하는 모든 MenuData를 한 곳에 모은 참조 목록.
    /// Resources/Addressables 계층 대신 단일 SO 에셋에 명시적으로 등록하고 참조로 로드한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Menu Database", fileName = "MenuDatabase")]
    public class MenuDatabase : ScriptableObject
    {
        [SerializeField] private List<MenuData> menus = new();

        public IReadOnlyList<MenuData> Menus => menus;

        /// <summary>IsActive이고 지정 미니게임 ID와 일치하는 메뉴만 수집 (UI/주문 공용)</summary>
        public void CollectActive(MiniGameId id, List<MenuData> result)
        {
            result.Clear();
            for (int i = 0; i < menus.Count; i++)
            {
                MenuData m = menus[i];
                if (m != null && m.IsActive && m.MiniGameId == id)
                {
                    result.Add(m);
                }
            }
        }

        /// <summary>IsActive인 모든 메뉴를 수집</summary>
        public void CollectAllActive(List<MenuData> result)
        {
            result.Clear();
            for (int i = 0; i < menus.Count; i++)
            {
                MenuData m = menus[i];
                if (m != null && m.IsActive)
                {
                    result.Add(m);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("프로젝트 내 모든 MenuData 자동 수집")]
        public void FindAllMenuDataInProject()
        {
            menus.Clear();
            string[] guids = AssetDatabase.FindAssets("t:MenuData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MenuData data = AssetDatabase.LoadAssetAtPath<MenuData>(path);
                if (data != null && !menus.Contains(data))
                {
                    menus.Add(data);
                }
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MenuDatabase] 총 {menus.Count}개의 MenuData를 수집하여 등록했습니다.");
        }
#endif
    }
}
