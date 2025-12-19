using System.Collections;
using System.Collections.Generic;
using TMPro;
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

    // Debug line offset.
    private Vector3 offset = new Vector3(0, 0.3f, 0);

    // A list of all waypoint nodes set to goal in the environment.
    private List<GameObject> waypointGoals = new List<GameObject>();

    // Movement variables.
    private float currentSpeed = 8f;
    private int currentTargetArrayIndex = 0;
    private Vector3 currentTargetPos;
    private bool agentMove = true;

    public GameObject tree;
    public GameObject woodLogPrefab;
    public float cuttingTime = 4f;

    private bool goingToTree = false;
    private bool cutting = false;
    private bool isIdle = false;

    [Header("Harvest Settings")]
    public int woodYield = 4; // How many logs one tree produces

    private Agent agent;
    private Part2_ParcelSystem parcelSystem;

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

            // Loop through a waypoint's connections.
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

        // Run A Star...
        // aStarPath stores all the connections in the path/route to the goal/end node.
        aStarPath = aStarManager.PathfindAStar(start, end);

        if (aStarPath == null || aStarPath.Count == 0)
        {
            Debug.Log("Warning, A* did not return a path between the start and end node.");
            agentMove = false;
        }
    }

    // Draws debug objects in the editor and during editor play (if option set).
    void OnDrawGizmos()
    {
        if (aStarPath == null)
            return;

        // Draw path.
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
        if (goingToTree && tree != null && !cutting)
        {
            Vector3 targetPos = new Vector3(
                tree.transform.position.x,
                transform.position.y,
                tree.transform.position.z
            );

            Vector3 dir = targetPos - transform.position;
            float dist = dir.magnitude;

            if (dist > 5.5f)
            {
                dir.Normalize();
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = rot;

                Vector3 move = dir * currentSpeed;
                move.y += Physics.gravity.y;

                // 2️⃣ Decide speed
                float speed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed; // fallback if parcel system is missing
                if (agent != null)
                {
                    agent.SetCurrentSpeed(speed);
                }
                controller.Move(move * speed * Time.deltaTime);
            }
            else
            {
                // Stop the agent
                isIdle = true;
                controller.Move(Vector3.zero);

                // Tell Agent.cs to set speed to 0
                GetComponent<Agent>().ForceIdle(); // optional method we can add
                StartCoroutine(CutTree());
            }
        }

        if (agentMove)
        {
            // No path or index out of range, just stop.
            if (
                aStarPath == null
                || aStarPath.Count == 0
                || currentTargetArrayIndex >= aStarPath.Count
            )
            {
                agentMove = false;
                return;
            }

            // Set the current target.
            currentTargetPos = aStarPath[currentTargetArrayIndex].ToNode.transform.position;

            // Get a vector to the target position.
            Vector3 flatTargetPos = new Vector3(
                currentTargetPos.x,
                transform.position.y,
                currentTargetPos.z
            );

            Vector3 direction = flatTargetPos - transform.position;
            float distance = direction.magnitude;

            // Face in the right direction.
            if (distance > 0.001f)
            {
                direction.y = 0;
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = rotation;

                // Normalised direction.
                Vector3 normDirection = direction / distance;

                // apply gravity so agent sticks to terrain ground
                Vector3 move = normDirection * currentSpeed;
                move.y += Physics.gravity.y;

                // Move the game object.
                // Decide speed
                float speed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed; // fallback if parcel system is missing
                if (agent != null)
                {
                    agent.SetCurrentSpeed(speed);
                }
                controller.Move(move * speed * Time.deltaTime);
            }

            // Check if close to current target.
            if (distance < 1.5f)
            {
                // Close to target, so move to the next target in the list (if there is one).
                currentTargetArrayIndex++;

                if (currentTargetArrayIndex == aStarPath.Count)
                {
                    Debug.Log("Reached waypoint");

                    agentMove = false;
                    Debug.Log("Agent move, returning to tree");
                    goingToTree = true;
                }
            }
        }
        else
        {
            // This code runs if agentMove is false.
            // (Idle / do nothing for now)
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WoodLog"))
        {
            // Add parcel to agent
            if (parcelSystem != null)
            {
                parcelSystem.AddParcel();
            }

            Destroy(other.gameObject); // remove log from the world
            Debug.Log("Picked up a wood log!");
        }
    }

    private IEnumerator CutTree()
    {
        cutting = true;
        goingToTree = false;

        Debug.Log("Cutting tree...");

        // Save tree position & rotation BEFORE destroying
        Vector3 basePos = tree.transform.position;
        basePos.y = transform.position.y;
        Quaternion rot = tree.transform.rotation;

        // Wait for cutting animation / time
        yield return new WaitForSeconds(cuttingTime);

        // Remove tree
        Destroy(tree);

        // Spawn multiple wood logs
        for (int i = 0; i < woodYield; i++)
        {
            Vector3 spawnPos =
                basePos + new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f));

            GameObject log = Instantiate(woodLogPrefab, spawnPos, rot);

            // Make sure the log has a trigger collider and tag
            Collider col = log.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            log.tag = "WoodLog";
        }

        Debug.Log($"Spawned {woodYield} wood logs");

        // Small pause so player can SEE the wood
        yield return new WaitForSeconds(3f);

        // Return to waypoint
        aStarPath = aStarManager.PathfindAStar(end, start);
        currentTargetArrayIndex = 0;
        agentMove = true;

        cutting = false;
    }
}
