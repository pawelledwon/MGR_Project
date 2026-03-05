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

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        mazeGenerator.GenerateMaze();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 dirToTarget = transform.InverseTransformDirection((target.position - transform.position).normalized);
        sensor.AddObservation(dirToTarget);

        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveForward = actions.DiscreteActions[0] == 1 ? 1f : (actions.DiscreteActions[0] == 2 ? -1f : 0f);
        float rotate = actions.DiscreteActions[1] == 1 ? 1f : (actions.DiscreteActions[1] == 2 ? -1f : 0f);

        transform.Rotate(Vector3.up, rotate * rotationSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(
            (transform.forward * moveForward * moveSpeed).x,
            rb.linearVelocity.y,
            (transform.forward * moveForward * moveSpeed).z
        );

        AddReward(-0.001f);

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget < 1.0f)
        {
            SetReward(10f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.01f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetAxisRaw("Vertical") > 0 ? 1 : (Input.GetAxisRaw("Vertical") < 0 ? 2 : 0);
        discreteActions[1] = Input.GetAxisRaw("Horizontal") > 0 ? 1 : (Input.GetAxisRaw("Horizontal") < 0 ? 2 : 0);
    }
}