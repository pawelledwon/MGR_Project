using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class MazeAgent : Agent
{
    [Header("Odnośniki")]
    public MazeGenerator mazeGenerator;
    public Transform target;

    [Header("Parametry Ruchu")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 200f;

    private Rigidbody rb;
    private float previousDistance;

    // Stagnation tracking
    private Vector3 lastCheckedPosition;
    private int stepsWithoutMoving = 0;
    private const int STAGNATION_LIMIT = 50;
    private const float MOVEMENT_THRESHOLD = 0.1f;

    // Breadcrumb exploration tracking
    private int[,] visitCounts;
    private int lastGridX = -1;
    private int lastGridZ = -1;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
        mazeGenerator.RespawnAgentAndTarget();
        previousDistance = Vector3.Distance(transform.position, target.position);

        // Reset stagnation
        lastCheckedPosition = transform.position;
        stepsWithoutMoving = 0;

        // Reset visit map each episode
        visitCounts = new int[mazeGenerator.width, mazeGenerator.height];
        lastGridX = -1;
        lastGridZ = -1;
    }

    private Vector2Int GetGridPos(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x);
        int y = Mathf.RoundToInt(worldPos.z);

        x = Mathf.Clamp(x, 0, mazeGenerator.width - 1);
        y = Mathf.Clamp(y, 0, mazeGenerator.height - 1);

        return new Vector2Int(x, y);
    }

    // Safely get visit count — returns high value if out of bounds (treat as wall/visited)
    private float GetVisitCount(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mazeGenerator.width || y >= mazeGenerator.height)
            return 10f; // treat out of bounds as heavily visited = wall
        return visitCounts[x, y];
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float maxDist = Mathf.Sqrt(mazeGenerator.width * mazeGenerator.width +
                               mazeGenerator.height * mazeGenerator.height);
        float normalizedDist = Vector3.Distance(transform.position, target.position) / maxDist;

        Vector3 dirToTarget = transform.InverseTransformDirection(
            (target.position - transform.position).normalized);

        sensor.AddObservation(dirToTarget);                                              // 3 values
        sensor.AddObservation(normalizedDist);                                           // 1 value
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));   // 3 values

        Vector2Int gridPos = GetGridPos(transform.position);
        float maxVisits = 20f;
        sensor.AddObservation(GetVisitCount(gridPos.x, gridPos.y + 1) / maxVisits); // forward
        sensor.AddObservation(GetVisitCount(gridPos.x, gridPos.y - 1) / maxVisits); // back
        sensor.AddObservation(GetVisitCount(gridPos.x + 1, gridPos.y) / maxVisits); // right
        sensor.AddObservation(GetVisitCount(gridPos.x - 1, gridPos.y) / maxVisits); // left
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveForward = actions.ContinuousActions[0];
        float rotate = actions.ContinuousActions[1];

        transform.Rotate(Vector3.up, rotate * rotationSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(
            (transform.forward * moveForward * moveSpeed).x,
            rb.linearVelocity.y,
            (transform.forward * moveForward * moveSpeed).z
        );

        AddReward(-0.001f);

        // Distance shaping
        float currentDistance = Vector3.Distance(transform.position, target.position);
        //float distanceDelta = previousDistance - currentDistance;

        //float maxDist = Mathf.Sqrt(mazeGenerator.width * mazeGenerator.width + mazeGenerator.height * mazeGenerator.height);
        //AddReward((distanceDelta / maxDist) * 0.5f);

        previousDistance = currentDistance;

        // Breadcrumb reward
        Vector2Int gridPos = GetGridPos(transform.position);
        if (gridPos.x != lastGridX || gridPos.y != lastGridZ)
        {
            if (visitCounts[gridPos.x, gridPos.y] == 0)
                AddReward(0.02f);
            else
                AddReward(-0.005f);

            visitCounts[gridPos.x, gridPos.y]++;
            lastGridX = gridPos.x;
            lastGridZ = gridPos.y;  // storing world Z in lastGridZ variable
        }

        // Stagnation penalty
        if (Vector3.Distance(transform.position, lastCheckedPosition) < MOVEMENT_THRESHOLD)
        {
            stepsWithoutMoving++;
            if (stepsWithoutMoving >= STAGNATION_LIMIT)
                AddReward(-0.01f);
        }
        else
        {
            stepsWithoutMoving = 0;
            lastCheckedPosition = transform.position;
        }

        if (currentDistance < 1.0f)
        {
            SetReward(10f);
            mazeGenerator.TargetReached();
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
            AddReward(-0.05f);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
            AddReward(-0.002f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }
}