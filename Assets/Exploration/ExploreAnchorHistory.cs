using System;
using System.Collections.Generic;

/// <summary>
/// Retains a chart's location as bounded changes of chart. A distant origin
/// need not be collapsed into a Poincare point near the numerical boundary.
/// </summary>
public sealed class ExploreAnchorHistory
{
    const double IdentityPositionToleranceSquared = 1e-10;
    const double IdentityRotationToleranceSquared = 1e-10;

    // Leave a margin inside the unit Poincare disk for stable local rendering.
    const double SafePoincareRadius = 0.995;
    const double SafeRadiusSquared = SafePoincareRadius * SafePoincareRadius;
    const int ShortLoopLength = 8;

    readonly List<GyroVectorD> chartChanges = new List<GyroVectorD>();

    /// <summary>Number of retained changes of chart after local cancellation.</summary>
    public int StepCount
    {
        get { return chartChanges.Count; }
    }

    /// <summary>Uses the current chart as the new history origin.</summary>
    public void Clear()
    {
        chartChanges.Clear();
    }

    /// <summary>
    /// Call once for each terrain rebase. The argument is the new chart's pose
    /// in the old chart, exactly as used by ExploreSpace's origin rebase.
    /// </summary>
    public void Advance(GyroVector anchor)
    {
        var chartChange = -(GyroVectorD)anchor;
        if (!IsFinite(chartChange) || chartChange.vec.sqrMagnitude >= 1)
        {
            throw new ArgumentException("A hyperbolic origin rebase must be a finite interior chart pose.", "anchor");
        }

        if (IsIdentity(chartChange))
        {
            return;
        }

        int lastIndex = chartChanges.Count - 1;
        if (lastIndex >= 0 && IsIdentity(chartChanges[lastIndex] + chartChange))
        {
            chartChanges.RemoveAt(lastIndex);
        }
        else
        {
            chartChanges.Add(chartChange);
        }

        SimplifyShortLoops();
    }

    /// <summary>
    /// Returns the transform from the original chart into the current chart.
    /// An object's current pose is originalPose + originPose. False means to
    /// hide it until the original chart is near again; the retained path stays
    /// intact so outward-and-return walks can be retraced without saturation.
    /// </summary>
    public bool TryGetOriginPose(out GyroVectorD pose)
    {
        pose = GyroVectorD.identity;
        for (int i = 0; i < chartChanges.Count; i++)
        {
            var nextPose = pose + chartChanges[i];
            if (!IsFinite(nextPose) || nextPose.vec.sqrMagnitude > SafeRadiusSquared)
            {
                pose = GyroVectorD.identity;
                return false;
            }
            pose = nextPose;
        }

        return true;
    }

    void SimplifyShortLoops()
    {
        // A returned square can have a different tangent orientation. Preserve
        // that residual rotation instead of incorrectly discarding holonomy.
        bool simplified;
        do
        {
            simplified = false;
            var suffixPose = GyroVectorD.identity;
            int earliestIndex = Math.Max(0, chartChanges.Count - ShortLoopLength);
            for (int i = chartChanges.Count - 1; i >= earliestIndex; i--)
            {
                suffixPose = chartChanges[i] + suffixPose;
                if (!IsFinite(suffixPose) || suffixPose.vec.sqrMagnitude > SafeRadiusSquared)
                {
                    break;
                }

                int suffixLength = chartChanges.Count - i;
                if (suffixLength < 2 || suffixPose.vec.sqrMagnitude > IdentityPositionToleranceSquared)
                {
                    continue;
                }

                chartChanges.RemoveRange(i, suffixLength);
                var residualRotation = new GyroVectorD(suffixPose.gyr);
                if (!IsIdentity(residualRotation))
                {
                    chartChanges.Add(residualRotation);
                }

                simplified = true;
                break;
            }
        } while (simplified);
    }

    static bool IsIdentity(GyroVectorD pose)
    {
        return IsFinite(pose) &&
            pose.vec.sqrMagnitude < IdentityPositionToleranceSquared &&
            pose.gyr.x * pose.gyr.x + pose.gyr.y * pose.gyr.y + pose.gyr.z * pose.gyr.z < IdentityRotationToleranceSquared;
    }

    static bool IsFinite(GyroVectorD pose)
    {
        return IsFinite(pose.vec.x) && IsFinite(pose.vec.y) && IsFinite(pose.vec.z) &&
            IsFinite(pose.gyr.x) && IsFinite(pose.gyr.y) && IsFinite(pose.gyr.z) && IsFinite(pose.gyr.w);
    }

    static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
