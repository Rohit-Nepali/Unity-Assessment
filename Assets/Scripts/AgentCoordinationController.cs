using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AgentCoordinationController : MonoBehaviour
{
    public static readonly List<AgentCoordinationController> AllAgents = new();

    [Header("Agent-Agent Safety Zones")]
    public float slowZoneRadius = 4f;
    public float stopZoneRadius = 2f;
    public float waypointStopDistance = 3f;

    [Header("Priority Weights")]
    public float speedWeight = 2f;
    public float directionWeight = 1f;
    public float distanceWeight = 0.5f;

    [Header("Obstacle Avoidance")]
    [Tooltip("How far ahead to detect obstacles")]
    public float obstacleDetectionDistance = 5f;
    
    [Tooltip("Height offset for raycast origin (from agent's feet)")]
    public float raycastHeightOffset = 1f;
    
    [Tooltip("How strongly to deviate when obstacle detected (0-1)")]
    [Range(0.1f, 1f)]
    public float avoidanceStrength = 0.5f;
    
    [Tooltip("Tag used to identify obstacles")]
    public string obstacleTag = "Obstacle";
    
    [Tooltip("Enable debug visualization")]
    public bool showDebugRays = true;

    // Components
    private Task3ParcelManager parcelSystem;
    private Task3Agent pathfinder;
    private CharacterController characterController;

    // State
    private Vector3 currentDestination;
    private Vector3 lastPosition;
    private float stuckTimer;
    private bool isWaitingForOtherAgent;
    private bool timerActive = true;

    // Obstacle avoidance state
    private bool isAvoidingObstacle = false;
    private float currentAvoidanceAmount = 0f;
    private const float AVOIDANCE_SMOOTH_SPEED = 5f;

    private const float STUCK_THRESHOLD = 3f;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        AllAgents.Add(this);

        parcelSystem = GetComponent<Task3ParcelManager>();
        pathfinder = GetComponent<Task3Agent>();
        characterController = GetComponent<CharacterController>();

        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        AllAgents.Remove(this);
    }

    void Update()
    {
        UpdateStuckTimer();
        UpdateObstacleDetection();
    }

    // ═══════════════════════════════════════════════════════════════
    // OBSTACLE DETECTION & AVOIDANCE
    // ═══════════════════════════════════════════════════════════════

    private void UpdateObstacleDetection()
    {
        // Only check when agent is moving
        if (pathfinder == null || !pathfinder.IsMoving())
        {
            // Smoothly reduce avoidance when not moving
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, 0f, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
            isAvoidingObstacle = false;
            return;
        }

        // Raycast origin: agent position + height offset
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeightOffset;
        
        // Raycast direction: agent's forward direction
        Vector3 rayDirection = transform.forward;

        // Perform raycast
        RaycastHit hit;
        bool obstacleDetected = Physics.Raycast(rayOrigin, rayDirection, out hit, obstacleDetectionDistance);

        // Check if hit object has obstacle tag
        if (obstacleDetected && hit.collider.CompareTag(obstacleTag))
        {
            isAvoidingObstacle = true;
            
            // Calculate avoidance amount based on distance to obstacle
            // Closer = stronger avoidance
            float distanceRatio = 1f - (hit.distance / obstacleDetectionDistance);
            float targetAvoidance = distanceRatio * avoidanceStrength;
            
            // Smooth transition to target avoidance
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, targetAvoidance, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
            
            if (showDebugRays)
            {
                Debug.DrawLine(rayOrigin, hit.point, Color.red);
                Debug.DrawLine(hit.point, hit.point + Vector3.up * 2f, Color.red);
            }
        }
        else
        {
            // No obstacle - smoothly reduce avoidance
            isAvoidingObstacle = false;
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, 0f, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
            
            if (showDebugRays)
            {
                Debug.DrawRay(rayOrigin, rayDirection * obstacleDetectionDistance, Color.green);
            }
        }
    }


    /// Get the obstacle avoidance vector to apply to movement direction.
    /// Call this from Task3Agent's MoveAlongPath() method.

    public Vector3 GetObstacleAvoidanceVector()
    {
        if (!isAvoidingObstacle && currentAvoidanceAmount < 0.01f)
        {
            return Vector3.zero;
        }

        // Always go RIGHT when obstacle detected
        Vector3 rightDirection = transform.right;
        
        // Apply avoidance strength
        return rightDirection * currentAvoidanceAmount;
    }

    /// Check if agent is currently avoiding an obstacle
    public bool IsAvoidingObstacle()
    {
        return isAvoidingObstacle;
    }

    /// Get current avoidance amount (0 = none, 1 = maximum)
    public float GetCurrentAvoidanceAmount()
    {
        return currentAvoidanceAmount;
    }

    // ═══════════════════════════════════════════════════════════════
    // STUCK LOGIC
    // ═══════════════════════════════════════════════════════════════

    private void UpdateStuckTimer()
    {
        if (pathfinder == null)
            return;

        bool isCutting = pathfinder.IsCutting();
        bool idleAtStart =
            !pathfinder.IsMoving() &&
            pathfinder.IsReturningHome() &&
            !isCutting;

        if (idleAtStart || isCutting)
        {
            stuckTimer = 0f;
            timerActive = false;
        }
        else
        {
            timerActive = true;

            if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    public bool ShouldReroute()
    {
        return timerActive && stuckTimer > STUCK_THRESHOLD && isWaitingForOtherAgent;
    }

    public float GetStuckTimer() => stuckTimer;
    public bool IsTimerActive() => timerActive;

    // ═══════════════════════════════════════════════════════════════
    // DESTINATION
    // ═══════════════════════════════════════════════════════════════

    public void SetDestination(Vector3 destination)
    {
        currentDestination = destination;
    }

    // ═══════════════════════════════════════════════════════════════
    // AGENT-AGENT SPEED NEGOTIATION
    // ═══════════════════════════════════════════════════════════════

    public float GetNegotiatedSpeed(float baseSpeed)
    {
        if (pathfinder != null && pathfinder.IsReturningHome())
            return baseSpeed;

        float finalSpeed = baseSpeed;
        isWaitingForOtherAgent = false;

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist <= stopZoneRadius)
            {
                if (!HasRightOfWayOver(other))
                {
                    isWaitingForOtherAgent = true;
                    return 0f;
                }
            }
            else if (dist <= slowZoneRadius)
            {
                if (!HasRightOfWayOver(other))
                {
                    isWaitingForOtherAgent = true;
                    finalSpeed *= 0.4f;
                }
            }
        }

        return finalSpeed;
    }

    // ═══════════════════════════════════════════════════════════════
    // WAYPOINT STOP
    // ═══════════════════════════════════════════════════════════════

    public bool ShouldStopBeforeWaypoint(Vector3 waypoint)
    {
        if (pathfinder != null && pathfinder.IsReturningHome())
            return false;

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float dAgent = Vector3.Distance(transform.position, other.transform.position);
            float dWaypoint = Vector3.Distance(transform.position, waypoint);

            if (dAgent < waypointStopDistance && dWaypoint < waypointStopDistance)
            {
                if (!HasRightOfWayOver(other))
                    return true;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // PRIORITY
    // ═══════════════════════════════════════════════════════════════

    private bool HasRightOfWayOver(AgentCoordinationController other)
    {
        float myP = CalculatePriority();
        float otherP = other.CalculatePriority();

        if (Mathf.Abs(myP - otherP) < 0.01f)
            return GetInstanceID() > other.GetInstanceID();

        return myP > otherP;
    }

    private float CalculatePriority()
    {
        float speed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : 1f;
        float speedFactor = speed / 8f;

        Vector3 toDest = currentDestination - transform.position;
        float distance = toDest.magnitude;

        float directionFactor = distance > 0.01f
            ? Vector3.Dot(transform.forward, toDest.normalized)
            : 0f;

        float distanceFactor = 1f / (1f + distance * 0.1f);

        return
            speedFactor * speedWeight +
            directionFactor * directionWeight +
            distanceFactor * distanceWeight;
    }

    // ═══════════════════════════════════════════════════════════════
    // AGENT-AGENT AVOIDANCE
    // ═══════════════════════════════════════════════════════════════

    public Vector3 GetAvoidanceVector(bool isReturning, bool hasParcels)
    {
        Vector3 avoidance = Vector3.zero;

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist > slowZoneRadius || dist < 0.01f) continue;

            bool otherHasParcels =
                other.parcelSystem != null &&
                other.parcelSystem.ParcelCount > 0;

            Vector3 dir = (transform.position - other.transform.position).normalized;

            if (isReturning && !hasParcels && otherHasParcels)
                avoidance += dir / dist;
            else
                avoidance += dir / (dist * 2f);
        }

        if (avoidance.magnitude > 1f)
            avoidance.Normalize();

        return avoidance;
    }

    // ═══════════════════════════════════════════════════════════════
    // DEBUG GIZMOS
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Agent-Agent zones
        Gizmos.color = new Color(1, 0, 0, 0.25f);
        Gizmos.DrawSphere(transform.position, stopZoneRadius);

        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, slowZoneRadius);

        Gizmos.color = new Color(0, 1, 1, 0.15f);
        Gizmos.DrawSphere(transform.position, waypointStopDistance);

        // Obstacle detection ray
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeightOffset;
        Gizmos.color = isAvoidingObstacle ? Color.red : Color.green;
        Gizmos.DrawLine(rayOrigin, rayOrigin + transform.forward * obstacleDetectionDistance);
        
        // Draw small sphere at ray origin
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rayOrigin, 0.1f);
        
        // Draw avoidance direction when avoiding
        if (isAvoidingObstacle || currentAvoidanceAmount > 0.01f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + transform.right * currentAvoidanceAmount * 3f);
        }
    }
}