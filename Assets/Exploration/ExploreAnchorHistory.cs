using System;
using System.Collections.Generic;

/// <summary>
/// Retains the central chart without representing a very distant origin as one
/// Poincare point. Each entry is a bounded change of chart, not a distant point.
/// </summary>
public sealed class ExploreAnchorHistory
{
    readonly List<GyroVectorD> steps = new List<GyroVectorD>();
    const double IdentityPositionToleranceSquared = 1e-10;
    const double IdentityRotationToleranceSquared = 1e-10;
    const double SafeRadiusSquared = .995 * .995;
    const int ShortLoopLength = 8;

    /// <summary>Number of retained changes of chart after local cancellation.</summary>
    public int StepCount { get { return steps.Count; } }

    public void Clear() { steps.Clear(); }

    /// <summary>
    /// Call once for each terrain rebase. The argument is the new chart's pose
    /// in the old chart, exactly as used by ExploreSpace's origin rebase.
    /// </summary>
    public void Advance(GyroVector anchor)
    {
        var change = -(GyroVectorD)anchor;
        if (!Finite(change) || change.vec.sqrMagnitude >= 1)
            throw new ArgumentException("A hyperbolic origin rebase must be a finite interior chart pose.", "anchor");
        if (Identity(change)) return;

        int last = steps.Count - 1;
        if (last >= 0 && Identity(steps[last] + change))
            steps.RemoveAt(last);
        else
            steps.Add(change);

        SimplifyShortLoops();
    }

    /// <summary>
    /// Returns the pose that maps objects from the ORIGINAL central chart into
    /// the CURRENT chart. Apply an object's original pose as original + pose.
    /// False means to hide that central group until its chart is near again;
    /// the retained path is untouched and can be retraced without saturation.
    /// </summary>
    public bool TryGetOriginPose(out GyroVectorD pose)
    {
        pose = GyroVectorD.identity;
        for (int i = 0; i < steps.Count; i++)
        {
            var next = pose + steps[i];
            if (!Finite(next) || next.vec.sqrMagnitude > SafeRadiusSquared)
            {
                pose = GyroVectorD.identity;
                return false;
            }
            pose = next;
        }
        return true;
    }

    void SimplifyShortLoops()
    {
        // A returned square can have a different tangent orientation. Preserve
        // that residual rotation instead of incorrectly discarding holonomy.
        bool changed;
        do
        {
            changed = false;
            var suffix = GyroVectorD.identity;
            int first = Math.Max(0, steps.Count - ShortLoopLength);
            for (int i = steps.Count - 1; i >= first; i--)
            {
                suffix = steps[i] + suffix;
                if (!Finite(suffix) || suffix.vec.sqrMagnitude > SafeRadiusSquared)
                    break;
                int count = steps.Count - i;
                if (count < 2 || suffix.vec.sqrMagnitude > IdentityPositionToleranceSquared)
                    continue;

                steps.RemoveRange(i, count);
                var rotation = new GyroVectorD(suffix.gyr);
                if (!Identity(rotation)) steps.Add(rotation);
                changed = true;
                break;
            }
        } while (changed);
    }

    static bool Identity(GyroVectorD pose)
    {
        return Finite(pose) &&
            pose.vec.sqrMagnitude < IdentityPositionToleranceSquared &&
            pose.gyr.x * pose.gyr.x + pose.gyr.y * pose.gyr.y + pose.gyr.z * pose.gyr.z < IdentityRotationToleranceSquared;
    }

    static bool Finite(GyroVectorD pose)
    {
        return Finite(pose.vec.x) && Finite(pose.vec.y) && Finite(pose.vec.z) &&
            Finite(pose.gyr.x) && Finite(pose.gyr.y) && Finite(pose.gyr.z) && Finite(pose.gyr.w);
    }

    static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
}
