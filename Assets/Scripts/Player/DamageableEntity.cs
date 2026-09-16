using UnityEngine;
using UnityEngine.InputSystem.Processors;

public class DamageableEntity : MonoBehaviour
{
    [SerializeField]
    private float maxHitPoints;
    [SerializeField]
    private float currentHitPoints;
    [SerializeField]
    public bool startAtMax = true;

    private bool justDied;
    
    void Start()
    {
        if (startAtMax)
        {
            currentHitPoints = maxHitPoints;
        }

        justDied = true;
    }

    /*void Update()
    {
        
    }*/

    public void Damage(float damage)
    {
        if (damage < 0)
        {
            return;
        }
        
        currentHitPoints -= damage;

        Check();
    }
    
    public void Heal(float health)
    {
        if (health < 0)
        {
            return;
        }
        
        currentHitPoints += health;

        Check();
    }

    private void Check()
    {
        if (currentHitPoints <= 0)
        {
            currentHitPoints = 0;
            Dead();
        }

        if (currentHitPoints > maxHitPoints)
        {
            currentHitPoints = maxHitPoints;
        }
    }
    
    private void Dead()
    {
        if (justDied)
        {
            justDied = false;
            print($"{gameObject.name} is dead.");
        }
    }

    public float GetHitPoints()
    {
        return currentHitPoints;
    }

    public float GetMaxHitPoints()
    {
        return maxHitPoints;
    }

    public void Kill()
    {
        currentHitPoints = 0;
    }
}
