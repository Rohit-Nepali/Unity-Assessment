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

    [Header("Obstacle Avoidance - Detection")]
    [Tooltip("How far ahead to detect obstacles")]
    public float obstacleDetectionDistance = 6f;
    
    [Tooltip("Height offset for raycast origin (from agent's feet)")]
    public float raycastHeightOffset = 1f;
    
    [Tooltip("Number of rays in the fan (5, 7, or 9 recommended)")]
    [Range(3, 11)]
    public int numberOfRays = 7;
    
    [Tooltip("Total angle of the ray fan in degrees")]
    [Range(30f, 120f)]
    public float rayFanAngle = 90f;
    
    [Header("Obstacle Avoidance - Response")]
    [Tooltip("How strongly to deviate when obstacle detected (0-1)")]
    [Range(0.1f, 1f)]
    public float avoidanceStrength = 0.7f;
    
    [Tooltip("Tag used to identify obstacles")]
    public string obstacleTag = "Obstacle";
    
    [Header("Debug")]
    [Tooltip("Enable debug visualization")]
    public bool showDebugRays = true;
    
    [Tooltip("Show which direction agent chose")]
    public bool showAvoidanceDirection = true;

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
    private float closestObstacleDistance = float.MaxValue;
    
    // Smart direction detection
    private bool shouldGoLeft = true;  // Automatically determined
    private float leftClearance = 0f;   // How clear is the left side
    private float rightClearance = 0f;  // How clear is the right side
    
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
    // SMART OBSTACLE DETECTION & AVOIDANCE
    // ═══════════════════════════════════════════════════════════════

    private void UpdateObstacleDetection()
    {
        // Only check when agent is moving
        if (pathfinder == null || !pathfinder.IsMoving())
        {
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, 0f, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
            isAvoidingObstacle = false;
            return;
        }

        // Raycast origin: agent position + height offset
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeightOffset;
        
        // Reset detection state
        bool anyObstacleDetected = false;
        closestObstacleDistance = float.MaxValue;
        
        // Track clearance on each side
        // Higher value = more obstacles (less clear)
        float leftObstacleScore = 0f;
        float rightObstacleScore = 0f;
        
        // Track closest obstacle on each side
        float leftClosestDist = obstacleDetectionDistance;
        float rightClosestDist = obstacleDetectionDistance;
        
        int leftHits = 0;
        int rightHits = 0;
        int centerHits = 0;

        // Calculate ray directions in a fan pattern
        float angleStep = rayFanAngle / (numberOfRays - 1);
        float startAngle = -rayFanAngle / 2f;

        for (int i = 0; i < numberOfRays; i++)
        {
            // Calculate ray direction
            float currentAngle = startAngle + (angleStep * i);
            Vector3 rayDirection = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            
            // Perform raycast
            RaycastHit hit;
            bool hitSomething = Physics.Raycast(rayOrigin, rayDirection, out hit, obstacleDetectionDistance);
            
            // Check if hit object has obstacle tag
            if (hitSomething && hit.collider.CompareTag(obstacleTag))
            {
                anyObstacleDetected = true;
                
                // Track overall closest obstacle
                if (hit.distance < closestObstacleDistance)
                {
                    closestObstacleDistance = hit.distance;
                }
                
                // Calculate score based on distance (closer = higher score = more dangerous)
                float dangerScore = 1f - (hit.distance / obstacleDetectionDistance);
                
                // Determine which side this ray belongs to
                if (currentAngle < -5f) // Left side (with small deadzone)
                {
                    leftHits++;
                    leftObstacleScore += dangerScore;
                    if (hit.distance < leftClosestDist)
                        leftClosestDist = hit.distance;
                }
                else if (currentAngle > 5f) // Right side (with small deadzone)
                {
                    rightHits++;
                    rightObstacleScore += dangerScore;
                    if (hit.distance < rightClosestDist)
                        rightClosestDist = hit.distance;
                }
                else // Center
                {
                    centerHits++;
                    // Center obstacles add to both sides (but less weight)
                    leftObstacleScore += dangerScore * 0.5f;
                    rightObstacleScore += dangerScore * 0.5f;
                }
                
                // Debug visualization - Red for hit
                if (showDebugRays)
                {
                    Debug.DrawLine(rayOrigin, hit.point, Color.red);
                }
            }
            else
            {
                // No hit - this direction is clear
                // Add clearance to the appropriate side
                if (currentAngle < -5f)
                {
                    // Left side is clear in this direction
                    leftClosestDist = Mathf.Max(leftClosestDist, obstacleDetectionDistance);
                }
                else if (currentAngle > 5f)
                {
                    // Right side is clear in this direction
                    rightClosestDist = Mathf.Max(rightClosestDist, obstacleDetectionDistance);
                }
                
                // Debug visualization - Green for clear
                if (showDebugRays)
                {
                    Debug.DrawRay(rayOrigin, rayDirection * obstacleDetectionDistance, Color.green);
                }
            }
        }

        // Calculate final clearance scores (higher = clearer)
        leftClearance = (obstacleDetectionDistance - leftObstacleScore) + (leftClosestDist * 0.5f);
        rightClearance = (obstacleDetectionDistance - rightObstacleScore) + (rightClosestDist * 0.5f);

        // Calculate avoidance response
        if (anyObstacleDetected)
        {
            isAvoidingObstacle = true;
            
            // ═══════════════════════════════════════════════════════
            // SMART DIRECTION DECISION
            // ═══════════════════════════════════════════════════════
            
            // Choose the side with more clearance
            if (leftClearance > rightClearance)
            {
                shouldGoLeft = true;
            }
            else if (rightClearance > leftClearance)
            {
                shouldGoLeft = false;
            }
            // If equal, keep current direction (prevents flickering)
            
            // Calculate avoidance amount based on closest obstacle
            float distanceRatio = 1f - (closestObstacleDistance / obstacleDetectionDistance);
            float targetAvoidance = distanceRatio * avoidanceStrength;
            
            // Boost avoidance if many rays detect obstacles (surrounded)
            int totalHits = leftHits + rightHits + centerHits;
            if (totalHits > 2)
            {
                targetAvoidance *= 1.0f + (totalHits * 0.15f);
                targetAvoidance = Mathf.Clamp01(targetAvoidance);
            }
            
            // Extra boost if center is blocked
            if (centerHits > 0)
            {
                targetAvoidance = Mathf.Max(targetAvoidance, 0.4f);
            }
            
            // Smooth transition to target avoidance
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, targetAvoidance, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
            
            // Debug log (optional - can be commented out)
            if (showAvoidanceDirection && Time.frameCount % 30 == 0) // Every 30 frames
            {
                string direction = shouldGoLeft ? "LEFT" : "RIGHT";
                Debug.Log($"[{gameObject.name}] Avoiding obstacle → Going {direction} (L:{leftClearance:F1} vs R:{rightClearance:F1})");
            }
        }
        else
        {
            // No obstacle - smoothly reduce avoidance
            isAvoidingObstacle = false;
            currentAvoidanceAmount = Mathf.Lerp(currentAvoidanceAmount, 0f, Time.deltaTime * AVOIDANCE_SMOOTH_SPEED);
        }
    }

    /// <summary>
    /// Get the obstacle avoidance vector to apply to movement direction.
    /// Automatically chooses LEFT or RIGHT based on which side is clearer.
    /// </summary>
    public Vector3 GetObstacleAvoidanceVector()
    {
        if (!isAvoidingObstacle && currentAvoidanceAmount < 0.01f)
        {
            return Vector3.zero;
        }

        // Automatically choose direction based on clearance analysis
        Vector3 avoidDirection = shouldGoLeft ? -transform.right : transform.right;
        
        // Apply avoidance strength
        return avoidDirection * currentAvoidanceAmount;
    }

    /// <summary>
    /// Check if agent is currently avoiding an obstacle
    /// </summary>
    public bool IsAvoidingObstacle()
    {
        return isAvoidingObstacle;
    }

    /// <summary>
    /// Get current avoidance amount (0 = none, 1 = maximum)
    /// </summary>
    public float GetCurrentAvoidanceAmount()
    {
        return currentAvoidanceAmount;
    }
    
    /// <summary>
    /// Get distance to closest detected obstacle
    /// </summary>
    public float GetClosestObstacleDistance()
    {
        return closestObstacleDistance;
    }
    
    /// <summary>
    /// Get which direction the agent decided to go
    /// </summary>
    public bool IsGoingLeft()
    {
        return shouldGoLeft;
    }
    
    /// <summary>
    /// Get clearance values for debugging
    /// </summary>
    public (float left, float right) GetClearanceValues()
    {
        return (leftClearance, rightClearance);
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

        // Obstacle detection rays (fan pattern)
        Vector3 rayOrigin = transform.position + Vector3.up * raycastHeightOffset;
        
        float angleStep = numberOfRays > 1 ? rayFanAngle / (numberOfRays - 1) : 0;
        float startAngle = -rayFanAngle / 2f;

        for (int i = 0; i < numberOfRays; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Vector3 rayDirection = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            
            // Color code: Left = Cyan, Center = White, Right = Yellow
            if (currentAngle < -5f)
                Gizmos.color = Color.cyan;      // Left rays
            else if (currentAngle > 5f)
                Gizmos.color = Color.yellow;    // Right rays
            else
                Gizmos.color = Color.white;     // Center rays
                
            Gizmos.DrawLine(rayOrigin, rayOrigin + rayDirection * obstacleDetectionDistance);
        }
        
        // Draw small sphere at ray origin
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rayOrigin, 0.15f);
        
        // Draw chosen avoidance direction
        if (isAvoidingObstacle || currentAvoidanceAmount > 0.01f)
        {
            Vector3 avoidDir = shouldGoLeft ? -transform.right : transform.right;
            
            // Magenta for LEFT, Orange for RIGHT
            Gizmos.color = shouldGoLeft ? Color.magenta : new Color(1f, 0.5f, 0f);
            
            Vector3 arrowStart = transform.position + Vector3.up * 1.5f;
            Vector3 arrowEnd = arrowStart + avoidDir * (currentAvoidanceAmount * 3f + 1f);
            
            Gizmos.DrawLine(arrowStart, arrowEnd);
            
            // Draw arrowhead
            Vector3 arrowHead1 = arrowEnd - avoidDir * 0.3f + transform.forward * 0.2f;
            Vector3 arrowHead2 = arrowEnd - avoidDir * 0.3f - transform.forward * 0.2f;
            Gizmos.DrawLine(arrowEnd, arrowHead1);
            Gizmos.DrawLine(arrowEnd, arrowHead2);
        }
        
        // Draw clearance indicators
        if (showAvoidanceDirection)
        {
            // Left clearance bar (cyan)
            Gizmos.color = Color.cyan;
            Vector3 leftBarStart = transform.position + Vector3.up * 2f - transform.right * 0.5f;
            Vector3 leftBarEnd = leftBarStart + Vector3.up * (leftClearance * 0.3f);
            Gizmos.DrawLine(leftBarStart, leftBarEnd);
            Gizmos.DrawWireCube(leftBarEnd, Vector3.one * 0.1f);
            
            // Right clearance bar (yellow)
            Gizmos.color = Color.yellow;
            Vector3 rightBarStart = transform.position + Vector3.up * 2f + transform.right * 0.5f;
            Vector3 rightBarEnd = rightBarStart + Vector3.up * (rightClearance * 0.3f);
            Gizmos.DrawLine(rightBarStart, rightBarEnd);
            Gizmos.DrawWireCube(rightBarEnd, Vector3.one * 0.1f);
        }
    }
}