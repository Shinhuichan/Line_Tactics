using UnityEngine;

public class HarpoonerAbility : UnitAbility
{
    [Header("작살(Harpoon) 설정")]
    public float pullDistance = 2.0f; // 당겨오는 거리
    public float stunDuration = 0.5f; // 당겨지는 시간 = 기절 시간
    public float scanRange = 8.0f;    // 원거리 유닛 탐색 범위 (공격 사거리보다 넓게)

    [Header("업그레이드: 작살 강화")]
    public string upgradeKey = "ENHANCED_HARPOON"; // 🌟 업그레이드 키
    public float buffRadius = 0.5f;       // 버프 범위
    public float buffAmount = 0.05f;      // 공속 5% 증가
    public float buffDuration = 1.0f;     // 1초 지속

    [Header("이펙트")]
    public GameObject harpoonEffect; // 작살 발사 이펙트

    // 타겟팅 오버라이드용 변수
    private GameObject priorityTarget;

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override void OnUpdate()
    {
        // 매 프레임(혹은 일정 간격) 원거리 유닛을 스캔하여 우선 타겟 설정
        ScanForRangedTargets();
    }

    // 🎯 원거리 유닛 우선 탐색 로직 (암살병 참고)
    void ScanForRangedTargets()
    {
        // 이미 훌륭한 타겟을 치고 있다면 패스 (너무 잦은 타겟 변경 방지)
        if (priorityTarget != null && priorityTarget.activeInHierarchy)
        {
            float d = Vector3.Distance(transform.position, priorityTarget.transform.position);
            if (d <= scanRange) return; // 아직 사거리 내에 있으면 유지
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRange);
        GameObject bestRanged = null;
        float minDst = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(owner.enemyTag) || hit.CompareTag(owner.targetBaseTag))
            {
                UnitController enemy = hit.GetComponent<UnitController>();
                
                // 1. 적 유닛이고 + 원거리 유닛인지 확인 (UnitData.isRangedUnit)
                if (enemy != null && !enemy.isStealthed) // 은신 유닛은 제외
                {
                    // UnitData 정보가 필요하므로 UnitController를 통해 접근하거나
                    // UnitType으로 하드코딩해서 판별 (여기선 UnitData의 isRangedUnit 활용 가정)
                    // 현재 UnitController에는 UnitData 참조가 없으므로 Type으로 판별하거나, 
                    // Init 때 저장해둔 데이터가 필요함. 일단 UnitType으로 예시 작성:
                    bool isRanged = IsRangedType(enemy.unitType);

                    if (isRanged)
                    {
                        float dst = Vector3.Distance(transform.position, hit.transform.position);
                        if (dst < minDst)
                        {
                            minDst = dst;
                            bestRanged = hit.gameObject;
                        }
                    }
                }
            }
        }

        // 원거리 유닛을 찾았다면 우선 타겟으로 설정
        if (bestRanged != null)
        {
            priorityTarget = bestRanged;
            // UnitController에게 강제로 타겟을 지정해주는 기능이 있다면 호출
            // owner.SetForcedTarget(priorityTarget); 
            // (만약 UnitController에 SetTarget이 없다면, 아래 OnAttack에서 처리)
        }
        else
        {
            priorityTarget = null; // 없으면 기본 AI(가까운 적) 따름
        }
    }

    // 도우미 함수: 원거리 타입 판별
    bool IsRangedType(UnitType type)
    {
        return type == UnitType.Archer || type == UnitType.Mage || 
               type == UnitType.BaseArcher || type == UnitType.Ballista ||
               type == UnitType.Harpooner || type == UnitType.Succubus ||
               type == UnitType.Corpse || type == UnitType.Necromancer;
    }

    // 🌟 [핵심] 공격 시 발동
    public override bool OnAttack(GameObject target)
    {
        UnitController enemy = target.GetComponent<UnitController>();
        if (enemy != null)
        {
            // 1. 데미지
            enemy.TakeDamage(owner.attackDamage, false);

            // 2. 작살 당기기
            enemy.ApplyPull(transform.position, pullDistance, stunDuration);

            // 텍스트
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(target.transform.position, "Hook!", Color.red, 30);
        }
        else
        {
            BaseController baseCtrl = target.GetComponent<BaseController>();
            if (baseCtrl != null) baseCtrl.TakeDamage(owner.attackDamage);
        }

        // 3. 🔱 [신규] 작살 강화 버프 발동 (주변 아군 공속 증가)
        if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(upgradeKey, owner.tag))
        {
            ApplyBuffToAllies();
        }

        return true; 
    }

    void ApplyBuffToAllies()
    {
        Collider2D[] allies = Physics2D.OverlapCircleAll(transform.position, buffRadius);
        
        foreach (var col in allies)
        {
            // 같은 팀인지 확인
            if (col.CompareTag(owner.tag))
            {
                UnitController allyUnit = col.GetComponent<UnitController>();
                if (allyUnit != null && !allyUnit.isDead)
                {
                    // 건물(성채 병사 등) 제외 여부는 기획에 따라 결정 (현재는 포함)
                    allyUnit.ApplyTemporaryAttackSpeedBuff(buffAmount, buffDuration);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 작살 탐색 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRange);

        // 버프 범위
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, buffRadius);
    }
}