using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// (+10/9) 아주 단순한 저장 — persistentDataPath/marea_save.txt 에 "키=값" 한 줄씩.
    /// 지우면 처음부터. (+10/9) 에디터에선 읽지도 쓰지도 않는다 — 플레이할 때마다 1일차 처음부터. shortcut: 일차가 바뀔 때만 쓴다 — 하루 중간 진행(골드 · 밭 등)은 안 남는다. 본 저장이 생기면 갈아탄다.
    /// </summary>
    public static class SaveFile
    {
        public static readonly string FilePath = Path.Combine(Application.persistentDataPath, "marea_save.txt");

        public static string Get(string key, string fallback = "")
        {
            if (Application.isEditor || !File.Exists(FilePath)) return fallback;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq) == key) return line.Substring(eq + 1);
            }
            return fallback;
        }

        public static int GetInt(string key, int fallback) => int.TryParse(Get(key), out int v) ? v : fallback;

        public static void Set(string key, object value)
        {
            if (Application.isEditor) return;
            var lines = new List<string>(File.Exists(FilePath) ? File.ReadAllLines(FilePath) : new string[0]);
            lines.RemoveAll(l => l.StartsWith(key + "="));
            lines.Add($"{key}={value}");
            File.WriteAllLines(FilePath, lines);
        }
    }
}
