using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class WorkerDashboardManager : SingletonBehaviour<WorkerDashboardManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("UI 설정")]
    public GameObject dashboardPanel; // 전체 패널
    public Transform slotContainer;   // Vertical Layout Group이 있는 부모
    public GameObject slotPrefab;     // WorkerSlotUI 프리팹

    [Header("현황판")]
    public TextMeshProUGUI totalIdleText;

    private List<WorkerSlotUI> activeSlots = new List<WorkerSlotUI>();
    private float updateTimer = 0f;

    private void Start()
    {
        // 1. 시작 시 패널 무조건 활성화 (Toggle 제거)
        if (dashboardPanel != null) dashboardPanel.SetActive(true);

        // 2. 초기 리스트 작성
        RebuildSlotList();
    }

    void Update()
    {
        // Toggle 키 입력(Tab) 로직 제거됨 - 상시 활성화

        // 3. 0.25초마다 상태 갱신
        updateTimer += Time.deltaTime;
        if (updateTimer >= 0.25f)
        {
            CheckAndRefreshDashboard();
            updateTimer = 0f;
        }
    }

    // 🌟 [핵심 로직] 값 갱신 및 새 건물 감지
    void CheckAndRefreshDashboard()
    {
        // 현재 완성된 아군 기지 목록을 가져옴
        List<BaseController> currentBases = GetConstructedPlayerBases();

        // 4. 기지 개수와 슬롯 개수가 다르면 -> 건물이 새로 지어졌거나 파괴됨 -> 리스트 재작성
        if (currentBases.Count != activeSlots.Count)
        {
            RebuildSlotList(currentBases); // 최적화를 위해 리스트를 넘겨줌
        }
        else
        {
            // 개수가 같으면 -> 값(인원 수, 버튼 상태)만 갱신 (성능 부하 최소화)
            RefreshAllSlots();
        }

        // 백수 카운트는 항상 갱신
        UpdateIdleCount();
    }

    // 현재 완성된 플레이어 기지를 찾는 헬퍼 함수
    List<BaseController> GetConstructedPlayerBases()
    {
        List<BaseController> list = new List<BaseController>();
        BaseController[] bases = FindObjectsByType<BaseController>(FindObjectsSortMode.None);
        foreach (var b in bases)
        {
            if (b.CompareTag("Player") && b.isConstructed)
            {
                list.Add(b);
            }
        }
        return list;
    }

    // 슬롯 목록을 새로고침 (오버로딩: 최적화를 위해 이미 찾은 리스트가 있으면 사용)
    public void RebuildSlotList(List<BaseController> preFoundBases = null)
    {
        // 기존 슬롯 삭제
        foreach (Transform child in slotContainer) Destroy(child.gameObject);
        activeSlots.Clear();

        // 기지 목록 확보
        List<BaseController> targets = preFoundBases ?? GetConstructedPlayerBases();

        // 슬롯 생성
        foreach (var b in targets)
        {
            GameObject obj = Instantiate(slotPrefab, slotContainer);
            WorkerSlotUI ui = obj.GetComponent<WorkerSlotUI>();
            ui.Setup(b);
            activeSlots.Add(ui);
        }
    }

    // 기존 슬롯들의 UI 텍스트/버튼 상태만 갱신
    void RefreshAllSlots()
    {
        // 혹시 모를 null 체크 (파괴된 슬롯 방지)
        for (int i = activeSlots.Count - 1; i >= 0; i--)
        {
            if (activeSlots[i] == null)
            {
                activeSlots.RemoveAt(i);
                continue;
            }
            activeSlots[i].Refresh();
        }
    }

    void UpdateIdleCount()
    {
        if (totalIdleText == null) return;

        int idleCount = 0;
        WorkerAbility[] allWorkers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        
        foreach (var w in allWorkers)
        {
            if (!w.CompareTag("Player")) continue;
            
            // 소속이 없으면(assignedBase == null) 백수로 간주
            if (w.assignedBase == null)
            {
                idleCount++;
            }
        }

        totalIdleText.text = $"Idle Workers: {idleCount}";
    }

    // --- 🎮 버튼 기능 구현 ---

    public void OnPlusClick(BaseController targetBase)
    {
        WorkerAbility bestWorker = null;
        float minDst = Mathf.Infinity;
        float bestScore = Mathf.Infinity;

        WorkerAbility[] allWorkers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach (var w in allWorkers)
        {
            if (!w.CompareTag("Player")) continue;
            if (w.assignedBase == targetBase) continue; 
            // 🛠️ 수리 중인 인원은 제외 (중요 작업 중)
            if (w.currentState == WorkerState.Repairing) continue;

            float dist = Vector3.Distance(w.transform.position, targetBase.transform.position);
            
            float score = dist;
            if (w.assignedBase != null) score += 10000f; 

            if (score < bestScore)
            {
                bestScore = score;
                bestWorker = w;
            }
        }

        if (bestWorker != null)
        {
            if (bestWorker.assignedBase != null)
                bestWorker.assignedBase.assignedWorkers.Remove(bestWorker);

            bestWorker.assignedBase = targetBase;
            targetBase.assignedWorkers.Add(bestWorker);
            
            ApplyTaskToWorker(bestWorker, targetBase.currentTask);
            
            CheckAndRefreshDashboard();
        }
    }

    public void OnMinusClick(BaseController targetBase)
    {
        if (targetBase.assignedWorkers.Count == 0) return;

        WorkerAbility workerToFire = null;
        foreach (var w in targetBase.assignedWorkers)
        {
            // 🛠️ 수리 중인 인원은 해고 대상에서 제외
            if (w.currentState == WorkerState.Repairing) continue;

            if (w.currentLoad == 0) 
            {
                workerToFire = w;
                break;
            }
        }
        
        // 만약 전원이 수리 중이거나 조건을 만족하는 사람이 없으면 강제로 첫 번째 (단, Repairing이 아닐 때)
        if (workerToFire == null && targetBase.assignedWorkers.Count > 0)
        {
            if (targetBase.assignedWorkers[0].currentState != WorkerState.Repairing)
                workerToFire = targetBase.assignedWorkers[0];
        }

        if (workerToFire != null)
        {
            targetBase.assignedWorkers.Remove(workerToFire);
            workerToFire.assignedBase = null; 
            workerToFire.SetStateToIdle();    
            CheckAndRefreshDashboard();
        }
    }

    public void OnTaskChange(BaseController targetBase, BaseTask newTask)
    {
        targetBase.currentTask = newTask;
        foreach (var w in targetBase.assignedWorkers)
        {
            // 🛠️ 수리 중인 일꾼은 작업 변경 명령을 받지 않음 (임무 완수 보장)
            if (w.currentState == WorkerState.Repairing) continue;
            ApplyTaskToWorker(w, newTask);
        }
        RefreshAllSlots();
    }

    // 🛠️ [신규] 수리 버튼 클릭 핸들러
    public void OnRepairClick(BaseController targetBase)
    {
        // 1. 농성 체크
        if (TacticalCommandManager.I != null && TacticalCommandManager.I.currentState == TacticalState.Siege)
        {
            ShowMessage("농성 중에는 수리할 수 없습니다!");
            return;
        }

        // 2. 이미 수리 중인지 체크 (이중 방지)
        if (targetBase.IsBeingRepaired) return;

        // 3. 자원 체크
        if (ResourceManager.I == null || !ResourceManager.I.CheckCost(5, 0))
        {
            ShowMessage("자원이 부족합니다!");
            return;
        }

        // 4. 노동병 선발 (우선순위 1: Idle, 2: Others, 제외: Repairing/Building)
        WorkerAbility bestWorker = null;
        
        // 4-1. 소속 노동병 중에서 Idle 찾기
        foreach (var w in targetBase.assignedWorkers)
        {
            if (w.currentState == WorkerState.Idle)
            {
                bestWorker = w;
                break;
            }
        }

        // 4-2. Idle이 없으면 다른 작업 중인 노동병 차출 (단, 건설/수리 중인 자 제외)
        if (bestWorker == null)
        {
            foreach (var w in targetBase.assignedWorkers)
            {
                if (w.currentState != WorkerState.Building && w.currentState != WorkerState.Repairing)
                {
                    bestWorker = w;
                    break;
                }
            }
        }

        if (bestWorker != null)
        {
            // 5. 비용 지불 (선불)
            ResourceManager.I.SpendResource(5, 0);

            // 6. 명령 하달
            bestWorker.SetStateToRepair(targetBase);

            // 7. 피드백
            ShowMessage("수리 시작!");
            RefreshAllSlots(); // 버튼 비활성화 갱신
        }
        else
        {
            ShowMessage("가용한 노동병이 없습니다.");
        }
    }

    void ApplyTaskToWorker(WorkerAbility worker, BaseTask task)
    {
        switch (task)
        {
            case BaseTask.Iron:
                worker.SetStateToMine(ResourceType.Iron);
                break;
            case BaseTask.Oil:
                worker.SetStateToMine(ResourceType.Oil);
                break;
            case BaseTask.Idle:
                worker.SetStateToIdle(); 
                break;
        }
    }

    void ShowMessage(string msg)
    {
        Debug.Log(msg);
        if (FloatingTextManager.I != null)
            FloatingTextManager.I.ShowText(Vector3.zero, msg, Color.white, 30);
    }
}