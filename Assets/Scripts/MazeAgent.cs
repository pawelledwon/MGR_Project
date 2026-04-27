using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class MazeAgent : Agent
{
    [Header("Odnośniki")]
    public MazeGenerator mazeGenerator;
    public Transform target;

    [Header("Metryki (Ewaluacja)")]
    public MazeMetrics metrics;

    [Header("Parametry Ruchu")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 200f;

    private Rigidbody rb;
    private bool episodeWasSuccess = false;

    // FIX: Dodany własny licznik kroków, niezależny od resetów ML-Agents
    private int episodeStepCount = 0;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 0;
    }

    public override void OnEpisodeBegin()
    {
        // FIX: Sprawdzamy nasz własny licznik, który zerujemy dopiero niżej
        if (episodeStepCount > 0 && metrics != null)
        {
            metrics.OnEpisodeEnd(episodeWasSuccess);
        }

        episodeWasSuccess = false; // Reset na nowy epizod
        episodeStepCount = 0;      // Reset licznika na nowy epizod

        if (metrics != null)
            metrics.OnEpisodeStart();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;

        Vector3 pos = transform.position;
        pos.y = 0.5f;
        transform.position = pos;

        mazeGenerator.RespawnAgentAndTarget();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeStepCount++; // Zwiększamy licznik co krok

        float moveForward = actions.ContinuousActions[0];
        float rotate = actions.ContinuousActions[1];

        transform.Rotate(Vector3.up, rotate * rotationSpeed * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(
            (transform.forward * moveForward * moveSpeed).x,
            0f,
            (transform.forward * moveForward * moveSpeed).z
        );

        AddReward(-0.001f);

        // Zapis do Heatmapy
        if (metrics != null)
            metrics.RecordStep(transform.localPosition);

        float currentDistance = Vector3.Distance(transform.position, target.position);

        if (currentDistance < 1.0f)
        {
            SetReward(10f);
            mazeGenerator.TargetReached();
            episodeWasSuccess = true; // Oznacz jako sukces
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        //if (collision.gameObject.CompareTag("Wall"))
        //    AddReward(-0.01f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }
}