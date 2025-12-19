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

            if (dist > 1.5f)
            {
                dir.Normalize();
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = rot;

                Vector3 move = dir * currentSpeed;
                move.y += Physics.gravity.y;

                float speed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;
                if (agent != null)
                {
                    agent.SetCurrentSpeed(speed);
                }
                controller.Move(move * speed * Time.deltaTime);
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
            Vector3 move = normDirection * currentSpeed;
            move.y += Physics.gravity.y;

            float speed = parcelSystem != null ? parcelSystem.GetModifiedSpeed() : currentSpeed;
            if (agent != null)
            {
                agent.SetCurrentSpeed(speed);
            }

            controller.Move(move * speed * Time.deltaTime);
        }

        if (distance < 1.5f)
        {
            currentTargetArrayIndex++;
            if (currentTargetArrayIndex == aStarPath.Count)
            {
                agentMove = false;
                SelectNextTree();
            }
        }
    }

    private void SelectNextTree()
    {
        // Pick the nearest uncut tree
        float minDist = float.MaxValue;
        GameObject nearestTree = null;
        foreach (var tree in trees)
        {
            if (tree == null)
                continue;
            float d = Vector3.Distance(transform.position, tree.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearestTree = tree;
            }
        }
        currentTree = nearestTree;
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
        trees.Remove(treeToCut);
        Destroy(treeToCut);

        // Small pause
        yield return new WaitForSeconds(1f);

        cutting = false;

        // Go back along A* path
        currentTargetArrayIndex = 0;
        aStarPath = aStarManager.PathfindAStar(end, start);
        agentMove = true;
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
    }
}
