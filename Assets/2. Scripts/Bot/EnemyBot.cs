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

    private List<BuildStep> runtimeMidGameBuildList = new List<BuildStep>();

    private float workerManageTimer = 0f;
    private const float WORKER_MANAGE_INTERVAL = 1.0f;

    [HideInInspector] public int currentWaveIndex = 0;
    [HideInInspector] public float gameTime = 0f;

    private static EnemyBot _instance;
    // 🌟 [수정] EnemyCommandManager를 통해 상태 반환
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

        // 🌟 [신규] 구조 통합: EnemyCommandManager가 없으면 추가
        if (EnemyCommandManager.I == null)
        {
            if (GetComponent<EnemyCommandManager>() == null)
            {
                gameObject.AddComponent<EnemyCommandManager>();
            }
        }
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

        InitializeRuntimeBuildList();
    }

    void InitializeRuntimeBuildList()
    {
        runtimeMidGameBuildList.Clear();
        if (activeStrategy != null)
        {
            foreach (var step in activeStrategy.midGameComposition)
            {
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
        
        ManageIdleWorkers();
    }

    void ManageIdleWorkers()
    {
        workerManageTimer += Time.deltaTime;
        if (workerManageTimer < WORKER_MANAGE_INTERVAL) return;
        workerManageTimer = 0f;

        foreach (var unit in UnitController.activeUnits)
        {
            if (unit == null || unit.isDead || !unit.CompareTag(myTeamTag)) continue;
            
            if (unit.unitType != UnitType.Worker && unit.unitType != UnitType.Slave) continue;

            WorkerAbility worker = unit.GetComponent<WorkerAbility>();
            if (worker == null) continue;

            if (worker.currentState == WorkerState.Idle)
            {
                bool needsMigration = false;
                
                if (worker.assignedBase == null)
                {
                    needsMigration = true;
                }
                else
                {
                    ResourceNode nearbyNode = worker.assignedBase.GetNearestResourceNode(worker.targetResourceType);
                    if (nearbyNode == null || nearbyNode.currentAmount <= 0)
                    {
                        needsMigration = true;
                    }
                }

                if (needsMigration)
                {
                    BaseController newBase = BaseController.FindNearestBaseWithResource(worker.targetResourceType, myTeamTag, worker.transform.position);

                    if (newBase != null && newBase != worker.assignedBase)
                    {
                        Debug.Log($"🤖 [EnemyBot] Idle Worker ({unit.name}) detected! Relocating to {newBase.name} for {worker.targetResourceType}.");
                        worker.TransferBase(newBase);
                        worker.SetStateToMine(worker.targetResourceType);
                    }
                }
            }
        }
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
                InitializeRuntimeBuildList(); // 🌟 Wave 완료 시 가중치 초기화
            }
        }
    }

    void FillProductionQueue()
    {
        if (activeStrategy == null) return;

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
            if (production.GetQueueCount() < 2 && runtimeMidGameBuildList.Count > 0)
            {
                int remainingIron = GetTotalRemainingIron();
                int currentIron = EnemyResourceManager.I != null ? EnemyResourceManager.I.currentIron : 0;
                
                UnitData outpostData = ConstructionManager.I.GetOutpostData(GameManager.I.enemyRace);
                int outpostCost = outpostData != null ? outpostData.ironCost : 300;

                bool isCriticalEconomicSituation = (currentIron + remainingIron) <= outpostCost;

                if (isCriticalEconomicSituation)
                {
                    if (ConstructionManager.I.HasFreeSpot())
                    {
                        Debug.Log("🤖 [EnemyBot] Critical Economy! Forcing Expansion (Last Stand).");
                        production.ClearQueue();
                        BuildStep expansionStep = new BuildStep { stepType = BuildStepType.Expansion, weight = 1000f };
                        production.EnqueueStep(expansionStep);
                    }
                    else
                    {
                        Debug.Log("🤖 [EnemyBot] No Land Left! All-In Attack Triggered!");
                        tactics.TryTriggerWave(new AttackWave()); // Force Attack
                    }
                    return; 
                }

                List<BuildStep> candidates = new List<BuildStep>(runtimeMidGameBuildList);
                float expansionWeight = CalculateExpansionWeight(currentIron, remainingIron, outpostCost);
                candidates.Add(new BuildStep { stepType = BuildStepType.Expansion, weight = expansionWeight });

                BuildStep pickedStep = GetWeightedRandomStep(candidates);
                
                // 🌟 [수정] Wave에 필요한 유닛이면 가중치를 계속 증폭 (Reset 방지)
                UpdateWeightsForNextWave(pickedStep);

                if (pickedStep.stepType == BuildStepType.Unit)
                    for (int i = 0; i < pickedStep.count; i++) production.EnqueueStep(pickedStep);
                else
                    production.EnqueueStep(pickedStep);
            }
        }
    }

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

    float CalculateExpansionWeight(int currentIron, int remainingIron, int outpostCost)
    {
        float weight = activeStrategy.expansionBaseWeight;
        float scarcity = Mathf.Max(0, (outpostCost * 3) - (currentIron + remainingIron)); 
        weight += scarcity * activeStrategy.expansionSensitivity;
        return Mathf.Max(1f, weight); 
    }

    // 🌟 [핵심 수정] Wave 필요 유닛의 가중치를 증폭시키는 로직 개선
    void UpdateWeightsForNextWave(BuildStep pickedStep)
    {
        if (pickedStep.stepType == BuildStepType.Expansion) return; 

        if (currentWaveIndex >= activeStrategy.attackWaves.Count) return;
        AttackWave nextWave = activeStrategy.attackWaves[currentWaveIndex];
        
        // 1. 필요한 유닛 목록 확인
        HashSet<UnitType> missingTypes = new HashSet<UnitType>();
        foreach(var req in nextWave.requiredUnits)
        {
            int currentCount = GetMyUnitCount(req.unitType);
            // 아직 필요량보다 부족하면 Missing 리스트에 추가
            if (currentCount < req.count) 
            {
                missingTypes.Add(req.unitType);
            }
        }

        // 2. 가중치 증폭 (방금 뽑았더라도, 아직 부족하면 계속 증폭!)
        // 기존에는 방금 뽑은 유닛(pickedStep)이 Missing에 있으면 증폭을 멈췄으나,
        // 이제는 '목표 수량'에 도달할 때까지 무조건 가중치를 올립니다.
        if (missingTypes.Count > 0)
        {
            for (int i = 0; i < runtimeMidGameBuildList.Count; i++)
            {
                BuildStep step = runtimeMidGameBuildList[i];
                
                // 이번에 생산할 유닛 타입이 Wave 필수 유닛이라면 가중치 대폭 증가
                if (step.stepType == BuildStepType.Unit && missingTypes.Contains(step.unitType))
                {
                    step.weight *= 1.25f; // 25%씩 계속 증가 (Wave 완료될 때까지)
                    runtimeMidGameBuildList[i] = step;
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