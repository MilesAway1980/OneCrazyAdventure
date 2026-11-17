using UnityEngine;
using Characters;

public class CharacterFreeze: MonoBehaviour
{
     [Range(0, 1)]
     public float maxFreeze;

     private float freezeSeconds;
     private float thawSeconds;
     
     private static HeatSource[] heatSources;
     private Character character;
     private bool inFreezeZone;
     
     public float currentFreeze;

     void Start()
     {
          character = GetComponent<Character>();
          if (heatSources == null)
          {
               heatSources = FindObjectsByType<HeatSource>(FindObjectsInactive.Exclude);
          }
     }

     void Update()
     {
          if (inFreezeZone)
          {
               currentFreeze += Time.deltaTime / freezeSeconds * maxFreeze;
               if (currentFreeze > maxFreeze)
               {
                    currentFreeze = maxFreeze;
               }
          }
          else
          {
               currentFreeze -= Time.deltaTime / thawSeconds;
          }
          
          foreach (var heatSource in heatSources)
          {
               var distance = Vector3.Distance(transform.position, heatSource.gameObject.transform.position);
               var intensity = heatSource.GetRange() - distance;
               if (intensity < 0)
               {
                    intensity = 0;
               }
               
               currentFreeze -= Time.deltaTime * intensity * heatSource.GetLevel();
          }
          
          if (currentFreeze <= 0)
          {
               currentFreeze = 0;
          }

          // Frozen, he runs and jumps that much less, whatever else he's doing or in.
          character.SetMovementScale(1f - currentFreeze, 1f - currentFreeze);

          inFreezeZone = false;
     }

     public void SetFreezeSeconds(float seconds)
     {
          freezeSeconds = seconds;
     }
     
     public void SetThawSeconds(float seconds)
     {
          thawSeconds = seconds;
     }

     public void SetMaxFreeze(float max)
     {
          maxFreeze = max * 0.01f;
          inFreezeZone = true;
     }
}