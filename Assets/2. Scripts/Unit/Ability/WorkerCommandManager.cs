using UnityEngine;

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
        // 화면 중앙에 텍스트 띄우기 (또는 로그)
        Debug.Log(msg);
        if (FloatingTextManager.I != null)
        {
            // 기지 근처나 화면 중앙에 띄우면 좋음. 일단 임시 위치 (0,0)
            FloatingTextManager.I.ShowText(Vector3.zero, msg, Color.white, 40);
        }
    }
}