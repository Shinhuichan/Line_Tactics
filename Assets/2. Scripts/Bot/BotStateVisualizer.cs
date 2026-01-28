using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class BotStateVisualizer : MonoBehaviour
{
    [Header("설정")]
    public bool showDebugInfo = true;
    public Color playerColor = new Color(0.2f, 0.2f, 1f, 0.8f); // 파란색 배경
    public Color enemyColor = new Color(1f, 0.2f, 0.2f, 0.8f);  // 빨간색 배경

    [Header("UI 크기 조절")]
    [Range(10, 60)] public int fontSize = 14;   // 폰트 크기 (기본값 상향)
    [Range(200, 600)] public float boxWidth = 300f; // 박스 너비
    public float verticalSpacing = 250f;        // 박스 간 수직 간격

    // 캐싱된 봇 리스트
    private PlayerBot[] playerBots;
    private EnemyBot[] enemyBots;

    private void Start()
    {
        // 씬 시작 시 존재하는 봇들을 찾음
        RefreshBots();
    }

    // 주기적으로 봇 목록 갱신 (생성/파괴 대응)
    private float refreshTimer = 0f;
    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer > 2.0f)
        {
            refreshTimer = 0f;
            RefreshBots();
        }
    }

    void RefreshBots()
    {
        playerBots = FindObjectsByType<PlayerBot>(FindObjectsSortMode.None);
        enemyBots = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        // 1. PlayerBot 정보 표시 (좌측 상단)
        float currentY = 10f;
        if (playerBots != null)
        {
            foreach (var bot in playerBots)
            {
                if (bot == null || !bot.gameObject.activeInHierarchy) continue;
                
                // 너비를 boxWidth 변수로 사용
                DrawBotInfo(bot, new Rect(10, currentY, boxWidth, 0), playerColor, "PLAYER BOT");
                currentY += verticalSpacing; // 간격 변수 사용
            }
        }

        // 2. EnemyBot 정보 표시 (우측 상단)
        currentY = 10f;
        float screenW = Screen.width;
        if (enemyBots != null)
        {
            foreach (var bot in enemyBots)
            {
                if (bot == null || !bot.gameObject.activeInHierarchy) continue;
                
                // 우측 정렬: 전체 화면 너비 - (박스 너비 + 여백)
                float xPos = screenW - (boxWidth + 10);
                DrawBotInfo(bot, new Rect(xPos, currentY, boxWidth, 0), enemyColor, "ENEMY BOT");
                currentY += verticalSpacing;
            }
        }
    }

    // 봇 정보 그리기 (공용)
    void DrawBotInfo(MonoBehaviour botScript, Rect rect, Color boxColor, string title)
    {
        StringBuilder sb = new StringBuilder();

        // 1. 기본 참조 가져오기
        string strategyName = "None";
        string stateStr = "Unknown";
        float combatPower = 0f;
        int iron = 0, oil = 0;
        List<string> queueList = null;

        // PlayerBot인 경우
        if (botScript is PlayerBot pBot)
        {
            strategyName = pBot.activeStrategy != null ? pBot.activeStrategy.name : "No Strategy";
            if (pBot.tactics != null)
            {
                stateStr = pBot.tactics.currentFrontBase != null ? 
                    $"{pBot.tactics.currentFrontBase.name} (Siege?)" : "Mobile"; 
            }
            if (pBot.tactics != null) combatPower = pBot.tactics.CalculateMyCombatPower();
            
            // 자원 (Player용 ResourceManager)
            if (ResourceManager.I != null)
            {
                iron = ResourceManager.I.currentIron;
                oil = ResourceManager.I.currentOil;
            }

            if (pBot.production != null) queueList = pBot.production.GetBuildQueueNames();
        }
        // EnemyBot인 경우
        else if (botScript is EnemyBot eBot)
        {
            strategyName = eBot.activeStrategy != null ? eBot.activeStrategy.name : "No Strategy";
            if (eBot.tactics != null) stateStr = eBot.tactics.currentState.ToString();
            if (eBot.tactics != null) combatPower = eBot.tactics.CalculateMyCombatPower();

            // 자원 (EnemyResourceManager)
            if (EnemyResourceManager.I != null)
            {
                iron = EnemyResourceManager.I.currentIron;
                oil = EnemyResourceManager.I.currentOil;
            }

            if (eBot.production != null) queueList = eBot.production.GetBuildQueueNames();
        }

        // 2. 텍스트 구성
        sb.AppendLine($"<b>[{title}]</b>");
        sb.AppendLine($"Strategy: <color=yellow>{strategyName}</color>");
        sb.AppendLine($"State: {stateStr}");
        sb.AppendLine($"Power: {combatPower:F0}");
        sb.AppendLine($"Res: <color=cyan>{iron} Fe</color> / <color=black>{oil} Oil</color>");
        sb.AppendLine("--------------------------");
        sb.AppendLine("<b>[Build Queue]</b>");

        if (queueList != null && queueList.Count > 0)
        {
            int count = 0;
            foreach (string item in queueList)
            {
                if (count >= 5) 
                {
                    sb.AppendLine($"... (+{queueList.Count - 5} more)");
                    break;
                }
                sb.AppendLine($"- {item}");
                count++;
            }
        }
        else
        {
            sb.AppendLine("(Idle / Empty)");
        }

        // 3. GUI 그리기
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        
        // 🌟 [핵심] Inspector 설정값 적용
        style.fontSize = fontSize; 
        style.normal.textColor = Color.white;
        style.richText = true;
        style.wordWrap = true; // 내용이 길면 자동 줄바꿈
        
        // 배경색 설정
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = boxColor;

        // 높이 자동 조절 (폰트 크기에 따라 높이도 변해야 함)
        float height = style.CalcHeight(new GUIContent(sb.ToString()), rect.width);
        rect.height = height + 20f; // 넉넉한 패딩

        GUI.Box(rect, sb.ToString(), style);

        GUI.backgroundColor = oldColor; // 색상 복구
    }
}