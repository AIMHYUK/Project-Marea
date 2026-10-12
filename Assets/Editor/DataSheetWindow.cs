using UnityEditor;
using UnityEngine;

/// <summary>
/// 구글 시트 가져오기를 탭으로 모아 둔 창. (+10/12)
/// 메뉴 탭만 실제로 동작한다(MenuSheetImporter.ImportMenus). 작물 · 재료 탭은 자리만 잡아 둔 것.
/// </summary>
public class DataSheetWindow : EditorWindow
{
    private static readonly string[] Tabs = { "메뉴", "작물", "재료" };
    private int tab;

    [MenuItem("Marea/데이터/데이터 시트 창")]
    private static void Open() => GetWindow<DataSheetWindow>("데이터 시트");

    private void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, Tabs);
        EditorGUILayout.Space();

        if (tab == 0)
        {
            EditorGUILayout.LabelField("시트: MenuData 탭");
            EditorGUILayout.LabelField("옮기는 칸: Name · MenuTier · BasePrice", EditorStyles.miniLabel);
            if (GUILayout.Button("시트에서 가져오기")) MenuSheetImporter.ImportMenus();
        }
        else
        {
            EditorGUILayout.HelpBox($"{Tabs[tab]} 가져오기는 준비 중이다.", MessageType.Info);
        }
    }
}
