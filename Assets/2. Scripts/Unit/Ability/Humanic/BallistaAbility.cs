using UnityEngine;

public class BallistaAbility : UnitAbility
{
    [Header("공성 설정")]
    public float buildingDamageMultiplier = 2.5f; 
    public GameObject projectileEffect; 

    [Header("신규 능력: 감전 화살 (Electric Arrow)")]
    public string electricUpgradeKey = "ELECTRIC_ARROW";
    public float shockDuration = 1.0f; // 1초 지속

    public override void Initialize(UnitController unit)
    {
        base.Initialize(unit);
    }

    public override bool OnAttack(GameObject target)
    {
        float finalDamage = owner.attackDamage;
        bool isBuilding = false;

        // 1. 타겟 확인 (기지/건물)
        BaseController baseCtrl = target.GetComponent<BaseController>();
        if (baseCtrl != null)
        {
            isBuilding = true;
            finalDamage *= buildingDamageMultiplier;
        }

        // 2. 데미지 적용 및 감전 부여
        if (isBuilding)
        {
            if (baseCtrl != null) baseCtrl.TakeDamage(finalDamage);
            
            if (FloatingTextManager.I != null)
                FloatingTextManager.I.ShowText(target.transform.position, "Siege!!", Color.red, 40);
        }
        else
        {
            UnitController enemyUnit = target.GetComponent<UnitController>();
            if (enemyUnit != null)
            {
                enemyUnit.TakeDamage(finalDamage);

                // ⚡ [핵심] 업그레이드 확인 후 감전 부여
                if (UpgradeManager.I != null && UpgradeManager.I.IsAbilityActive(electricUpgradeKey, owner.tag))
                {
                    enemyUnit.ApplyShock(shockDuration);
                }
            }
        }

        // 3. 이펙트 생성
        if (projectileEffect != null)
        {
            GameObject vfx = Instantiate(projectileEffect, target.transform.position, Quaternion.identity);
            Destroy(vfx, 1.0f);
        }

        return true; 
    }
}