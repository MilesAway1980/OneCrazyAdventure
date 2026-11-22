using UnityEngine;
using UnityEngine.InputSystem.Processors;

public class DamageableEntity : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The most hit points it can have. 0 means it can't be hurt without dying.")]
    private float maxHitPoints;
    [SerializeField]
    [Tooltip("How many hit points it has now. Starts at Max Hit Points if Start At Max is ticked.")]
    private float currentHitPoints;
    [SerializeField]
    [Tooltip("Starts with full hit points, instead of the Current Hit Points set here.")]
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

            // A character who dies comes back at the checkpoint, the hub that was reached last, with all their hit points. Only
            // for one that has some: one with none can't be hurt without dying.
            var character = GetComponent<Characters.Character>();

            if (character && maxHitPoints > 0f && CharacterHub.Respawn(character))
            {
                currentHitPoints = maxHitPoints;
                justDied = true;
            }
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
