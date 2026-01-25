using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // 🌟 TMP 사용을 위해 추가

public class GameManager : SingletonBehaviour<GameManager>
{
    protected override bool IsDontDestroy() => true;

    [Header("Game State")]
    public UnitRace playerRace;
    public UnitRace enemyRace;
    public bool isGameStarted = false;

    // 🌟 [신규] 상태 확인용 (외부에서 읽기 가능)
    public bool IsGameOver => isGameOver;
    private bool isGameOver = false;

    [Header("UI 연결")]
    // 🌟 [신규] 시간을 표시할 텍스트 (Inspector에서 연결)
    public TextMeshProUGUI gameTimerText; 

    // ⏱️ [신규] 실제 플레이 시간 누적 변수
    private float playTimeTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        Time.timeScale = 0f; // 시작 전 정지
    }

    private void Update()
    {
        // ⏱️ [신규] 게임이 시작되었고, 끝나지 않았을 때만 시간 흐름
        if (isGameStarted && !isGameOver)
        {
            // Time.deltaTime은 timeScale의 영향을 받으므로, 
            // 일시정지 시에는 자동으로 멈추고 배속 시에는 빨리 흐릅니다.
            playTimeTimer += Time.deltaTime;
            
            UpdateTimerUI();
        }
    }

    // ⏱️ [신규] UI 갱신 로직 (00:00 포맷)
    void UpdateTimerUI()
    {
        if (gameTimerText == null) return;

        int minutes = Mathf.FloorToInt(playTimeTimer / 60F);
        int seconds = Mathf.FloorToInt(playTimeTimer % 60F);

        // "00:00" 형태로 텍스트 갱신
        gameTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void SelectRaceAndStart(string raceName)
    {
        // 1. 플레이어 종족
        if (raceName == "Random")
        {
            int rnd = Random.Range(0, 2);
            playerRace = (rnd == 0) ? UnitRace.Humanic : UnitRace.Demonic;
        }
        else
        {
            if (System.Enum.TryParse(raceName, out UnitRace parsedRace))
                playerRace = parsedRace;
            else
                playerRace = UnitRace.Humanic;
        }

        // 2. 적 종족 (반대 종족 or 랜덤)
        enemyRace = (playerRace == UnitRace.Humanic) ? UnitRace.Demonic : UnitRace.Humanic;

        StartGame();
    }

    void StartGame()
    {
        isGameStarted = true;
        playTimeTimer = 0f; 
        Time.timeScale = 1f;

        Debug.Log($"🎮 Game Start! Player: {playerRace}");

        // 🌟 기지 초기화 및 UI 갱신 호출
        if (ConstructionManager.I != null) 
        {
            ConstructionManager.I.InitializeStartingBases(playerRace, enemyRace);
            
            // 🌟 [추가] 건설 버튼 텍스트도 종족에 맞게 변경
            ConstructionManager.I.UpdateBuildButtonUI();
        }
        else
        {
            Debug.LogError("ConstructionManager가 없습니다! 초기화 실패.");
        }

        if (UpgradeUI.I != null) UpgradeUI.I.RefreshUI(); 
        if (SpawnManager.I != null) SpawnManager.I.RefreshUnitButtons(); 
    }

    public void OnGameEnd(bool isPlayerWin)
    {
        if (isGameOver) return;
        isGameOver = true;
        // 게임 종료 시 시간은 멈추지 않음 (배경 등 연출을 위해). 
        // 하지만 Update문 조건에 의해 타이머 UI 갱신은 여기서 멈춤.

        string header = isPlayerWin ? "<color=#50bcdf>VICTORY!</color>" : "<color=#ff5050>DEFEAT...</color>";
        string content = isPlayerWin 
            ? "적 기지를 파괴했습니다.\n<b>'R' 키를 눌러 재시작</b>" 
            : "아군 기지가 파괴되었습니다.\n<b>'R' 키를 눌러 재시작</b>";

        if (TooltipManager.I != null) 
            TooltipManager.I.Show(content, header, true);

        Debug.Log(isPlayerWin ? "승리!" : "패배...");
    }
}