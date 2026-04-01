using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class CTFGameController : MonoBehaviour
{
    [Header("Teams")]
    public List<CTFAgent> team0Agents;
    public List<CTFAgent> team1Agents;

    [Header("Flags")]
    public Transform team0Flag;   // Team 1 tries to capture this
    public Transform team1Flag;   // Team 0 tries to capture this

    [Header("Episode")]
    public int maxSteps = 5000;   // episode timeout; tune to your arena size

    private CTFMetricsLogger metricsLogger;

    private Vector3 team0FlagSpawn;
    private Vector3 team1FlagSpawn;
    private CTFAgent team0FlagCarrier = null;  // team1 agent carrying team0's flag
    private CTFAgent team1FlagCarrier = null;  // team0 agent carrying team1's flag

    private int stepCount = 0;
    private bool episodePending = false;

    private SimpleMultiAgentGroup m_Team0Group;
    private SimpleMultiAgentGroup m_Team1Group;


    void Start()
    {
        metricsLogger = GetComponent<CTFMetricsLogger>();

        Time.timeScale = 20f;

        team0FlagSpawn = team0Flag.position;
        team1FlagSpawn = team1Flag.position;

        m_Team0Group = new SimpleMultiAgentGroup();
        m_Team1Group = new SimpleMultiAgentGroup();

        foreach (var a in team0Agents) m_Team0Group.RegisterAgent(a);
        foreach (var a in team1Agents) m_Team1Group.RegisterAgent(a);

        StartEpisode();
    }

    void FixedUpdate()
    {
        if (episodePending)
        {
            episodePending = false;
            StartEpisode();
            return;
        }

        metricsLogger.RecordFrame(team0Agents, team1Agents);

        stepCount++;
        if (stepCount >= maxSteps)
        {
            //m_Team0Group.GroupEpisodeInterrupted(); //MA - POCA
            //m_Team1Group.GroupEpisodeInterrupted();

            foreach (var a in team0Agents) a.EpisodeInterrupted(); //PPO
            foreach (var a in team1Agents) a.EpisodeInterrupted();

            episodePending = true;
        }
    }

    public void RequestEpisodeReset()
    {
        episodePending = true;
    }

    private void StartEpisode()
    {
        metricsLogger.RecordEpisodeEnd();

        stepCount = 0;
        ResetAllFlags();

        foreach (var a in team0Agents) a.ResetAgent();
        foreach (var a in team1Agents) a.ResetAgent();
    }

    public void AgentTouchedFlag(CTFAgent agent, int flagTeamID)
    {
        if (agent.teamID != flagTeamID && !agent.hasEnemyFlag)
        {
            Transform flag = flagTeamID == 0 ? team0Flag : team1Flag;

            flag.SetParent(agent.transform);
            flag.localPosition = new Vector3(0f, 2f, 0f);
            flag.localRotation = Quaternion.identity;
            flag.GetComponent<FlagTrigger>().IsCarried = true;

            if (flagTeamID == 0) team0FlagCarrier = agent;
            else team1FlagCarrier = agent;

            agent.OnFlagPickedUp();
        }
    }

    private void ResetAllFlags()
    {
        team0Flag.SetParent(transform);
        team0Flag.position = team0FlagSpawn;
        team0Flag.localRotation = Quaternion.identity;
        team0Flag.gameObject.SetActive(true);
        team0Flag.GetComponent<FlagTrigger>().ResetFlag();
        team0FlagCarrier = null;

        team1Flag.SetParent(transform);
        team1Flag.position = team1FlagSpawn;
        team1Flag.localRotation = Quaternion.identity;
        team1Flag.gameObject.SetActive(true);
        team1Flag.GetComponent<FlagTrigger>().ResetFlag();
        team1FlagCarrier = null;
    }

    public void ScoreCapture(CTFAgent agent)
    {
        if (!agent.hasEnemyFlag) return;

        // Individual bonus for the carrier
        agent.OnFlagCaptured();

        /*MA - POCA*/

        //SimpleMultiAgentGroup winningGroup = agent.teamID == 0 ? m_Team0Group : m_Team1Group;
        //SimpleMultiAgentGroup losingGroup = agent.teamID == 0 ? m_Team1Group : m_Team0Group;

        //float timeBonus = 1f - (float)stepCount / (float)maxSteps;

        //winningGroup.AddGroupReward(2f + timeBonus);  // +2 for win + up to +1 for speed
        //losingGroup.AddGroupReward(-1f);               // -1 for losing

        //// EndGroupEpisode tells MA-POCA this was a natural completion —
        //// different from GroupEpisodeInterrupted which signals a timeout
        //winningGroup.EndGroupEpisode();
        //losingGroup.EndGroupEpisode();

        /*PPO*/

        List<CTFAgent> winners = agent.teamID == 0 ? team0Agents : team1Agents;
        List<CTFAgent> losers = agent.teamID == 0 ? team1Agents : team0Agents;

        float timeBonus = 1f - (float)stepCount / (float)maxSteps;

        // Same reward values as MA-POCA group rewards — critical for fair comparison
        foreach (var a in winners) a.AddReward(2f + timeBonus);
        foreach (var a in losers) a.AddReward(-1f);

        foreach (var a in team0Agents) a.EndEpisode();
        foreach (var a in team1Agents) a.EndEpisode();

        /**/
        metricsLogger.RecordCapture();

        RequestEpisodeReset();
    }

    public void DropFlag(CTFAgent carrier)
    {
        if (carrier.teamID == 1 && team0FlagCarrier == carrier)
        {
            team0Flag.SetParent(transform);
            team0Flag.position = carrier.transform.position + Vector3.up * 0.5f;
            team0FlagCarrier = null;
            team0Flag.GetComponent<FlagTrigger>().DropCooldown();
        }
        else if (carrier.teamID == 0 && team1FlagCarrier == carrier)
        {
            team1Flag.SetParent(transform);
            team1Flag.position = carrier.transform.position + Vector3.up * 0.5f;
            team1FlagCarrier = null;
            team1Flag.GetComponent<FlagTrigger>().DropCooldown();
        }
    }

    public Vector3 GetEnemyFlagPosition(int teamID)
    {
        if (teamID == 0)
            return team1FlagCarrier != null ? team1FlagCarrier.transform.position : team1Flag.position;
        else
            return team0FlagCarrier != null ? team0FlagCarrier.transform.position : team0Flag.position;
    }

    public List<CTFAgent> GetTeammates(CTFAgent agent)
    {
        var list = agent.teamID == 0 ? team0Agents : team1Agents;
        var result = new List<CTFAgent>();
        foreach (var a in list)
            if (a != agent) result.Add(a);
        return result;
    }

    public List<CTFAgent> GetEnemies(CTFAgent agent)
    {
        return agent.teamID == 0 ? team1Agents : team0Agents;
    }
}