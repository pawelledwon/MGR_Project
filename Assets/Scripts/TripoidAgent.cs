using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class TripodAgent : Agent
{
    [Header("Fizyka Robota")]
    public Rigidbody bodyRigidbody;
    public HingeJoint[] legs = new HingeJoint[3];

    [Header("Parametry Uczenia")]
    public float legMovementLimit = 60f;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private Quaternion[] startingLegRotations;

    // FLAGA: Czy brzuch aktualnie szoruje po ziemi?
    private bool isTouchingGround = false;

    public override void Initialize()
    {
        startingPosition = transform.localPosition;
        startingRotation = transform.localRotation;

        startingLegRotations = new Quaternion[legs.Length];
        for (int i = 0; i < legs.Length; i++)
        {
            startingLegRotations[i] = legs[i].transform.localRotation;
        }
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = startingPosition;
        transform.localRotation = startingRotation;
        bodyRigidbody.linearVelocity = Vector3.zero;
        bodyRigidbody.angularVelocity = Vector3.zero;

        for (int i = 0; i < legs.Length; i++)
        {
            legs[i].transform.localRotation = startingLegRotations[i];
            legs[i].GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            legs[i].GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }

        isTouchingGround = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(bodyRigidbody.linearVelocity.z);
        sensor.AddObservation(transform.up);

        // Obserwacja: Czy agent wie, ¿e dotyka ziemi? (1 = tak, 0 = nie)
        sensor.AddObservation(isTouchingGround ? 1.0f : 0.0f);

        foreach (var leg in legs)
        {
            sensor.AddObservation(leg.spring.targetPosition / legMovementLimit);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float leg1Action = actions.ContinuousActions[0];
        float leg2Action = actions.ContinuousActions[1];
        float leg3Action = actions.ContinuousActions[2];

        ApplyLegAction(legs[0], leg1Action);
        ApplyLegAction(legs[1], leg2Action);
        ApplyLegAction(legs[2], leg3Action);

        float forwardVelocity = bodyRigidbody.linearVelocity.z;
        AddReward(Mathf.Clamp(forwardVelocity, 0f, 2f) * 0.05f);

        if (bodyRigidbody.linearVelocity.magnitude < 0.1f)
        {
            AddReward(-0.01f);
        }

        if (isTouchingGround)
        {
            AddReward(-0.02f); // Boli, wiêc bêdzie chcia³ wstaæ
        }
        else
        {
            AddReward(0.01f); // Brawo, unios³eœ ciê¿ar cia³a!
        }

        float energyUsed = Mathf.Abs(leg1Action) + Mathf.Abs(leg2Action) + Mathf.Abs(leg3Action);
        AddReward(-0.001f * energyUsed); // Delikatna kara za niepotrzebne wierzganie

        if (transform.up.y < 0.2f)
        {
            SetReward(-1f);
            EndEpisode();
        }
    }

    private void ApplyLegAction(HingeJoint leg, float actionValue)
    {
        JointSpring spring = leg.spring;
        spring.targetPosition = actionValue * legMovementLimit;
        leg.spring = spring;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isTouchingGround = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isTouchingGround = false;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetKey(KeyCode.Alpha1) ? 1f : (Input.GetKey(KeyCode.Q) ? -1f : 0f);
        continuousActions[1] = Input.GetKey(KeyCode.Alpha2) ? 1f : (Input.GetKey(KeyCode.W) ? -1f : 0f);
        continuousActions[2] = Input.GetKey(KeyCode.Alpha3) ? 1f : (Input.GetKey(KeyCode.E) ? -1f : 0f);
    }
}