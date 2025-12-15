using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PathfindingTester : MonoBehaviour
{
    private Animator anim;
    private Vector3 animLastPos;
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

    // Route Timer.
    private float timer = 0f;

    // Distance Calculator.
    private float totalDistance = 0f;
    private Vector3 lastPosition = Vector3.zero;

    void Start()
    {
        anim = GetComponent<Animator>();
        animLastPos = transform.position;
        controller = GetComponent<CharacterController>();


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

        lastPosition = transform.position;
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

    // Update is called once per frame
    [Header("UI Display")]
    public TMP_Text agentInfoText; // Drag the UI Text here in Inspector

    void Update()
    {
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

            // Timer and distance travelled.
            Vector3 tmpDir = transform.position - lastPosition;
            float tmpDistance = tmpDir.magnitude;
            totalDistance += tmpDistance;
            lastPosition = transform.position;
            timer += Time.deltaTime;

            // Set the current target.
            currentTargetPos = aStarPath[currentTargetArrayIndex].ToNode.transform.position;

            // Clear y to avoid up/down movement. Assumes flat surface.
            // currentTargetPos.y = transform.position.y;

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
                controller.Move(move * Time.deltaTime);
                // transform.position += normDirection * currentSpeed * Time.deltaTime;
            }

            // Check if close to current target.
            if (distance < 1.2f)
            {
                // Close to target, so move to the next target in the list (if there is one).
                currentTargetArrayIndex++;

                if (currentTargetArrayIndex == aStarPath.Count)
                {
                    // The A* agent has reached the goal location.
                    Debug.Log("Time: " + timer);
                    Debug.Log("Distance: " + totalDistance);

                    totalDistance = 0f;
                    timer = 0f;

                    // Check if the current target is the start node. If yes, then stop.
                    if (aStarPath[aStarPath.Count - 1].ToNode == start)
                    {
                        agentMove = false;
                        Debug.Log("Agent Stopped.");
                        return;
                    }

                    // Not back at start, so plan path back to the start.
                    aStarPath = aStarManager.PathfindAStar(end, start);
                    currentTargetArrayIndex = 0;

                    if (aStarPath == null || aStarPath.Count == 0)
                    {
                        Debug.Log("Warning, A* did not return a path back to the start.");
                        agentMove = false;
                    }
                }
            }
        }
        else
        {
            // This code runs if agentMove is false.
            // (Idle / do nothing for now)
        }

        // ------ ANIMATION CONTROL ------

        // Calculate movement speed
        float animSpeed = (transform.position - animLastPos).magnitude / Time.deltaTime;
        if (animSpeed < 0.05f)
            animSpeed = 0f;

        // Send to Animator
        anim.SetFloat("speed", animSpeed);
        // Update stored pos
        animLastPos = transform.position;

        // ------ UI UPDATE ------
        // ------ UI UPDATE ------
        if (agentInfoText != null)
        {
            const int maxLabelLength = 9; // "Distance:" is the longest label at 9 chars

            agentInfoText.text =
                $"<color=#FFFFFF><b>Path:</b></color>{new string(' ', maxLabelLength - 5)}<color=#FFFFFF>Metric Info</color>\n"
                + $"<color=#FFFFFF><b>Time:</b></color>{new string(' ', maxLabelLength - 5)}{timer:F2} s\n"
                + $"<color=#FFFFFF><b>Agent:</b></color>{new string(' ', maxLabelLength - 6)}{gameObject.name}\n"
                + $"<color=#FFFFFF><b>Speed:</b></color>{new string(' ', maxLabelLength - 6)}{animSpeed:F2} u/s\n"
                + $"<color=#FFFFFF><b>Distance:</b></color>{new string(' ', maxLabelLength - 9)}{totalDistance:F2} units";
        }
    }
}
