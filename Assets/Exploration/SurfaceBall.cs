using UnityEngine;

/// <summary>
/// Rolls a kickable ball along ground geodesics, with curved-space collisions
/// and a retained position across streamed changes of chart.
/// </summary>
public class SurfaceBall : MonoBehaviour
{
    public HyperObject hyper;
    public Transform visual;
    public float speed;

    /// <summary>Visual ball radius in unit-curvature geodesic distance.</summary>
    public const float Radius = 0.025f;

    // HyperTranslate(d) travels 2d in the unit-curvature metric. The movement
    // constants therefore use half-distance units per second (speed) and
    // per second squared (deceleration).
    const float KickSpeed = 0.65f;
    const float RollingDeceleration = 0.035f;
    const float MaximumStepDistance = 0.002f;
    const float MaximumFrameTime = 0.05f;
    const float MetricDistancePerTranslation = 2f;
    const float CollisionRestitution = 0.55f;

    // Interaction distances are measured in the player's ground Poincare chart.
    const float KickRange = 0.22f;
    const float MinimumLookDirectionSquared = 0.01f;
    const float MinimumAimAlignment = 0.4f;
    const double SafePoincareRadius = 0.995;
    const double SafePoincareRadiusSquared = SafePoincareRadius * SafePoincareRadius;
    const float PoincareRadiusPerMetricRadius = 0.5f;
    const float MaximumObstacleChartDistanceSquared = 2f;
    const float MinimumClosestPointDistanceSquared = 1e-12f;
    const float MinimumTangentNormalSquared = 1e-10f;

    GyroVectorD currentGroundPose;
    GyroVectorD anchoredGroundPose;
    readonly ExploreAnchorHistory chartHistory = new ExploreAnchorHistory();
    bool isInitialized;
    bool isVisible = true;
    Renderer[] ballRenderers;

    /// <summary>Number of obstacle impacts since the ball was initialized.</summary>
    public int CollisionCount { get; private set; }

    /// <summary>Whether the ball can currently be represented in the local chart.</summary>
    public bool IsVisible
    {
        get { return isVisible; }
    }

    /// <summary>
    /// Places a stationary ball on the ground and resets its motion history.
    /// The recall control also uses this to restore a distant ball.
    /// </summary>
    public void Initialize(GyroVector pose)
    {
        currentGroundPose = ProjectOntoGround((GyroVectorD)pose);
        isInitialized = true;
        speed = 0;
        CollisionCount = 0;
        hyper.highPrecision = true;
        RememberCurrentChart();
        SetVisible(true);
        ApplyPose();
    }

    void Start()
    {
        if (!isInitialized)
        {
            Initialize(hyper.localGV);
        }
    }

    /// <summary>
    /// Kicks a nearby ball in the player's horizontal look direction without
    /// changing its ground point, including when the player is jumping.
    /// </summary>
    public bool TryKick(Player player)
    {
        if (!isInitialized || !isVisible || InputManager.pauseInput || !player || !player.mainCamera)
        {
            return false;
        }

        // Project the observer onto the ground before choosing a tangent direction.
        // A kick while the player is bouncing must never pitch the ball off the surface.
        var groundView = ProjectOntoGround(HyperObject.worldGVD);
        var ballInPlayerChart = currentGroundPose + groundView;
        Vector3 ballPosition = (Vector3)ballInPlayerChart.Point();
        if (new Vector2(ballPosition.x, ballPosition.z).magnitude > KickRange)
        {
            return false;
        }

        Vector3 lookDirection = player.mainCamera.transform.forward;
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude < MinimumLookDirectionSquared)
        {
            return false;
        }

        Vector3 directionToBall = new Vector3(ballPosition.x, 0, ballPosition.z).normalized;
        if (Vector3.Dot(lookDirection.normalized, directionToBall) < MinimumAimAlignment)
        {
            return false;
        }

        // Turn the local tangent frame without changing the ball's ground point.
        Vector3 localDirection = (Vector3)(QuaternionD.Inverse(ballInPlayerChart.gyr) *
            (Vector3D)lookDirection.normalized);
        currentGroundPose = TurnTangentFrame(currentGroundPose, localDirection);
        RememberCurrentChart();
        speed = KickSpeed;
        ApplyPose();
        return true;
    }

    /// <summary>
    /// Advances rolling by elapsed seconds. Short geodesic steps prevent the
    /// ball from crossing a collider between samples; impacts reflect its tangent.
    /// </summary>
    public void Advance(float dt)
    {
        if (!isInitialized || InputManager.pauseInput || dt <= 0 || speed <= 0)
        {
            return;
        }

        float distance = speed * dt;
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(distance / MaximumStepDistance));
        float stepDistance = distance / stepCount;
        var step = new GyroVectorD((Vector3D)HM.HyperTranslate(0, 0, stepDistance));
        bool hasLocalChart = TryResolvePose();
        var movingPose = hasLocalChart ? currentGroundPose : anchoredGroundPose;
        float traveledDistance = 0;

        for (int i = 0; i < stepCount; i++)
        {
            // Left composition parallel transports the local forward direction.
            var candidatePose = step + movingPose;
            if (hasLocalChart && TryGetObstacleNormal(candidatePose, out Vector3 normal))
            {
                Vector3 reflectedDirection = Vector3.Reflect(Vector3.forward, normal);
                movingPose = TurnTangentFrame(movingPose, reflectedDirection);
                speed *= CollisionRestitution;
                CollisionCount++;
                break;
            }

            movingPose = candidatePose;
            traveledDistance += stepDistance;
        }

        speed = Mathf.Max(0, speed - RollingDeceleration * dt);
        // HyperTranslate(d) advances 2d in the unit-curvature metric.
        float rollAngle = MetricDistancePerTranslation * traveledDistance / Radius * Mathf.Rad2Deg;
        visual.Rotate(Vector3.right, rollAngle, Space.Self);
        if (hasLocalChart)
        {
            // Reanchor active motion in the current local chart before it can
            // accumulate a long path near the Poincare boundary.
            currentGroundPose = movingPose;
            RememberCurrentChart();
            ApplyPose();
        }
        else
        {
            anchoredGroundPose = movingPose;
        }
    }

    void Update()
    {
        Advance(Mathf.Min(Time.deltaTime, MaximumFrameTime));
    }

    void ApplyPose()
    {
        hyper.localGVD = currentGroundPose;
        hyper.localGV = (GyroVector)currentGroundPose;
    }

    void RememberCurrentChart()
    {
        anchoredGroundPose = currentGroundPose;
        chartHistory.Clear();
    }

    /// <summary>
    /// Records a terrain-origin change while preserving the ball's position,
    /// including when it is too distant to render safely in the current chart.
    /// </summary>
    public void Rebase(GyroVector anchor)
    {
        if (!isInitialized)
        {
            return;
        }

        chartHistory.Advance(anchor);
        TryResolvePose();
    }

    bool TryResolvePose()
    {
        if (HM.K >= 0)
        {
            currentGroundPose = anchoredGroundPose;
            SetVisible(true);
            return true;
        }

        if (chartHistory.TryGetOriginPose(out GyroVectorD originPose))
        {
            // The original object pose composes on the left of the chart change.
            var resolvedPose = anchoredGroundPose + originPose;
            double radiusSquared = resolvedPose.vec.sqrMagnitude;
            if (!double.IsNaN(radiusSquared) && !double.IsInfinity(radiusSquared) &&
                radiusSquared < SafePoincareRadiusSquared)
            {
                currentGroundPose = resolvedPose;
                SetVisible(true);
                ApplyPose();
                return true;
            }
        }

        // Keep the simulation and its path history alive while rendering is
        // hidden. Retracing terrain changes restores the stored ground pose.
        SetVisible(false);
        hyper.localGVD = GyroVectorD.identity;
        hyper.localGV = GyroVector.identity;
        return false;
    }

    void SetVisible(bool value)
    {
        isVisible = value;
        hyper.enabled = value;
        if (ballRenderers == null)
        {
            ballRenderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (var renderer in ballRenderers)
        {
            renderer.enabled = value;
        }
    }

    static GyroVectorD ProjectOntoGround(GyroVectorD pose)
    {
        return new GyroVectorD(HM.ProjectToPlane(pose.vec),
            new QuaternionD(0, pose.gyr.y, 0, pose.gyr.w));
    }

    static GyroVectorD TurnTangentFrame(GyroVectorD pose, Vector3 localDirection)
    {
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        var turn = new GyroVectorD((QuaternionD)Quaternion.Euler(0, yaw, 0));
        // A local rotation changes the heading while leaving the point fixed.
        return turn + pose;
    }

    static bool TryGetObstacleNormal(GyroVectorD pose, out Vector3 normal)
    {
        // Evaluate each obstacle in the ball's own chart. This remains stable
        // across the spherical projection pole and hyperbolic origin rebases.
        // A metric radius r has center height tan_k(r/2) and local Poincare
        // radius approximately r/2 at this small ball size.
        Vector3 center = Vector3.up * HM.TanK(Radius * PoincareRadiusPerMetricRadius);
        float collisionRadius = Radius * PoincareRadiusPerMetricRadius;

        foreach (var group in WCollider.AllColliders)
        {
            if (group == null || !group.ho || !group.ho.isActiveAndEnabled)
            {
                continue;
            }

            var obstaclePose = group.ho.highPrecision ? group.ho.localGVD : (GyroVectorD)group.ho.localGV;
            var relativePose = (GyroVector)(obstaclePose - pose);
            if (relativePose.vec.sqrMagnitude > MaximumObstacleChartDistanceSquared)
            {
                continue;
            }

            foreach (var collider in group.colliders)
            {
                Vector3 deltaToObstacle = collider.ClosestPoint(center, relativePose) - center;
                float distanceSquared = deltaToObstacle.sqrMagnitude;
                if (distanceSquared <= MinimumClosestPointDistanceSquared ||
                    distanceSquared >= collisionRadius * collisionRadius || float.IsNaN(distanceSquared))
                {
                    continue;
                }

                normal = -deltaToObstacle;
                normal.y = 0;
                if (normal.sqrMagnitude < MinimumTangentNormalSquared)
                {
                    continue;
                }

                normal.Normalize();
                return true;
            }
        }

        normal = Vector3.zero;
        return false;
    }
}
