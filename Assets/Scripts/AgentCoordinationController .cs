using System.Collections.Generic;
using UnityEngine;

public class AgentCoordinationController : MonoBehaviour
{
    public static List<AgentCoordinationController> AllAgents = new List<AgentCoordinationController>();

    [Header("Safety Zones")]
    public float slowZoneRadius = 4f;
    public float stopZoneRadius = 2f;
    public float waypointStopDistance = 3f;

    [Header("Priority Weights")]
    public float speedWeight = 2.0f;
    public float directionWeight = 1.0f;
    public float distanceWeight = 0.5f;

    private CharacterController controller;
    private Agent agent;
    private Part2_ParcelSystem parcelSystem;
    private Vector3 currentDestination;
    private float desiredSpeed;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f;
    private Vector3 lastPosition;
    private bool isWaitingForOtherAgent = false;
    private bool timerActive = true;

    public bool IsTimerActive() => timerActive;
    public float GetStuckTimer() => stuckTimer;

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
        PathfindingTester pathfinder = GetComponent<PathfindingTester>();
        bool isCutting = pathfinder != null && pathfinder.IsCutting();
        bool isIdleAfterReturn = pathfinder != null && !pathfinder.agentMoveActive() && pathfinder.IsReturningToStart() && !isCutting;

        if (isIdleAfterReturn)
        {
            stuckTimer = 0f; // stop timer when idle at start
            timerActive = false;
        }
        else if (!isCutting)
        {
            timerActive = true;
            if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;
        }
        else
        {
            timerActive = false;
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
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, stopZoneRadius);

        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, slowZoneRadius);

        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawSphere(transform.position, waypointStopDistance);
    }

    public float GetNegotiatedSpeed(float baseSpeed)
    {
        var pathTester = GetComponent<PathfindingTester>();
        if (pathTester != null && pathTester.IsLeavingDeliveryPoint())
            return baseSpeed;

        desiredSpeed = baseSpeed;
        isWaitingForOtherAgent = false;

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist < stopZoneRadius)
            {
                bool hasRightOfWay = HasRightOfWayOver(other);
                if (!hasRightOfWay)
                {
                    isWaitingForOtherAgent = true;
                    return 0f;
                }
            }
            else if (dist < slowZoneRadius)
            {
                bool hasRightOfWay = HasRightOfWayOver(other);
                if (!hasRightOfWay)
                {
                    isWaitingForOtherAgent = true;
                    desiredSpeed *= 0.4f;
                }
            }
        }

        return desiredSpeed;
    }

    public bool ShouldStopBeforeWaypoint(Vector3 waypointPosition)
    {
        var pathTester = GetComponent<PathfindingTester>();
        if (pathTester != null && pathTester.IsLeavingDeliveryPoint())
            return false;

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float distToOther = Vector3.Distance(transform.position, other.transform.position);
            float distToWaypoint = Vector3.Distance(transform.position, waypointPosition);

            if (distToOther < waypointStopDistance && distToWaypoint < waypointStopDistance)
            {
                if (!HasRightOfWayOver(other))
                    return true;
            }
        }
        return false;
    }

    private bool HasRightOfWayOver(AgentCoordinationController other)
    {
        float myPriority = CalculatePriority();
        float otherPriority = other.CalculatePriority();

        if (Mathf.Abs(myPriority - otherPriority) < 0.01f)
            return GetInstanceID() > other.GetInstanceID();

        return myPriority > otherPriority;
    }

    private float CalculatePriority()
    {
        float baseSpeedWithParcels = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : 1f;
        float speedFactor = baseSpeedWithParcels / 8f;

        Vector3 myDir = currentDestination - transform.position;
        float distanceToDest = myDir.magnitude;
        myDir.Normalize();
        float directionFactor = Vector3.Dot(transform.forward, myDir);

        float distanceFactor = 1f / (1f + distanceToDest * 0.1f);

        return (speedFactor * speedWeight) +
               (directionFactor * directionWeight) +
               (distanceFactor * distanceWeight);
    }

    // ------------------ AVOIDANCE ------------------
    public Vector3 GetAvoidanceVector(bool isReturning, bool hasParcels)
    {
        Vector3 avoidance = Vector3.zero;

        // Avoid non-target trees
        PathfindingTester pathfinder = GetComponent<PathfindingTester>();
        if (pathfinder != null)
        {
            foreach (var tree in pathfinder.trees)
            {
                if (tree == null || tree == pathfinder.GetCurrentTree()) continue;

                float dist = Vector3.Distance(transform.position, tree.transform.position);
                if (dist < slowZoneRadius)
                {
                    Vector3 dir = (transform.position - tree.transform.position).normalized;
                    avoidance += dir / dist;
                }
            }
        }

        foreach (var other in AllAgents)
        {
            if (other == this) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < slowZoneRadius)
            {
                bool otherHasParcels = false;
                Part2_ParcelSystem otherParcelSystem = other.GetComponent<Part2_ParcelSystem>();
                if (otherParcelSystem != null)
                    otherHasParcels = otherParcelSystem.parcelCount > 0;

                if (isReturning && !hasParcels && otherHasParcels)
                {
                    Vector3 dir = (transform.position - other.transform.position).normalized;
                    avoidance += dir / dist; // stronger repulsion
                }
                else
                {
                    Vector3 dir = (transform.position - other.transform.position).normalized;
                    avoidance += dir / (dist * 2f);
                }
            }
        }

        // Normalize final avoidance to prevent speed boost
        if (avoidance.magnitude > 1f)
            avoidance.Normalize();

        return avoidance;
    }
}
