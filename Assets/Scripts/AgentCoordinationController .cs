using System.Collections.Generic;
using UnityEngine;

public class AgentCoordinationController : MonoBehaviour
{
    public static List<AgentCoordinationController> AllAgents =
        new List<AgentCoordinationController>();

    [Header("Safety Zones")]
    public float slowZoneRadius = 4f;
    public float stopZoneRadius = 2f;
    public float waypointStopDistance = 3f; // Stop before waypoints when other agent nearby

    [Header("Priority Weights")]
    public float speedWeight = 2.0f; // Increased weight for speed
    public float directionWeight = 1.0f;
    public float distanceWeight = 0.5f; // Distance to destination

    private CharacterController controller;
    private Agent agent;
    private Part2_ParcelSystem parcelSystem;
    private Vector3 currentDestination;
    private float desiredSpeed;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f; // Seconds before considering reroute
    private Vector3 lastPosition;
    private bool isWaitingForOtherAgent = false;

    void Awake()
    {
        AllAgents.Add(this);
        controller = GetComponent<CharacterController>();
        agent = GetComponent<Agent>();
        parcelSystem = GetComponent<Part2_ParcelSystem>();
        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        AllAgents.Remove(this);
    }

    void Update()
    {
        // Check if stuck
        if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPosition = transform.position;
    }

    public void SetDestination(Vector3 destination)
    {
        currentDestination = destination;
    }

    public bool ShouldReroute()
    {
        return stuckTimer > STUCK_THRESHOLD && isWaitingForOtherAgent;
    }

    void OnDrawGizmos()
    {
        // Draw stop zone (red)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, stopZoneRadius);

        // Draw slow zone (yellow)
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, slowZoneRadius);

        // Draw waypoint stop distance
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawSphere(transform.position, waypointStopDistance);
    }

    public float GetNegotiatedSpeed(float baseSpeed)
    {
        desiredSpeed = baseSpeed;
        isWaitingForOtherAgent = false;

        foreach (var other in AllAgents)
        {
            if (other == this)
                continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist < stopZoneRadius)
            {
                bool hasRightOfWay = HasRightOfWayOver(other);
                if (!hasRightOfWay)
                {
                    isWaitingForOtherAgent = true;
                    return 0f; // STOP
                }
            }
            else if (dist < slowZoneRadius)
            {
                bool hasRightOfWay = HasRightOfWayOver(other);
                if (!hasRightOfWay)
                {
                    isWaitingForOtherAgent = true;
                    desiredSpeed *= 0.4f; // SLOW
                }
            }
        }

        return desiredSpeed;
    }

    public bool ShouldStopBeforeWaypoint(Vector3 waypointPosition)
    {
        foreach (var other in AllAgents)
        {
            if (other == this)
                continue;

            float distToOther = Vector3.Distance(transform.position, other.transform.position);
            float distToWaypoint = Vector3.Distance(transform.position, waypointPosition);

            // If another agent is close and also heading to same waypoint
            if (distToOther < waypointStopDistance && distToWaypoint < waypointStopDistance)
            {
                if (!HasRightOfWayOver(other))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ---------------- NEGOTIATION ---------------- 

    private bool HasRightOfWayOver(AgentCoordinationController other)
    {
        float myPriority = CalculatePriority();
        float otherPriority = other.CalculatePriority();

        // Tie-breaker: use instance ID to prevent deadlocks
        if (Mathf.Abs(myPriority - otherPriority) < 0.01f)
        {
            return GetInstanceID() > other.GetInstanceID();
        }

        return myPriority > otherPriority;
    }

    private float CalculatePriority()
    {
        // Use BASE speed with parcels, not negotiated speed
        float baseSpeedWithParcels = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : 1f;
        
        // Normalize speed factor (assuming max speed around 8-10)
        float speedFactor = baseSpeedWithParcels / 8f;

        // Direction alignment factor
        Vector3 myDir = (currentDestination - transform.position);
        float distanceToDest = myDir.magnitude;
        myDir.Normalize();
        float directionFactor = Vector3.Dot(transform.forward, myDir);
        
        // Distance factor (closer = higher priority, but normalize)
        float distanceFactor = 1f / (1f + distanceToDest * 0.1f);

        return (speedFactor * speedWeight) + 
               (directionFactor * directionWeight) + 
               (distanceFactor * distanceWeight);
    }
}