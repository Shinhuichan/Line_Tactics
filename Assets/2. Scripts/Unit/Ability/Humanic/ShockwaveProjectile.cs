using UnityEngine;
using System.Collections.Generic;

public class ShockwaveProjectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private float maxDistance;
    private float knockbackForce;
    private string targetTag;
    private string targetBaseTag;

    private Vector3 startPos;
    private HashSet<int> hitTargets = new HashSet<int>();

    public void Initialize(float dmg, float spd, float dist, float knockback, string enemyTag, string baseTag)
    {
        this.damage = dmg;
        this.speed = spd;
        this.maxDistance = dist;
        this.knockbackForce = knockback;
        this.targetTag = enemyTag;
        this.targetBaseTag = baseTag;
        this.startPos = transform.position;

        // 🌟 [수정] CreateVisuals() 삭제. 프리팹 그대로 사용.
        
        // 물리 설정은 필수
        if (GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 0.8f);
        }
    }

    void Update()
    {
        // 앞으로 이동
        float moveStep = speed * Time.deltaTime;
        transform.Translate(Vector3.up * moveStep); 

        // 사거리 도달 체크
        if (Vector3.Distance(startPos, transform.position) >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag) || other.CompareTag(targetBaseTag))
        {
            if (hitTargets.Contains(other.gameObject.GetInstanceID())) return;
            hitTargets.Add(other.gameObject.GetInstanceID());

            UnitController enemyUnit = other.GetComponent<UnitController>();
            if (enemyUnit != null)
            {
                enemyUnit.TakeDamage(damage);
                enemyUnit.ApplyKnockback(transform.up, knockbackForce);
                if (FloatingTextManager.I != null) 
                    FloatingTextManager.I.ShowText(other.transform.position, "Shock!", Color.white, 25);
            }
            else
            {
                BaseController baseCtrl = other.GetComponent<BaseController>();
                if (baseCtrl != null) baseCtrl.TakeDamage(damage);
            }
        }
    }
}