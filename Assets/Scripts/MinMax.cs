using System.Collections.Generic;
using UnityEngine;

public class MinMax
{
    public float max;
    public float min = 0;

    public float changeRatePerSecond = 0;

    private float current;

    public float timeOfLastUpdate;

    private Dictionary<string, float> previousMin;
    private Dictionary<string, float> previousMax;

    public void Update()
    {
        float timeSpan = Time.time - timeOfLastUpdate;
        timeOfLastUpdate = Time.time;

        if (changeRatePerSecond != 0)
        {
            current += timeSpan * changeRatePerSecond;
            Check();
        }
    }

    public void Fill()
    {
        current = max;
    }

    public void Empty()
    {
        current = min;
    }

    public void SetNewMax(float newMax)
    {
        max = newMax;
    }

    public void SetNewMin(float newMin)
    {
        min = newMin;
    }

    public void Increase(float amount)
    {
        if (amount > 0)
        {
            current += amount;
            Check();
        }
    }

    public void Decrease(float amount)
    {
        if (amount > 0)
        {
            current -= amount;
            Check();
        }
    }

    public float GetCurrent()
    {
        return current;
    }

    private void Check()
    {
        current = Mathf.Clamp(current, min, max);
    }

    private void SaveMin(string name)
    {
        previousMin[name] = min;
    }

    private void SaveMax(string name)
    {
        previousMax[name] = max;
    }

    private bool RestorePreviousMin(string name)
    {
        return previousMin.TryGetValue(name, out min);
    }

    private bool RestorePreviousMax(string name)
    {
        return previousMax.TryGetValue(name, out max);
    }
}
