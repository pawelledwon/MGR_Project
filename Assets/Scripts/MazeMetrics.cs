using System;
using System.IO;
using UnityEngine;

public class MazeMetrics : MonoBehaviour
{
    [Header("Konfiguracja")]
    public MazeAgent agent;
    public MazeGenerator generator;
    public string algorithmName = "PPO";

    [Header("Zatrzymanie symulacji")]
    [Tooltip("Liczba epizodów, po których symulacja zostanie zatrzymana i zapisana zostanie Heatmapa")]
    public int maxEpisodes = 2000;

    // Metryki
    private int[,] heatmap;
    private int currentSteps = 0;
    private int episodeCount = 0;
    private int successCount = 0;

    // Pliki
    private StreamWriter metricsWriter;
    private string heatmapFilePath;
    private bool filesClosed = false;

    void Start()
    {
        Time.timeScale = 20f; // Przyspieszenie symulacji

        // Inicjalizacja tablicy na Heatmape (rozmiar siatki z generatora)
        heatmap = new int[generator.width, generator.height];

        string dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Plik z metrykami per epizod (Długość, Sukces)
        string metricsPath = Path.Combine(Application.dataPath, $"../Maze_Metrics_{algorithmName}_{dateStr}.csv");
        metricsWriter = new StreamWriter(metricsPath);
        metricsWriter.WriteLine("Episode,Steps,Success,CumulativeSuccessRate");

        // Ścieżka do Heatmapy (zapisywana dopiero na samym końcu)
        heatmapFilePath = Path.Combine(Application.dataPath, $"../Maze_Heatmap_{algorithmName}_{dateStr}.csv");

        Debug.Log($"[MazeMetrics] Start zapisu. Limit: {maxEpisodes} epizodów.");
    }

    // Wywoływane z Agenta na start epizodu
    public void OnEpisodeStart()
    {
        currentSteps = 0;
    }

    // Wywoływane z Agenta w każdym kroku (OnActionReceived)
    public void RecordStep(Vector3 agentPosition)
    {
        currentSteps++;

        // Zamiana pozycji w świecie Unity na index tablicy 2D
        int x = Mathf.RoundToInt(agentPosition.x);
        int z = Mathf.RoundToInt(agentPosition.z);

        // Zabezpieczenie przed wyjściem poza tablicę
        if (x >= 0 && x < generator.width && z >= 0 && z < generator.height)
        {
            heatmap[x, z]++;
        }
    }

    // Wywoływane z Agenta na koniec epizodu
    public void OnEpisodeEnd(bool success)
    {
        episodeCount++;
        if (success) successCount++;

        float successRate = (float)successCount / episodeCount * 100f;

        // Zapis do CSV per epizod
        metricsWriter.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1},{2},{3:F2}",
            episodeCount, currentSteps, success ? 1 : 0, successRate));
        metricsWriter.Flush();

        // LOGOWANIE DO KONSOLI
        string status = success ? "SUKCES" : "MAX STEPS (PORAŻKA)";
        Debug.Log($"[MazeMetrics] Epizod {episodeCount}/{maxEpisodes} | Status: {status} | Kroki: {currentSteps} | Średni sukces: {successRate:F1}%");

        // Przerwanie po osiągnięciu limitu
        if (episodeCount >= maxEpisodes)
        {
            Debug.Log($"[MazeMetrics] Osiągnięto limit {maxEpisodes} epizodów. Zapisuję mapę ciepła i kończę.");
            SaveAndCloseFiles();

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
    }

    private void SaveAndCloseFiles()
    {
        if (filesClosed) return;

        // Zapis Heatmapy jako macierz (dla łatwego czytania w Pythonie)
        using (StreamWriter hw = new StreamWriter(heatmapFilePath))
        {
            // Piszemy od góry do dołu (z-axis w Unity to wysokość mapy)
            for (int z = generator.height - 1; z >= 0; z--)
            {
                string[] row = new string[generator.width];
                for (int x = 0; x < generator.width; x++)
                {
                    row[x] = heatmap[x, z].ToString();
                }
                hw.WriteLine(string.Join(",", row));
            }
        }
        Debug.Log($"[MazeMetrics] Mapa ciepła zapisana do: {heatmapFilePath}");

        metricsWriter?.Close();
        filesClosed = true;
    }

    void OnApplicationQuit()
    {
        SaveAndCloseFiles();
    }
}
