using TMPro;
using UnityEngine;

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
    }

    [SerializeField]
    private QuadrantUI[] quadrantUIs = new QuadrantUI[4];

    void Start()
    {
        // Initialize quadrants
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            var quad = quadrantUIs[i];

            if (quadrantUIs[i].agent != null && quadrantUIs[i].infoText != null)
            {
                quad.timer = 0f;
                quad.totalDistance = 0f;
                quad.lastPosition = quad.agent.transform.position;

                quadrantUIs[i].infoText.text = "Waiting for agent...";
            }
        }
    }
    void Update()
    {
        // Update UI for each quadrant
        for (int i = 0; i < quadrantUIs.Length; i++)
        {
            if (quadrantUIs[i].agent != null && quadrantUIs[i].infoText != null)
            {
                UpdateQuadrantUI(ref quadrantUIs[i]);
            }
        }
    }
    private void UpdateQuadrantUI(ref QuadrantUI quadUI)
    {
        Agent agent = quadUI.agent;
        Part2_ParcelSystem parcelSystem = agent.GetComponent<Part2_ParcelSystem>();
        PathfindingTester pathfinder = agent.GetComponent<PathfindingTester>();

        int parcels = parcelSystem != null ? parcelSystem.parcelCount : 0;

        // Only increment timer if agent is NOT idle at start
        bool isIdleAtStart = pathfinder != null && pathfinder.IsIdleAtStart();
        if (!isIdleAtStart)
        {
            quadUI.timer += Time.deltaTime;
        }
        // Distance (per agent)
        Vector3 delta = agent.transform.position - quadUI.lastPosition;
        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);

        quadUI.totalDistance += horizontalDelta.magnitude;
        quadUI.lastPosition = agent.transform.position;

        float animSpeed = agent.currentSpeed;

        // Format the UI text
        quadUI.infoText.text =
            $"<color=#FFFF00><b>Agent:</b></color> {agent.gameObject.name}\n"
            + $"<color=#00FF00><b>Time:</b></color> {quadUI.timer:F2}s\n"
            + $"<color=#00FFFF><b>Speed:</b></color> {animSpeed:F2} u/s\n"
            + $"<color=#FF00FF><b>Distance:</b></color> {quadUI.totalDistance:F2} units \n"
            + $"<color=#FFA500><b>Parcels:</b></color> {parcels}\n";
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
            }
        }
    }
}
