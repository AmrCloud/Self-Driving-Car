using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TMPro; // Needed for the UI
using System.Collections;

public class SelfParkingAgent : Agent
{
    [Header("Movement")]
    public float moveSpeed = 30f;
    public float turnSpeed = 100f;
    private Rigidbody rBody;

    [Header("Environment")]
    public Transform spawnArea;
    public Transform targetSpot;

    [Header("UI & Visuals")] // --- NEW ---
    public TextMeshProUGUI episodeText;
    public TextMeshProUGUI stepText;
    public TextMeshProUGUI rewardText;
    public MeshRenderer floorRenderer; // Drag 'Ground' here
    public MeshRenderer carRenderer;   // Drag 'Car' visual here

    private Color defaultCarColor;
    private Color defaultFloorColor;
    private int episodeCount = 0;

    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();

        // Save original colors so we can reset them later
        if (carRenderer != null) defaultCarColor = carRenderer.material.color;
        if (floorRenderer != null) defaultFloorColor = floorRenderer.material.color;
    }

    public override void OnEpisodeBegin()
    {
        episodeCount++;
        rBody.linearVelocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;

        // Reset Car Color on respawn
        if (carRenderer != null) carRenderer.material.color = defaultCarColor;

        // Teleport to Spawn
        transform.position = spawnArea.position;
        transform.rotation = spawnArea.rotation;
    }

    void Update()
    {
        // --- NEW: Update UI every frame ---
        if (episodeText != null) episodeText.text = $"Episode: {episodeCount}";
        if (stepText != null) stepText.text = $"Steps: {StepCount}";
        if (rewardText != null) rewardText.text = $"Reward: {GetCumulativeReward():F2}";
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(targetSpot.position));
        sensor.AddObservation(rBody.linearVelocity.x);
        sensor.AddObservation(rBody.linearVelocity.z);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        float moveSignal = actionBuffers.ContinuousActions[0];
        float turnSignal = actionBuffers.ContinuousActions[1];

        Vector3 forceVector = transform.forward * moveSignal * moveSpeed;
        rBody.AddForce(forceVector);
        transform.Rotate(Vector3.up * turnSignal * turnSpeed * Time.fixedDeltaTime);

        AddReward(-0.001f); // Time penalty
    }

    // --- NEW: Collision Visuals ---
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Turn RED immediately upon touching wall
            if (carRenderer != null) carRenderer.material.color = Color.red;

            AddReward(-1.0f);
            StartCoroutine(FlashFloor(Color.red)); // Flash floor RED for fail
            EndEpisode();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // Return to normal color if we stop touching the wall 
        // (Note: Since we EndEpisode on wall hit, this mostly helps if you remove EndEpisode later)
        if (collision.gameObject.CompareTag("Wall"))
        {
            if (carRenderer != null) carRenderer.material.color = defaultCarColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ParkingSpot"))
        {
            AddReward(2.0f);
            StartCoroutine(FlashFloor(Color.green)); // Flash floor GREEN for success
            EndEpisode();
        }
    }

    // --- NEW: Floor Flashing Logic ---
    IEnumerator FlashFloor(Color targetColor)
    {
        if (floorRenderer != null)
        {
            floorRenderer.material.color = targetColor;
            // Wait for 0.5 seconds (Realtime so it works even if timescale changes)
            yield return new WaitForSecondsRealtime(0.5f);
            floorRenderer.material.color = defaultFloorColor;
        }
    }

    // Add this back to enable keyboard control
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Input.GetAxis("Vertical") reads W/S keys (Forward/Back)
        continuousActionsOut[0] = Input.GetAxis("Vertical");

        // Input.GetAxis("Horizontal") reads A/D keys (Turning)
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }
}