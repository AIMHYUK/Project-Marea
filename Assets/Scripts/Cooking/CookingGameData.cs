using Marea.Data; // MenuData 참조 추가

namespace Marea.Cooking
{
    public enum HitGrade
    {
        Miss,       // 실패
        Bad,        // 나쁨 (-10%)
        Good,       // 일반 성공 (+0%)
        Perfect     // 완벽 성공 (+10%)
    }

    public struct CookingResult
    {
        public bool isSuccess;
        public int finalPrice;
        public HitGrade bestGrade;
        public MenuData menuData; // 추가
    }
}
