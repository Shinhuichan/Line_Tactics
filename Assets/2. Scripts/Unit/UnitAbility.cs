using UnityEngine;

public abstract class UnitAbility : MonoBehaviour
{
    public UnitController owner;

    public virtual void Initialize(UnitController unit)
    {
        owner = unit;
    }

    public virtual bool IsBusy => false;

    public virtual void OnUpdate() { }

    public virtual bool OnAttack(GameObject target)
    {
        return false; 
    }

    public virtual float OnTakeDamage(float incomingDamage, GameObject attacker)
    {
        return incomingDamage; 
    }

    // 🌟 [신규] 사망 직전 호출되는 훅 (Hook)
    // true를 반환하면 UnitController는 즉시 Destroy하지 않고 대기합니다.
    // (능력 쪽에서 연출 후 FinishDeath를 호출해야 함)
    public virtual bool OnDie() 
    { 
        return false; 
    }
}