using System;
using System.IO;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class TripoidAgentMetrics : MonoBehaviour
{
    [Header("Konfiguracja")]
    public TripodAgent agent;
    public string algorithmName = "PPO";

    [Header("Zatrzymanie symulacji")]
    [Tooltip("Liczba epizodów, po których symulacja zostanie zatrzymana")]
    public int maxEpisodes = 2000;

    [Header("Akcja w czasie")]
    [Tooltip("Ktory epizod zapisac jako akcja w czasie (1 = pierwszy epizod ewaluacji)")]
    public int actionOverTimeEpisode = 1;

    private string csvFilePath;
    private StreamWriter csvWriter;
    private int episodeCount = 0;

    private string actionCsvFilePath;
    private StreamWriter actionCsvWriter;
    private bool actionFileWritten = false;

    private float[] previousActions = new float[6];
    private Vector3[] previousAngularVelocities = new Vector3[6];
    private float episodeActionJitter = 0f;
    private float episodeMechJitter = 0f;
    private int stepsInCurrentEpisode = 0;

    private float episodeSpeedSum = 0f;

    private Rigidbody[] swingRbs = new Rigidbody[3];
    private Rigidbody[] liftRbs = new Rigidbody[3];

    void Start()
    {
        Time.timeScale = 20f;

        if (agent == null)
            agent = GetComponent<TripodAgent>();

        for (int i = 0; i < 3; i++)
        {
            swingRbs[i] = agent.swingJoints[i].GetComponent<Rigidbody>();
            liftRbs[i] = agent.liftJoints[i].GetComponent<Rigidbody>();
        }

        // Plik glowny — metryki per epizod
        string dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        csvFilePath = Path.Combine(Application.dataPath,
            $"../Tripoid_Metrics_{algorithmName}_{dateStr}.csv");
        csvWriter = new StreamWriter(csvFilePath);
        csvWriter.WriteLine("Episode,TotalGlobalSteps,MeanActionJitter,MeanMechJitter,MeanLinearSpeed");
        Debug.Log($"[Metrics] Zapis metryk do: {csvFilePath}");

        // Plik akcji w czasie — jeden wybrany epizod
        actionCsvFilePath = Path.Combine(Application.dataPath,
            $"../Tripoid_ActionOverTime_{algorithmName}_{dateStr}.csv");
        actionCsvWriter = new StreamWriter(actionCsvFilePath);
        actionCsvWriter.WriteLine("Step,Action0_deg,Action1_deg,Action2_deg,Action3_deg,Action4_deg,Action5_deg");
        Debug.Log($"[Metrics] Zapis akcji w czasie do: {actionCsvFilePath}");
    }

    public void OnEpisodeStart()
    {
        if (stepsInCurrentEpisode > 0)
        {
            SaveEpisodeData();
        }

        episodeActionJitter = 0f;
        episodeMechJitter = 0f;
        episodeSpeedSum = 0f;
        stepsInCurrentEpisode = 0;
        Array.Clear(previousActions, 0, previousActions.Length);
        Array.Clear(previousAngularVelocities, 0, previousAngularVelocities.Length);
    }

    // Wywolywana z TripodAgent.cs po otrzymaniu akcji
    public void RecordJitter(ActionBuffers actions)
    {
        stepsInCurrentEpisode++;

        // ── Action Jitter ────────────────────────────────────
        for (int i = 0; i < 6; i++)
        {
            float currentAction = actions.ContinuousActions[i];
            episodeActionJitter += Mathf.Abs(currentAction - previousActions[i]);
            previousActions[i] = currentAction;
        }

        // ── Mechanical Jitter ────────────────────────────────
        int index = 0;
        for (int i = 0; i < 3; i++)
        {
            Vector3 currentSwingAngVel = swingRbs[i].angularVelocity;
            episodeMechJitter += (currentSwingAngVel - previousAngularVelocities[index]).magnitude;
            previousAngularVelocities[index] = currentSwingAngVel;
            index++;

            Vector3 currentLiftAngVel = liftRbs[i].angularVelocity;
            episodeMechJitter += (currentLiftAngVel - previousAngularVelocities[index]).magnitude;
            previousAngularVelocities[index] = currentLiftAngVel;
            index++;
        }

        // ── Predkosc liniowa ─────────────────────────────────
        episodeSpeedSum += agent.bodyRigidbody.linearVelocity.magnitude;

        // ── Akcja w czasie — zapis wybranego epizodu ─────────
        // episodeCount jest inkrementowany w SaveEpisodeData wiec tu jest o 1 mniejszy
        // porownujemy z (actionOverTimeEpisode - 1) bo liczymy od 0
        if (!actionFileWritten && episodeCount == actionOverTimeEpisode - 1)
        {
            // Przeskalowanie akcji do stopni: [-1,1] -> [-60,60]
            float legMovementLimit = agent.legMovementLimit;
            float a0 = actions.ContinuousActions[0] * legMovementLimit;
            float a1 = actions.ContinuousActions[1] * legMovementLimit;
            float a2 = actions.ContinuousActions[2] * legMovementLimit;
            float a3 = actions.ContinuousActions[3] * legMovementLimit;
            float a4 = actions.ContinuousActions[4] * legMovementLimit;
            float a5 = actions.ContinuousActions[5] * legMovementLimit;

            actionCsvWriter.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}",
                stepsInCurrentEpisode, a0, a1, a2, a3, a4, a5));
            actionCsvWriter.Flush();
        }
    }

    public void OnActionOverTimeEpisodeEnd()
    {
        if (!actionFileWritten && episodeCount == actionOverTimeEpisode - 1 && stepsInCurrentEpisode > 0)
        {
            actionFileWritten = true;
            actionCsvWriter.Flush();
            Debug.Log($"[Metrics] Zakończono zapis wybranego epizodu - Akcja w czasie ({stepsInCurrentEpisode} krokow).");
        }
    }

    private void SaveEpisodeData()
    {
        episodeCount++;

        float meanActionJitter = (episodeActionJitter / stepsInCurrentEpisode) / 6f;
        float meanMechJitter = (episodeMechJitter / stepsInCurrentEpisode) / 6f;
        float meanLinearSpeed = episodeSpeedSum / stepsInCurrentEpisode;

        csvWriter.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1},{2:F6},{3:F6},{4:F6}",
            episodeCount,
            agent.StepCount,
            meanActionJitter,
            meanMechJitter,
            meanLinearSpeed));
        csvWriter.Flush();

        Debug.Log($"[Metrics] Epizod {episodeCount}/{maxEpisodes} zakończony ({stepsInCurrentEpisode} kroków): ActionJitter={meanActionJitter:F4}, MechJitter={meanMechJitter:F4}, Speed={meanLinearSpeed:F4}");

        // Sprawdzenie limitu epizodów i zatrzymanie symulacji
        if (episodeCount >= maxEpisodes)
        {
            Debug.Log($"[Metrics] Osiągnięto limit {maxEpisodes} epizodów. Przerywam symulację i zamykam pliki.");
            
            // Zamykanie plików tuż przed zatrzymaniem
            csvWriter?.Close();
            actionCsvWriter?.Close();

            // Zatrzymanie edytora Unity lub wyjście ze zbudowanej aplikacji
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }

    private void OnApplicationQuit()
    {
        csvWriter?.Close();
        actionCsvWriter?.Close();
    }
}