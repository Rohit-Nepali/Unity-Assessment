using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathfindingTester : MonoBehaviour
{
    private CharacterController controller;

    // The A* manager.
    private AStarManager aStarManager = new AStarManager();

    // List of possible waypoints.
    private List<GameObject> waypoints = new List<GameObject>();

    // List of waypoint map connections. Represents a path.
    private List<Connection> aStarPath = new List<Connection>();

    // The start and end nodes.
    [SerializeField]
    private GameObject start;

    [SerializeField]
    private GameObject end;

    [SerializeField]
    private GameObject deliveryPoint; // Delivery location for parcels

    // Debug line offset.
    private Vector3 offset = new Vector3(0, 0.3f, 0);

    // A list of all waypoint nodes set to goal in the environment.
    private List<GameObject> waypointGoals = new List<GameObject>();

    // Movement variables.
    private float currentSpeed = 8f;
    private int currentTargetArrayIndex = 0;
    private Vector3 currentTargetPos;
    private bool agentMove = true;

    public List<GameObject> trees = new List<GameObject>(); // Support multiple trees
    public GameObject woodLogPrefab;
    public float cuttingTime = 4f;
    public int woodYield = 4; // How many logs one tree produces

    private bool cutting = false;
    private bool isIdle = false;

    public bool IsCutting()
    {
        return cutting;
    }

    private Agent agent;
    private Part2_ParcelSystem parcelSystem;
    private GameObject currentTree;
    private float finalSpeed;
    private bool hasParcels = false; // Track if agent is carrying parcels
    private bool returningToStart = false;
    private bool leavingDeliveryPoint = false;
    private bool deliveryCompleted = false;

    // Add near other private variables
    private bool movingToTreeViaAStar = false;
    private GameObject treeTargetWaypoint = null;

    // Add near other private variables
    private List<GameObject> visitedTrees = new List<GameObject>();
    private List<GameObject> remainingTrees = new List<GameObject>();
    private bool allTreesVisited = false;

    public bool IsReturningToStart()
    {
        return returningToStart;
    }
    // PathfindingTester.cs
    public bool IsIdleAtStart()
    {
        // Agent is idle if:
        // 1. Not moving along path
        // 2. Not cutting
        // 3. Returning to start has finished
        return !agentMove && !cutting && !hasParcels && !leavingDeliveryPoint;
    }

    public bool agentMoveActive()
    {
        return agentMove;
    }

    void Start()
    {
        agent = GetComponent<Agent>();
        controller = GetComponent<CharacterController>();
        parcelSystem = GetComponent<Part2_ParcelSystem>();

        if (start == null || end == null)
        {
            Debug.Log("No start or end waypoints.");
            return;
        }

        VisGraphWaypointManager tmpWpM = start.GetComponent<VisGraphWaypointManager>();
        if (tmpWpM == null)
        {
            Debug.Log("Start is not a waypoint.");
            return;
        }

        tmpWpM = end.GetComponent<VisGraphWaypointManager>();
        if (tmpWpM == null)
        {
            Debug.Log("End is not a waypoint.");
            return;
        }

        // Find all the waypoints in the level.
        GameObject[] gameObjectsWithWaypointTag = GameObject.FindGameObjectsWithTag("Waypoint");
        foreach (GameObject waypoint in gameObjectsWithWaypointTag)
        {
            VisGraphWaypointManager tmpWaypointMan =
                waypoint.GetComponent<VisGraphWaypointManager>();
            if (tmpWaypointMan)
            {
                waypoints.Add(waypoint);
            }
        }

        // Go through the waypoints and create connections.
        foreach (GameObject waypoint in waypoints)
        {
            VisGraphWaypointManager tmpWaypointMan =
                waypoint.GetComponent<VisGraphWaypointManager>();

            if (tmpWaypointMan.WaypointType == VisGraphWaypointManager.waypointPropsList.Goal)
            {
                waypointGoals.Add(waypoint);
            }

            foreach (VisGraphConnection aVisGraphConnection in tmpWaypointMan.Connections)
            {
                if (aVisGraphConnection.ToNode != null)
                {
                    Connection aConnection = new Connection();
                    aConnection.FromNode = waypoint;
                    aConnection.ToNode = aVisGraphConnection.ToNode;
                    aStarManager.AddConnection(aConnection);
                }
                else
                {
                    Debug.Log(
                        "Warning, " + waypoint.name + " has a missing to node for a connection!"
                    );
                }
            }
        }

        // Run A* pathfinding.
        aStarPath = aStarManager.PathfindAStar(start, end);
        if (aStarPath == null || aStarPath.Count == 0)
        {
            Debug.Log("Warning, A* did not return a path.");
            agentMove = false;
        }

        // Initialize tree tracking
        remainingTrees = new List<GameObject>(trees); // Copy of all trees
        visitedTrees = new List<GameObject>();
        allTreesVisited = false;

        // Select the first available tree
        SelectNextTree();
    }

    public GameObject GetCurrentTree()
    {
        return currentTree;
    }

    void OnDrawGizmos()
    {
        if (aStarPath == null)
            return;

        foreach (Connection aConnection in aStarPath)
        {
            if (aConnection == null || aConnection.FromNode == null || aConnection.ToNode == null)
                continue;

            Gizmos.color = Color.white;
            Gizmos.DrawLine(
                aConnection.FromNode.transform.position + offset,
                aConnection.ToNode.transform.position + offset
            );
        }
    }

    void Update()
    {
        if (cutting)
            return; // Do not move while cutting

        // Check for rerouting if stuck
        CheckAndRerouteIfStuck();

        if (leavingDeliveryPoint && deliveryPoint != null)
        {
            float dist = Vector3.Distance(transform.position, deliveryPoint.transform.position);
            Debug.Log($"{name} leavingDeliveryPoint dist={dist}");

            if (dist > 4f) // safely outside trigger + stop zone
            {
                Debug.Log($"{name} fully left delivery point");
                leavingDeliveryPoint = false;
                deliveryCompleted = false; // reset for next delivery
            }
        }

        // === DELIVERY ONLY AFTER ALL TREES ARE VISITED ===
        if (allTreesVisited && hasParcels && deliveryPoint != null)
        {
            // If we have a path to follow
            if (agentMove && aStarPath != null && aStarPath.Count > 0)
            {
                MoveAlongPath();
                return;
            }
            // If we should be moving but aren't
            else if (!agentMove)
            {
                MoveToDeliveryPoint();
                return;
            }
        }

        // Move to tree if agent reached the end of path
        if (currentTree != null && !cutting && !allTreesVisited)
        {
            // Check if tree still exists (might have been destroyed by another agent)
            if (currentTree == null || !trees.Contains(currentTree))
            {
                currentTree = null;
                movingToTreeViaAStar = false;
                SelectNextTree();
                return;
            }

            // If we're not already using A* to reach the tree, set it up
            if (!movingToTreeViaAStar)
            {
                Debug.Log($"{name} setting up A* to reach tree: {currentTree.name}");
                // Find nearest waypoint to the tree
                treeTargetWaypoint = FindNearestWaypoint(currentTree.transform.position);

                if (treeTargetWaypoint != null)
                {
                    // Get current waypoint (nearest to agent)
                    GameObject currentWp = FindNearestWaypoint(transform.position);

                    // Calculate A* path to tree's waypoint
                    aStarPath = aStarManager.PathfindAStar(currentWp, treeTargetWaypoint);

                    if (aStarPath != null && aStarPath.Count > 0)
                    {
                        currentTargetArrayIndex = 0;
                        agentMove = true;  // Use the existing A* movement system
                        movingToTreeViaAStar = true;
                        Debug.Log($"{name} using A* to reach tree via {treeTargetWaypoint.name}");
                    }
                    else
                    {
                        // Fallback to direct movement if no A* path
                        Debug.LogWarning($"{name} no A* path to tree, using direct movement");
                        movingToTreeViaAStar = false;
                    }
                }
            }

            // If we're using A* to reach the tree, let MoveAlongPath() handle movement
            if (movingToTreeViaAStar && agentMove)
            {
                MoveAlongPath();
                return;
            }

            Vector3 targetPos = new Vector3(
                currentTree.transform.position.x,
                transform.position.y,
                currentTree.transform.position.z
            );

            Vector3 dir = targetPos - transform.position;
            float dist = dir.magnitude;

            if (dist > 1.5f)
            {
                dir.Normalize();
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = rot;

                Vector3 move = dir.normalized;
                move.y = 0;

                float baseSpeed =
                    parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;

                var coordinator = GetComponent<AgentCoordinationController>();
                if (coordinator != null)
                {
                    coordinator.SetDestination(targetPos);
                    finalSpeed = coordinator.GetNegotiatedSpeed(baseSpeed);
                }
                else
                {
                    finalSpeed = baseSpeed;
                }

                if (agent != null)
                {
                    agent.SetCurrentSpeed(finalSpeed);
                }
                controller.Move(
                    move * finalSpeed * Time.deltaTime
                        + Vector3.up * Physics.gravity.y * Time.deltaTime
                );
            }
            else
            {
                // Reached tree, start cutting
                if (currentTree != null)
                {
                    StartCoroutine(CutTreeRoutine(currentTree));
                }
                else
                {
                    // Tree was destroyed, select new one
                    movingToTreeViaAStar = false;
                    SelectNextTree();
                }
            }
            return;
        }
        // === INITIAL A* PATH (only if not tree hunting and not all trees visited) ===
        if (agentMove && currentTree == null && !allTreesVisited && !hasParcels)
        {
            MoveAlongPath();
        }
        else if (currentTree == null && !allTreesVisited && !hasParcels && !agentMove)
        {
            // Finished initial path, start tree hunting
            SelectNextTree();
        }
    }

    public bool IsLeavingDeliveryPoint()
    {
        return leavingDeliveryPoint;
    }


    private void CheckAndRerouteIfStuck()
    {
        // Don't check for rerouting if cutting or moving to tree directly
        if (cutting || (currentTree != null && !agentMove))
            return;

        var coordinator = GetComponent<AgentCoordinationController>();
        if (coordinator != null && coordinator.ShouldReroute())
        {
            Debug.Log($"{name} is stuck, attempting reroute...");

            // Try to find alternative path
            if (
                aStarPath != null
                && aStarPath.Count > 0
                && currentTargetArrayIndex < aStarPath.Count - 1
            )
            {
                // Skip current waypoint and recalculate
                GameObject newStart = aStarPath[currentTargetArrayIndex].ToNode;
                if (newStart != null)
                {
                    // Determine destination based on current state
                    GameObject destination =
                        hasParcels && deliveryPoint != null
                            ? FindNearestWaypoint(deliveryPoint.transform.position)
                            : end;

                    if (destination != null)
                    {
                        aStarPath = aStarManager.PathfindAStar(newStart, destination);
                        currentTargetArrayIndex = 0;
                    }
                }
            }
        }
    }

    private void MoveAlongPath()
    {
        if (aStarPath == null || aStarPath.Count == 0 || currentTargetArrayIndex >= aStarPath.Count)
        {
            agentMove = false;
            return;
        }

        currentTargetPos = aStarPath[currentTargetArrayIndex].ToNode.transform.position;

        Vector3 flatTargetPos = new Vector3(
            currentTargetPos.x,
            transform.position.y,
            currentTargetPos.z
        );

        Vector3 direction = flatTargetPos - transform.position;
        float distance = direction.magnitude;

        if (distance > 0.001f)
        {
            direction.y = 0;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = rotation;

            Vector3 normDirection = direction / distance;
            Vector3 move = normDirection; // define move first
            move.y = 0;

            var coordinator = GetComponent<AgentCoordinationController>();
            if (coordinator != null)
            {
                // Apply avoidance vector
                Vector3 avoidance = coordinator.GetAvoidanceVector(returningToStart, hasParcels);
                move += avoidance;
                move = move.normalized; // normalize after adding avoidance
            }

            float baseSpeed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;

            // Coordinator negotiation
            if (coordinator != null)
            {
                coordinator.SetDestination(currentTargetPos);
                finalSpeed = coordinator.GetNegotiatedSpeed(baseSpeed);

                // Check if should stop before waypoint
                if (coordinator.ShouldStopBeforeWaypoint(currentTargetPos))
                {
                    Debug.Log($"{name} STOPPED before waypoint due to coordination");
                    if (agent != null)
                        agent.SetCurrentSpeed(0f);
                    return; // skip movement this frame
                }
            }
            else
            {
                finalSpeed = baseSpeed;
            }

            if (agent != null)
                agent.SetCurrentSpeed(finalSpeed);

            controller.Move(
                move * finalSpeed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime
            );
        }

        if (distance < 1.5f)
        {
            currentTargetArrayIndex++;
            if (currentTargetArrayIndex == aStarPath.Count)
            {
                agentMove = false;

                if (movingToTreeViaAStar)
                {
                    // We've reached the tree's waypoint
                    movingToTreeViaAStar = false;
                    Debug.Log($"{name} reached tree waypoint via A*, switching to direct movement");

                    // Check distance to actual tree from waypoint
                    float treeDist = Vector3.Distance(
                        transform.position,
                        currentTree.transform.position
                    );

                    if (treeDist <= 3f) // Close enough to start cutting
                    {
                        if (currentTree != null)
                        {
                            StartCoroutine(CutTreeRoutine(currentTree));
                        }
                    }
                    else
                    {
                        // Need final approach - tree is not exactly at waypoint
                        // Will use direct movement from Update() next frame
                    }
                }

                // Finished RETURNING to start
                if (returningToStart)
                {
                    returningToStart = false;
                    // Agent has returned to start, go idle
                    agentMove = false;
                    if (agent != null)
                        agent.ForceIdle();  // Set idle animation/state
                }
                else
                {
                    // Finished GOING TO TREE if not returning
                    SelectNextTree();
                }
            }
        }
    }


    private void SelectNextTree()
    {
        // Check if we've visited all trees
        if (remainingTrees.Count == 0)
        {
            allTreesVisited = true;
            currentTree = null;
            Debug.Log($"{name} visited ALL trees! Going to delivery.");

            // Go to delivery with all collected logs
            if (parcelSystem != null && parcelSystem.parcelCount > 0)
            {
                hasParcels = true;
                GoToDeliveryPoint();
            }
            return;
        }

        // Clear current tree if invalid
        if (currentTree == null || !remainingTrees.Contains(currentTree))
        {
            currentTree = null;
        }
        else if (currentTree != null && remainingTrees.Contains(currentTree))
        {
            // Already have a valid tree from remaining list
            Debug.Log($"{name} already has tree: {currentTree.name}");
            return;
        }

        float minDist = float.MaxValue;
        GameObject nearestTree = null;

        // Search only in remaining trees
        foreach (var tree in remainingTrees)
        {
            if (tree == null)
                continue;

            if (TreeReservationManager.Instance.IsTreeReserved(tree))
                continue;

            float d = Vector3.Distance(transform.position, tree.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearestTree = tree;
            }
        }

        if (nearestTree != null)
        {
            if (TreeReservationManager.Instance.ReserveTree(nearestTree))
            {
                currentTree = nearestTree;
                Debug.Log(name + " RESERVED tree: " + currentTree.name +
                         $" ({remainingTrees.Count} trees remaining)");
            }
            else
            {
                Debug.Log($"{name} tree {nearestTree.name} already reserved");
                currentTree = null;
            }
        }
        else
        {
            currentTree = null;
            Debug.Log(name + " found no available trees to cut.");

            // If no trees available but some remain (all reserved), wait
            if (remainingTrees.Count > 0)
            {
                Debug.Log($"{name} waiting for trees to become available...");
            }
        }

        // Create a copy of trees list to avoid modification during iteration
        // List<GameObject> validTrees = new List<GameObject>();
        // foreach (var tree in trees)
        // {
        //     if (tree != null)
        //         validTrees.Add(tree);
        // }

        // foreach (var tree in validTrees)
        // {
        //     if (tree == null)
        //         continue;

        //     if (TreeReservationManager.Instance.IsTreeReserved(tree))
        //         continue;

        //     float d = Vector3.Distance(transform.position, tree.transform.position);
        //     if (d < minDist)
        //     {
        //         minDist = d;
        //         nearestTree = tree;
        //     }
        // }

        // if (nearestTree != null)
        // {
        //     if (TreeReservationManager.Instance.ReserveTree(nearestTree))
        //     {
        //         currentTree = nearestTree;
        //         Debug.Log(name + " RESERVED tree: " + currentTree.name);

        //         // Check if tree is VERY close (might be same spot as previous)
        //         float distToTree = Vector3.Distance(transform.position, currentTree.transform.position);
        //         if (distToTree < 2f)
        //         {
        //             Debug.Log($"{name} tree is very close ({distToTree:F2}m), starting cut immediately");
        //             StartCoroutine(CutTreeRoutine(currentTree));
        //         }
        //     }
        //     else
        //     {
        //         // Tree was reserved by another agent, try again
        //         Debug.Log($"{name} tree {nearestTree.name} already reserved by another agent");
        //         currentTree = null;
        //     }
        // }
        // else
        // {
        //     currentTree = null;
        //     Debug.Log(name + " found no trees to cut.");
        // }
    }

    private IEnumerator CutTreeRoutine(GameObject treeToCut)
    {
        if (treeToCut == null)
        {
            cutting = false;
            currentTree = null;
            yield break;
        }

        cutting = true;
        agent.ForceIdle();
        yield return new WaitForSeconds(cuttingTime);

        // 1. COLLECT LOG IMMEDIATELY (before destroying tree)
        if (parcelSystem != null)
        {
            int beforeCount = parcelSystem.parcelCount;
            parcelSystem.AddParcel();
            int afterCount = parcelSystem.parcelCount;

            if (afterCount > beforeCount)
            {
                Debug.Log($"{name} ✓ Collected log from {treeToCut.name}. " +
                          $"Logs: {beforeCount} → {afterCount}");
            }
        }

        // 2. Mark tree as visited
        visitedTrees.Add(treeToCut);
        remainingTrees.Remove(treeToCut);

        // 3. Destroy tree
        TreeReservationManager.Instance.ReleaseTree(treeToCut);
        trees.Remove(treeToCut);
        Destroy(treeToCut);

        yield return new WaitForSeconds(0.3f);
        cutting = false;
        currentTree = null;

        // 4. Check if done
        if (remainingTrees.Count == 0)
        {
            allTreesVisited = true;
            int totalLogs = parcelSystem?.parcelCount ?? 0;
            Debug.Log($"{name} 🎉 FINISHED ALL {visitedTrees.Count} TREES! " +
                      $"Total logs: {totalLogs}");

            if (parcelSystem != null && parcelSystem.parcelCount > 0)
            {
                hasParcels = true;
                GoToDeliveryPoint();
            }
        }
        else
        {
            SelectNextTree();
        }
    }
    // private IEnumerator CutTreeRoutine(GameObject treeToCut)
    // {
    //     if (treeToCut == null)
    //     {
    //         cutting = false;
    //         currentTree = null;
    //         yield break;
    //     }

    //     cutting = true;

    //     // Optional: Force idle animation
    //     agent.ForceIdle();

    //     // Wait for cutting
    //     yield return new WaitForSeconds(cuttingTime);

    //     // 1. COLLECT LOG IMMEDIATELY (before destroying tree)
    //     if (parcelSystem != null)
    //     {
    //         int beforeCount = parcelSystem.parcelCount;
    //         parcelSystem.AddParcel();
    //         int afterCount = parcelSystem.parcelCount;

    //         if (afterCount > beforeCount)
    //         {
    //             Debug.Log($"{name} ✓ Collected log from {treeToCut.name}. " +
    //                       $"Logs: {beforeCount} → {afterCount}");
    //         }
    //     }

    //     // Store tree position before destroying
    //     Vector3 treePosition = treeToCut.transform.position;

    //     // Spawn only one wood log
    //     Vector3 spawnPos = treePosition + new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f));
    //     GameObject log = Instantiate(woodLogPrefab, spawnPos, Quaternion.identity);

    //     Collider col = log.GetComponent<Collider>();
    //     if (col != null)
    //         col.isTrigger = true;
    //     log.tag = "WoodLog";

    //     // Mark tree as visited
    //     visitedTrees.Add(treeToCut);
    //     remainingTrees.Remove(treeToCut);
    //     Debug.Log($"{name} cut {treeToCut.name}. " +
    //               $"Visited: {visitedTrees.Count}/{trees.Count}, " +
    //               $"Remaining: {remainingTrees.Count}");


    //     // Destroy the tree
    //     TreeReservationManager.Instance.ReleaseTree(treeToCut);
    //     trees.Remove(treeToCut);
    //     Destroy(treeToCut);

    //     // Small pause
    //     yield return new WaitForSeconds(1f);

    //     cutting = false;
    //     currentTree = null;

    //     // Check if we've visited ALL trees
    //     if (remainingTrees.Count == 0)
    //     {
    //         allTreesVisited = true;
    //         Debug.Log($"{name} FINISHED ALL TREES! Total logs: {parcelSystem?.parcelCount ?? 0}");

    //         // Go to delivery with all collected logs
    //         if (parcelSystem != null && parcelSystem.parcelCount > 0)
    //         {
    //             hasParcels = true;
    //             GoToDeliveryPoint();
    //         }
    //     }
    //     else
    //     {
    //         // Not finished yet, select next tree
    //         SelectNextTree();
    //     }
    // }

    private void GoToDeliveryPoint()
    {
        if (deliveryPoint == null)
        {
            Debug.LogError($"{name} deliveryPoint is null!");
            return;
        }

        if (!allTreesVisited)
        {
            Debug.LogWarning($"{name} trying to deliver before visiting all trees!");
            return;
        }

        Debug.Log($"{name} going to deliver {parcelSystem?.parcelCount ?? 0} logs");

        // Reset movement state
        agentMove = false;
        movingToTreeViaAStar = false;
        currentTree = null;

        GameObject currentWp = FindNearestWaypoint(transform.position);
        GameObject deliveryWp = FindNearestWaypoint(deliveryPoint.transform.position);

        Debug.Log($"{name} Current waypoint: {currentWp?.name}, Delivery waypoint: {deliveryWp?.name}");

        if (currentWp != null && deliveryWp != null)
        {
            aStarPath = aStarManager.PathfindAStar(currentWp, deliveryWp);

            if (aStarPath != null && aStarPath.Count > 0)
            {
                Debug.Log($"{name} A* path to delivery found with {aStarPath.Count} steps");
                currentTargetArrayIndex = 0;
                agentMove = true;  // CRITICAL: Enable movement
                hasParcels = true; // Ensure delivery flag is set

                // Force Update() to use MoveAlongPath()
                Debug.Log($"{name} Starting delivery movement. agentMove={agentMove}");
            }
            else
            {
                Debug.LogError($"{name} NO A* path found from {currentWp.name} to {deliveryWp.name}!");
                // Fallback to direct movement
                // StartCoroutine(MoveDirectlyToDeliveryFallback());
            }
        }
        else
        {
            Debug.LogError($"{name} Cannot find waypoints: current={currentWp != null}, delivery={deliveryWp != null}");
        }
    }

    // private void GoToDeliveryPoint()
    // {
    //     if (deliveryPoint == null) return;

    //     if (!allTreesVisited)
    //     {
    //         Debug.LogWarning($"{name} trying to deliver before visiting all trees!");
    //         return;
    //     }

    //     Debug.Log($"{name} going to deliver {parcelSystem?.parcelCount ?? 0} logs");

    //     GameObject currentWp = FindNearestWaypoint(transform.position);
    //     GameObject deliveryWp = FindNearestWaypoint(deliveryPoint.transform.position);

    //     if (currentWp != null && deliveryWp != null)
    //     {
    //         currentTargetArrayIndex = 0;
    //         aStarPath = aStarManager.PathfindAStar(currentWp, deliveryWp);
    //         agentMove = true;
    //     }
    // }

    // private void GoToDeliveryPoint()
    // {

    //     if (deliveryPoint == null) return; 

    //     if (deliveryPoint == null)
    //     {
    //         // No delivery point, return to start
    //         GameObject currentWpt = FindNearestWaypoint(transform.position);
    //         GameObject startWp = FindNearestWaypoint(start.transform.position);

    //         if (currentWpt != null && startWp != null)
    //         {
    //             currentTargetArrayIndex = 0;
    //             aStarPath = aStarManager.PathfindAStar(currentWpt, startWp);
    //             agentMove = true;
    //         }
    //         return;
    //     }

    //     // Calculate path to delivery point
    //     GameObject currentWp = FindNearestWaypoint(transform.position);
    //     GameObject deliveryWp = FindNearestWaypoint(deliveryPoint.transform.position);

    //     if (currentWp != null && deliveryWp != null)
    //     {
    //         currentTargetArrayIndex = 0;
    //         aStarPath = aStarManager.PathfindAStar(currentWp, deliveryWp);
    //         agentMove = true;
    //         Debug.Log($"{name} at max parcels ({GetComponent<Part2_ParcelSystem>()?.parcelCount}), going to delivery");
    //     }
    // }

    private GameObject FindNearbyLog()
    {
        // Find all wood logs in scene
        GameObject[] allLogs = GameObject.FindGameObjectsWithTag("WoodLog");
        GameObject nearestLog = null;
        float minDist = float.MaxValue;

        foreach (GameObject log in allLogs)
        {
            if (log == null) continue;

            float dist = Vector3.Distance(transform.position, log.transform.position);
            if (dist < 3f && dist < minDist) // Within 3 units
            {
                minDist = dist;
                nearestLog = log;
            }
        }

        return nearestLog;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WoodLog"))
        {
            Debug.Log($"{name} encountered wood log");
            // Check parcel system exists
            if (parcelSystem == null)
            {
                parcelSystem = GetComponent<Part2_ParcelSystem>();
            }

            if (parcelSystem != null)
            {
                parcelSystem.AddParcel();
                Destroy(other.gameObject);
                Debug.Log($"{name} collected wood log, total parcels: {parcelSystem.parcelCount}");
            }
            else
            {
                Debug.Log($"{name} at max parcels, ignoring log");
            }
        }
        else if (other.CompareTag("DeliveryPoint"))
        {
            Debug.Log($"{name} reached delivery point");
            if (!deliveryCompleted && hasParcels && parcelSystem != null && parcelSystem.parcelCount > 0)
            {
                DeliverParcels();
            }
        }
    }

    private void MoveToDeliveryPoint()
    {
        if (deliveryPoint == null)
            return;

        Debug.Log($"{name} MoveToDeliveryPoint: hasParcels={hasParcels}, agentMove={agentMove}");

        // If we're already following an A* path, let it continue
        if (agentMove && aStarPath != null && aStarPath.Count > 0)
        {
            Debug.Log($"{name} Already following A* path to delivery");
            return;
        }

        // Try to calculate A* path to delivery
        GameObject currentWp = FindNearestWaypoint(transform.position);
        GameObject deliveryWp = FindNearestWaypoint(deliveryPoint.transform.position);

        if (currentWp == null || deliveryWp == null)
        {
            Debug.LogError($"{name} Cannot find waypoints for delivery!");
            return;
        }

        Debug.Log($"{name} Calculating A* from {currentWp.name} to {deliveryWp.name}");

        aStarPath = aStarManager.PathfindAStar(currentWp, deliveryWp);

        if (aStarPath != null && aStarPath.Count > 0)
        {
            Debug.Log($"{name} A* path to delivery found ({aStarPath.Count} steps)");
            currentTargetArrayIndex = 0;
            agentMove = true;
            movingToTreeViaAStar = false; // Make sure this is false for delivery
        }
        else
        {
            Debug.LogWarning($"{name} No A* path to delivery. Using direct movement as fallback");
            // Fallback to direct movement
            MoveDirectlyToDelivery();
        }
    }

    private void MoveDirectlyToDelivery()
    {
        Vector3 targetPos = new Vector3(
            deliveryPoint.transform.position.x,
            transform.position.y,
            deliveryPoint.transform.position.z
        );

        Vector3 dir = targetPos - transform.position;
        float dist = dir.magnitude;

        if (dist > 1.5f)
        {
            dir.Normalize();
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = rot;

            Vector3 move = dir.normalized;
            move.y = 0;

            float baseSpeed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;

            var coordinator = GetComponent<AgentCoordinationController>();
            if (coordinator != null)
            {
                coordinator.SetDestination(targetPos);
                finalSpeed = coordinator.GetNegotiatedSpeed(baseSpeed);
            }
            else
            {
                finalSpeed = baseSpeed;
            }

            if (agent != null)
            {
                agent.SetCurrentSpeed(finalSpeed);
            }
            controller.Move(
                move * finalSpeed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime
            );
        }
        else
        {
            // Reached delivery point
            DeliverParcels();
        }
    }

    private void DeliverParcels()
    {
        if (deliveryCompleted) return;

        if (parcelSystem == null) return;

        int deliveredCount = parcelSystem.parcelCount;
        if (deliveredCount <= 0) return;

        deliveryCompleted = true;

        Debug.Log($"{name} delivered ALL {deliveredCount} logs from {visitedTrees.Count} trees!");

        //clear parcels
        parcelSystem.ClearParcels();
        hasParcels = false;
        allTreesVisited = false;
        visitedTrees.Clear();
        remainingTrees = new List<GameObject>(trees);

        //switch states
        returningToStart = true;
        leavingDeliveryPoint = true;

        GameObject from = FindNearestWaypoint(transform.position);
        GameObject to = FindNearestWaypoint(start.transform.position);

        if (from != null && to != null)
        {
            currentTargetArrayIndex = 0;
            aStarPath = aStarManager.PathfindAStar(from, to);
            agentMove = true;

            Debug.Log($"{name} delivery complete → returning to start from {from.name} to {to.name}");
        }
        else
        {
            Debug.LogWarning($"{name} could not find waypoints to return to start");
        }

    }

    private GameObject FindNearestWaypoint(Vector3 position)
    {
        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (GameObject waypoint in waypoints)
        {
            if (waypoint == null)
                continue;

            float dist = Vector3.Distance(position, waypoint.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = waypoint;
            }
        }

        return nearest;
    }
}
