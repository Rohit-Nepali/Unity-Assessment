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

    private Agent agent;
    private Part2_ParcelSystem parcelSystem;
    private GameObject currentTree;
    private float finalSpeed;
    private bool hasParcels = false; // Track if agent is carrying parcels

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

        // Select the first available tree
        SelectNextTree();
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

        // Check if agent should move to delivery point (if has parcels and delivery point exists)
        if (hasParcels && deliveryPoint != null && currentTree == null && !agentMove)
        {
            MoveToDeliveryPoint();
            return;
        }

        // Move to tree if agent reached the end of path
        if (currentTree != null && !cutting)
        {
            Vector3 targetPos = new Vector3(
                currentTree.transform.position.x,
                transform.position.y,
                currentTree.transform.position.z
            );

            Vector3 dir = targetPos - transform.position;
            float dist = dir.magnitude;

            Debug.Log(name + " moving towards " + currentTree.name);

            if (dist > 1.5f)
            {
                dir.Normalize();
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = rot;

                Vector3 move = dir.normalized ;
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
                controller.Move(move * finalSpeed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime);
            }
            else
            {
                StartCoroutine(CutTreeRoutine(currentTree));
            }
        }
        else if (agentMove)
        {
            MoveAlongPath();
        }
    }

    private void CheckAndRerouteIfStuck()
    {
        var coordinator = GetComponent<AgentCoordinationController>();
        if (coordinator != null && coordinator.ShouldReroute())
        {
            Debug.Log($"{name} is stuck, attempting reroute...");
            
            // Try to find alternative path
            if (currentTargetArrayIndex < aStarPath.Count - 1)
            {
                // Skip current waypoint and recalculate
                GameObject newStart = aStarPath[currentTargetArrayIndex].ToNode;
                aStarPath = aStarManager.PathfindAStar(newStart, end);
                currentTargetArrayIndex = 0;
                // Note: stuckTimer is managed in AgentCoordinationController
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

            var coordinator = GetComponent<AgentCoordinationController>();

            // Check if should stop before waypoint
            if (coordinator != null && coordinator.ShouldStopBeforeWaypoint(currentTargetPos))
            {
                // Stop and wait
                if (agent != null)
                {
                    agent.SetCurrentSpeed(0f);
                }
                return; // Don't move this frame
            }

            direction.y = 0;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = rotation;

            Vector3 normDirection = direction / distance;
            Vector3 move = normDirection;
            move.y = 0;

            float baseSpeed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;

            // coordinator already declared above, reuse it
            if (coordinator != null)
            {
                coordinator.SetDestination(currentTargetPos);
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

            controller.Move(move * finalSpeed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime);
        }

        if (distance < 1.5f)
        {
            currentTargetArrayIndex++;
            if (currentTargetArrayIndex == aStarPath.Count)
            {
                agentMove = false;
                
                // If agent has parcels and reached end of path, check if at delivery point
                if (hasParcels && deliveryPoint != null)
                {
                    float distToDelivery = Vector3.Distance(transform.position, deliveryPoint.transform.position);
                    if (distToDelivery < 3f)
                    {
                        // At delivery point, deliver parcels
                        DeliverParcels();
                    }
                    else
                    {
                        // Not at delivery point yet, will move directly to it in Update()
                        // agentMove is false, so Update() will call MoveToDeliveryPoint()
                    }
                }
                else
                {
                    // No parcels or no delivery point, select next tree
                    SelectNextTree();
                }
            }
        }
    }

    private void SelectNextTree()
    {
        float minDist = float.MaxValue;
        GameObject nearestTree = null;

        foreach (var tree in trees)
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
                Debug.Log(name + " RESERVED tree: " + currentTree.name);
            }
        }
        else
        {
            currentTree = null;
            Debug.Log(name + " found no trees to cut.");
        }
    }

    private IEnumerator CutTreeRoutine(GameObject treeToCut)
    {
        if (treeToCut == null)
            yield break;

        cutting = true;

        // Optional: Force idle animation
        agent.ForceIdle();

        // Wait for cutting
        yield return new WaitForSeconds(cuttingTime);

        // Spawn wood logs
        for (int i = 0; i < woodYield; i++)
        {
            Vector3 spawnPos =
                treeToCut.transform.position
                + new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f));
            GameObject log = Instantiate(woodLogPrefab, spawnPos, Quaternion.identity);
            Collider col = log.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
            log.tag = "WoodLog";
        }

        // Destroy the tree
        TreeReservationManager.Instance.ReleaseTree(treeToCut);
        trees.Remove(treeToCut);
        Destroy(treeToCut);

        // Small pause
        yield return new WaitForSeconds(1f);

        cutting = false;

        // After cutting, agent has parcels, go to delivery point
        hasParcels = true;
        
        // If delivery point exists, go there. Otherwise go back to start
        if (deliveryPoint != null)
        {
            // Find nearest waypoint to delivery point for pathfinding
            GameObject nearestWaypoint = FindNearestWaypoint(deliveryPoint.transform.position);
            if (nearestWaypoint != null)
            {
                currentTargetArrayIndex = 0;
                aStarPath = aStarManager.PathfindAStar(end, nearestWaypoint);
                agentMove = true;
            }
            else
            {
                // Fallback: go back to start
                currentTargetArrayIndex = 0;
                aStarPath = aStarManager.PathfindAStar(end, start);
                agentMove = true;
            }
        }
        else
        {
            // No delivery point, go back to start
            currentTargetArrayIndex = 0;
            aStarPath = aStarManager.PathfindAStar(end, start);
            agentMove = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WoodLog"))
        {
            if (parcelSystem != null)
            {
                parcelSystem.AddParcel();
            }
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("DeliveryPoint"))
        {
            // Agent reached delivery point
            if (hasParcels && parcelSystem != null && parcelSystem.parcelCount > 0)
            {
                DeliverParcels();
            }
        }
    }

    private void MoveToDeliveryPoint()
    {
        if (deliveryPoint == null)
            return;

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
            controller.Move(move * finalSpeed * Time.deltaTime + Vector3.up * Physics.gravity.y * Time.deltaTime);
        }
        else
        {
            // Reached delivery point
            DeliverParcels();
        }
    }

    private void DeliverParcels()
    {
        if (parcelSystem == null)
            return;

        int deliveredCount = parcelSystem.parcelCount;
        if (deliveredCount > 0)
        {
            Debug.Log($"{name} delivered {deliveredCount} parcels at delivery point!");
            parcelSystem.ClearParcels();
            hasParcels = false;

            // After delivery, go back to start and select next tree
            // Find nearest waypoint to delivery point and start for pathfinding
            GameObject nearestToDelivery = FindNearestWaypoint(deliveryPoint.transform.position);
            GameObject nearestToStart = FindNearestWaypoint(start.transform.position);
            
            if (nearestToDelivery != null && nearestToStart != null)
            {
                currentTargetArrayIndex = 0;
                aStarPath = aStarManager.PathfindAStar(nearestToDelivery, nearestToStart);
                agentMove = true;
            }
            else if (nearestToStart != null)
            {
                // Fallback: just pathfind to start from current position's nearest waypoint
                GameObject currentNearest = FindNearestWaypoint(transform.position);
                if (currentNearest != null)
                {
                    currentTargetArrayIndex = 0;
                    aStarPath = aStarManager.PathfindAStar(currentNearest, nearestToStart);
                    agentMove = true;
                }
            }
            
            SelectNextTree();
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
