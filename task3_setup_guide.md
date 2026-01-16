# Task 3 Agent & Dashboard Setup Guide

Follow these steps to configure your Agents and the Dashboard.

## 1. Agent GameObject Setup
Select your Agent GameObject (e.g., Agent_Red, Agent_Blue). Ensure it has:
1.  **Character Controller**
2.  **Agent** script
3.  **Task 3 Agent** script
4.  **Task 3 Parcel Manager** script
5.  **Agent Coordination Controller** script

### Task 3 Parcel Manager
*   **Parcel Stack Parent**: Empty child object on agent (where logs appear).
*   **Wood Log Visual Prefab**: Wood log prefab.
*   **Parcel Count Text**: (Optional) World Space Text above agent head.

## 2. Dashboard Setup (Quadrant UI Manager)
Find the `QuadrantUIManager` GameObject in your scene (usually on the Main Camera or a dedicated Manager object).

### Inspector Configuration
*   **Show Dashboard**: Checked.
*   **Dashboard Panel**: Assign the RectTransform of the UI Panel in the 4th Quadrant (Bottom-Right).
*   **Dashboard Text**: Assign the **TextMeshPro - UGUI** component inside that panel.

### Assigning Agents
There is an array `Quadrant UIs` (Size 4).
*   **Element 0**: Assign Agent 1 (e.g., Red) and its UI Text (Top-Left).
*   **Element 1**: Assign Agent 2 (e.g., Blue) and its UI Text (Top-Right).
*   **Element 2**: Assign Agent 3 (e.g., Green) and its UI Text (Bottom-Left).
*   **Element 3**: **Leave Agent Empty** (This is the Dashboard quadrant).

## 3. How It Works
*   **Quadrants 1-3** will show live stats for individual agents.
*   **Quadrant 4** will show the **Mission Dashboard**:
    *   Overview of total agents/parcels.
    *   Detailed status list.
    *   Global statistics (Average Time, Total Distance).
    *   Leaderboard (Fastest Agent).

## 4. Verification
1.  Play the scene.
2.  Agents should cut trees and collect logs.
3.  Quadrant 4 should display "MISSION DASHBOARD" with live updating numbers.
4.  When an agent returns home, it marks "✓" on the dashboard.
