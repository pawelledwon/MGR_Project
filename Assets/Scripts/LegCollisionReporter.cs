using UnityEngine;

public class LegCollisionReporter : MonoBehaviour
{
    [SerializeField]
    private TripodAgent agent;

    void Start()
    {
        agent = GetComponentInParent<TripodAgent>();
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            agent.OnLegHitWall();
        }
    }
}