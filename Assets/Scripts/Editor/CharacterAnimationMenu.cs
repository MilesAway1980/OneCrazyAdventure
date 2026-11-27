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
//  - Create Torch and Magnet Animation Set make theirs the same way, with the Torch's Snuffed variant and the Magnet's field.
//  - Create Brick Animation Set makes his the same way, with turning solid, and Create Bulb Animation Set with his flickering filament and light.
//  - Create Balloon Animation Set makes his, which is about his shape and face: blowing up, blinking, and being held.
//  - Create Bouncy Ball Animation Set makes his: a soft blob that breathes, blinks, squashes as he walks, and charges and inflates.
//  - Create Pebble Animation Set makes his: fidgeting, running, and a roll at the top of a big jump.
//  - Create Dynamite Animation Set makes his: a swaying fuse, the fuse burning in three stages, the blast and smouldering, and being put back together.
//  - Create Ladder Animation Set makes his: googly eyes, and the top of the ladder with his face while he's stretched out.
//  - Create Bowling Ball, Paperclip, Pencil and Die Animation Set make theirs the same way: see the methods.
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

    // The Brick: the ordinary clips and the Carrying ones, and turning solid, which is his action. Action pulls his limbs in and shuts his eyes, on
    // into Solid (the solid block); AirAction is the block; and Unsolid, his own state, is the first the other way round.
    [MenuItem("Tools/Animation/Create Brick Animation Set")]
    private static void CreateBrickSet()
    {
        var standing = LoadSprites(PlayerSprites + "Brick.png");
        var sheets = new[] { "Brick_walk", "Brick_air", "Brick_interact", "Brick_carry", "Brick_curl" }
            .Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (standing.Length < 1 || Missing(sheets, new[] { 4, 3, 2, 7, 3 }))
        {
            EditorUtility.DisplayDialog("Create Brick Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: Brick, and the cut-up sheets Brick_walk (4), _air (3), _interact (2), " +
                "_carry (7) and _curl (3).", "OK");
            return;
        }

        var set = GetOrCreateSet("Brick");

        AddOrdinaryClips(set, null, standing[0], sheets[0], sheets[1], sheets[2], 8f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[3]);

        // The last of the curling pictures is the solid block.
        var curling = sheets[4];
        var solid = new[] { curling[curling.Length - 1] };
        var opening = (Sprite[])curling.Clone();
        System.Array.Reverse(opening);

        set.SetClip(CharacterAnimationSet.Action, null, curling, 12f, false, false).thenPlay = Brick.SolidState;
        set.SetClip(Brick.SolidState, null, solid, 1f, true, false);
        set.SetClip(CharacterAnimationSet.AirAction, null, solid, 1f, true, false);
        set.SetClip(Brick.UnsolidState, null, opening, 12f, false, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Brick's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk, Rise, Fall, Interact, Carrying, and for turning solid " +
                  "Action, Solid, AirAction and Unsolid. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Bulb: the ordinary clips and the Carrying ones, and his light. His filament flickers a little when he stands (Idle is six pictures, going
    // round slowly), flares as he pulls (Interact), and goes orange under a load (Carrying). Lit is his action: Action is standing with the filament
    // flickering bright, AirAction the same in the air, and ActionWalk (his own state, which his script asks for) walking with it.
    [MenuItem("Tools/Animation/Create Bulb Animation Set")]
    private static void CreateBulbSet()
    {
        var sheets = new[] { "Bulb_idle", "Bulb_walk", "Bulb_air", "Bulb_interact", "Bulb_carry", "Bulb_action", "Bulb_actionwalk", "Bulb_airaction" }
            .Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 6, 4, 3, 2, 7, 6, 4, 4 }))
        {
            EditorUtility.DisplayDialog("Create Bulb Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets Bulb_idle (6), _walk (4), _air (3), _interact (2), " +
                "_carry (7), _action (6), _actionwalk (4) and _airaction (4).", "OK");
            return;
        }

        var set = GetOrCreateSet("Bulb");

        AddOrdinaryClips(set, null, sheets[0][0], sheets[1], sheets[2], sheets[3], 12f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[4]);

        // Standing, the filament flickers: the first picture of each set above is only one of them.
        set.SetClip(CharacterAnimationSet.Idle, null, sheets[0], 5f, true, false);

        set.SetClip(CharacterAnimationSet.Action, null, sheets[5], 12f, true, false);
        set.SetClip(Bulb.ActionWalkState, null, sheets[6], 12f, true, true);
        set.SetClip(CharacterAnimationSet.AirAction, null, sheets[7], 12f, true, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Bulb's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle (flickering), Walk, Rise, Fall, Interact, Carrying, and for his " +
                  "light Action, ActionWalk and AirAction. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Balloon: he's a round balloon, with no arms or legs, so his clips are about his shape and his face. Standing he blinks (the long open-eyed
    // picture and a short blink, going round). Action blows him up in six pictures, on into Inflated, the full size, which blinks too; Deflate is the
    // blowing up the other way round (his script asks for it as he lets go). Held is a variant, for while someone is hanging from his string: the same
    // with the eyes squeezed shut, > and <, which is also his Interact and his Carrying, as he has no arms to reach or lift with.
    [MenuItem("Tools/Animation/Create Balloon Animation Set")]
    private static void CreateBalloonSet()
    {
        var sheets = new[] { "Balloon_idle", "Balloon_big", "Balloon_inflate", "Balloon_held" }.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 2, 2, 6, 6 }))
        {
            EditorUtility.DisplayDialog("Create Balloon Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets Balloon_idle (2), _big (2), _inflate (6) and _held (6).", "OK");
            return;
        }

        var set = GetOrCreateSet("Balloon");

        // Blinking: open for a while, then a blink, at eight pictures a second.
        Sprite[] Blinking(Sprite[] sheet)
        {
            return Enumerable.Repeat(sheet[0], 20).Concat(new[] { sheet[1] }).ToArray();
        }

        Sprite[] Backwards(Sprite[] sheet)
        {
            var reversed = (Sprite[])sheet.Clone();
            System.Array.Reverse(reversed);
            return reversed;
        }

        var small = new[] { sheets[0][0] };

        foreach (var state in new[] { CharacterAnimationSet.Walk, CharacterAnimationSet.Rise, CharacterAnimationSet.Fall })
        {
            set.SetClip(state, null, small, 1f, true, false);
        }

        set.SetClip(CharacterAnimationSet.Idle, null, Blinking(sheets[0]), 8f, true, false);
        set.SetClip(CharacterAnimationSet.Action, null, sheets[2], 14f, false, false).thenPlay = Balloon.InflatedState;
        set.SetClip(Balloon.InflatedState, null, Blinking(sheets[1]), 8f, true, false);
        set.SetClip(Balloon.DeflateState, null, Backwards(sheets[2]), 14f, false, false);

        // Held: eyes squeezed shut and no blinking. He pulls a lever the same way, so that's his Interact, and his Carrying, as he has no arms.
        var held = new[] { sheets[3][0] };
        var heldInflated = new[] { sheets[3][5] };

        foreach (var state in new[] { CharacterAnimationSet.Idle, CharacterAnimationSet.Walk, CharacterAnimationSet.Rise, CharacterAnimationSet.Fall })
        {
            set.SetClip(state, Balloon.HeldVariant, held, 1f, true, false);
            set.SetClip(state, CharacterAnimationSet.Carrying, held, 1f, true, false);
        }

        set.SetClip(CharacterAnimationSet.Action, Balloon.HeldVariant, sheets[3], 14f, false, false).thenPlay = Balloon.InflatedState;
        set.SetClip(Balloon.InflatedState, Balloon.HeldVariant, heldInflated, 1f, true, false);
        set.SetClip(Balloon.DeflateState, Balloon.HeldVariant, Backwards(sheets[3]), 14f, false, false);
        set.SetClip(CharacterAnimationSet.Interact, null, new[] { held[0], held[0], held[0] }, 6f, false, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Balloon's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle (blinking), Walk, Rise, Fall, Interact, Carrying, and blowing up: " +
                  "Action, Inflated and Deflate, each again with the variant Held for while someone is hanging from his string. " +
                  "Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Bouncy Ball: a soft blob with two eyes and no limbs, so his clips are his shape and his face. Standing he breathes and blinks (Idle is the four pictures
    // of his idle sheet in a pattern), and walking he squashes and stretches. Charging a jump is his own state:
    // Charge squashes him down over the two seconds it takes, on into ChargeFull, trembling. Inflated is his own too, for while he's blown up, and he's
    // made bigger by his script. The picture shown for Rise is his own stretched one.
    [MenuItem("Tools/Animation/Create Bouncy Ball Animation Set")]
    private static void CreateBouncyBallSet()
    {
        var names = new[] { "BouncyBall_idle", "BouncyBall_walk", "BouncyBall_air", "BouncyBall_interact", "BouncyBall_carry", "BouncyBall_inflated",
                            "BouncyBall_charge", "BouncyBall_chargefull" };
        var sheets = names.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 4, 4, 3, 2, 7, 4, 4, 2 }))
        {
            EditorUtility.DisplayDialog("Create Bouncy Ball Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets BouncyBall_idle (4), _walk (4), _air (3), _interact (2), " +
                "_carry (7), _inflated (4), _charge (4) and _chargefull (2).", "OK");
            return;
        }

        var set = GetOrCreateSet("Bouncy Ball");

        AddOrdinaryClips(set, null, sheets[0][0], sheets[1], sheets[2], sheets[3], 10f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[4]);

        // Standing: his own picture, a breath in and out, and every so often a blink (the fourth picture), at eight a second.
        var idle = sheets[0];
        set.SetClip(CharacterAnimationSet.Idle, null,
            new[] { idle[0], idle[0], idle[0], idle[1], idle[2], idle[2], idle[1], idle[0], idle[0], idle[0], idle[0], idle[0], idle[3], idle[0] },
            8f, true, false);

        // Blown up: the same shape, pulsing. (His size is his script's.)
        set.SetClip(BouncyBall.InflatedState, null, sheets[5], 8f, true, false);

        // Charging: four pictures at two a second takes the two seconds his charge does, and then he trembles.
        set.SetClip(BouncyBall.ChargeState, null, sheets[6], 2f, false, false).thenPlay = BouncyBall.ChargeFullState;
        set.SetClip(BouncyBall.ChargeFullState, null, sheets[7], 20f, true, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Bouncy Ball's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle (breathing and blinking), Walk, Rise, Fall, Interact, " +
                  "Carrying, Inflated, and Charge and ChargeFull for charging a jump. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Pebble: fast and nimble, with no action. He fidgets standing (a toe tap, a hop on the spot, a blink), runs in six pictures, and at the top of a
    // big jump he rolls (Roll, his own state, which his script asks for): he tucks in, spins once, the whole stone turning, and opens out. Running flat out for a second he's Sprint: hands up and smiling. The rest is the
    // ordinary clips and the Carrying ones.
    [MenuItem("Tools/Animation/Create Pebble Animation Set")]
    private static void CreatePebbleSet()
    {
        var names = new[] { "Pebble_idle", "Pebble_run", "Pebble_air", "Pebble_interact", "Pebble_carry", "Pebble_roll", "Pebble_sprint" };
        var sheets = names.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 4, 6, 3, 2, 7, 10, 6 }))
        {
            EditorUtility.DisplayDialog("Create Pebble Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets Pebble_idle (4), _run (6), _air (3), _interact (2), _carry (7) " +
                "and _roll (10) and _sprint (6).", "OK");
            return;
        }

        var set = GetOrCreateSet("Pebble");

        AddOrdinaryClips(set, null, sheets[0][0], sheets[1], sheets[2], sheets[3], 14f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[4]);

        // Standing, he can't keep still: a toe tap, a hop on the spot, and a blink, at eight a second.
        var idle = sheets[0];
        set.SetClip(CharacterAnimationSet.Idle, null,
            new[] { idle[0], idle[0], idle[0], idle[0], idle[1], idle[0], idle[0], idle[0], idle[0], idle[3], idle[0], idle[0], idle[2], idle[0], idle[0], idle[0] },
            8f, true, false);

        // The roll: ten pictures at twenty a second is half a second, which is what his script gives it.
        set.SetClip(Pebble.RollState, null, sheets[5], 20f, false, false);

        // Running flat out, hands up and smiling: the run with that on top, so it follows his speed like Walk does.
        set.SetClip(Pebble.SprintState, null, sheets[6], 14f, true, true);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Pebble's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle (fidgeting), Walk (running), Rise, Fall, Interact, Carrying, " +
                  "Roll, for the top of a big jump, and Sprint, for running flat out. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Dynamite: he loves going off. Standing he's sleepy, his fuse swaying, and blinking. As his fuse burns (his charge) there are three states, Charge1,
    // 2 and 3, a grin that gets wilder and a spark that works down the fuse; going off is Boom, the blast; and Restore, a puff of smoke and twinkles, is
    // when he's put back together. Those are states his script asks for. While he's blown up he can still move, so he has the variants Exploded and
    // ExplodedCarrying: standing (smoke drifting off him), walking, in the air, interacting and carrying, on his blown-up picture.
    [MenuItem("Tools/Animation/Create Dynamite Animation Set")]
    private static void CreateDynamiteSet()
    {
        var names = new[] { "Dynamite_idle", "Dynamite_walk", "Dynamite_air", "Dynamite_interact", "Dynamite_carry", "Dynamite_charge", "Dynamite_boom",
                            "Dynamite_smoulder", "Dynamite_restore", "Dynamite_xwalk", "Dynamite_xair", "Dynamite_xinteract", "Dynamite_xcarry" };
        var sheets = names.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 6, 4, 3, 2, 7, 6, 5, 4, 4, 4, 3, 2, 7 }))
        {
            EditorUtility.DisplayDialog("Create Dynamite Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets Dynamite_idle (6), _walk (4), _air (3), _interact (2), " +
                "_carry (7), _charge (6), _boom (5), _smoulder (4), _restore (4), and the blown-up ones _xwalk (4), _xair (3), _xinteract (2) and " +
                "_xcarry (7).", "OK");
            return;
        }

        var set = GetOrCreateSet("Dynamite");

        AddOrdinaryClips(set, null, sheets[0][0], sheets[1], sheets[2], sheets[3], 6f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[4]);

        // Standing: the fuse sways slowly one way and the other, with a blink.
        var idle = sheets[0];
        set.SetClip(CharacterAnimationSet.Idle, null,
            new[] { idle[0], idle[1], idle[2], idle[1], idle[0], idle[4], idle[5], idle[4], idle[0], idle[3], idle[0], idle[1] }, 5f, true, false);

        // The fuse burning: two pictures in each of the three stages, going faster as it gets nearer.
        var charge = sheets[5];
        set.SetClip(Dynamite.Charge1State, null, new[] { charge[0], charge[1] }, 8f, true, false);
        set.SetClip(Dynamite.Charge2State, null, new[] { charge[2], charge[3] }, 12f, true, false);
        set.SetClip(Dynamite.Charge3State, null, new[] { charge[4], charge[5] }, 18f, true, false);

        // Going off, and being put back together.
        set.SetClip(Dynamite.BoomState, null, sheets[6], 14f, false, false);
        set.SetClip(Dynamite.RestoreState, null, sheets[8], 10f, false, false);

        // Blown up, he can still move: his blown-up pictures are a variant of the ordinary clips. Standing, he smoulders; the rest go as before.
        AddOrdinaryClips(set, Dynamite.ExplodedVariant, sheets[7][0], sheets[9], sheets[10], sheets[11], 6f);
        set.SetClip(CharacterAnimationSet.Idle, Dynamite.ExplodedVariant, sheets[7], 5f, true, false);
        AddCarryingClips(set, Dynamite.ExplodedCarryingVariant, sheets[12]);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Dynamite's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk, Rise, Fall, Interact, Carrying, Charge1, Charge2 and " +
                  "Charge3 for the fuse burning, Boom for going off, Restore, and the same moves again with the variants Exploded and ExplodedCarrying while he's " +
                  "blown up. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Ladder: googly eyes, and a ladder that stretches. Standing, his eyes look about and he blinks, and walking they swing about as googly eyes do. When he
    // does something hard (stretching, shrinking, being stretched out, pulling a lever, carrying) his eyes each point their own random, wrong way. Stretched
    // out he's the Action state, and Extend and Retract as he goes up and down; those clips are of the top of the ladder, where his face is, and his script
    // puts their pictures on the top piece.
    [MenuItem("Tools/Animation/Create Ladder Animation Set")]
    private static void CreateLadderSet()
    {
        var names = new[] { "Ladder_idle", "Ladder_walk", "Ladder_air", "Ladder_interact", "Ladder_carry", "Ladder_hold", "Ladder_stretch", "Ladder_retract" };
        var sheets = names.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();

        if (Missing(sheets, new[] { 6, 4, 3, 2, 7, 6, 4, 4 }))
        {
            EditorUtility.DisplayDialog("Create Ladder Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: the cut-up sheets Ladder_idle (6), _walk (4), _air (3), _interact (2), _carry (7), " +
                "_hold (6), _stretch (4) and _retract (4).", "OK");
            return;
        }

        var set = GetOrCreateSet("Ladder");

        AddOrdinaryClips(set, null, sheets[0][0], sheets[1], sheets[2], sheets[3], 7f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[4]);

        // Standing: looking about, a blink, an "oh", and a silly cross-eyed look.
        var idle = sheets[0];
        set.SetClip(CharacterAnimationSet.Idle, null,
            new[] { idle[0], idle[0], idle[1], idle[1], idle[0], idle[2], idle[2], idle[0], idle[3], idle[0], idle[0], idle[4], idle[4], idle[0], idle[5], idle[0] },
            4f, true, false);

        // Stretched out, and going up and down: the top of the ladder, with his face, and his eyes pointing every which way.
        var hold = sheets[5];
        set.SetClip(CharacterAnimationSet.Action, null, new[] { hold[0], hold[1], hold[0], hold[2], hold[3], hold[4], hold[5] }, 3f, true, false);
        set.SetClip(Ladder.ExtendState, null, sheets[6], 8f, true, false);
        set.SetClip(Ladder.RetractState, null, sheets[7], 8f, true, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Ladder's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk, Rise, Fall, Interact, Carrying, and stretched out " +
                  "Action (held), Extend and Retract, which are pictures of the top of the ladder. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    [MenuItem("Tools/Animation/Create Paperclip Animation Set")]
    private static void CreatePaperclipSet()
    {
        CreateWalkingCarryingSet("Paperclip", "Paperclip", 12f);
    }

    [MenuItem("Tools/Animation/Create Pencil Animation Set")]
    private static void CreatePencilSet()
    {
        CreateWalkingCarryingSet("Pencil", "Pencil", 12f);
    }

    // The character is called Die, and his pictures are Dice. He's heavy, so he walks slowly.
    [MenuItem("Tools/Animation/Create Die Animation Set")]
    private static void CreateDieSet()
    {
        CreateWalkingCarryingSet("Die", "Dice", 8f);
    }

    // The set for a character with no ability, whose pictures are named with the prefix (usually the character's name): <prefix>.png standing, and
    // the cut-up sheets <prefix>_walk (4), <prefix>_air (3: going up, then two for coming down that swap), <prefix>_interact (2) and <prefix>_carry
    // (7: standing, four walking, going up, coming down). No Action or AirAction. Paperclip, Pencil and Die are built this way.
    private static void CreateWalkingCarryingSet(string characterName, string pictures, float walkFps)
    {
        var standing = LoadSprites($"{PlayerSprites}{pictures}.png");
        var walking = LoadSprites($"{PlayerSprites}{pictures}_walk.png");
        var inAir = LoadSprites($"{PlayerSprites}{pictures}_air.png");
        var interacting = LoadSprites($"{PlayerSprites}{pictures}_interact.png");
        var carrying = LoadSprites($"{PlayerSprites}{pictures}_carry.png");

        if (standing.Length < 1 || walking.Length < 1 || inAir.Length < 3 || interacting.Length < 2 || carrying.Length < 7)
        {
            EditorUtility.DisplayDialog($"Create {characterName} Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: {pictures}, and the cut-up sheets {pictures}_walk (4), _air (3), " +
                "_interact (2) and _carry (7).", "OK");
            return;
        }

        var set = GetOrCreateSet(characterName);

        AddOrdinaryClips(set, null, standing[0], walking, inAir, interacting, walkFps);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, carrying);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"{characterName}'s animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk ({walking.Length} pictures), Rise, Fall, " +
                  "Interact, and Carrying (Idle, Walk, Rise, Fall). Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // Idle, Walk, Rise, Fall and Interact for one look (the variant, or null for the ordinary one): the standing picture; the walking sheet; the air
    // sheet (going up, then two for coming down that swap); and the interact sheet (reaching, then pulling, which it ends on).
    private static void AddOrdinaryClips(CharacterAnimationSet set, string variant, Sprite standing, Sprite[] walking, Sprite[] inAir,
                                         Sprite[] interacting, float walkFps)
    {
        set.SetClip(CharacterAnimationSet.Idle, variant, new[] { standing }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Walk, variant, walking, walkFps, true, true);
        set.SetClip(CharacterAnimationSet.Rise, variant, new[] { inAir[0] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Fall, variant, inAir.Skip(1).ToArray(), 8f, true, false);
        set.SetClip(CharacterAnimationSet.Interact, variant, interacting, 10f, false, false);
    }

    // The carrying clips for one look: the sheet is standing, then four walking, then going up and coming down.
    private static void AddCarryingClips(CharacterAnimationSet set, string variant, Sprite[] carrying)
    {
        set.SetClip(CharacterAnimationSet.Idle, variant, new[] { carrying[0] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Walk, variant, carrying.Skip(1).Take(4).ToArray(), 6f, true, true);
        set.SetClip(CharacterAnimationSet.Rise, variant, new[] { carrying[5] }, 1f, true, false);
        set.SetClip(CharacterAnimationSet.Fall, variant, new[] { carrying[6] }, 1f, true, false);
    }

    // Whether any sheet has fewer pictures than it needs.
    private static bool Missing(Sprite[][] sheets, int[] needed)
    {
        for (var i = 0; i < sheets.Length; i++)
        {
            if (sheets[i].Length < needed[i])
            {
                return true;
            }
        }

        return false;
    }

    // The Torch: lit; Snuffed, which is how he looks put out under liquid; and Flickering, the small flame he lights up with as he comes out of it
    // (his script asks for the variants). Each has its own walking, air, interact and carrying sheets (the carrying ones are taller, as the arms go up over the flame). The lit idle is his own picture, and the snuffed
    // one is the first picture of the snuffed walking sheet, and the small flame's the first of its, so that they line up with the rest.
    [MenuItem("Tools/Animation/Create Torch Animation Set")]
    private static void CreateTorchSet()
    {
        var standing = LoadSprites(PlayerSprites + "torch.png");
        var lit = new[] { "Torch_walk", "Torch_air", "Torch_interact", "Torch_carry" }.Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();
        var snuffed = new[] { "Torch_snuffed_walk", "Torch_snuffed_air", "Torch_snuffed_interact", "Torch_snuffed_carry" }
            .Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();
        var small = new[] { "Torch_small_walk", "Torch_small_air", "Torch_small_interact", "Torch_small_carry" }
            .Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();
        var needed = new[] { 4, 3, 2, 7 };

        if (standing.Length < 1 || Missing(lit, needed) || Missing(snuffed, needed) || Missing(small, needed))
        {
            EditorUtility.DisplayDialog("Create Torch Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: torch, and the cut-up sheets Torch_, Torch_snuffed_ and Torch_small_ walk (4), air (3), " +
                "interact (2) and carry (7).", "OK");
            return;
        }

        var set = GetOrCreateSet("Torch");

        AddOrdinaryClips(set, null, standing[0], lit[0], lit[1], lit[2], 12f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, lit[3]);
        AddOrdinaryClips(set, Torch.SnuffedVariant, snuffed[0][0], snuffed[0], snuffed[1], snuffed[2], 12f);
        AddCarryingClips(set, Torch.SnuffedCarryingVariant, snuffed[3]);
        AddOrdinaryClips(set, Torch.FlickeringVariant, small[0][0], small[0], small[1], small[2], 12f);
        AddCarryingClips(set, Torch.FlickeringCarryingVariant, small[3]);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Torch's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk, Rise, Fall, Interact and Carrying, lit, and the same " +
                  "with the variants Snuffed and SnuffedCarrying for when he's put out, and Flickering and FlickeringCarrying for the small flame as he lights up again. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
    }

    // The Magnet: the ordinary clips and the Carrying ones, and his action, which switches his field on. Action is standing with the field lines
    // flickering round him, AirAction the same in the air, and ActionWalk (his own state, which his script asks for) walking with them.
    [MenuItem("Tools/Animation/Create Magnet Animation Set")]
    private static void CreateMagnetSet()
    {
        var standing = LoadSprites(PlayerSprites + "Magnet.png");
        var sheets = new[] { "Magnet_walk", "Magnet_air", "Magnet_interact", "Magnet_carry", "Magnet_action", "Magnet_actionwalk", "Magnet_airaction" }
            .Select(n => LoadSprites($"{PlayerSprites}{n}.png")).ToArray();
        var needed = new[] { 4, 3, 2, 7, 4, 4, 4 };

        if (standing.Length < 1 || Missing(sheets, needed))
        {
            EditorUtility.DisplayDialog("Create Magnet Animation Set",
                $"Couldn't find all of his pictures in {PlayerSprites}: Magnet, and the cut-up sheets Magnet_walk (4), _air (3), _interact (2), " +
                "_carry (7), _action (4), _actionwalk (4) and _airaction (4).", "OK");
            return;
        }

        var set = GetOrCreateSet("Magnet");

        AddOrdinaryClips(set, null, standing[0], sheets[0], sheets[1], sheets[2], 12f);
        AddCarryingClips(set, CharacterAnimationSet.Carrying, sheets[3]);

        set.SetClip(CharacterAnimationSet.Action, null, sheets[4], 8f, true, false);
        set.SetClip(Magnet.ActionWalkState, null, sheets[5], 12f, true, true);
        set.SetClip(CharacterAnimationSet.AirAction, null, sheets[6], 8f, true, false);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        Debug.Log($"The Magnet's animation set is at {AssetDatabase.GetAssetPath(set)}: Idle, Walk, Rise, Fall, Interact, Carrying, and for his field " +
                  "Action, ActionWalk and AirAction. Run Tools > Animation > Add Animation To Characters to give it to him.", set);
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
