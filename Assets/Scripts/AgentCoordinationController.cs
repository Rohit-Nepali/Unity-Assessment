using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AgentCoordinationController : MonoBehaviour
{
    public static readonly List<AgentCoordinationController> AllAgents = new();

    [Header("Safety Zones")]
    public float slowZoneRadius = 4f;
    public float stopZoneRadius = 2f;
    public float waypointStopDistance = 3f;

    [Header("Priority Weights")]
    public float speedWeight = 2f;
    public float directionWeight = 1f;
    public float distanceWeight = 0.5f;

    // private Agent agent;
    private Task3ParcelManager parcelSystem;
    private Task3Agent pathfinder;

    private Vector3 currentDestination;
    private Vector3 lastPosition;

    private float stuckTimer;
    private bool isWaitingForOtherAgent;
    private bool timerActive = true;

    private const float STUCK_THRESHOLD = 3f;

    // -------------------- LIFECYCLE --------------------

    void Awake()
    {
        AllAgents.Add(this);

        // agent = GetComponent<Agent>();
        parcelSystem = GetComponent<Task3ParcelManager>();
        pathfinder = GetComponent<Task3Agent>();

        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        AllAgents.Remove(this);
    }

    void Update()
    {
        UpdateStuckTimer();
    }

    // -------------------- STUCK LOGIC --------------------

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

    // -------------------- DESTINATION --------------------

    public void SetDestination(Vector3 destination)
    {
        currentDestination = destination;
    }

    // -------------------- SPEED NEGOTIATION --------------------

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

    // -------------------- WAYPOINT STOP --------------------

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

    // -------------------- PRIORITY --------------------

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

    // -------------------- AVOIDANCE --------------------

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

    // -------------------- DEBUG GIZMOS --------------------

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.25f);
        Gizmos.DrawSphere(transform.position, stopZoneRadius);

        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, slowZoneRadius);

        Gizmos.color = new Color(0, 1, 1, 0.15f);
        Gizmos.DrawSphere(transform.position, waypointStopDistance);
    }
}
