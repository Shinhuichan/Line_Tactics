using UnityEngine;
using System.Collections.Generic;

// 🏗️ 빌드 단계 타입 정의
public enum BuildStepType 
{ 
    Unit,       // 유닛 생산
    Upgrade,    // 업그레이드 연구
    Expansion   // ⛺ 확장 기지 건설
}

[System.Serializable]
public struct BuildStep
{
    public BuildStepType stepType; 
    
    [Tooltip("유닛 생산일 경우 설정")]
    public UnitType unitType;
    [Tooltip("유닛 생산일 경우 마리 수")]
    public int count;

    [Tooltip("업그레이드일 경우 설정")]
    public UpgradeData upgradeData;

    // ⚖️ [신규] 생산 가중치 (기본값 10)
    [Range(1, 100)]
    [Tooltip("중반 랜덤 생산 시 선택될 확률 가중치입니다. (높을수록 자주 생산)")]
    public float weight; 
}

[System.Serializable]
public struct AttackWave
{
    public float timing;
    public float requiredPowerRatio;
    public List<UnitCountPair> requiredUnits;

    // 🏳️ [신규] 후퇴 임계점 (0.0 ~ 1.0)
    // 0.0: 전멸할 때까지 싸움 (Power <= 0)
    // 0.5: 전력이 절반으로 줄어들면 후퇴
    // 0.8: 전력이 20%만 줄어들어도 바로 후퇴 (치고 빠지기)
    [Range(0f, 1f)]
    [Tooltip("전투 시작 시점 대비 현재 전력이 이 비율 이하로 떨어지면 후퇴합니다. (0=전멸시까지, 1=즉시후퇴)")]
    public float retreatThreshold; 
}

[System.Serializable]
public struct UnitCountPair
{
    public UnitType unitType;
    public int count;
}

[CreateAssetMenu(fileName = "NewBotStrategy", menuName = "AI/Bot Strategy Data")]
public class BotStrategyData : ScriptableObject
{
    [Header("전략 정보")]
    public UnitRace strategyRace; 
    [TextArea] public string strategyDescription;

    [Header("🔄 Plan B: 전략 전환 (Strategy Chaining)")]
    [Tooltip("이 전략이 실패하거나 시간이 지나면 전환할 다음 전략 (비워두면 전환 안 함)")]
    public BotStrategyData fallbackStrategy;

    [Tooltip("게임 시작 후 이 시간이 지나면 자동으로 전략 전환 (0 = 시간 제한 없음)")]
    public float transitionTimeLimit = 0f;

    [Tooltip("공격(러쉬)을 갔다가 퇴각하게 되면(실패 시) 즉시 전략 전환")]
    public bool switchOnAttackFailure = true;

    // ---------------------------------------------------------
    // 🏗️ 1. 통합 빌드 오더
    // ---------------------------------------------------------
    [Header("1. 초반 빌드 오더 (순서대로 실행, 가중치 무시)")]
    public List<BuildStep> openingBuildOrder = new List<BuildStep>();

    [Header("2. 중반 이후 생산 (가중치 기반 랜덤 순환)")]
    public List<BuildStep> midGameComposition = new List<BuildStep>();

    // ---------------------------------------------------------
    // ⛺ 3. 스마트 확장 (Smart Expansion)
    // ---------------------------------------------------------
    [Header("3. 스마트 확장 설정 (Smart Expansion)")]
    [Tooltip("확장의 기본 가중치입니다. (자원이 풍족할 때의 확장 욕구)")]
    public float expansionBaseWeight = 10f;

    [Tooltip("자원 결핍 민감도입니다. 높을수록 자원이 줄어들 때 확장 확률이 급격히 올라갑니다.")]
    public float expansionSensitivity = 0.5f; 

    // ---------------------------------------------------------
    // ⚔️ 공격 웨이브 설정
    // ---------------------------------------------------------
    [Header("⚔️ 공격 웨이브 설정")]
    public List<AttackWave> attackWaves = new List<AttackWave>();

    // ---------------------------------------------------------
    // ⚙️ 경제 설정
    // ---------------------------------------------------------
    [Header("경제 설정")]
    public int idealWorkerCount = 15; 
    public ExpansionPolicy expansionPolicy = ExpansionPolicy.SafeExpand; 
}