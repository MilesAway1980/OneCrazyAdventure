using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Characters
{
    // A body that walks about as normal, with a head that turns all the way round to aim a spot light.
    //
    // Hold the action and the direction input aims the head instead of walking him. If the button is let go without
    // having aimed, that was a tap, and the light steps up to the next brightness level; after the last level it goes
    // back to the first, which is normally off. The light keeps pointing the way it was aimed.
    public class Flashlight : Character
    {
        [Tooltip("The head: a child object whose sprite points right when it isn't rotated. It turns to aim the light. If left empty, a child called Head is used.")]
        public Transform head;

        [Tooltip("Degrees per second the head turns toward the direction being aimed. 0 turns it instantly.")]
        public float headTurnSpeed = 360f;

        [Tooltip("Added to the head's angle, for head art that doesn't point right when it isn't rotated.")]
        public float headAngleOffset;

        [Header("Light")]
        [Tooltip("How far the beam reaches, in world units. The brightness levels change how soft its edge is, not how far it goes.")]
        public float beamRadius = 10f;

        [Tooltip("The brightness levels he steps through, one for each tap of the action. The brighter the level, the sharper the edge of the beam and the faster it drains the battery. A level of 0 is off, so the first level is normally 0, and the light is off again when it comes back round to it.")]
        public float[] brightnessLevels = { 0f, 1f, 3f, 5f, 10f, 20f };

        [Tooltip("The light's Falloff Strength at the dimmest level that shines. The higher it is, the softer the edges of the beam.")]
        [Range(0f, 1f)]
        public float dimFalloffStrength = 1f;

        [Tooltip("The light's Falloff Strength at the brightest level. The lower it is, the sharper the edges of the beam. The levels between are spread evenly from the dimmest to this.")]
        [Range(0f, 1f)]
        public float brightFalloffStrength = 0.2f;

        [Tooltip("The level he starts at: a position in the Brightness Levels list, starting from 0.")]
        public int startingLevel;

        [Tooltip("The spot light, which should point out of the head's front. Leave empty to use a Light2D found under the head, or to have one added using the settings below.")]
        public Light2D spotLight;

        [Header("Battery")]
        [Tooltip("Whether the light runs the battery down. The brighter the level, the faster it drains, and it recharges while the light is off.")]
        public bool useBattery = true;

        [Tooltip("How much charge a full battery holds. Only the ratio to the two rates below matters.")]
        public float batteryCapacity = 100f;

        [Tooltip("Charge used each second for each unit of brightness. At 0.1 a level of 10 uses 1 a second, so a full battery of 100 lasts 100 seconds, while a level of 20 lasts 50.")]
        public float drainPerBrightness = 0.1f;

        [Tooltip("Charge regained each second while the light is off.")]
        public float rechargePerSecond = 5f;

        [Tooltip("Once the battery has run flat, the light can't be switched on again until it has recharged this much of a full battery.")]
        [Range(0f, 1f)]
        public float restartFraction = 0.1f;

        [Tooltip("Below this much of a full battery the beam starts to flicker, as a warning that it's about to run out.")]
        [Range(0f, 1f)]
        public float lowBatteryFraction = 0.2f;

        [Header("Battery Meter")]
        [Tooltip("Shows a bar in the upper right of the view of the player using him, with how much battery is left. It appears while the light is on, the battery is charging or it has run flat, and fades away a little after the battery is full again.")]
        public bool showBatteryMeter = true;

        [Tooltip("The bar's width, as a fraction of the width of the view it's in.")]
        [Range(0.05f, 0.6f)]
        public float meterWidth = 0.2f;

        [Tooltip("The bar's height, as a fraction of the height of the view it's in.")]
        [Range(0.01f, 0.15f)]
        public float meterHeight = 0.04f;

        [Tooltip("How far the bar sits from the corner, as a fraction of the height of the view.")]
        [Range(0f, 0.2f)]
        public float meterMargin = 0.03f;

        [Tooltip("How many seconds the bar stays once the light is off and the battery is full.")]
        public float meterShowSeconds = 2f;

        [Header("Water")]
        [Tooltip("Whether the light goes out while he's in liquid. It comes back when he's out of it, and can't be switched on while he's in it.")]
        public bool shortsOutInLiquid = true;

        [Header("Refusing")]
        [Tooltip("The colour his head flashes when the light can't be switched on, because the battery is flat or he is in liquid.")]
        public Color refusedColor = Color.red;

        public float refusedSeconds = 0.3f;

        [Header("Shadows")]
        [Tooltip("Off: the beam follows the project's Global Light Settings. On: it uses the shadow settings below instead. Either way, these override whatever is set on the Light2D's own Shadows section, including a light you placed yourself.")]
        public bool overrideGlobalShadows;

        [Tooltip("Whether the beam is blocked by tiles and anything else with a Shadow Caster 2D. Only used when Override Global Shadows is on.")]
        public bool castShadows = true;

        [Tooltip("How dark the shadows are. Only used when Override Global Shadows is on.")]
        [Range(0f, 1f)]
        public float shadowStrength = 0.75f;

        [Tooltip("How soft the edges of the shadows are. Only used when Override Global Shadows is on.")]
        [Range(0f, 1f)]
        public float shadowSoftness = 0.3f;

        [Tooltip("How quickly the soft edge fades. Only used when Override Global Shadows is on.")]
        [Range(0f, 1f)]
        public float shadowFalloffStrength = 0.5f;

        [Header("Settings for the light the script adds")]
        [Tooltip("These settings only apply when the script adds the light itself, because there isn't one under the head. Changing them while playing takes effect straight away, and they stay when you stop. A light you place under the head yourself keeps its own settings.")]
        public Color lightColor = Color.white;

        public float lightIntensity = 1f;

        [Tooltip("The angle, in degrees, of the beam at full brightness.")]
        public float lightInnerAngle = 20f;

        [Tooltip("The angle, in degrees, where the beam fades out completely.")]
        public float lightOuterAngle = 50f;

        // Ignores a stick that's hardly off centre.
        private const float AimDeadZone = 0.2f;

        private SpriteRenderer headRenderer;

        // Angles are in degrees, 0 pointing right and counting up from there.
        private float aimAngle;
        private float targetAngle;

        private int level;

        private float charge;

        // Set when the battery runs out, and cleared once it has recharged enough to be used again.
        private bool flat;

        private Color headColor = Color.white;
        private float refusedUntil;

        private ScreenMeter meter;
        private float meterShowUntil;

        // Whether the script made the light, and so looks after its settings.
        private bool ownsLight;

        // Whether the action is down, and whether the head has been aimed since it went down. A press that aimed
        // wasn't a tap.
        private bool actionHeld;
        private bool aimedThisPress;

        protected override void Start()
        {
            base.Start();

            if (!head)
            {
                foreach (var candidate in GetComponentsInChildren<Transform>(true))
                {
                    if (candidate != transform && candidate.name.ToLower() == "head")
                    {
                        head = candidate;
                        break;
                    }
                }
            }

            if (!head)
            {
                Debug.LogError($"{name}: the Flashlight has no head. Assign one, or add a child called Head.", this);
                enabled = false;
                return;
            }

            if (brightnessLevels == null || brightnessLevels.Length == 0)
            {
                Debug.LogWarning($"{name}: the Flashlight has no brightness levels, so it will switch between off and 8.", this);
                brightnessLevels = new[] { 0f, 8f };
            }

            level = Mathf.Clamp(startingLevel, 0, brightnessLevels.Length - 1);
            charge = batteryCapacity;

            headRenderer = head.GetComponentInChildren<SpriteRenderer>();
            if (headRenderer)
            {
                headColor = headRenderer.color;
            }

            // The body's sprite is the one the base class changes for walking, jumping and facing, so it must not be
            // the head's, which turns on its own.
            foreach (var candidate in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!candidate.transform.IsChildOf(head))
                {
                    spriteRenderer = candidate;
                    break;
                }
            }

            aimAngle = Mathf.DeltaAngle(0f, head.eulerAngles.z - headAngleOffset);
            targetAngle = aimAngle;

            FindOrCreateLight();
            ApplyLight();

            if (showBatteryMeter)
            {
                meter = gameObject.AddComponent<ScreenMeter>();
            }
        }

        protected override void Update()
        {
            base.Update();

            TurnHead();
            UpdateBattery();

            // Every frame, as the battery, the water and the flicker can all change it, even while someone else is
            // being played.
            ApplyLight();
            performingAction = IsLightOn();

            UpdateMeter();

            if (headRenderer)
            {
                headRenderer.color = Time.time < refusedUntil ? refusedColor : headColor;
            }

            if (ownsLight)
            {
                ApplyOwnedLightSettings();
            }

            // Every frame, so edits to the shared settings or to this light's own are seen while playing.
            ApplyShadows();

            // With the light on the base class shows his action sprite, and there may not be one set.
            if (IsLightOn() && !actionSprite && spriteRenderer && !spriteRenderer.sprite)
            {
                spriteRenderer.sprite = walkSprite;
            }
        }

        // Called every frame with whether the button is held.
        public override void Action(bool active)
        {
            if (active)
            {
                actionHeld = true;
            }
            else if (actionHeld)
            {
                // Just let go. If the head wasn't aimed in the meantime, that was a tap.
                actionHeld = false;

                if (!aimedThisPress)
                {
                    NextLevel();
                }

                aimedThisPress = false;
            }

            performingAction = IsLightOn();
        }

        // The player has moved on to another character. A button that was down is no longer being read, so its
        // release will never be seen; forget it, or the first frame back would count as letting go and step the level.
        public override void ControlLost()
        {
            actionHeld = false;
            aimedThisPress = false;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (actionHeld)
            {
                if (directionValue.magnitude > AimDeadZone)
                {
                    targetAngle = Mathf.Atan2(directionValue.y, directionValue.x) * Mathf.Rad2Deg;
                    aimedThisPress = true;
                }

                return;
            }

            if (canMove)
            {
                Move(directionValue);
            }
        }

        // Whether the beam is actually shining: a level is chosen, and it hasn't been put out by water or a flat battery.
        public bool IsLightOn()
        {
            return GetBrightness() > 0f && !IsShortedOut() && !flat;
        }

        // How bright the current level is. This is the level chosen, even if the light is out for now.
        public float GetBrightness()
        {
            return brightnessLevels[level];
        }

        // The Falloff Strength for the current level: the dimmest level that shines is the softest, the brightest the
        // sharpest, and the ones between are spread evenly by how bright they are.
        private float GetFalloffStrength()
        {
            var dimmest = float.MaxValue;
            var brightest = 0f;

            foreach (var brightness in brightnessLevels)
            {
                if (brightness > 0f)
                {
                    dimmest = Mathf.Min(dimmest, brightness);
                    brightest = Mathf.Max(brightest, brightness);
                }
            }

            var position = brightest > dimmest ? Mathf.InverseLerp(dimmest, brightest, GetBrightness()) : 1f;

            return Mathf.Lerp(dimFalloffStrength, brightFalloffStrength, position);
        }

        // How full the battery is, from 0 to 1.
        public float BatteryFraction
        {
            get { return batteryCapacity > 0f ? Mathf.Clamp01(charge / batteryCapacity) : 1f; }
        }

        private bool IsShortedOut()
        {
            return shortsOutInLiquid && inLiquid;
        }

        // Steps up a level, and back to the first after the last. Switching the light on from off is refused while the
        // battery is flat or he is in liquid, and his head flashes to show it.
        public void NextLevel()
        {
            var next = (level + 1) % brightnessLevels.Length;

            if (brightnessLevels[next] > 0f && (flat || IsShortedOut()))
            {
                refusedUntil = Time.time + refusedSeconds;
                return;
            }

            level = next;
            ApplyLight();
        }

        // The light uses charge according to how bright it is, and the battery recharges while it's off. Out of charge,
        // the light goes out and stays out until the battery has recovered a little.
        private void UpdateBattery()
        {
            if (!useBattery)
            {
                return;
            }

            if (IsLightOn())
            {
                charge -= GetBrightness() * drainPerBrightness * Time.deltaTime;

                if (charge <= 0f)
                {
                    charge = 0f;
                    flat = true;
                    level = OffLevel();
                }
            }
            else if (!IsShortedOut())
            {
                charge = Mathf.Min(batteryCapacity, charge + rechargePerSecond * Time.deltaTime);

                if (flat && charge >= batteryCapacity * restartFraction)
                {
                    flat = false;
                }
            }
        }

        // Keeps the bar over his head up to date. It's shown whenever something is happening to the battery, and for a
        // moment after it settles.
        private void UpdateMeter()
        {
            if (!meter)
            {
                return;
            }

            meter.SetLayout(meterWidth, meterHeight, meterMargin);

            var busy = useBattery && (IsLightOn() || flat || BatteryFraction < 0.999f || Time.time < refusedUntil);
            if (busy)
            {
                meterShowUntil = Time.time + meterShowSeconds;
            }

            // Whoever is playing him sees the meter. When they move on to another character it stays in their view,
            // since the light is still draining the battery.
            meter.Show(
                GetPlayerCamera(),
                BatteryFraction,
                Time.time < meterShowUntil,
                flat ? ScreenMeter.Pulse(Color.red, new Color(0.45f, 0f, 0f)) : ScreenMeter.RedToGreen(BatteryFraction),
                flat ? "FLAT" : ScreenMeter.Percent(BatteryFraction),
                flat ? restartFraction : -1f);
        }

        // The first level with no light, or the current one if every level shines.
        private int OffLevel()
        {
            for (var i = 0; i < brightnessLevels.Length; i++)
            {
                if (brightnessLevels[i] <= 0f)
                {
                    return i;
                }
            }

            return level;
        }

        // The beam flickering as the battery gets low: 1 when it's fine, dipping lower the closer it is to empty.
        private float FlickerFactor()
        {
            if (!useBattery || lowBatteryFraction <= 0f || BatteryFraction >= lowBatteryFraction)
            {
                return 1f;
            }

            var severity = 1f - BatteryFraction / lowBatteryFraction;
            return 1f - severity * 0.6f * Mathf.PerlinNoise(Time.time * 20f, 0.5f);
        }

        private void TurnHead()
        {
            aimAngle = headTurnSpeed > 0f
                ? Mathf.MoveTowardsAngle(aimAngle, targetAngle, headTurnSpeed * Time.deltaTime)
                : targetAngle;

            head.rotation = Quaternion.Euler(0f, 0f, aimAngle + headAngleOffset);

            // Facing left, the head would be upside down.
            if (headRenderer)
            {
                headRenderer.flipY = Mathf.Cos(aimAngle * Mathf.Deg2Rad) < 0f;
            }
        }

        private void ApplyLight()
        {
            if (!spotLight)
            {
                return;
            }

            var on = IsLightOn();

            spotLight.enabled = on;

            // How far the beam reaches is the radius of the spot light, which only the flicker of a low battery changes.
            // How bright it is shows in how soft its edges are.
            if (on && spotLight.lightType == Light2D.LightType.Point)
            {
                spotLight.pointLightOuterRadius = beamRadius * FlickerFactor();
                spotLight.falloffIntensity = GetFalloffStrength();
            }
        }

        private void FindOrCreateLight()
        {
            if (!spotLight)
            {
                spotLight = head.GetComponentInChildren<Light2D>(true);
            }

            if (spotLight)
            {
                ApplyShadows();
                return;
            }

            var lightObject = new GameObject("Spot Light");
            lightObject.transform.SetParent(head, false);

            // A spot light points up, and the head's front is to the right.
            lightObject.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            spotLight = lightObject.AddComponent<Light2D>();
            spotLight.lightType = Light2D.LightType.Point;
            ownsLight = true;

            ApplyOwnedLightSettings();

            Debug.Log($"{name}: added a spot light to the head.", this);
        }

        // The settings on this component, copied onto the light it added.
        private void ApplyOwnedLightSettings()
        {
            spotLight.color = lightColor;
            spotLight.intensity = lightIntensity;
            spotLight.pointLightInnerAngle = lightInnerAngle;
            spotLight.pointLightOuterAngle = lightOuterAngle;
        }

        private void ApplyShadows()
        {
            if (spotLight)
            {
                GlobalLightSettings.Apply(
                    spotLight, overrideGlobalShadows, castShadows, shadowStrength, shadowSoftness, shadowFalloffStrength);
            }
        }
    }
}
