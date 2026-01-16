using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Task3Agent : MonoBehaviour
{
    [Header("ACO Configuration")]
    [Tooltip("Pheromone importance")]
    public float Alpha = 2f;
    [Tooltip("Distance importance")]
    public float Beta = 1f;
    [Tooltip("Evaporation rate")]
    public float Evaporation = 0.5f;
    [Tooltip("Pheromone deposit amount")]
    public float Q = 1.0f;
    public int MaxIterations = 100;
    public int NumberOfAnts = 20;

    [Header("Mission Targets")]
    [Tooltip("The home base to return to after mission")]
    public GameObject StartHomeNode;

    [Tooltip("The final destination to drop off ALL wood logs")]
    public GameObject DeliveryPoint;

    [Tooltip("The specific trees this agent must cut and collect")]
    public List<GameObject> TreesToCut = new List<GameObject>();

    [Header("Movement")]
    public float CuttingTime = 4f;

    // --- Components ---
    private CharacterController m_Controller;
    private Agent m_AgentVisuals;
    private AgentCoordinationController m_Coordinator;
    private Task3ParcelManager m_ParcelSystem;
    private ACOCON m_ACO;
    private AStarManager m_AStarManager = new AStarManager();

    // --- State ---
    private List<ACOConnection> m_ACORoute;
    private List<GameObject> m_OrderedTrees;
    private int m_CurrentTargetIndex = 0;

    private List<Connection> m_CurrentPhysicalPath;
    private int m_CurrentWaypointIndex = 0;

    // FSM Flags
    private bool m_IsMoving = false;
    private bool m_IsCutting = false;
    private bool m_Delivering = false;
    private bool m_ReturningHome = false;
    private bool m_MissionComplete = false;

    // --- Metrics ---
    private float m_TotalDistance = 0f;
    private float m_MissionStartTime = 0f;
    private float m_TimeToDelivery = 0f;
    private float m_TimeReturning = 0f;

    private float m_TimeAtDeliveryArrival = 0f;
    private float m_TimeAtDeliveryDeparture = 0f;

    // Tracking
    private Vector3 m_LastPos;

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC STATE QUERIES (For AgentCoordinationController)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if the agent is currently cutting a tree
    /// </summary>
    public bool IsCutting()
    {
        return m_IsCutting;
    }

    /// <summary>
    /// Returns true if the agent has completed its entire mission
    /// </summary>
    public bool IsMissionComplete()
    {
        return m_MissionComplete;
    }

    /// <summary>
    /// Returns true if the agent is in the delivery phase (going to drop off logs)
    /// </summary>
    public bool IsDelivering()
    {
        return m_Delivering && !m_ReturningHome;
    }

    /// <summary>
    /// Returns true if the agent is returning to home base after delivery
    /// </summary>
    public bool IsReturningHome()
    {
        return m_ReturningHome;
    }

    /// <summary>
    /// Returns true if the agent is currently moving
    /// </summary>
    public bool IsMoving()
    {
        return m_IsMoving;
    }

    /// <summary>
    /// Returns the current number of parcels/logs the agent is carrying
    /// </summary>
    public int GetParcelCount()
    {
        return m_ParcelSystem != null ? m_ParcelSystem.ParcelCount : 0;
    }

    /// <summary>
    /// Returns true if the agent has any parcels/logs
    /// </summary>
    public bool HasParcels()
    {
        return GetParcelCount() > 0;
    }

    /// <summary>
    /// Returns the current phase of the mission as a string (for debugging)
    /// </summary>
    public string GetCurrentPhase()
    {
        if (m_MissionComplete) return "Complete";
        if (m_ReturningHome) return "Returning Home";
        if (m_Delivering) return "Delivering";
        if (m_IsCutting) return "Cutting Tree";
        if (m_IsMoving) return "Moving to Tree";
        return "Idle";
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        Debug.Log($"[Task3Agent] Initializing ACO route for {name}...");

        m_Controller = GetComponent<CharacterController>();
        m_AgentVisuals = GetComponent<Agent>();
        m_Coordinator = GetComponent<AgentCoordinationController>();
        m_ParcelSystem = GetComponent<Task3ParcelManager>();

        // Debug check for parcel system
        if (m_ParcelSystem == null)
        {
            Debug.LogError($"[Task3Agent] Part2_ParcelSystem not found on {name}!");
        }
        else
        {
            Debug.Log($"[Task3Agent] ParcelSystem found on {name}");
        }

        // Debug check for coordinator
        if (m_Coordinator == null)
        {
            Debug.LogWarning($"[Task3Agent]  AgentCoordinationController not found on {name}. Adding one...");
            m_Coordinator = gameObject.AddComponent<AgentCoordinationController>();
        }

        m_ACO = new ACOCON();

        // Load Scene Graph for A* (Physical Movement)
        InitializeAStarGraph();

        // ACO PREPARATION
        if (TreesToCut.Count == 0 || StartHomeNode == null)
        {
            Debug.LogError("[Task3Agent] Miss configuration! Assign TreesToCut and StartHomeNode.");
            return;
        }

        // 1. Find nearest waypoints for the Start Node and ALL Trees
        List<GameObject> routeNodes = new List<GameObject>();
        routeNodes.Add(StartHomeNode);

        foreach (GameObject tree in TreesToCut)
        {
            if (tree == null) continue;
            GameObject wp = FindNearestWaypoint(tree.transform.position);
            if (wp != null && !routeNodes.Contains(wp))
            {
                routeNodes.Add(wp);
            }
        }

        // 2. Build ACO Meta-Graph
        List<ACOConnection> metaConnections = BuildMetaGraph(routeNodes);

        // 3. Setup ACO
        m_ACO.Alpha = Alpha;
        m_ACO.Beta = Beta;
        m_ACO.EvaporationFactor = Evaporation;
        m_ACO.Q = Q;

        // 4. Run ACO
        Debug.Log("[Task3Agent] Running ACO to optimize Tree Visit Order...");
        m_ACORoute = m_ACO.ACO(MaxIterations, NumberOfAnts, routeNodes.ToArray(),
                               metaConnections, StartHomeNode, routeNodes.Count + 5);

        if (m_ACORoute == null || m_ACORoute.Count == 0)
        {
            Debug.LogError("[Task3Agent] ACO failed. Defaulting to list order.");
            m_OrderedTrees = new List<GameObject>(TreesToCut);
        }
        else
        {
            Debug.Log($"[Task3Agent] ACO Success! Optimizing tree order...");
            m_OrderedTrees = new List<GameObject>();

            foreach (var leg in m_ACORoute)
            {
                GameObject targetWp = leg.ToNode;
                GameObject matchedTree = GetClosestAvailableTree(targetWp, m_OrderedTrees);
                if (matchedTree != null)
                {
                    m_OrderedTrees.Add(matchedTree);
                }
            }

            // Add any missed trees
            foreach (var t in TreesToCut)
            {
                if (!m_OrderedTrees.Contains(t)) m_OrderedTrees.Add(t);
            }
        }

        // Start Mission
        m_MissionStartTime = Time.time;
        m_LastPos = transform.position;
        StartNextObjective();
    }

    void Update()
    {
        // 1. Distance Tracking
        float distThisFrame = Vector3.Distance(transform.position, m_LastPos);
        if (distThisFrame > 0.001f) // Filter jitter
        {
            m_TotalDistance += distThisFrame;
        }
        m_LastPos = transform.position;

        // 2. Metrics Updates to Manager
        if (m_ParcelSystem != null)
        {
            // Calculate live times if not finalized
            float dispTimeDel = (m_TimeAtDeliveryArrival > 0) ? (m_TimeAtDeliveryArrival - m_MissionStartTime) : (Time.time - m_MissionStartTime);
            float dispTimeRet = (m_TimeAtDeliveryDeparture > 0) ? (Time.time - m_TimeAtDeliveryDeparture) : 0f;

            if (m_MissionComplete)
            {
                // Final frozen values
                dispTimeDel = m_TimeToDelivery;
                dispTimeRet = m_TimeReturning;
            }
            else if (m_ReturningHome && m_TimeAtDeliveryDeparture > 0)
            {
                // Current return time
                dispTimeDel = m_TimeToDelivery; // Frozen
                dispTimeRet = Time.time - m_TimeAtDeliveryDeparture;
            }
            else if (m_Delivering || m_CurrentTargetIndex < m_OrderedTrees.Count)
            {
                // On the way
                dispTimeRet = 0f;
            }

            m_ParcelSystem.UpdateStats(m_TotalDistance, dispTimeDel, dispTimeRet);
        }

        if (m_MissionComplete) return;

        if (m_IsMoving && m_CurrentPhysicalPath != null)
        {
            MoveAlongPath();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MAIN LOGIC LOOP
    // ═══════════════════════════════════════════════════════════════

    private void StartNextObjective()
    {
        m_IsMoving = false;

        // PHASE 1: TREE VISITING
        if (m_CurrentTargetIndex < m_OrderedTrees.Count)
        {
            GameObject targetTree = m_OrderedTrees[m_CurrentTargetIndex];

            if (targetTree == null)
            {
                m_CurrentTargetIndex++;
                StartNextObjective();
                return;
            }

            Debug.Log($"[Task3Agent] Objective: Go to Tree {targetTree.name} ({m_CurrentTargetIndex + 1}/{m_OrderedTrees.Count})");

            GameObject currentWp = FindNearestWaypoint(transform.position);
            GameObject treeWp = FindNearestWaypoint(targetTree.transform.position);

            m_CurrentPhysicalPath = m_AStarManager.PathfindAStar(currentWp, treeWp);

            if (m_CurrentPhysicalPath != null && m_CurrentPhysicalPath.Count > 0)
            {
                m_CurrentWaypointIndex = 0;
                m_IsMoving = true;
            }
            else if (currentWp == treeWp)
            {
                Debug.Log($"[Task3Agent] Already at tree waypoint. Starting action.");
                OnArrivedAtObjective();
            }
            else
            {
                Debug.LogWarning($"[Task3Agent] No path to tree {targetTree.name}. Skipping...");
                m_CurrentTargetIndex++;
                StartNextObjective();
            }
            return;
        }

        // PHASE 2: DELIVERY
        if (!m_Delivering)
        {
            m_Delivering = true;
            Debug.Log("[Task3Agent] All Trees Cut. Delivering to Drop Zone...");

            if (DeliveryPoint == null)
            {
                Debug.LogError("[Task3Agent] No Delivery Point assigned!");
                StartNextObjective();
                return;
            }

            GameObject currentWp = FindNearestWaypoint(transform.position);
            GameObject destWp = FindNearestWaypoint(DeliveryPoint.transform.position);

            m_CurrentPhysicalPath = m_AStarManager.PathfindAStar(currentWp, destWp);
            if (m_CurrentPhysicalPath != null && m_CurrentPhysicalPath.Count > 0)
            {
                m_CurrentWaypointIndex = 0;
                m_IsMoving = true;
            }
            return;
        }

        // PHASE 3: RETURN HOME
        if (!m_ReturningHome)
        {
            m_ReturningHome = true;
            Debug.Log("[Task3Agent] Delivered Parcels. Returning to Base...");

            GameObject currentWp = FindNearestWaypoint(transform.position);

            m_CurrentPhysicalPath = m_AStarManager.PathfindAStar(currentWp, StartHomeNode);
            if (m_CurrentPhysicalPath != null && m_CurrentPhysicalPath.Count > 0)
            {
                m_CurrentWaypointIndex = 0;
                m_IsMoving = true;
            }
            else
            {
                FinishMission();
            }
            return;
        }

        // PHASE 4: FINISH
        FinishMission();
    }

    private void FinishMission()
    {
        Debug.Log("[Task3Agent] MISSION COMPLETE. Agent is Home.");
        m_MissionComplete = true;
        m_IsMoving = false;
        m_AgentVisuals?.ForceIdle();

        // Calculate Final Return Time
        if (m_TimeAtDeliveryDeparture > 0)
        {
            m_TimeReturning = Time.time - m_TimeAtDeliveryDeparture;
        }

        // Final Stats Update
        if (m_ParcelSystem != null)
            m_ParcelSystem.UpdateStats(m_TotalDistance, m_TimeToDelivery, m_TimeReturning);

        // Notify dashboard if available
        QuadrantUIManager dashboard = FindObjectOfType<QuadrantUIManager>();
        if (dashboard != null && m_AgentVisuals != null)
        {
            dashboard.MarkMissionComplete(m_AgentVisuals);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MOVEMENT & EXECUTION
    // ═══════════════════════════════════════════════════════════════


    private void MoveAlongPath()
    {
        if (m_CurrentPhysicalPath == null) return;

        // Check if path finished
        if (m_CurrentWaypointIndex >= m_CurrentPhysicalPath.Count)
        {
            m_IsMoving = false;
            OnArrivedAtObjective();
            return;
        }

        // Get Target
        GameObject targetWp = m_CurrentPhysicalPath[m_CurrentWaypointIndex].ToNode;
        Vector3 targetPos = targetWp.transform.position;

        // Flatten Y
        Vector3 myPos = transform.position;
        myPos.y = targetPos.y;

        // Distance Check
        if (Vector3.Distance(myPos, targetPos) < 1.0f)
        {
            m_CurrentWaypointIndex++;
            return;
        }

        // Calculate base movement direction (towards waypoint)
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;

        // Speed Control
        // Speed Control
        float speed = 8f; // Fallback default

        // 1. Parcel Weight Check (Authoritative Speed Source)
        if (m_ParcelSystem != null)
        {
            speed = m_ParcelSystem.GetModifiedSpeed();
        }

        // 2. Coordination Check (Agent-Agent)
        if (m_Coordinator != null)
        {
            m_Coordinator.SetDestination(targetPos);

            // Check if should stop for other agent
            if (m_Coordinator.ShouldStopBeforeWaypoint(targetPos))
            {
                speed = 0;
            }
            else
            {
                // Negotiate speed with other agents
                float negotiatedSpeed = m_Coordinator.GetNegotiatedSpeed(speed);
                speed = negotiatedSpeed;
            }

            // Agent-Agent avoidance
            Vector3 agentAvoidance = m_Coordinator.GetAvoidanceVector(m_ReturningHome, HasParcels());
            if (agentAvoidance.magnitude > 0.01f)
            {
                dir += agentAvoidance * 0.5f;
            }

            // ═══════════════════════════════════════════════════════════
            // OBSTACLE AVOIDANCE (NEW!)
            // ═══════════════════════════════════════════════════════════
            Vector3 obstacleAvoidance = m_Coordinator.GetObstacleAvoidanceVector();
            if (obstacleAvoidance.magnitude > 0.01f)
            {
                dir += obstacleAvoidance;

                // Optional: Log when avoiding (comment out if too spammy)
                // Debug.Log($"[Task3Agent] {name} avoiding obstacle, deviation: {obstacleAvoidance.magnitude:F2}");
            }

            // Normalize the final direction
            dir.Normalize();
        }

        // Rotate towards movement direction
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 5f
            );
        }

        // Update animation speed
        if (m_AgentVisuals != null)
        {
            m_AgentVisuals.SetCurrentSpeed(speed);
        }

        // Apply movement
        m_Controller.Move(dir * speed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime);
    }

    private void OnArrivedAtObjective()
    {
        if (m_ReturningHome)
        {
            StartNextObjective();
            return;
        }

        if (m_Delivering)
        {
            Debug.Log("[Task3Agent] Arrived at Delivery Point. Offloading...");

            // Record Arrival Time
            if (m_TimeAtDeliveryArrival == 0)
            {
                m_TimeAtDeliveryArrival = Time.time;
                m_TimeToDelivery = m_TimeAtDeliveryArrival - m_MissionStartTime;
            }

            StartCoroutine(OffloadRoutine());
            return;
        }

        // At a Tree
        if (m_CurrentTargetIndex < m_OrderedTrees.Count)
        {
            GameObject tree = m_OrderedTrees[m_CurrentTargetIndex];
            StartCoroutine(CutTreeRoutine(tree));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // COROUTINES
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator CutTreeRoutine(GameObject tree)
    {
        if (tree == null)
        {
            m_CurrentTargetIndex++;
            StartNextObjective();
            yield break;
        }

        Debug.Log($"[Task3Agent] Reached Tree. Starting Cutting Sequence ({CuttingTime}s)...");
        m_IsCutting = true;
        m_AgentVisuals?.ForceIdle();

        yield return new WaitForSeconds(CuttingTime);

        Debug.Log($"[Task3Agent] TIMBER! Destroying {tree.name} and adding Parcel.");

        // Add Parcel
        if (m_ParcelSystem != null)
        {
            int beforeCount = m_ParcelSystem.ParcelCount;
            m_ParcelSystem.AddParcel();
            int afterCount = m_ParcelSystem.ParcelCount;
            Debug.Log($"[Task3Agent] Parcel count: {beforeCount} → {afterCount}");
        }
        else
        {
            Debug.LogError("[Task3Agent] ❌ Cannot add parcel - ParcelSystem is null!");
        }

        // Destroy Tree
        Destroy(tree);

        m_IsCutting = false;
        m_CurrentTargetIndex++;

        yield return new WaitForSeconds(0.5f);

        StartNextObjective();
    }


    private IEnumerator OffloadRoutine()
    {
        m_AgentVisuals?.ForceIdle();

        int count = m_ParcelSystem != null ? m_ParcelSystem.ParcelCount : 0;
        GameObject logPrefab = m_ParcelSystem != null ? m_ParcelSystem.WoodLogVisualPrefab : null;

        // --- VISUAL TRANSFER LOGIC ---
        if (DeliveryPoint != null && count > 0)
        {
            DeliveryZoneVisuals zoneVis = DeliveryPoint.GetComponent<DeliveryZoneVisuals>();
            if (zoneVis == null)
            {
                // Auto-add if missing, just in case
                zoneVis = DeliveryPoint.AddComponent<DeliveryZoneVisuals>();
            }

            // Transfer visuals
            zoneVis.AddLogs(count, logPrefab);
        }
        // -----------------------------

        if (m_ParcelSystem != null)
        {
            m_ParcelSystem.ClearParcels();
        }

        Debug.Log($"[Task3Agent] Offloaded {count} logs at {DeliveryPoint.name}");

        yield return new WaitForSeconds(1.0f);

        // Record Departure Time
        m_TimeAtDeliveryDeparture = Time.time;

        StartNextObjective();
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void InitializeAStarGraph()
    {
        GameObject[] allWaypoints = GameObject.FindGameObjectsWithTag("Waypoint");
        foreach (GameObject wp in allWaypoints)
        {
            VisGraphWaypointManager wpMan = wp.GetComponent<VisGraphWaypointManager>();
            if (wpMan != null)
            {
                foreach (VisGraphConnection con in wpMan.Connections)
                {
                    if (con.ToNode != null)
                    {
                        Connection c = new Connection();
                        c.FromNode = wp;
                        c.ToNode = con.ToNode;
                        m_AStarManager.AddConnection(c);
                    }
                }
            }
        }
    }

    private List<ACOConnection> BuildMetaGraph(List<GameObject> nodes)
    {
        List<ACOConnection> output = new List<ACOConnection>();

        Debug.Log($"[META-GRAPH] Building for {nodes.Count} goal nodes...");

        foreach (var from in nodes)
        {
            foreach (var to in nodes)
            {
                if (from == to) continue;

                var path = m_AStarManager.PathfindAStar(from, to);

                if (path != null && path.Count > 0)
                {
                    // Calculate A* path distance
                    float aStarDistance = 0f;
                    foreach (var connection in path)
                    {
                        aStarDistance += Vector3.Distance(
                            connection.FromNode.transform.position,
                            connection.ToNode.transform.position
                        );
                    }

                    ACOConnection con = new ACOConnection();
                    con.SetConnection(from, to, m_ACO.DefaultPheromone);

                    // Note: You need to add a Distance setter to ACOConnection
                    // con.Distance = aStarDistance;

                    output.Add(con);
                }
            }
        }

        Debug.Log($"[META-GRAPH] Created {output.Count} connections");
        return output;
    }

    private GameObject FindNearestWaypoint(Vector3 pos)
    {
        GameObject[] wps = GameObject.FindGameObjectsWithTag("Waypoint");
        GameObject best = null;
        float minDst = float.MaxValue;
        foreach (GameObject wp in wps)
        {
            float d = Vector3.Distance(pos, wp.transform.position);
            if (d < minDst)
            {
                minDst = d;
                best = wp;
            }
        }
        return best;
    }

    private GameObject GetClosestAvailableTree(GameObject waypoint, List<GameObject> alreadyOrdered)
    {
        GameObject best = null;
        float minDst = float.MaxValue;

        foreach (var tree in TreesToCut)
        {
            if (tree == null || alreadyOrdered.Contains(tree)) continue;

            float d = Vector3.Distance(waypoint.transform.position, tree.transform.position);
            if (d < minDst)
            {
                minDst = d;
                best = tree;
            }
        }
        return best;
    }
}