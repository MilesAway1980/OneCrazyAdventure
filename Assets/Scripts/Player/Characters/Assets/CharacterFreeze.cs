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

     private MovementSettings frozenMovementSettings;
     private MovementSettings baseMovementSettings;

     void Start()
     {
          character = GetComponent<Character>();
          if (heatSources == null)
          {
               heatSources = FindObjectsByType<HeatSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
          }
          
          frozenMovementSettings = character.gameObject.AddComponent<MovementSettings>();
          frozenMovementSettings.identifier = "frozen";
          baseMovementSettings = character.GetMovementSetting("base", 1);
          frozenMovementSettings.CloneSettings(baseMovementSettings);
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
               character.SetMovementSettingsOverride(null);
               //Destroy(this);
          }
          else if (currentFreeze > 0)
          {
               frozenMovementSettings.maxMoveSpeed = baseMovementSettings.maxMoveSpeed - baseMovementSettings.maxMoveSpeed * currentFreeze;
               frozenMovementSettings.jumpForce = baseMovementSettings.jumpForce - baseMovementSettings.jumpForce * currentFreeze;
               //character.maxMoveSpeed = character.originalMaxMoveSpeed - character.originalMaxMoveSpeed * currentFreeze;
               //character.jumpForce = character.originalJumpForce - character.originalJumpForce * currentFreeze;

               character.SetMovementSettingsOverride(frozenMovementSettings);
          }

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