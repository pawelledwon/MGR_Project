using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class TripodAgent : Agent
{
    [Header("Fizyka Robota")]
    public Rigidbody bodyRigidbody;
    public HingeJoint[] swingJoints = new HingeJoint[3]; 
    public HingeJoint[] liftJoints = new HingeJoint[3];

    [Header("Parametry Uczenia")]
    public float legMovementLimit = 60f;

    [Header("Cel")]
    public float arenaSize = 10f;
    public GameObject targetPrefab;   
    private GameObject targetInstance;
    private Transform target;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private Vector3[] startingSwingPositions;
    private Quaternion[] startingSwingRotations;
    private Vector3[] startingLiftPositions;
    private Quaternion[] startingLiftRotations;

    private bool isTouchingGround = false;
    private float previousDistanceToTarget;

    private Rigidbody[] swingRbs = new Rigidbody[3];
    private Rigidbody[] liftRbs = new Rigidbody[3];

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

            swingRbs[i] = swingJoints[i].GetComponent<Rigidbody>();
            liftRbs[i] = liftJoints[i].GetComponent<Rigidbody>();
        }
    }

    public override void OnEpisodeBegin()
    {
        if (targetInstance != null)
        {
            Destroy(targetInstance);
            targetInstance = null;
            target = null;
        }

        bodyRigidbody.isKinematic = false;

        for (int i = 0; i < 3; i++)
        {
            swingRbs[i].isKinematic = false;
            liftRbs[i].isKinematic = false;
        }

        bodyRigidbody.linearVelocity = Vector3.zero;
        bodyRigidbody.angularVelocity = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            swingRbs[i].linearVelocity = Vector3.zero;
            swingRbs[i].angularVelocity = Vector3.zero;
            liftRbs[i].linearVelocity = Vector3.zero;
            liftRbs[i].angularVelocity = Vector3.zero;
        }

        bodyRigidbody.isKinematic = true;

        for (int i = 0; i < 3; i++)
        {
            swingRbs[i].isKinematic = true;
            liftRbs[i].isKinematic = true;
        }

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

        SpawnTarget();
    }

    private void SpawnTarget()
    {
        if (targetInstance != null)
        {
            Destroy(targetInstance);
            targetInstance = null;
        }

        float halfArena = arenaSize / 2f;

        Vector3 localSpawnPos = new Vector3(
            Random.Range(-halfArena, halfArena),
            startingPosition.y,
            Random.Range(-halfArena, halfArena)
        );

        Vector3 worldSpawnPos = transform.parent != null
            ? transform.parent.TransformPoint(localSpawnPos)
            : localSpawnPos;

        targetInstance = Instantiate(targetPrefab, worldSpawnPos, Quaternion.identity);

        if (transform.parent != null)
            targetInstance.transform.SetParent(transform.parent);

        target = targetInstance.transform;
        previousDistanceToTarget = Vector3.Distance(transform.position, target.position);
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
        sensor.AddObservation(bodyRigidbody.linearVelocity);   
        sensor.AddObservation(bodyRigidbody.angularVelocity);  
        sensor.AddObservation(transform.forward);             
        sensor.AddObservation(transform.up);                   

        sensor.AddObservation(isTouchingGround ? 1.0f : 0.0f);

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            sensor.AddObservation(toTarget.normalized);           
            sensor.AddObservation(toTarget.magnitude);            
        }

        for (int i = 0; i < 3; i++)
        {
            sensor.AddObservation(swingJoints[i].spring.targetPosition / legMovementLimit);
            sensor.AddObservation(liftJoints[i].spring.targetPosition / legMovementLimit);
            sensor.AddObservation(swingRbs[i].angularVelocity);                            
            sensor.AddObservation(liftRbs[i].angularVelocity);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        for (int i = 0; i < 3; i++)
        {
            ApplyLegAction(swingJoints[i], actions.ContinuousActions[i * 2]);
            ApplyLegAction(liftJoints[i], actions.ContinuousActions[i * 2 + 1]);
        }

        if (target != null)
        {
            float currentDistance = Vector3.Distance(transform.position, target.position);
            float deltaDistance = previousDistanceToTarget - currentDistance;

            AddReward(deltaDistance * 5.0f);               
            AddReward(-0.001f * currentDistance);           
            previousDistanceToTarget = currentDistance;

            if (currentDistance < 1.5f)
            {
                AddReward(5f);
                EndEpisode();
            }
        }

        if (isTouchingGround)
            AddReward(-0.02f);

        AddReward(-0.001f);

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            if (hit.distance < 0.5f)
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

        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.5f);
            EndEpisode();
        }

        if (collision.gameObject.CompareTag("Goal"))
        {
            AddReward(5f);
            EndEpisode();
        }
    }

    public void OnLegHitWall()
    {
        AddReward(-0.5f);
        EndEpisode();
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