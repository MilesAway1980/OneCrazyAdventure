using System;
using System.Linq;
using System.Text;
using Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Tools > Animation: sets up character animation. See Docs/Animation.md.
//
//  - Add Animation To Characters puts a Character Animator on every character in the open scene, and gives each the Animation Set that is
//    named like the character, from the folder below. The characters are objects in each scene, not prefabs, so it's done once per scene.
//  - Create Bowling Ball Animation Set makes the Bowling Ball's set the same way: see the method.
//  - Create Zipper Animation Set makes Zipper's set from his pictures: standing, walking, going up and down, the same carrying something, and
//    interacting. It can be run again, and only changes those clips, so any others added to his set are left alone.
public static class CharacterAnimationMenu
{
    // The sets, one for each character, named like the character: Zipper.asset for the character called Zipper.
    private const string SetFolder = "Assets/Animations/Characters";

    private const string ZipperStanding = "Assets/Sprites/Player/Zipper.png";
    private const string ZipperWalking = "Assets/Sprites/Player/Zipper_walk.png";
    private const string ZipperInAir = "Assets/Sprites/Player/Zipper_air.png";
    private const string ZipperCarrying = "Assets/Sprites/Player/Zipper_carry.png";
    private const string ZipperInteracting = "Assets/Sprites/Player/Zipper_interact.png";

    [MenuItem("Tools/Animation/Add Animation To Characters")]
    private static void AddToCharacters()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Add Animation To Characters", "Stop the game first: changes made while it plays are lost when it stops.", "OK");
            return;
        }

        var characters = Object.FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (characters.Length == 0)
        {
            EditorUtility.DisplayDialog("Add Animation To Characters", "There are no characters in the open scene.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Animation To Characters");

        var added = 0;
        var given = new StringBuilder();
        var without = new StringBuilder();

        foreach (var character in characters.OrderBy(c => c.name, StringComparer.Ordinal))
        {
            var go = character.gameObject;
            var animator = go.GetComponent<CharacterAnimator>();

            if (!animator)
            {
                animator = Undo.AddComponent<CharacterAnimator>(go);
                added++;
            }

            if (!animator.animationSet)
            {
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>($"{SetFolder}/{go.name}.asset");

                if (set)
                {
                    Undo.RecordObject(animator, "Add Animation To Characters");
                    animator.animationSet = set;
                    given.AppendLine("  " + go.name);
                }
                else
                {
                    without.AppendLine("  " + go.name);
                }
            }

            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        Undo.CollapseUndoOperations(group);

        var message = new StringBuilder();
        message.AppendLine($"{characters.Length} characters in the open scene. Added the animator to {added}.");

        if (given.Length > 0)
        {
            message.AppendLine().AppendLine("Given the animation set that's named like them:").Append(given);
        }

        if (without.Length > 0)
        {
            message.AppendLine().AppendLine($"No animation set in {SetFolder} named like them, so they keep their ordinary pictures:").Append(without);
        }

        EditorUtility.DisplayDialog("Add Animation To Characters", message.ToString(), "OK");
    }

    [MenuItem("Tools/Animation/Create Zipper Animation Set")]
    private static void CreateZipperSet()
    {
        var standing = AssetDatabase.LoadAssetAtPath<Sprite>(ZipperStanding);
        var walking = AssetDatabase.LoadAllAssetsAtPath(ZipperWalking).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();

        if (!standing || walking.Length == 0)
        {
            EditorUtility.DisplayDialog("Create Zipper Animation Set",
                $"Couldn't find his pictures. Needed: {ZipperStanding} as a sprite, and {ZipperWalking} cut into sprites.", "OK");
            return;
        }

        var set = GetOrCreateSet("Zipper");

        // Standing is one picture, and Walking goes round, quicker the faster he walks.
        set.SetClip(CharacterAnimationSet.Idle, null, new[] { standing }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Walk, null, walking, 12f, true, true);

        // In the air: the first picture is going up, and the rest, which swap back and forth, are coming down.
        var inAir = AssetDatabase.LoadAllAssetsAtPath(ZipperInAir).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();

        if (inAir.Length >= 2)
        {
            set.SetClip(CharacterAnimationSet.Rise, null, new[] { inAir[0] }, 1f, true, false);
            set.SetClip(CharacterAnimationSet.Fall, null, inAir.Skip(1).ToArray(), 8f, true, false);
        }
        else
        {
            Debug.LogWarning($"Zipper has no pictures for going up and down at {ZipperInAir}, so he has no Rise or Fall clips yet.");
        }

        // Carrying: the first picture is standing, the next four walking, then going up, then coming down.
        var carrying = LoadSprites(ZipperCarrying);

        if (carrying.Length >= 7)
        {
            set.SetClip(CharacterAnimationSet.Idle, CharacterAnimationSet.Carrying, new[] { carrying[0] }, 1f, true, false);
            set.SetClip(CharacterAnimationSet.Walk, CharacterAnimationSet.Carrying, carrying.Skip(1).Take(4).ToArray(), 8f, true, true);
            set.SetClip(CharacterAnimationSet.Rise, CharacterAnimationSet.Carrying, new[] { carrying[5] }, 1f, true, false);
            set.SetClip(CharacterAnimationSet.Fall, CharacterAnimationSet.Carrying, new[] { carrying[6] }, 1f, true, false);
        }
        else
        {
            Debug.LogWarning($"Zipper has no pictures for carrying at {ZipperCarrying} (seven are needed), so he has no Carrying clips yet.");
        }

        // Interacting: reaching and then pulling. It plays through once and stays on the pull for as long as he keeps working something, so the
        // pull is the last picture. (The sheet's third picture, letting go, isn't used yet.)
        var interacting = LoadSprites(ZipperInteracting);

        if (interacting.Length >= 2)
        {
            set.SetClip(CharacterAnimationSet.Interact, null, interacting.Take(2).ToArray(), 10f, false, false);
        }
        else
        {
            Debug.LogWarning($"Zipper has no pictures for interacting at {ZipperInteracting} (two are needed), so he has no Interact clip yet.");
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"Zipper's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle (1 picture), Walk ({walking.Length} pictures), " +
                  $"Rise and Fall ({inAir.Length} pictures between them), Carrying ({carrying.Length}) and Interact ({interacting.Length}). " +
                  "Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    private const string PlayerSprites = "Assets/Sprites/Player/";

    [MenuItem("Tools/Animation/Create Bowling Ball Animation Set")]
    private static void CreateBowlingBallSet()
    {
        var standing = LoadSprites(PlayerSprites + "BowlingBall.png");
        var jumping = LoadSprites(PlayerSprites + "BowlingBallJump.png");
        var rolled = LoadSprites(PlayerSprites + "BowlingBall_use.png");
        var walking = LoadSprites(PlayerSprites + "BowlingBall_walk.png");
        var curling = LoadSprites(PlayerSprites + "BowlingBall_curl.png");
        var falling = LoadSprites(PlayerSprites + "BowlingBall_fall.png");
        var interacting = LoadSprites(PlayerSprites + "BowlingBall_interact.png");

        if (standing.Length < 1 || jumping.Length < 1 || rolled.Length < 1 || walking.Length < 1 || curling.Length < 1 ||
            falling.Length < 1 || interacting.Length < 1)
        {
            EditorUtility.DisplayDialog("Create Bowling Ball Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: BowlingBall, BowlingBallJump, BowlingBall_use, and the cut-up sheets " +
                "BowlingBall_walk, _curl, _fall and _interact.", "OK");
            return;
        }

        // His set is named like him: the character is called Bowling Ball.
        var set = GetOrCreateSet("Bowling Ball");

        set.SetClip(CharacterAnimationSet.Idle, null, new[] { standing[0] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Walk, null, walking, 8f, true, true);
        set.SetClip(CharacterAnimationSet.Rise, null, new[] { jumping[0] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Fall, null, falling, 8f, true, false);

        // Rolling: curling up, which goes on into the ball. In the air there's nothing to curl up from, so it's the ball.
        set.SetClip(CharacterAnimationSet.Action, null, curling, 14f, false, false).thenPlay = BowlingBall.RollLoopState;
        set.SetClip(BowlingBall.RollLoopState, null, new[] { rolled[0] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.AirAction, null, new[] { rolled[0] }, 1f, true, false);

        // Letting go: the curl the other way round.
        var unrolling = (Sprite[])curling.Clone();
        System.Array.Reverse(unrolling);
        set.SetClip(BowlingBall.UnrollState, null, unrolling, 14f, false, false);

        set.SetClip(CharacterAnimationSet.Interact, null, interacting, 10f, false, false);

        // Carrying: arms up, with a strained face. The first picture is standing, the next four walking, then going up and coming down, which
        // are on a taller sheet.
        var carrying = LoadSprites(PlayerSprites + "BowlingBall_carry.png");
        var carryingInAir = LoadSprites(PlayerSprites + "BowlingBall_carryair.png");

        if (carrying.Length >= 5 && carryingInAir.Length >= 2)
        {
            set.SetClip(CharacterAnimationSet.Idle, CharacterAnimationSet.Carrying, new[] { carrying[0] }, 1f, true, false);
            set.SetClip(CharacterAnimationSet.Walk, CharacterAnimationSet.Carrying, carrying.Skip(1).Take(4).ToArray(), 6f, true, true);
            set.SetClip(CharacterAnimationSet.Rise, CharacterAnimationSet.Carrying, new[] { carryingInAir[0] }, 1f, true, false);
            set.SetClip(CharacterAnimationSet.Fall, CharacterAnimationSet.Carrying, new[] { carryingInAir[1] }, 1f, true, false);
        }
        else
        {
            Debug.LogWarning("The Bowling Ball has no pictures for carrying (BowlingBall_carry, five, and BowlingBall_carryair, two), so he has no " +
                             "Carrying clips yet.");
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Bowling Ball's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk ({walking.Length} pictures), Rise, " +
                  $"Fall ({falling.Length}), Action (curling up, {curling.Length}), RollLoop, AirAction, Unroll, Interact ({interacting.Length}) and " +
                  $"Carrying ({carrying.Length + carryingInAir.Length}). " +
                  "Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The sprites a sheet has been cut into, in order of their names.
    private static Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
    }

    // The set for a character, made if there isn't one yet.
    private static CharacterAnimationSet GetOrCreateSet(string characterName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
        {
            AssetDatabase.CreateFolder("Assets", "Animations");
        }

        if (!AssetDatabase.IsValidFolder(SetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Animations", "Characters");
        }

        var path = $"{SetFolder}/{characterName}.asset";
        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(path);

        if (!set)
        {
            set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            AssetDatabase.CreateAsset(set, path);
        }

        return set;
    }
}
