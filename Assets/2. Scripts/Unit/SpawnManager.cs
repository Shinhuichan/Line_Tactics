using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : SingletonBehaviour<SpawnManager>
{
    protected override bool IsDontDestroy() => false;

    [Header("종족별 유닛 리스트 (인덱스 순서 중요!)")]
    // 0:기본보병, 1:기본원거리, 2:탱커 ... 순서로 Inspector에서 할당해야 함
    public List<UnitData> humanicUnits; 
    public List<UnitData> demonicUnits;

    [Header("방어 유닛 데이터 리스트")]
    // 0: Humanic (BaseArcher), 1: Demonic (BaseCorpse) 순서로 Inspector에서 넣어주세요.
    public List<UnitData> baseDefenseUnits; 

    // 💰 [수정] 자원별/진영별 현재 비용 관리 변수
    public int playerDefIronCost { get; private set; }
    public int playerDefOilCost { get; private set; }

    public int enemyDefIronCost { get; private set; }
    public int enemyDefOilCost { get; private set; }

    [Header("버튼 부모 객체 (UI 갱신용)")]
    public Transform unitButtonGrid; 

    [Header("기타 설정")]
    private Transform playerSpawnPoint;
    private Transform enemySpawnPoint;
    
    // 성채 가격 관리 (구형 변수 유지 - 하위 호환성)
    public int baseArcherStartCost = 255;
    
    // 📈 [요청사항] 가격 상승 배율 (1.5배)
    private const float COST_MULTIPLIER = 1.5f;

    // 🛠️ [신규] 비용 초기화 상태 추적용 변수
    private UnitRace? lastInitializedRace = null;
    private bool isCostsInitialized = false;

    // (참고: 아래 두 변수는 이제 위쪽의 playerDefIronCost 등을 사용하므로 잘 안 쓰이지만, 에러 방지용으로 남겨둡니다)
    public int playerBaseArcherCost { get; private set; }
    public int enemyBaseArcherCost { get; private set; }

    private void Start()
    {
        FindSpawnPoints();
    }

    // 🔄 비용 초기화 (종족 변경 시 또는 최초 실행 시 호출)
    void InitializeDefenseCosts()
    {
        if (GameManager.I == null) return;

        // 현재 종족 기록
        lastInitializedRace = GameManager.I.playerRace;
        isCostsInitialized = true;

        // 1. 플레이어 데이터 (현재 종족에 맞게)
        UnitData playerData = GetBaseDefenseData("Player");
        if (playerData != null)
        {
            playerDefIronCost = playerData.ironCost;
            playerDefOilCost = playerData.oilCost;
            playerBaseArcherCost = playerData.ironCost; 
            Debug.Log($"💰 [SpawnManager] Player Defense Cost Initialized: Fe {playerDefIronCost} (Race: {GameManager.I.playerRace})");
        }

        // 2. 적 데이터 (적 종족에 맞게)
        UnitData enemyData = GetBaseDefenseData("Enemy");
        if (enemyData != null)
        {
            enemyDefIronCost = enemyData.ironCost;
            enemyDefOilCost = enemyData.oilCost;
            enemyBaseArcherCost = enemyData.ironCost; 
        }
    }

    // 🔍 종족 변경 감지 및 데이터 반환
    void CheckAndRefreshCosts()
    {
        if (GameManager.I == null) return;

        // 초기화가 안 됐거나, 저장된 종족과 현재 종족이 다르면 재초기화
        if (!isCostsInitialized || lastInitializedRace != GameManager.I.playerRace)
        {
            InitializeDefenseCosts();
            RefreshUnitButtons(); // 가격이 바뀌었으니 버튼 UI도 갱신
        }
    }

    // 🔍 종족에 맞는 방어 유닛 데이터 반환
    public UnitData GetBaseDefenseData(string teamTag)
    {
        if (GameManager.I == null) return null;

        UnitRace race = (teamTag == "Player") ? GameManager.I.playerRace : GameManager.I.enemyRace;

        // 리스트 순서: 0=Humanic, 1=Demonic
        int index = (race == UnitRace.Humanic) ? 0 : 1;
        
        if (baseDefenseUnits != null && index < baseDefenseUnits.Count)
        {
            return baseDefenseUnits[index];
        }
        return null;
    }

    // ⚔️ [수정] 방어 유닛 소환
    public void TrySpawnBaseDefense(string teamTag)
    {
        // 🛠️ 소환 시도 전 비용 상태 점검 (Issue 2 해결)
        CheckAndRefreshCosts();

        UnitData data = GetBaseDefenseData(teamTag);
        if (data == null) return;

        bool isPlayer = (teamTag == "Player");
        int currentIron = isPlayer ? playerDefIronCost : enemyDefIronCost;
        int currentOil = isPlayer ? playerDefOilCost : enemyDefOilCost;

        bool canAfford = false;

        if (isPlayer)
        {
            if (ResourceManager.I.CheckCost(currentIron, currentOil))
            {
                ResourceManager.I.SpendResource(currentIron, currentOil);
                canAfford = true;
            }
        }
        else // Enemy
        {
            if (EnemyResourceManager.I.CheckCost(currentIron, currentOil))
            {
                EnemyResourceManager.I.SpendResource(currentIron, currentOil);
                canAfford = true;
            }
        }

        if (canAfford)
        {
            SpawnBaseDefenseUnit(data, teamTag);

            if (isPlayer)
            {
                playerDefIronCost = (int)(playerDefIronCost * COST_MULTIPLIER);
                playerDefOilCost = (int)(playerDefOilCost * COST_MULTIPLIER);
                // 디버그 로그
                Debug.Log($"🏰 [Spawn] {data.unitName} 소환 완료. 다음 가격: Fe {playerDefIronCost}");
            }
            else
            {
                enemyDefIronCost = (int)(enemyDefIronCost * COST_MULTIPLIER);
                enemyDefOilCost = (int)(enemyDefOilCost * COST_MULTIPLIER);
            }
            
            RefreshUnitButtons();
        }
    }

    void SpawnBaseDefenseUnit(UnitData data, string teamTag)
    {
        Transform spawnPoint = (teamTag == "Player") ? playerSpawnPoint : enemySpawnPoint;
        
        if (PoolManager.I != null && spawnPoint != null)
        {
            GameObject unitObj = PoolManager.I.Get(data.type);
            if (unitObj != null)
            {
                // 🌟 [수정] 랜덤 위치 제거 -> 기지 정확한 위치(Center)에 소환
                unitObj.transform.position = spawnPoint.position;
                
                // 회전값 설정 (Player는 정면, Enemy는 반대)
                unitObj.transform.rotation = (teamTag == "Player") ? Quaternion.identity : Quaternion.Euler(0, 0, 180);

                UnitController ctrl = unitObj.GetComponent<UnitController>();
                if (ctrl != null)
                {
                    ctrl.Initialize(data, teamTag);
                }
            }
        }
    }

    // ==================================================================================
    // 🔍 UI 버튼용 데이터 검색 (버그 수정됨)
    // ==================================================================================
    public UnitData GetUnitData(int listIndex)
    {
        if (GameManager.I == null) return null;

        // 🛠️ 데이터를 가져올 때마다 비용/종족 상태 점검
        CheckAndRefreshCosts();

        UnitRace race = GameManager.I.playerRace;
        List<UnitData> targetList = (race == UnitRace.Humanic) ? humanicUnits : demonicUnits;

        if (targetList == null) return null;

        // 1. 일반 유닛 범위 내인지 확인
        if (listIndex >= 0 && listIndex < targetList.Count)
        {
            return targetList[listIndex];
        }

        // 🌟 [수정] Issue 1 해결: 인덱스가 리스트 크기보다 '크거나 같으면' 무조건 방어 유닛 반환
        // 예: 유닛이 4개(0~3)일 때, 버튼 인덱스가 4, 5, 99 등등이면 방어 유닛으로 처리
        if (listIndex >= targetList.Count)
        {
            return GetBaseDefenseData("Player"); 
        }

        return null;
    }

    // ==================================================================================
    // 🔍 데이터 검색 (봇 전용 - UnitType Enum 검색)
    // ==================================================================================
    public UnitData GetUnitDataByType(UnitType type)
    {
        // 1. 휴머닉 리스트 검색
        var data = humanicUnits.Find(u => u.type == type);
        if (data != null) return data;

        // 2. 데모닉 리스트 검색
        data = demonicUnits.Find(u => u.type == type);
        if (data != null) return data;

        // 3. 성채 리스트 검색 (🌟 수정: baseArcherData -> baseDefenseUnits)
        if (baseDefenseUnits != null)
        {
            data = baseDefenseUnits.Find(u => u.type == type);
            if (data != null) return data;
        }

        return null;
    }

    // 🔄 UI 갱신
    public void RefreshUnitButtons()
    {
        if (unitButtonGrid == null) return;

        UnitSpawnButton[] buttons = unitButtonGrid.GetComponentsInChildren<UnitSpawnButton>(true);
        foreach (var btn in buttons)
        {
            btn.UpdateUnitInfo(); 
        }
    }

    // 🏰 [구형 호환] 성채 장궁병 소환 (이제 TrySpawnBaseDefense로 통합됨)
    public bool TrySpawnBaseArcher(string tag)
    {
        // 신규 함수로 위임
        TrySpawnBaseDefense(tag);
        return true; 
    }

    // ==================================================================================
    // ⚔️ [수정] 플레이어 유닛 소환 (버튼 클릭 시 호출됨)
    // ==================================================================================
    public void SpawnUnit(int unitTypeIndex)
    {
        if (playerSpawnPoint == null) FindSpawnPoints();
        if (playerSpawnPoint == null) return;

        // 1. 데이터 가져오기
        UnitData data = GetUnitData(unitTypeIndex);
        if (data == null) return;

        // 🌟 [핵심 수정] 만약 방어 유닛(BaseArcher/BaseCorpse)이라면 전용 함수 호출
        // (그래야 가격 1.5배 상승 로직이 적용됨)
        if (data.type == UnitType.BaseArcher || data.type == UnitType.BaseCorpse)
        {
            TrySpawnBaseDefense("Player");
            return; // 여기서 종료
        }

        // --- 이하 일반 유닛 소환 로직 (고정 가격) ---
        SpawnProcess(unitTypeIndex, playerSpawnPoint, "Player", ResourceManager.I);
    }

    public void SpawnEnemyUnit(int unitTypeIndex)
    {
        if (enemySpawnPoint == null) FindSpawnPoints();
        if (enemySpawnPoint == null) return;
        SpawnProcess(unitTypeIndex, enemySpawnPoint, "Enemy", null, true); 
    }

    private void SpawnProcess(int index, Transform spawnPos, string tag, ResourceManager playerRM, bool isEnemy = false)
    {
        // 🌟 주의: 여기서 index는 리스트 인덱스임
        UnitData data = GetUnitData(index);
        if (data == null) return;

        if (isEnemy)
        {
            if (EnemyResourceManager.I != null)
            {
                if (!EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost)) return;
                EnemyResourceManager.I.SpendResource(data.ironCost, data.oilCost);
            }
        }
        else
        {
            if (playerRM != null)
            {
                if (!playerRM.CheckCost(data.ironCost, data.oilCost))
                {
                    if (FloatingTextManager.I != null) 
                        FloatingTextManager.I.ShowText(spawnPos.position + Vector3.up, "자원 부족!", Color.red, 30);
                    return;
                }
                playerRM.SpendResource(data.ironCost, data.oilCost);
            }
        }

        if (PoolManager.I != null)
        {
            GameObject unitObj = PoolManager.I.Get(data.type);
            if (unitObj != null)
            {
                unitObj.transform.position = spawnPos.position;
                unitObj.transform.rotation = isEnemy ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;

                UnitController unit = unitObj.GetComponent<UnitController>();
                if (unit != null) unit.Initialize(data, tag);
            }
        }
    }

    public void SpawnUnitFree(int unitTypeIndex)
    {
        if (playerSpawnPoint == null) FindSpawnPoints();
        if (playerSpawnPoint == null) return;

        UnitData targetData = GetUnitData(unitTypeIndex);
        if (targetData == null) return;

        if (PoolManager.I != null)
        {
            GameObject unitObj = PoolManager.I.Get(targetData.type);
            if (unitObj != null)
            {
                unitObj.transform.position = playerSpawnPoint.position;
                unitObj.transform.rotation = Quaternion.identity;

                UnitController unit = unitObj.GetComponent<UnitController>();
                if (unit != null) unit.Initialize(targetData, "Player");
            }
        }
    }

    void FindSpawnPoints()
    {
        GameObject pBase = GameObject.FindGameObjectWithTag("Player");
        if (pBase != null) playerSpawnPoint = pBase.transform;

        GameObject eBase = GameObject.FindGameObjectWithTag("Enemy");
        if (eBase != null) enemySpawnPoint = eBase.transform;
    }

    // 🏰 [수정] 특정 위치 성채 소환 (봇 전용 - baseDefenseUnits 사용)
    public bool TrySpawnBaseArcherAt(string teamTag, Vector3 spawnPos)
    {
        if (GameManager.I == null) return false;
        // 🛠️ 여기서도 비용 체크 전 초기화 확인
        CheckAndRefreshCosts();
        
        UnitRace targetRace = (teamTag == "Player") ? GameManager.I.playerRace : GameManager.I.enemyRace;
        UnitData correctData = null;
        if (baseDefenseUnits != null)
        {
             correctData = baseDefenseUnits.Find(u => u.race == targetRace);
        }

        if (correctData == null) return false;

        int currentIron = (teamTag == "Player") ? playerDefIronCost : enemyDefIronCost;
        int currentOil = (teamTag == "Player") ? playerDefOilCost : enemyDefOilCost;
        
        if (teamTag == "Enemy")
        {
            if (EnemyResourceManager.I == null || !EnemyResourceManager.I.CheckCost(currentIron, currentOil)) 
                return false;
            EnemyResourceManager.I.SpendResource(currentIron, currentOil);
            
            enemyDefIronCost = (int)(enemyDefIronCost * COST_MULTIPLIER);
            enemyDefOilCost = (int)(enemyDefOilCost * COST_MULTIPLIER);
        }
        else 
        {
            if (ResourceManager.I == null || !ResourceManager.I.CheckCost(currentIron, currentOil)) 
                return false;
            ResourceManager.I.SpendResource(currentIron, currentOil);

            playerDefIronCost = (int)(playerDefIronCost * COST_MULTIPLIER);
            playerDefOilCost = (int)(playerDefOilCost * COST_MULTIPLIER);
        }

        if (PoolManager.I != null)
        {
            GameObject unitObj = PoolManager.I.Get(correctData.type);
            if (unitObj != null)
            {
                unitObj.transform.position = spawnPos;
                unitObj.transform.rotation = (teamTag == "Enemy") ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;

                UnitController unit = unitObj.GetComponent<UnitController>();
                if (unit != null) unit.Initialize(correctData, teamTag);
                return true;
            }
        }
        return false;
    }

    // ==================================================================================
    // 🤖 적군 유닛 소환
    // ==================================================================================
    public bool TrySpawnEnemyUnit(int unitIdentity)
    {
        if (enemySpawnPoint == null) FindSpawnPoints();
        if (enemySpawnPoint == null) return false;

        UnitData data = GetUnitDataByType((UnitType)unitIdentity);
        if (data == null) return false; // GetUnitDataByType에서 이미 종족별 리스트를 다 뒤짐

        if (EnemyResourceManager.I == null) return false;

        if (EnemyResourceManager.I.CheckCost(data.ironCost, data.oilCost))
        {
            EnemyResourceManager.I.SpendResource(data.ironCost, data.oilCost);

            if (PoolManager.I != null)
            {
                GameObject unitObj = PoolManager.I.Get(data.type);
                if (unitObj != null)
                {
                    unitObj.transform.position = enemySpawnPoint.position;
                    unitObj.transform.rotation = Quaternion.Euler(0, 0, 180);

                    UnitController unit = unitObj.GetComponent<UnitController>();
                    if (unit != null) unit.Initialize(data, "Enemy");
                    return true;
                }
            }
        }
        return false;
    }

    // 🤖 [신규] PlayerBot 전용 소환 함수 (UnitType으로 소환)
    public bool TrySpawnPlayerUnit(int unitIdentity)
    {
        if (playerSpawnPoint == null) FindSpawnPoints();
        if (playerSpawnPoint == null) return false;

        // 1. 데이터 검색 (EnemyBot과 동일한 방식)
        UnitData data = GetUnitDataByType((UnitType)unitIdentity);
        if (data == null) return false;

        // 2. Player 자원 매니저 사용
        if (ResourceManager.I == null) return false;

        // 3. 방어 타워 처리
        if (data.type == UnitType.BaseArcher || data.type == UnitType.BaseCorpse)
        {
            // 방어 타워는 별도 로직(가격 상승 등)이 있으므로 TrySpawnBaseDefense 활용
            // 다만 TrySpawnBaseDefense는 void 반환이므로, 여기서 비용 체크를 미리 하고 호출
            int costFe = playerDefIronCost;
            int costOil = playerDefOilCost;
            
            if (ResourceManager.I.CheckCost(costFe, costOil))
            {
                TrySpawnBaseDefense("Player"); // 내부에서 자원 소모 및 가격 상승 처리됨
                return true;
            }
            return false;
        }

        // 4. 일반 유닛 처리
        if (ResourceManager.I.CheckCost(data.ironCost, data.oilCost))
        {
            ResourceManager.I.SpendResource(data.ironCost, data.oilCost);

            if (PoolManager.I != null)
            {
                GameObject unitObj = PoolManager.I.Get(data.type);
                if (unitObj != null)
                {
                    unitObj.transform.position = playerSpawnPoint.position;
                    // Player는 기본 회전 (Identity)
                    unitObj.transform.rotation = Quaternion.identity;

                    UnitController unit = unitObj.GetComponent<UnitController>();
                    if (unit != null) unit.Initialize(data, "Player");
                    return true;
                }
            }
        }
        return false;
    }
}