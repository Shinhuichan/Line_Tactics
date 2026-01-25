using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum BotResponsiveness { Stubborn, Reactive }
public enum ExpansionPolicy { ForwardBase, SafeExpand }

public class EnemyBot : MonoBehaviour
{
    [HideInInspector] public EnemyProductionManager production;
    [HideInInspector] public EnemyTacticsManager tactics;
    [HideInInspector] public EnemyScoutManager scout;

    [Header("🤖 AI Strategy Pools (Random Pick)")]
    public List<BotStrategyData> humanicStrategyPool;
    public List<BotStrategyData> demonicStrategyPool;

    [Header("Current Active Strategy")]
    public BotStrategyData activeStrategy; 

    [Header("Bot Personality")]
    public BotResponsiveness responsiveness = BotResponsiveness.Reactive;
    public string myTeamTag = "Enemy";

    [Header("🛠️ Developer & Tester Options")]
    public bool useDebugOverrides = false;
    public UnitRace debugRaceOverride = UnitRace.Humanic; 
    public BotStrategyData forceSpecificStrategy;

    private Queue<BuildStep> executionQueue = new Queue<BuildStep>();
    private bool isOpeningFinished = false;
    private bool hasLoadedOpening = false; 
    
    private TacticalState lastTacticalState = TacticalState.Defend;

    // ⚖️ 런타임 가중치 관리 리스트
    private List<BuildStep> runtimeMidGameBuildList = new List<BuildStep>();

    [HideInInspector] public int currentWaveIndex = 0;
    [HideInInspector] public float gameTime = 0f;

    private static EnemyBot _instance;
    public static TacticalState enemyState => (_instance && _instance.tactics) ? _instance.tactics.currentState : TacticalState.Defend;
    public static Vector3 enemyFrontLinePos => (_instance && _instance.tactics) ? _instance.tactics.enemyFrontLinePos : Vector3.zero;
    
    public BotStrategyData Strategy => activeStrategy;
    public bool IsOpeningFinished => isOpeningFinished;

    void Awake()
    {
        _instance = this;
        production = GetComponent<EnemyProductionManager>();
        tactics = GetComponent<EnemyTacticsManager>();
        scout = GetComponent<EnemyScoutManager>();
    }

    void Start()
    {
        InitializeStrategy();

        production.Initialize(this);
        tactics.Initialize(this);
        scout.Initialize(this);

        FillProductionQueue();
    }

    void InitializeStrategy()
    {
        if (GameManager.I == null) return;

        UnitRace myRace = GameManager.I.enemyRace;

        if (useDebugOverrides)
        {
            myRace = debugRaceOverride;
            Debug.LogWarning($"🤖 [EnemyBot] Debug Mode Active! Forcing Race to: {myRace}");
        }

        List<BotStrategyData> targetPool = (myRace == UnitRace.Demonic) ? demonicStrategyPool : humanicStrategyPool;

        if (useDebugOverrides && forceSpecificStrategy != null)
        {
            activeStrategy = forceSpecificStrategy;
            Debug.LogWarning($"🤖 [EnemyBot] Strategy Forced: {activeStrategy.name}");
        }
        else
        {
            if (targetPool != null && targetPool.Count > 0)
            {
                int rnd = Random.Range(0, targetPool.Count);
                activeStrategy = targetPool[rnd];
                Debug.Log($"🤖 EnemyBot Selected Strategy: {activeStrategy.name} (Race: {myRace})");
            }
            else
            {
                Debug.LogError($"🚫 [{myRace}] 종족을 위한 전략이 Pool에 없습니다!");
            }
        }

        // ⚖️ [신규] 전략 선택 후 런타임 리스트 초기화
        InitializeRuntimeBuildList();
    }

    void InitializeRuntimeBuildList()
    {
        runtimeMidGameBuildList.Clear();
        if (activeStrategy != null)
        {
            foreach (var step in activeStrategy.midGameComposition)
            {
                // 사용자가 Inspector에서 실수로 Expansion을 넣었더라도, 코드로 동적 처리하므로 여기서 제외할 수도 있음.
                // 하지만 사용자가 "리스트에서 뺄 것"이라고 했으므로 그대로 둠.
                runtimeMidGameBuildList.Add(step);
            }
        }
    }

    void Update()
    {
        if (!GameManager.I.isGameStarted || GameManager.I.IsGameOver) return;
        
        gameTime += Time.deltaTime;

        scout.OnUpdate();
        tactics.OnUpdate();
        production.OnUpdate();

        CheckAttackWaves();
        FillProductionQueue();
        MonitorStrategyStatus();
    }

    void MonitorStrategyStatus()
    {
        if (activeStrategy == null || activeStrategy.fallbackStrategy == null) return;

        bool shouldSwitch = false;
        string switchReason = "";

        if (activeStrategy.transitionTimeLimit > 0 && gameTime >= activeStrategy.transitionTimeLimit)
        {
            shouldSwitch = true;
            switchReason = "Time Limit Exceeded";
        }

        if (!shouldSwitch && activeStrategy.switchOnAttackFailure)
        {
            if (lastTacticalState == TacticalState.Attack && tactics.currentState == TacticalState.Defend)
            {
                shouldSwitch = true;
                switchReason = "Attack Failed (Retreat)";
            }
        }

        lastTacticalState = tactics.currentState;

        if (shouldSwitch)
        {
            SwitchStrategy(activeStrategy.fallbackStrategy, switchReason);
        }
    }

    void SwitchStrategy(BotStrategyData newStrategy, string reason)
    {
        if (newStrategy == null) return;
        activeStrategy = newStrategy;
        production.ClearQueue();
        executionQueue.Clear();
        isOpeningFinished = false;
        hasLoadedOpening = false;
        currentWaveIndex = 0; 
        InitializeRuntimeBuildList();
        FillProductionQueue();
    }

    void CheckAttackWaves()
    {
        if (activeStrategy == null) return;
        if (currentWaveIndex >= activeStrategy.attackWaves.Count) return;

        AttackWave nextWave = activeStrategy.attackWaves[currentWaveIndex];

        if (gameTime >= nextWave.timing)
        {
            if (tactics.TryTriggerWave(nextWave))
            {
                currentWaveIndex++;
                InitializeRuntimeBuildList(); // 웨이브 발동 시 가중치 초기화
            }
        }
    }

    // 🌟 [핵심] 생산 큐 채우기 로직 (스마트 확장 & 배수진 포함)
    void FillProductionQueue()
    {
        if (activeStrategy == null) return;

        // 1. 오프닝 빌드 (기존 유지)
        if (!isOpeningFinished) 
        {
            if (!hasLoadedOpening && activeStrategy.openingBuildOrder.Count > 0)
            {
                foreach (var step in activeStrategy.openingBuildOrder)
                {
                    if (step.stepType == BuildStepType.Unit && step.count > 1)
                        for (int i = 0; i < step.count; i++) executionQueue.Enqueue(step);
                    else
                        executionQueue.Enqueue(step);
                }
                hasLoadedOpening = true; 
            }

            if (executionQueue.Count > 0)
            {
                if (production.GetQueueCount() < 3) production.EnqueueStep(executionQueue.Dequeue());
            }
            else if (production.GetQueueCount() == 0)
            {
                isOpeningFinished = true;
                Debug.Log("🤖 EnemyBot: Opening Finished. Switching to Macro Mode.");
                InitializeRuntimeBuildList();
            }
        }
        else
        {
            // 2. 중반 운영 (스마트 확장 시스템 적용)
            if (production.GetQueueCount() < 2 && runtimeMidGameBuildList.Count > 0)
            {
                // ⛺ [스마트 확장 체크]
                // 1. 경제 상황 분석
                int remainingIron = GetTotalRemainingIron();
                int currentIron = EnemyResourceManager.I != null ? EnemyResourceManager.I.currentIron : 0;
                
                UnitData outpostData = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                int outpostCost = outpostData != null ? outpostData.ironCost : 300;

                // 배수진 판정: 보유 자원 + 남은 자원 <= Outpost 가격
                bool isCriticalEconomicSituation = (currentIron + remainingIron) <= outpostCost;

                if (isCriticalEconomicSituation)
                {
                    // 🔥 [배수진] 가망이 없음 -> 확장 아니면 올인
                    if (ConstructionManager.I.HasFreeSpot())
                    {
                        // 부지가 있다 -> 당장 확장해라! (큐 비우고 확장 최우선)
                        Debug.Log("🤖 [EnemyBot] Critical Economy! Forcing Expansion (Last Stand).");
                        production.ClearQueue();
                        BuildStep expansionStep = new BuildStep { stepType = BuildStepType.Expansion, weight = 1000f };
                        production.EnqueueStep(expansionStep);
                    }
                    else
                    {
                        // 부지가 없다 -> 공격만이 살 길이다! (올인 러쉬)
                        Debug.Log("🤖 [EnemyBot] No Land Left! All-In Attack Triggered!");
                        // 강제 공격 태세 전환
                        tactics.LaunchAllOutAttack(); 
                    }
                    return; // 배수진 상황에서는 일반 생산 로직 스킵
                }

                // ⚖️ [동적 경쟁] 확장을 포함한 후보 리스트 생성
                List<BuildStep> candidates = new List<BuildStep>(runtimeMidGameBuildList);

                // 확장 가중치 계산
                float expansionWeight = CalculateExpansionWeight(currentIron, remainingIron, outpostCost);
                
                // 확장 스텝 생성 및 추가
                BuildStep dynamicExpansion = new BuildStep { stepType = BuildStepType.Expansion, weight = expansionWeight };
                candidates.Add(dynamicExpansion);

                // 가중치 추첨
                BuildStep pickedStep = GetWeightedRandomStep(candidates);

                // 선택된 유닛이 다음 웨이브 필수 유닛인지 확인하고 가중치 보정 (Pity System)
                UpdateWeightsForNextWave(pickedStep);

                if (pickedStep.stepType == BuildStepType.Unit)
                    for (int i = 0; i < pickedStep.count; i++) production.EnqueueStep(pickedStep);
                else
                    production.EnqueueStep(pickedStep);
            }
        }
    }

    // 💰 남은 철재 총량 계산
    int GetTotalRemainingIron()
    {
        int total = 0;
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl != null && baseCtrl.isConstructed && baseCtrl.CompareTag(myTeamTag))
            {
                total += baseCtrl.GetSurroundingResourceAmount(ResourceType.Iron);
            }
        }
        return total;
    }

    // 📈 확장 가중치 동적 계산
    float CalculateExpansionWeight(int currentIron, int remainingIron, int outpostCost)
    {
        // 기본값
        float weight = activeStrategy.expansionBaseWeight;
        
        // 자원이 줄어들수록 가중치 증가
        // 공식: 민감도 * (최대치 - 현재 잔여량)
        // 여기서 최대치 기준은 대략적으로 Outpost 가격의 5배(1500) 정도로 가정하거나, 
        // 단순히 (OutpostCost - (Current + Remaining))으로 갈 수도 있음.
        // 기획 의도: "잔여 자원이 소모될 수록 가중치 증가"
        
        // 안전한 기준점: Outpost 3개 분량(900)보다 적으면 위기감 조성
        float scarcity = Mathf.Max(0, (outpostCost * 3) - (currentIron + remainingIron)); 
        
        weight += scarcity * activeStrategy.expansionSensitivity;

        return Mathf.Max(1f, weight); // 최소 1 보장
    }

    void UpdateWeightsForNextWave(BuildStep pickedStep)
    {
        if (pickedStep.stepType == BuildStepType.Expansion) return; // 확장은 웨이브 필수 요소가 아님

        if (currentWaveIndex >= activeStrategy.attackWaves.Count) return;
        AttackWave nextWave = activeStrategy.attackWaves[currentWaveIndex];
        
        bool isRequiredAndMissing = false;

        if (pickedStep.stepType == BuildStepType.Unit)
        {
            foreach (var req in nextWave.requiredUnits)
            {
                if (req.unitType == pickedStep.unitType)
                {
                    int currentCount = GetMyUnitCount(req.unitType);
                    if (currentCount <= req.count) isRequiredAndMissing = true;
                    break;
                }
            }
        }

        if (!isRequiredAndMissing)
        {
            HashSet<UnitType> missingTypes = new HashSet<UnitType>();
            foreach(var req in nextWave.requiredUnits)
            {
                int currentCount = GetMyUnitCount(req.unitType);
                if (currentCount < req.count) missingTypes.Add(req.unitType);
            }

            if (missingTypes.Count > 0)
            {
                for (int i = 0; i < runtimeMidGameBuildList.Count; i++)
                {
                    BuildStep step = runtimeMidGameBuildList[i];
                    if (step.stepType == BuildStepType.Unit && missingTypes.Contains(step.unitType))
                    {
                        step.weight *= 1.125f; 
                        runtimeMidGameBuildList[i] = step;
                    }
                }
            }
        }
    }

    int GetMyUnitCount(UnitType type)
    {
        int count = 0;
        foreach (var unit in UnitController.activeUnits)
        {
            if (unit != null && !unit.isDead && unit.CompareTag(myTeamTag) && unit.unitType == type)
                count++;
        }
        return count;
    }

    BuildStep GetWeightedRandomStep(List<BuildStep> options)
    {
        if (options == null || options.Count == 0) return default;
        float totalWeight = 0;
        foreach (var step in options) totalWeight += (step.weight <= 0) ? 1 : step.weight;
        float rnd = Random.Range(0, totalWeight);
        foreach (var step in options)
        {
            float w = (step.weight <= 0) ? 1 : step.weight;
            if (rnd < w) return step;
            rnd -= w;
        }
        return options[0]; 
    }
}