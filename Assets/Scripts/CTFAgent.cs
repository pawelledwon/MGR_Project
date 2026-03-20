using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public class CTFAgent : Agent
{
    [Header("Team")]
    public int teamID;

    [Header("References")]
    public Transform homeBase;
    public Transform enemyBase;
    public CTFGameController gameController;

    [Header("Buffer Sensors")]
    public BufferSensorComponent teamBuffer;   // Inspector: Observation Size = 5, Max Count = 2
    public BufferSensorComponent enemyBuffer;  // Inspector: Observation Size = 5, Max Count = 3

    [Header("Dash Settings")]
    public float dashCooldown = 2.0f;
    public float dashForce = 18f;

    [Header("Shoot Settings")]
    public float shootCooldown = 1.5f;
    public float projectileForce = 25f;
    public float stunDuration = 3.0f;
    public Transform shootPoint;         // child transform at the agent's front
    public GameObject projectilePrefab;  // projectile prefab

    [HideInInspector] public bool hasEnemyFlag = false;
    [HideInInspector] public bool isStunned = false;

    private Rigidbody agentRigidbody;
    private BehaviorParameters agentBehaviorParameters;
    private float dashTimer = 0f;
    private float shootTimer = 0f;
    private Vector3 spawnPos;
    private Quaternion spawnRot;

    private const float POS_NORM = 8f;

    private static readonly RigidbodyConstraints defaultConstraints = RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezeRotationX
                               | RigidbodyConstraints.FreezeRotationZ;

    public override void Initialize()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        agentBehaviorParameters = GetComponent<BehaviorParameters>();
        spawnPos = transform.position;
        spawnRot = transform.rotation;
    }
    public override void OnEpisodeBegin() { }

    public void ResetAgent()
    {
        StopAllCoroutines();

        transform.position = spawnPos;
        transform.rotation = spawnRot;

        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;

        agentRigidbody.constraints = defaultConstraints;

        dashTimer = 0f;
        shootTimer = 0f;
        hasEnemyFlag = false;
        isStunned = false;
    }

    void FixedUpdate()
    {
        if (dashTimer > 0f) dashTimer -= Time.fixedDeltaTime;
        if (shootTimer > 0f) shootTimer -= Time.fixedDeltaTime;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(hasEnemyFlag);                                           // 1
        sensor.AddObservation(isStunned);
        sensor.AddObservation(dashTimer <= 0f);                                        // 1
        sensor.AddObservation(shootTimer <= 0f);
        sensor.AddObservation(LocalVelocity());                                        // 2
        sensor.AddObservation(agentRigidbody.linearVelocity.y / 10f);                 // 1

        sensor.AddObservation(transform.InverseTransformPoint(enemyBase.position) / POS_NORM); // 3
        sensor.AddObservation(transform.InverseTransformPoint(homeBase.position) / POS_NORM); // 3

        Vector3 flagPos = gameController.GetEnemyFlagPosition(teamID);
        sensor.AddObservation(transform.InverseTransformPoint(flagPos) / POS_NORM);            // 3

        foreach (var t in gameController.GetTeammates(this))
        {
            if (!t.gameObject.activeInHierarchy) continue;
            teamBuffer.AppendObservation(AgentObs(t));
        }

        foreach (var e in gameController.GetEnemies(this))
        {
            if (!e.gameObject.activeInHierarchy) continue;
            enemyBuffer.AppendObservation(AgentObs(e));
        }

        if (!hasEnemyFlag)
        {
            float distToFlag = Vector3.Distance(transform.position, flagPos);
            AddReward(-distToFlag * 0.0001f);
        }
        else
        {
            float distToHome = Vector3.Distance(transform.position, homeBase.position);
            AddReward(-distToHome * 0.0001f);
        }
    }

    // 5 floats — must match "Observation Size" on both BufferSensorComponents in the Inspector
    private float[] AgentObs(CTFAgent other)
    {
        Vector3 relPos = transform.InverseTransformPoint(other.transform.position) / POS_NORM;

        float relVelFwd = Vector3.Dot(other.agentRigidbody.linearVelocity, transform.forward) / 20f;
        float relVelRight = Vector3.Dot(other.agentRigidbody.linearVelocity, transform.right) / 20f;

        return new float[]
        {
            relPos.x,
            relPos.z,                       // z not y — ground-plane position
            relVelFwd,
            relVelRight,
            other.hasEnemyFlag ? 1f : 0f,
            other.isStunned    ? 1f : 0f,
        };
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isStunned) return;

        var c = actions.ContinuousActions;
        var d = actions.DiscreteActions;

        Vector3 moveDir = transform.forward * c[0] + transform.right * c[1];
        agentRigidbody.AddForce(moveDir * 10f, ForceMode.Force);

        float turnAmount = c[2] * 180f * Time.fixedDeltaTime;
        agentRigidbody.MoveRotation(agentRigidbody.rotation * Quaternion.Euler(0f, turnAmount, 0f));

        if (d[0] == 1 && dashTimer <= 0f && moveDir.magnitude > 0.1f)
        {
            agentRigidbody.AddForce(moveDir.normalized * dashForce, ForceMode.Impulse);
            dashTimer = dashCooldown;
        }

        if (d[1] == 1 && shootTimer <= 0f)
        {
            Shoot();
            shootTimer = shootCooldown;
        }

        AddReward(-0.0005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        var d = actionsOut.DiscreteActions;

        c[0] = Input.GetAxis("Vertical");
        c[1] = Input.GetAxis("Horizontal");
        c[2] = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        d[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        d[1] = Input.GetKey(KeyCode.F) ? 1 : 0;
    }

    private void Shoot()
    {
        if (projectilePrefab == null || shootPoint == null) return;

        // Spawn at shootPoint — position and rotation follow the agent automatically
        // since shootPoint is a child transform
        GameObject proj = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);

        // Set ownership so the projectile knows who fired it
        CTFProjectile p = proj.GetComponent<CTFProjectile>();
        if (p != null)
        {
            p.owner = this;
            p.teamID = teamID;
        }

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 shootDir = shootPoint.forward;
            shootDir.y = 0f;
            shootDir.Normalize();

            rb.useGravity = false;  // fly straight, no arc
            rb.AddForce(shootDir * projectileForce, ForceMode.Impulse);
        }

        Destroy(proj, 4f);  // safety cleanup if it somehow misses everything
    }

    public void OnHitByProjectile(CTFAgent shooter)
    {
        if (isStunned) return;

        shooter.AddReward(0.1f);

        if (hasEnemyFlag)
        {
            gameController.DropFlag(this);
            hasEnemyFlag = false;
        }

        Vector3 knockbackDir = (transform.position - shooter.transform.position).normalized;
        agentRigidbody.AddForce(knockbackDir * 12f, ForceMode.Impulse);

        StartCoroutine(StunCoroutine());
    }

    private IEnumerator StunCoroutine()
    {
        isStunned = true;

        yield return new WaitForSeconds(0.15f);


        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;
        agentRigidbody.constraints = RigidbodyConstraints.FreezeAll;

        yield return new WaitForSeconds(stunDuration);
        agentRigidbody.constraints = defaultConstraints;


        isStunned = false;
    }

    public void OnFlagPickedUp()
    {
        hasEnemyFlag = true;
        AddReward(0.5f);
    }

    public void OnFlagCaptured()
    {
        hasEnemyFlag = false;
        AddReward(1.0f);

        
        foreach (var t in gameController.GetTeammates(this))
            t.AddReward(0.5f);
    }

    public void OnEnemyCapturedOurFlag()
    {
        AddReward(-1.0f);
    }

    // ── Helpers ────────────────────────────────────────────

    private Vector2 LocalVelocity()
    {
        return new Vector2(
            Vector3.Dot(agentRigidbody.linearVelocity, transform.forward) / 20f,
            Vector3.Dot(agentRigidbody.linearVelocity, transform.right) / 20f
        );
    }
}
