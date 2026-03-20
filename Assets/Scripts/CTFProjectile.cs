using UnityEngine;

public class CTFProjectile : MonoBehaviour
{
    [HideInInspector] public CTFAgent owner;
    [HideInInspector] public int teamID;

    private void OnTriggerEnter(Collider other)
    {
        // Destroy on wall hit
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        CTFAgent hit = other.GetComponent<CTFAgent>();
        if (hit == null) return;
        if (hit == owner) return; 
        if (hit.teamID == teamID) return;

        hit.OnHitByProjectile(owner);
        Destroy(gameObject);
    }
}
