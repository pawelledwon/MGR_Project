using UnityEngine;

public class FlagFieldTrigger : MonoBehaviour
{
    public int FlagTeamID;
    private CTFGameController m_Controller;
    void Start()
    {
        m_Controller = GetComponentInParent<CTFGameController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        CTFAgent agent = other.GetComponent<CTFAgent>();

        if (agent == null) return;
        if (agent.teamID != FlagTeamID) return;

        m_Controller.ScoreCapture(agent);
    }
}
