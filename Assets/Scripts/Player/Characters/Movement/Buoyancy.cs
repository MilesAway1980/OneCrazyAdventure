using UnityEngine;

// What a liquid does to a body that is in it: how much of the body is in one, and which. A character and a floating crate both use it.
public static class Buoyancy
{
    // How many places up a body are looked at, to tell how much of it is under.
    private const int Samples = 5;

    // How far above the top of a body is looked, to tell whether the surface is at it: if there's no liquid that far above it, the
    // liquid's surface is within that of its top.
    private const float SurfaceReach = 0.3f;

    // How much of its air weight a body has in a liquid, which is what its gravity is multiplied by: 1 less how much it's held up
    // (lift). Something lighter than the liquid goes negative, and rises. Something denser never has less than the tuning's Min Sink
    // Weight, since it can't float anyway, so something only a little denser sinks properly instead of hanging there.
    public static float Weight(float lift, float liquidDensity, float bodyDensity, MovementTuning tuning)
    {
        var weight = 1f - lift;

        return bodyDensity > liquidDensity ? Mathf.Max(weight, tuning.minSinkWeight) : weight;
    }

    // How much a liquid damps a body moving through it. Its thickness times two things: Fluid Damping, for as much of the body as is
    // under, which is what settles a floater and sets how fast a sinker sinks; and Splash Drag times its speed, which is what checks
    // something that goes in fast. The speed part starts as soon as any of the body is in, so a dive is slowed from the surface.
    public static float Damping(Fluid fluid, float submerged, float speed, MovementTuning tuning)
    {
        return fluid.viscosity * (tuning.fluidDamping * submerged + tuning.fluidSplashDrag * Mathf.Sqrt(submerged) * speed);
    }

    // How fast a kick or hop is, in a liquid, compared with in water, which is 1: the thicker the liquid, the slower the kick. It's
    // as high as it would be, and takes longer: the speed is multiplied by this, and the gravity by this squared, which keeps the
    // height, so a thick liquid makes the kick floaty. A thinner liquid than water kicks a little quicker.
    public static float KickPace(Fluid fluid, MovementTuning tuning)
    {
        return Mathf.Clamp((1f + tuning.kickDrag) / (1f + tuning.kickDrag * fluid.viscosity), 0.1f, 2f);
    }

    // How much of the body is in a liquid, from 0 to 1, and which liquid. Sampled up the body, so it changes smoothly as it goes in.
    // The buffer is for finding the body's colliders, so that nothing has to be made each time.
    public static float Sample(Rigidbody2D body, Collider2D[] buffer, out Fluid fluid)
    {
        return Sample(body, buffer, out fluid, out _);
    }

    // As above, and whether the body is at the surface: some of it is in a liquid, and the liquid ends at about the top of it, rather
    // than being deep over it.
    public static float Sample(Rigidbody2D body, Collider2D[] buffer, out Fluid fluid, out bool atSurface)
    {
        fluid = null;
        atSurface = false;

        var count = body.GetAttachedColliders(buffer);
        var bounds = new Bounds();
        var found = false;

        for (var i = 0; i < count; i++)
        {
            var solid = buffer[i];

            if (solid.isTrigger || !solid.enabled)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(solid.bounds);
            }
            else
            {
                bounds = solid.bounds;
                found = true;
            }
        }

        var z = body.transform.position.z;

        if (!found)
        {
            fluid = Map.FluidAt(new Vector3(body.position.x, body.position.y, z));
            return fluid != null ? 1f : 0f;
        }

        var inLiquid = 0;

        for (var i = 0; i < Samples; i++)
        {
            var y = Mathf.Lerp(bounds.min.y, bounds.max.y, (i + 0.5f) / Samples);
            var here = Map.FluidAt(new Vector3(bounds.center.x, y, z));

            if (here != null)
            {
                inLiquid++;
                fluid = here;
            }
        }

        atSurface = inLiquid > 0 && Map.FluidAt(new Vector3(bounds.center.x, bounds.max.y + SurfaceReach, z)) == null;

        return inLiquid / (float)Samples;
    }
}
