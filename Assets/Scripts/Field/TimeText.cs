using UnityEngine;

namespace Marea.Field
{
    /// <summary>남은 시간 표기 "m:ss". 탐사와 농사가 같이 쓴다. (+9/28)</summary>
    public static class TimeText
    {
        /// <summary>남은 초를 올림해서 "1:05"처럼 쓴다. 0 이하면 "0:00".</summary>
        public static string Format(float remainingSeconds)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            return $"{sec / 60}:{sec % 60:00}";
        }
    }
}
