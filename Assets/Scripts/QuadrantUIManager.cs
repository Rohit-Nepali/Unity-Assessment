using TMPro;
using UnityEngine;
using System.Linq;

public class QuadrantUIManager : MonoBehaviour
{
    [System.Serializable]
    public class QuadrantUI
    {
        public CameraFollow.CameraQuadrant quadrant;
        public RectTransform panel;
        public TextMeshProUGUI infoText;
        public Agent agent;

        [HideInInspector]
        public float timer;

        [HideInInspector]
        public float totalDistance;

        [HideInInspector]
        public Vector3 lastPosition;

        [HideInInspector]
        public bool missionComplete;

        [HideInInspector]
        public float completionTime;
    }

    [Header("Agent Quadrants (First 3)")]
    [SerializeField]
    private QuadrantUI[] quadrantUIs = new QuadrantUI[4];

    // [Header("Dashboard Settings (4th Quadrant)")]
    // [SerializeField]
    // private RectTransform dashboardPanel;

    // [SerializeField]
    // private TextMeshProUGUI dashboardText;

    // [Header("Dashboard Styling")]
    // [SerializeField]
    // private bool showDashboard = true;

    [SerializeField]
    // private string dashboardTitle = "MISSION DASHBOARD";

    void Start()
    {
        // Initialize agent quadrants (first 3)
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];

            if (quad.agent != null && quad.infoText != null)
            {
                quad.timer = 0f;
                quad.totalDistance = 0f;
                quad.lastPosition = quad.agent.transform.position;
                quad.missionComplete = false;
                quad.completionTime = 0f;

                quad.infoText.text = "Waiting for agent...";
            }
        }

        // // Initialize dashboard
        // if (dashboardText != null)
        // {
        //     dashboardText.text = "Initializing Dashboard...";
        // }
    }

    void Update()
    {
        // Update UI for each agent quadrant
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null && quadrantUIs[i].infoText != null)
            {
                UpdateQuadrantUI(ref quadrantUIs[i]);
            }
        }

        // // Update dashboard in 4th quadrant
        // if (showDashboard && dashboardText != null)
        // {
        //     UpdateDashboard();
        // }
    }

    private void UpdateQuadrantUI(ref QuadrantUI quadUI)
    {
        Agent agent = quadUI.agent;
        Task3ParcelManager parcelSystem = agent.GetComponent<Task3ParcelManager>();
        Task3Agent task3Agent = agent.GetComponent<Task3Agent>();

        int parcels = parcelSystem != null ? parcelSystem.ParcelCount : 0;
        string phase = task3Agent != null ? task3Agent.GetCurrentPhase() : "Active";

        // Check if mission complete (for Task3Agent)
        if (task3Agent != null && !quadUI.missionComplete && task3Agent.IsMissionComplete())
        {
             MarkMissionComplete(agent);
        }

        // Only increment timer if agent is NOT idle at start
        // Use Task3Agent's moving state
        bool isMoving = task3Agent != null && (task3Agent.IsMoving() || task3Agent.IsCutting() || task3Agent.IsDelivering() || task3Agent.IsReturningHome());
        
        if (isMoving && !quadUI.missionComplete)
        {
            quadUI.timer += Time.deltaTime;
        }

        // Distance (per agent)
        float distanceTraveled = 0f;
        Vector3 currentPos = agent.transform.position;
        if (isMoving) {
             Vector3 delta = currentPos - quadUI.lastPosition;
             Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
             distanceTraveled = horizontalDelta.magnitude;
             quadUI.totalDistance += distanceTraveled;
        }
        quadUI.lastPosition = currentPos;

        float animSpeed = agent.currentSpeed;

        // Format the UI text
        string statusColor = quadUI.missionComplete ? "#00FF00" : "#FFFFFF";
        string statusText = quadUI.missionComplete ? "COMPLETE" : phase;

        quadUI.infoText.text =
            $"<color=#FFFF00><b>Agent:</b></color> {agent.gameObject.name}\n"
            + $"<color=#00FF00><b>Time:</b></color> {quadUI.timer:F2}s\n"
            + $"<color=#00FFFF><b>Speed:</b></color> {animSpeed:F2} u/s\n"
            + $"<color=#FF00FF><b>Distance:</b></color> {quadUI.totalDistance:F2} units\n"
            + $"<color=#FFA500><b>Parcels:</b></color> {parcels}\n"
            + $"<color={statusColor}><b>Status:</b></color> {statusText}";
    }

    private void UpdateDashboard()
    {
        // Gather stats from all agents
        int totalAgents = 0;
        int activeAgents = 0;
        int completedAgents = 0;
        float totalTime = 0f;
        float totalDistance = 0f;
        int totalParcels = 0;
        float fastestTime = float.MaxValue;
        float slowestTime = 0f;
        string fastestAgent = "N/A";
        string slowestAgent = "N/A";
        float shortestDistance = float.MaxValue;
        float longestDistance = 0f;

        string agentDetailsSection = "";

        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];
            if (quad.agent == null) continue;

            totalAgents++;

            Task3ParcelManager parcelSystem = quad.agent.GetComponent<Task3ParcelManager>();
            int parcels = parcelSystem != null ? parcelSystem.ParcelCount : 0;

            // Accumulate totals
            totalTime += quad.timer;
            totalDistance += quad.totalDistance;
            totalParcels += parcels;

            // Track fastest/slowest (only if completed or timer > 0)
            if (quad.timer > 0)
            {
                if (quad.missionComplete && quad.timer < fastestTime)
                {
                    fastestTime = quad.timer;
                    fastestAgent = quad.agent.gameObject.name;
                }
                // Just tracking running max
                if (quad.timer > slowestTime)
                {
                    slowestTime = quad.timer;
                    slowestAgent = quad.agent.gameObject.name;
                }
            }

            // Track shortest/longest distance
            if (quad.totalDistance > 0)
            {
                if (quad.totalDistance < shortestDistance)
                {
                    shortestDistance = quad.totalDistance;
                }
                if (quad.totalDistance > longestDistance)
                {
                    longestDistance = quad.totalDistance;
                }
            }

            // Check if active
            if (quad.agent.currentSpeed > 0.1f)
            {
                activeAgents++;
            }

            if (quad.missionComplete)
            {
                completedAgents++;
            }

            // Build agent details
            string agentColor = GetAgentColor(i);
            string statusIcon = quad.missionComplete ? "✓" : "●";
            agentDetailsSection += $"<color={agentColor}>{statusIcon} {quad.agent.gameObject.name}: {quad.timer:F1}s | {quad.totalDistance:F1}u | {parcels}p</color>\n";
        }

        // Calculate averages
        float avgTime = totalAgents > 0 ? totalTime / totalAgents : 0f;
        float avgDistance = totalAgents > 0 ? totalDistance / totalAgents : 0f;

        // Build dashboard text
        // string dashboard = "";

        // Title
        // dashboard += $"<size=120%><color=#FFD700><b>═══ {dashboardTitle} ═══</b></color></size>\n\n";

        // Overview Section
        // dashboard += "<color=#00FFFF><b>📊 OVERVIEW</b></color>\n";
        // dashboard += $"<color=#FFFFFF>Total Agents: </color><color=#FFFF00>{totalAgents}</color>\n";
        // dashboard += $"<color=#FFFFFF>Active: </color><color=#00FF00>{activeAgents}</color> | ";
        // dashboard += $"<color=#FFFFFF>Completed: </color><color=#00FF00>{completedAgents}</color>\n\n";

        // Agent Details Section
        // dashboard += "<color=#FF00FF><b>👥 AGENT STATUS</b></color>\n";
        // dashboard += agentDetailsSection + "\n";

        // Statistics Section
        // dashboard += "<color=#FFA500><b>📈 STATISTICS</b></color>\n";
        // dashboard += $"<color=#FFFFFF>Total Distance:</color> <color=#00FFFF>{totalDistance:F1}</color> units\n";
        // dashboard += $"<color=#FFFFFF>Total Parcels:</color> <color=#FFA500>{totalParcels}</color>\n";
        // dashboard += $"<color=#FFFFFF>Avg Time:</color> <color=#FFFF00>{avgTime:F1}s</color>\n";
        // dashboard += $"<color=#FFFFFF>Avg Distance:</color> <color=#00FFFF>{avgDistance:F1}</color> units\n\n";

        // Leaderboard Section
        // dashboard += "<color=#00FF00><b>🏆 LEADERBOARD</b></color>\n";
        // if (fastestTime < float.MaxValue)
        // {
        //     dashboard += $"<color=#FFD700>⚡ Fastest:</color> {fastestAgent} ({fastestTime:F1}s)\n";
        // }
        // if (shortestDistance < float.MaxValue)
        // {
        //     dashboard += $"<color=#FFD700>📍 Shortest:</color> {shortestDistance:F1} units\n";
        // }
        
        // dashboardText.text = dashboard;
    }

    private string GetAgentColor(int index)
    {
        switch (index)
        {
            case 0: return "#FF6B6B";  // Red
            case 1: return "#4ECDC4";  // Teal
            case 2: return "#95E1D3";  // Mint
            default: return "#FFFFFF"; // White
        }
    }

    // Mark an agent's mission as complete
    public void MarkMissionComplete(Agent agent)
    {
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent == agent && !quadrantUIs[i].missionComplete)
            {
                quadrantUIs[i].missionComplete = true;
                quadrantUIs[i].completionTime = quadrantUIs[i].timer;
                Debug.Log($"[Dashboard] {agent.gameObject.name} completed in {quadrantUIs[i].timer:F2}s");
                break;
            }
        }
    }

    // Reset method for when you want to restart the simulation
    public void ResetMetrics()
    {
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null)
            {
                quadrantUIs[i].timer = 0f;
                quadrantUIs[i].totalDistance = 0f;
                quadrantUIs[i].lastPosition = quadrantUIs[i].agent.transform.position;
                quadrantUIs[i].missionComplete = false;
                quadrantUIs[i].completionTime = 0f;
            }
        }
    }

    // Get specific agent stats (useful for other scripts)
    public float GetAgentTime(int index)
    {
        if (index >= 0 && index < quadrantUIs.Length && quadrantUIs[index].agent != null)
        {
            return quadrantUIs[index].timer;
        }
        return 0f;
    }

    public float GetAgentDistance(int index)
    {
        if (index >= 0 && index < quadrantUIs.Length && quadrantUIs[index].agent != null)
        {
            return quadrantUIs[index].totalDistance;
        }
        return 0f;
    }
}
