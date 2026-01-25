using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RaceSelectionUI : MonoBehaviour
{
    public GameObject panelRoot;
    [Header("Description Area")]
    public TextMeshProUGUI raceTitleText;
    public TextMeshProUGUI raceDescText;

    public void UpdateDescription(string race)
    {
        switch (race)
        {
            case "Humanic":
                raceTitleText.text = "휴머닉 (Humanic)";
                raceDescText.text = "밸런스 잡힌 능력치와 다양한 전술.\n초보자에게 추천합니다.";
                raceTitleText.color = new Color(0.3f, 0.6f, 1f); // 파란색
                break;
            case "Demonic":
                raceTitleText.text = "데모닉 (Demonic)";
                raceDescText.text = "높은 재생력과 파괴적인 자폭 공격.\n공격적인 운영에 특화되었습니다.";
                raceTitleText.color = new Color(1f, 0.3f, 0.3f); // 빨간색
                break;
            case "Angelic":
                raceTitleText.text = "엔젤릭 (Angelic)";
                raceDescText.text = "자가 재생되는 보호막과 강력한 화력.\n소수 정예 유닛을 운용합니다.";
                raceTitleText.color = new Color(1f, 0.8f, 0.2f); // 금색
                break;
            case "Random":
                raceTitleText.text = "무작위 (Random)";
                raceDescText.text = "운명에 맡깁니다.\n자신 있는 분들만 선택하세요!";
                raceTitleText.color = Color.white;
                break;
        }
    }

    // Click 시 호출될 함수 (Button OnClick에 연결)
    public void OnClickRace(string raceName)
    {
        if (GameManager.I != null)
        {
            GameManager.I.SelectRaceAndStart(raceName);
            panelRoot.SetActive(false); // UI 끄기
        }
    }
}