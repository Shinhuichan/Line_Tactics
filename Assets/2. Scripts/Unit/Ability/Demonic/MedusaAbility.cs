using UnityEngine;

public class MedusaAbility : UnitAbility
{
    [Header("메두사 능력: 석화 (Petrification)")]
    [Tooltip("석화 후 유닛이 파괴되기까지 걸리는 시간")]
    public float stoneDuration = 1.5f; 

    [Header("이펙트")]
    public GameObject eyeBeamEffect; // (선택) 눈에서 나가는 광선 이펙트

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override bool OnAttack(GameObject target)
    {
        // 1. 타겟 확인
        UnitController enemyUnit = target.GetComponent<UnitController>();
        
        if (enemyUnit != null)
        {
            // 2. 속성 확인: 건물이거나 기계(거인병 등)인가?
            if (enemyUnit.isMechanical) 
            {
                // 🧱 기계/건물 속성 -> 일반 데미지
                enemyUnit.TakeDamage(owner.attackDamage, false);
            }
            else
            {
                // 🗿 생명체 -> 즉사 (석화)
                // 데미지 계산 없이 바로 상태이상으로 보내버림
                enemyUnit.ApplyPetrify(stoneDuration);
            }
        }
        else
        {
            // 3. BaseController(건물) -> 일반 데미지
            BaseController enemyBase = target.GetComponent<BaseController>();
            if (enemyBase != null)
            {
                enemyBase.TakeDamage(owner.attackDamage);
            }
        }

        // (선택) 발사 이펙트 생성
        if (eyeBeamEffect != null)
        {
            Instantiate(eyeBeamEffect, transform.position, Quaternion.identity);
        }

        return true; 
    }
}