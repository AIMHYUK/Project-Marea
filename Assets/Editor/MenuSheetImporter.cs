using System;
using System.Collections.Generic;
using System.Text;
using Marea.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트 MenuData 탭 → 메뉴 에셋(MenuData). (+10/9, 이슈 128 시험)
///
/// Unity에 구글 시트 전용 API는 없다. 시트를 "링크가 있는 모든 사용자 보기"로 공유하면
/// docs.google.com/spreadsheets/d/{시트}/export?format=csv&gid={탭} 에서 인증 없이 CSV를 받을 수 있어
/// UnityWebRequest로 받아 파싱한다. 비공개 시트는 Google Sheets API(v4) + 키가 필요하다.
///
/// 행 ↔ 에셋은 MenuData.sheetId(= 시트의 MenuID)로 잇는다. 영문 헤더 줄(MenuID, Name …)을 찾아 열 위치를 정하므로
/// 시트에서 열 순서를 바꿔도 된다. 지금 옮기는 칸: Name → displayName, MenuTier → menuTier, BasePrice → basePrice.
/// Station · MiniGame1~3ID · 아이콘 · 프리팹 키는 우리 쪽에 같은 체계가 없어 안 옮긴다(로그에만).
/// 바뀌는 값은 Undo로 되돌릴 수 있고 콘솔에 전 → 후로 남는다.
/// </summary>
public static class MenuSheetImporter
{
    private const string SheetId = "1pJa8bYjXheHTiyAgk9KN36ykGiGBZ5Lfz-7QrtH_EsY";
    private const string MenuTabGid = "2090244055";   // MenuData 탭
    private const int TimeoutSeconds = 15;

    [MenuItem("Marea/데이터/시트에서 메뉴 가져오기")]
    public static void ImportMenus()
    {
        string url = $"https://docs.google.com/spreadsheets/d/{SheetId}/export?format=csv&gid={MenuTabGid}";
        if (!TryDownload(url, out string csv)) return;

        List<string[]> rows = ParseCsv(csv);
        int header = rows.FindIndex(r => Array.IndexOf(r, "MenuID") >= 0);
        if (header < 0)
        {
            Debug.LogError("[시트 가져오기] 영문 헤더 줄(MenuID …)을 못 찾았다. 시트 모양이 바뀌었는지 확인할 것.");
            return;
        }
        string[] head = rows[header];
        int cId = Array.IndexOf(head, "MenuID"), cName = Array.IndexOf(head, "Name"),
            cTier = Array.IndexOf(head, "MenuTier"), cPrice = Array.IndexOf(head, "BasePrice");

        var assets = new Dictionary<string, MenuData>();
        foreach (string guid in AssetDatabase.FindAssets("t:MenuData"))
        {
            var menu = AssetDatabase.LoadAssetAtPath<MenuData>(AssetDatabase.GUIDToAssetPath(guid));
            if (menu != null && !string.IsNullOrWhiteSpace(menu.SheetId)) assets[menu.SheetId.Trim()] = menu;
        }

        var log = new StringBuilder("[시트 가져오기] MenuData 탭\n");
        int changed = 0, skipped = 0;
        for (int i = header + 1; i < rows.Count; i++)
        {
            string key = Cell(rows[i], cId);
            if (string.IsNullOrEmpty(key)) continue;
            if (!assets.TryGetValue(key, out MenuData menu))
            {
                log.AppendLine($"  {key}: 이 sheetId를 가진 메뉴 에셋이 없어 건너뜀");
                skipped++;
                continue;
            }

            var so = new SerializedObject(menu);
            var diffs = new List<string>();
            SetString(so, "displayName", Cell(rows[i], cName), diffs);
            SetTier(so, Cell(rows[i], cTier), diffs, key, log);
            SetInt(so, "basePrice", Cell(rows[i], cPrice), diffs, key, log);

            if (diffs.Count == 0)
            {
                log.AppendLine($"  {key} → {menu.name}: 같음");
                continue;
            }
            Undo.RecordObject(menu, "시트에서 메뉴 가져오기");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(menu);
            changed++;
            log.AppendLine($"  {key} → {menu.name}: {string.Join(", ", diffs)}");
        }
        AssetDatabase.SaveAssets();
        log.Append($"바뀐 메뉴 {changed}개 · 에셋 없음 {skipped}개");
        Debug.Log(log.ToString());
    }

    private static bool TryDownload(string url, out string text)
    {
        text = null;
        using var req = UnityWebRequest.Get(url);
        req.timeout = TimeoutSeconds;
        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        DateTime until = DateTime.Now.AddSeconds(TimeoutSeconds + 1);
        while (!op.isDone && DateTime.Now < until)
            EditorUtility.DisplayProgressBar("시트 가져오기", "구글 시트에서 CSV 받는 중…", req.downloadProgress);
        EditorUtility.ClearProgressBar();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[시트 가져오기] 받기 실패: {req.error} — 시트가 '링크가 있는 모든 사용자 보기'로 공유됐는지 확인할 것. {url}");
            return false;
        }
        text = req.downloadHandler.text;
        if (text.TrimStart().StartsWith("<"))
        {
            Debug.LogError($"[시트 가져오기] CSV 대신 HTML이 왔다 — 로그인 화면일 가능성(비공개 시트). {url}");
            return false;
        }
        return true;
    }

    private static string Cell(string[] row, int col) => col >= 0 && col < row.Length ? row[col].Trim() : "";

    private static void SetString(SerializedObject so, string field, string value, List<string> diffs)
    {
        if (string.IsNullOrEmpty(value)) return;
        SerializedProperty p = so.FindProperty(field);
        if (p.stringValue == value) return;
        diffs.Add($"{field} '{p.stringValue}' → '{value}'");
        p.stringValue = value;
    }

    private static void SetInt(SerializedObject so, string field, string value, List<string> diffs, string key, StringBuilder log)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (!int.TryParse(value.Replace(",", ""), out int n))
        {
            log.AppendLine($"  {key}: {field} '{value}'는 숫자가 아니라 건너뜀");
            return;
        }
        SerializedProperty p = so.FindProperty(field);
        if (p.intValue == n) return;
        diffs.Add($"{field} {p.intValue} → {n}");
        p.intValue = n;
    }

    // 시트는 BASIC / ADVANCED, 우리 enum은 Basic / Advanced.
    private static void SetTier(SerializedObject so, string value, List<string> diffs, string key, StringBuilder log)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (!Enum.TryParse(value, true, out MenuTier tier))
        {
            log.AppendLine($"  {key}: MenuTier '{value}'는 MenuTier에 없어 건너뜀");
            return;
        }
        SerializedProperty p = so.FindProperty("menuTier");
        if (p.enumValueIndex == (int)tier) return;
        diffs.Add($"menuTier {(MenuTier)p.enumValueIndex} → {tier}");
        p.enumValueIndex = (int)tier;
    }

    /// <summary>따옴표 안의 쉼표 · 줄바꿈 · "" 를 처리하는 CSV 파서.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row.ToArray()); row.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
