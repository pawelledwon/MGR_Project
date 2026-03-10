using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class TripodAgent : Agent
{
    [Header("Fizyka Robota")]
    public Rigidbody bodyRigidbody;
    public HingeJoint[] swingJoints = new HingeJoint[3]; // LegRoot - forward/back
    public HingeJoint[] liftJoints = new HingeJoint[3];  // LegCapsule - up/down

    [Header("Parametry Uczenia")]
    public float legMovementLimit = 60f;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private Vector3[] startingSwingPositions;
    private Quaternion[] startingSwingRotations;
    private Vector3[] startingLiftPositions;
    private Quaternion[] startingLiftRotations;

    private bool isTouchingGround = false;

    public override void Initialize()
    {
        startingPosition = transform.localPosition;
        startingRotation = transform.localRotation;

        startingSwingPositions = new Vector3[3];
        startingSwingRotations = new Quaternion[3];
        startingLiftPositions = new Vector3[3];
        startingLiftRotations = new Quaternion[3];

        for (int i = 0; i < 3; i++)
        {
            startingSwingPositions[i] = swingJoints[i].transform.localPosition;
            startingSwingRotations[i] = swingJoints[i].transform.localRotation;
            startingLiftPositions[i] = liftJoints[i].transform.localPosition;
            startingLiftRotations[i] = liftJoints[i].transform.localRotation;
        }
    }

    public override void OnEpisodeBegin()
    {
        // Zero velocities FIRST while still dynamic
        bodyRigidbody.linearVelocity = Vector3.zero;
        bodyRigidbody.angularVelocity = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            swingJoints[i].GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            swingJoints[i].GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            liftJoints[i].GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            liftJoints[i].GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }

        // Go kinematic for safe teleport
        bodyRigidbody.isKinematic = true;
        for (int i = 0; i < 3; i++)
        {
            swingJoints[i].GetComponent<Rigidbody>().isKinematic = true;
            liftJoints[i].GetComponent<Rigidbody>().isKinematic = true;
        }

        // Reposition
        transform.localPosition = startingPosition;
        transform.localRotation = startingRotation;

        for (int i = 0; i < 3; i++)
        {
            swingJoints[i].transform.localPosition = startingSwingPositions[i];
            swingJoints[i].transform.localRotation = startingSwingRotations[i];
            liftJoints[i].transform.localPosition = startingLiftPositions[i];
            liftJoints[i].transform.localRotation = startingLiftRotations[i];

            JointSpring swingSpring = swingJoints[i].spring;
            swingSpring.targetPosition = 0f;
            swingJoints[i].spring = swingSpring;

            JointSpring liftSpring = liftJoints[i].spring;
            liftSpring.targetPosition = 0f;
            liftJoints[i].spring = liftSpring;
        }

        StartCoroutine(ReenablePhysics());
        isTouchingGround = false;
    }

    private System.Collections.IEnumerator ReenablePhysics()
    {
        yield return new WaitForFixedUpdate();
        bodyRigidbody.isKinematic = false;
        for (int i = 0; i < 3; i++)
        {
            swingJoints[i].GetComponent<Rigidbody>().isKinematic = false;
            liftJoints[i].GetComponent<Rigidbody>().isKinematic = false;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Body state - 8 values
        sensor.AddObservation(bodyRigidbody.linearVelocity);   // 3
        sensor.AddObservation(bodyRigidbody.angularVelocity);  // 3
        sensor.AddObservation(transform.forward);              // 3
        sensor.AddObservation(transform.up);                   // 3

        // Ground contact - 1 value
        sensor.AddObservation(isTouchingGround ? 1.0f : 0.0f);

        // Per leg: swing position, lift position, swing velocity, lift velocity - 4 values x 3 legs = 12
        for (int i = 0; i < 3; i++)
        {
            sensor.AddObservation(swingJoints[i].spring.targetPosition / legMovementLimit);
            sensor.AddObservation(liftJoints[i].spring.targetPosition / legMovementLimit);
            sensor.AddObservation(swingJoints[i].GetComponent<Rigidbody>().angularVelocity);
            sensor.AddObservation(liftJoints[i].GetComponent<Rigidbody>().angularVelocity);
        }
        // Total: 12 + 1 + 12 = 25... update Space Size to 25 in Inspector
        // 3+3+3+3 = 12 body, 1 ground, 3*(1+1+3+3) = 27 legs = 40 total
        // Set Space Size to 40 in Behavior Parameters
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 6 actions: swing0, lift0, swing1, lift1, swing2, lift2
        for (int i = 0; i < 3; i++)
        {
            ApplyLegAction(swingJoints[i], actions.ContinuousActions[i * 2]);
            ApplyLegAction(liftJoints[i], actions.ContinuousActions[i * 2 + 1]);
        }

        float forwardVelocity = bodyRigidbody.linearVelocity.z;
        AddReward(Mathf.Clamp(forwardVelocity, 0f, 2f) * 0.05f);

        if (isTouchingGround)
            AddReward(-0.02f);
        else
            AddReward(0.01f);

        AddReward(-0.001f);

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            if (hit.distance < 0.3f)
            {
                SetReward(-1f);
                EndEpisode();
            }
        }
    }

    private void ApplyLegAction(HingeJoint joint, float actionValue)
    {
        JointSpring spring = joint.spring;
        spring.targetPosition = actionValue * legMovementLimit;
        joint.spring = spring;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
            isTouchingGround = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
            isTouchingGround = false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;

        // --- LEG 1 ---
        // Swing (forward/back): U / J
        ca[0] = Input.GetKey(KeyCode.U) ? 1f : (Input.GetKey(KeyCode.J) ? -1f : 0f);
        // Lift (up/down):       Y / H
        ca[1] = Input.GetKey(KeyCode.Y) ? 1f : (Input.GetKey(KeyCode.H) ? -1f : 0f);

        // --- LEG 2 ---
        // Swing (forward/back): I / K
        ca[2] = Input.GetKey(KeyCode.I) ? 1f : (Input.GetKey(KeyCode.K) ? -1f : 0f);
        // Lift (up/down):       O / L
        ca[3] = Input.GetKey(KeyCode.O) ? 1f : (Input.GetKey(KeyCode.L) ? -1f : 0f);

        // --- LEG 3 ---
        // Swing (forward/back): N / M
        ca[4] = Input.GetKey(KeyCode.N) ? 1f : (Input.GetKey(KeyCode.M) ? -1f : 0f);
        // Lift (up/down):       B / V
        ca[5] = Input.GetKey(KeyCode.B) ? 1f : (Input.GetKey(KeyCode.V) ? -1f : 0f);
    }
}