using System;
using System.IO;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class TripoidAgentMetrics : MonoBehaviour
{
    [Header("Konfiguracja")]
    public TripodAgent agent;
    public string algorithmName = "PPO"; // Zmieniaj na SAC/PPO przed testami

    private string csvFilePath;
    private StreamWriter csvWriter;
    private int episodeCount = 0;

    // Dane do obliczeñ
    private float[] previousActions = new float[6];
    private Vector3[] previousAngularVelocities = new Vector3[6];

    private float episodeActionJitter = 0f;
    private float episodeMechJitter = 0f;
    private int stepsInCurrentEpisode = 0;

    // Referencje do cia³ fizycznych nóg
    private Rigidbody[] swingRbs = new Rigidbody[3];
    private Rigidbody[] liftRbs = new Rigidbody[3];

    void Start()
    {
        Time.timeScale = 1f;

        if (agent == null)
            agent = GetComponent<TripodAgent>();

        for (int i = 0; i < 3; i++)
        {
            swingRbs[i] = agent.swingJoints[i].GetComponent<Rigidbody>();
            liftRbs[i] = agent.liftJoints[i].GetComponent<Rigidbody>();
        }

        string dateStr = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        csvFilePath = Path.Combine(Application.dataPath, $"../Tripoid_Jitter_{algorithmName}_{dateStr}.csv");
        csvWriter = new StreamWriter(csvFilePath);

        // Nag³ówki w pliku
        csvWriter.WriteLine("Episode,TotalGlobalSteps,MeanActionJitter,MeanMechJitter");
        Debug.Log($"[Metrics] Rozpoczêto zapis logów p³ynnoœci do: {csvFilePath}");
    }

    public void OnEpisodeStart()
    {
        if (stepsInCurrentEpisode > 0)
        {
            SaveEpisodeData();
        }

        episodeActionJitter = 0f;
        episodeMechJitter = 0f;
        stepsInCurrentEpisode = 0;
        System.Array.Clear(previousActions, 0, previousActions.Length);
    }

    // Ta metoda bêdzie wywo³ywana z TripodAgent.cs po otrzymaniu akcji
    public void RecordJitter(ActionBuffers actions)
    {
        stepsInCurrentEpisode++;

        for (int i = 0; i < 6; i++)
        {
            float currentAction = actions.ContinuousActions[i];
            episodeActionJitter += Mathf.Abs(currentAction - previousActions[i]);
            previousActions[i] = currentAction;
        }

        int index = 0;
        for (int i = 0; i < 3; i++)
        {
            Vector3 currentSwingAngVel = swingRbs[i].angularVelocity;
            episodeMechJitter += Vector3.Distance(currentSwingAngVel, previousAngularVelocities[index]);
            previousAngularVelocities[index] = currentSwingAngVel;
            index++;

            Vector3 currentLiftAngVel = liftRbs[i].angularVelocity;
            episodeMechJitter += Vector3.Distance(currentLiftAngVel, previousAngularVelocities[index]);
            previousAngularVelocities[index] = currentLiftAngVel;
            index++;
        }
    }

    private void SaveEpisodeData()
    {
        episodeCount++;

        Debug.Log("Nr epizodu: " + episodeCount);

        float meanActionJitter = episodeActionJitter / stepsInCurrentEpisode;
        float meanMechJitter = episodeMechJitter / stepsInCurrentEpisode;

        csvWriter.WriteLine($"{episodeCount},{agent.StepCount},{meanActionJitter.ToString(System.Globalization.CultureInfo.InvariantCulture)},{meanMechJitter.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        csvWriter.Flush();
    }

    private void OnApplicationQuit()
    {
        if (csvWriter != null)
        {
            csvWriter.Close();
        }
    }
}
