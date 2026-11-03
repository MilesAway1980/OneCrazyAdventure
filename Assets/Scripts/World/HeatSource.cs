using UnityEngine;

public class HeatSource : MonoBehaviour
{
    [SerializeField] private float heatLevel;
    [SerializeField] private float heatRange;

    // On at full strength, unless whatever carries it turns it down, so that a heat source just placed gives off heat.
    private float intensity = 1;
    private float range = 1;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
           
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetIntensity(float newIntensity)
    {
        if (newIntensity >= 0)
        {
            intensity = newIntensity;
        }
    }

    public void BoostRange(float newBoost)
    {
        if (newBoost >= 0)
        {
            range = newBoost;
        }
    }

    public void Reset()
    {
        intensity = 1;
        range = 1;
    }

    public void ShutOff()
    {
        intensity = 0;
        range = 0;
    }

    // Gives off all its heat, or none.
    public void SetOn(bool on)
    {
        if (on)
        {
            Reset();
        }
        else
        {
            ShutOff();
        }
    }

    public float GetLevel()
    {
        return heatLevel * intensity;
    }

    public float GetRange()
    {
        return heatRange * range;
    }
}
