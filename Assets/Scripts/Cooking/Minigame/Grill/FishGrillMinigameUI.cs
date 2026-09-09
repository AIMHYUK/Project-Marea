using TMPro;
using UnityEngine;

namespace Marea.Cooking
{
    public class FishGrillMinigameUI : MonoBehaviour
    {
        [Header("루트 패널")]
        [SerializeField] private GameObject rootCanvas;

        [Header("안내 문구")]
        [SerializeField] private TextMeshProUGUI txtGuide;

        [Header("결과 창")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI txtResultGrade;

        public void Open()
        {
            if (rootCanvas != null) rootCanvas.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (txtGuide != null) txtGuide.text = "알맞게 익었을 때 클릭하여 뒤집으세요!";
        }

        public void Close()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        public void ShowResult(HitGrade grade)
        {
            if (resultPanel != null) resultPanel.SetActive(true);
            if (txtResultGrade != null)
            {
                txtResultGrade.text = grade switch
                {
                    HitGrade.Miss => "FAIL",
                    _ => grade.ToString().ToUpper()
                };

                txtResultGrade.color = grade switch
                {
                    HitGrade.Perfect => Color.yellow,
                    HitGrade.Good => Color.green,
                    HitGrade.Bad => new Color(1f, 0.5f, 0f),
                    _ => Color.red
                };
            }
        }
    }
}
