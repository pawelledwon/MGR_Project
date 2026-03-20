using System.Collections;
using UnityEngine;

public class FlagTrigger : MonoBehaviour
{
    public int FlagTeamID;
    public bool IsCarried = false;

    private bool m_PickupBlocked = false;
    private CTFGameController m_Controller;

    void Start()
    {
        m_Controller = GetComponentInParent<CTFGameController>();
    }

    public void ResetFlag()
    {
        StopAllCoroutines();
        IsCarried = false;
        m_PickupBlocked = false;
    }

    public void DropCooldown()
    {
        IsCarried = false;
        StartCoroutine(BlockPickupBriefly());
    }

    private IEnumerator BlockPickupBriefly()
    {
        m_PickupBlocked = true;
        yield return new WaitForSeconds(1.5f);
        m_PickupBlocked = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsCarried) return;
        if (m_PickupBlocked) return;

        CTFAgent agent = other.GetComponent<CTFAgent>();
        if (agent == null) return;
        m_Controller.AgentTouchedFlag(agent, FlagTeamID);
    }
}