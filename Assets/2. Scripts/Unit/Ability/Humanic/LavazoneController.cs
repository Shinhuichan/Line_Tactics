using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class LavaZoneController : MonoBehaviour
{
    private float damagePerTick;
    private float tickInterval;
    private string targetTag;
    private string targetBaseTag;

    private HashSet<UnitController> victims = new HashSet<UnitController>();
    private HashSet<BaseController> baseVictims = new HashSet<BaseController>();

    public void Initialize(float damage, float interval, float duration, string enemyTag, string baseTag)
    {
        this.damagePerTick = damage;
        this.tickInterval = interval;
        this.targetTag = enemyTag;
        this.targetBaseTag = baseTag;

        // 🌟 [수정] CreateVisuals() 삭제됨. 프리팹의 모습 그대로 사용.
        
        // 충돌체(Trigger)가 없으면 추가 (프리팹에 있으면 통과)
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 0.5f);
        }

        // ⏳ 3. 지속시간 후 사라짐 (요청사항 3번 반영)
        Destroy(gameObject, duration);
        StartCoroutine(DoTRoutine());
    }

    IEnumerator DoTRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(tickInterval);

            foreach (var victim in new List<UnitController>(victims))
            {
                if (victim != null && victim.gameObject.activeInHierarchy)
                {
                    victim.TakeDamage(damagePerTick, false); 
                    if (FloatingTextManager.I != null) 
                        FloatingTextManager.I.ShowText(victim.transform.position, "Hot!", new Color(1f, 0.5f, 0f), 20);
                    UnityEngine.Debug.Log("용암 지옥!");
                }
            }
            foreach (var baseCtrl in new List<BaseController>(baseVictims))
            {
                if (baseCtrl != null) baseCtrl.TakeDamage(damagePerTick);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag) || other.CompareTag(targetBaseTag))
        {
            UnitController unit = other.GetComponent<UnitController>();
            if (unit != null) victims.Add(unit);
            else {
                BaseController baseCtrl = other.GetComponent<BaseController>();
                if (baseCtrl != null) baseVictims.Add(baseCtrl);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(targetTag) || other.CompareTag(targetBaseTag))
        {
            UnitController unit = other.GetComponent<UnitController>();
            if (unit != null && victims.Contains(unit))
            {
                victims.Remove(unit);
                if (unit.currentHP > 0) unit.ApplyBurn(); // 나갈 때 화상
            }
            else {
                BaseController baseCtrl = other.GetComponent<BaseController>();
                if (baseCtrl != null) baseVictims.Remove(baseCtrl);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var unit in victims)
        {
            if (unit != null && unit.gameObject.activeInHierarchy && unit.currentHP > 0)
            {
                unit.ApplyBurn();
            }
        }
    }
}