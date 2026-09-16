using UnityEngine;

public class BrightTileSource : MonoBehaviour
{
    public float maxBrightness;
    public float minBrightness;
    public float glowSpeedSeconds;
    
    private bool lightUp;
    private float currentBrightness;

    private float glowStartTime;

    private float intensity; 
    
    void Start()
    {
        if (minBrightness > maxBrightness)
        {
            var temp = minBrightness;
            maxBrightness = minBrightness;
            minBrightness = temp;
        }

        intensity = 1;  //Set the base intensity, otherwise the brightness will be 0.
        
        currentBrightness = minBrightness;
        glowStartTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update()
    {
        if (glowSpeedSeconds > 0)
        {
            if (Time.realtimeSinceStartup - glowStartTime > glowSpeedSeconds)
            {
                glowStartTime = Time.realtimeSinceStartup;
                lightUp = !lightUp;
            }

            var brightDiff = (Time.realtimeSinceStartup - glowStartTime) / glowSpeedSeconds;

            if (lightUp)
            {
                currentBrightness = Mathf.Lerp(minBrightness, maxBrightness, brightDiff);
            }
            else
            {
                currentBrightness = Mathf.Lerp(maxBrightness, minBrightness, brightDiff);
            }
        }
    }

    public float GetIntensity()
    {
        return currentBrightness * intensity;
    }

    public void SetIntensity(float newIntensity)
    {
        if (newIntensity >= 0)
        {
            intensity = newIntensity;
        }
    }

    public void ResetIntensity()
    {
        intensity = 1;
    }

    public void ShutOff()
    {
        intensity = 0;
    }
}
