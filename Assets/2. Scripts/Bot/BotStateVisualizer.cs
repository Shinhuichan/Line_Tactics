using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BotStateVisualizer : MonoBehaviour
{
    private EnemyBot bot;
    private EnemyTacticsManager tactics;
    private EnemyScoutManager scout;
    private EnemyProductionManager production;

    [Header("Debug Settings")]
    public bool showGizmos = true;
    public Vector3 textOffset = new Vector3(0, 3, 0);

    void Start()
    {
        bot = GetComponent<EnemyBot>();
        tactics = GetComponent<EnemyTacticsManager>();
        scout = GetComponent<EnemyScoutManager>();
        production = GetComponent<EnemyProductionManager>();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || bot == null || tactics == null) return;

#if UNITY_EDITOR
        string stateInfo = $"[State: {tactics.currentState}]\n";
        
        // 1. 전투력 비교
        if (scout != null)
        {
            float myPower = tactics.CalculateMyCombatPower();
            float enemyPower = scout.enemyTotalPower;
            stateInfo += $"Power: {myPower:F0} vs {enemyPower:F0}\n";
        }

        // 2. 생산/경제 정보 (핵심 디버깅)
        if (production != null)
        {
            string nextItem = production.GetNextItemName();
            ResourceType? missing = production.GetMissingResourceForNextItem();
            string missingStr = missing.HasValue ? $"<color=red>Need: {missing}</color>" : "<color=green>Ready</color>";

            stateInfo += $"Next: {nextItem}\n";
            stateInfo += $"{missingStr}\n";
            
            if (EnemyResourceManager.I != null)
            {
                stateInfo += $"Res: {EnemyResourceManager.I.currentIron}Fe / {EnemyResourceManager.I.currentOil}Oil";
            }
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.richText = true; // 컬러 태그 사용

        Handles.Label(transform.position + textOffset, stateInfo, style);
#endif
    }
}