using UnityEngine;

public class HeatSource : MonoBehaviour
{
    [SerializeField] private float heatLevel;
    [SerializeField] private float heatRange;

    private float intensity;
    private float range;
    
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

    public float GetLevel()
    {
        return heatLevel * intensity;
    }

    public float GetRange()
    {
        return heatRange * range;
    }
}
