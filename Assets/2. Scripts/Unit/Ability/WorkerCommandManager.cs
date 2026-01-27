using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WorkerCommandManager : MonoBehaviour
{
    // 버튼 1: 철 캐기 명령
    public void CommandAllMineIron()
    {
        CommandAllWorkersToMine(ResourceType.Iron);
        ShowCommandMessage("모든 일꾼: 철 채집 시작!");
    }

    // 버튼 2: 기름 캐기 명령
    public void CommandAllMineOil()
    {
        CommandAllWorkersToMine(ResourceType.Oil);
        ShowCommandMessage("모든 일꾼: 기름 채집 시작!");
    }

    // 내부 로직: 공격 명령
    public void CommandAllAttack()
    {
        WorkerAbility[] workers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach (var worker in workers)
        {
            if (worker.CompareTag("Player"))
            {
                // 🏗️ 건설 중인 노동자는 열외
                if (worker.currentState == WorkerState.Building) continue;

                worker.SetStateToAttack();
            }
        }
        ShowCommandMessage("모든 일꾼(건설자 제외): 공격 개시!");
    }

    // 🔧 [신규] 스마트 수리 명령
    public void CommandRepairAllBases()
    {
        // 1. 아군 기지 중 손상된 기지 찾기
        List<BaseController> damagedBases = new List<BaseController>();
        foreach (var baseCtrl in BaseController.activeBases)
        {
            if (baseCtrl.CompareTag("Player") && baseCtrl.isConstructed && baseCtrl.currentHP < baseCtrl.maxHP)
            {
                // 이미 누군가 수리 중이라면 스킵할 수도 있지만, 
                // "여러 명이 붙어서 빨리 수리"하는 것이 좋으므로 중복 허용하거나,
                // 기획에 따라 '수리공이 없는 기지'만 찾을 수도 있음. 
                // 여기서는 "손상된 모든 기지에 한 명씩 배정"하는 로직으로 구현.
                if (!IsBeingRepairedByAnyone(baseCtrl))
                {
                    damagedBases.Add(baseCtrl);
                }
            }
        }

        if (damagedBases.Count == 0)
        {
            ShowCommandMessage("수리가 필요한 기지가 없습니다.");
            return;
        }

        int assignedCount = 0;

        // 2. 각 기지마다 최적의 일꾼 배정
        foreach (var targetBase in damagedBases)
        {
            WorkerAbility bestWorker = FindBestWorkerForRepair(targetBase);
            
            if (bestWorker != null)
            {
                bestWorker.SetStateToRepair(targetBase);
                assignedCount++;
            }
        }

        if (assignedCount > 0)
            ShowCommandMessage($"{assignedCount}명의 일꾼이 수리를 시작합니다!");
        else
            ShowCommandMessage("가용한 일꾼이 없습니다.");
    }

    // 이미 수리공이 붙었는지 확인 (한 기지에 한 명만 보내려면 사용)
    bool IsBeingRepairedByAnyone(BaseController target)
    {
        WorkerAbility[] workers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach (var w in workers)
        {
            if (w.CompareTag("Player") && w.targetConstructionSite == target && w.currentState == WorkerState.Repairing)
                return true;
        }
        return false;
    }

    // 🌟 [핵심] 우선순위 기반 일꾼 선별 알고리즘
    WorkerAbility FindBestWorkerForRepair(BaseController targetBase)
    {
        WorkerAbility[] allWorkers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        List<WorkerAbility> myWorkers = allWorkers.Where(w => w.CompareTag("Player") && w.currentState != WorkerState.Building && w.currentState != WorkerState.Repairing).ToList();

        if (myWorkers.Count == 0) return null;

        // 1순위: Idle 상태인 일꾼 (가장 가까운 순)
        var idleWorkers = myWorkers.Where(w => w.currentState == WorkerState.Idle)
                                   .OrderBy(w => Vector3.Distance(w.transform.position, targetBase.transform.position))
                                   .ToList();
        if (idleWorkers.Count > 0) return idleWorkers[0];

        // 2순위: 해당 기지에 소속된 일꾼 (Local)
        var localWorkers = myWorkers.Where(w => w.assignedBase == targetBase)
                                    .OrderBy(w => Vector3.Distance(w.transform.position, targetBase.transform.position))
                                    .ToList();
        if (localWorkers.Count > 0) return localWorkers[0];

        // 3순위: 거리상 가장 가까운 일꾼 (Global)
        var closestWorker = myWorkers.OrderBy(w => Vector3.Distance(w.transform.position, targetBase.transform.position))
                                     .FirstOrDefault();
        
        return closestWorker;
    }

    // 내부 로직: 채집 명령
    private void CommandAllWorkersToMine(ResourceType type)
    {
        WorkerAbility[] workers = FindObjectsByType<WorkerAbility>(FindObjectsSortMode.None);
        foreach (var worker in workers)
        {
            if (worker.CompareTag("Player"))
            {
                // 🏗️ 건설 중인 노동자는 건드리지 않음
                if (worker.currentState == WorkerState.Building) continue;

                worker.SetStateToMine(type);
            }
        }
    }

    private void ShowCommandMessage(string msg)
    {
        if (FloatingTextManager.I != null)
        {
            // 화면 중앙쯤에 메시지 띄우기 (플레이어 기지 위치 등)
            GameObject baseObj = GameObject.FindGameObjectWithTag("Player");
            Vector3 pos = baseObj != null ? baseObj.transform.position + Vector3.up * 2 : Vector3.zero;
            FloatingTextManager.I.ShowText(pos, msg, Color.white, 30);
        }
        Debug.Log($"[Command] {msg}");
    }
}