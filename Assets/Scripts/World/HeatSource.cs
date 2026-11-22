using System.Collections.Generic;
using UnityEngine;

public class HeatSource : MonoBehaviour
{
    [Tooltip("How strongly it warms characters, which is how fast it thaws them out.")]
    [SerializeField] private float heatLevel;
    [Tooltip("How far, in units, its warmth reaches.")]
    [SerializeField] private float heatRange;

    [Tooltip("How intense the heat is for setting fire to tiles. A tile that burns needs this much, or more, to catch. This is " +
             "separate from the heat level above, which is for warming characters up.")]
    [SerializeField] private float fireIntensity;

    // On at full strength, unless whatever carries it turns it down, so that a heat source just placed gives off heat.
    private float intensity = 1;
    private float range = 1;

    // Every heat source that is switched on in the scene, whether or not it is giving off heat, so that anything that
    // wants to know about them can ask without searching the scene for them.
    private static readonly List<HeatSource> active = new List<HeatSource>();

    public static IReadOnlyList<HeatSource> Active
    {
        get { return active; }
    }

    private Collider2D body;

    void Awake()
    {
        body = GetComponentInChildren<Collider2D>();
    }

    void OnEnable()
    {
        active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    // The space it takes up: that of its collider, if it has one, and otherwise just the point it is at.
    public Bounds GetBounds()
    {
        return body ? body.bounds : new Bounds(transform.position, Vector3.zero);
    }

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

    // How intense it is for setting fire to tiles, which is none while it is switched off.
    public float GetFireIntensity()
    {
        return fireIntensity * intensity;
    }

    public float GetRange()
    {
        return heatRange * range;
    }
}
