using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class CTFMetricsLogger : MonoBehaviour
{
    [Header("Settings")]
    public string algorithmName = "MAPOCA";
    public float escortDistanceThreshold = 5f;
    public float arenaHalfwayZ = 0f; // world Z coordinate of arena centre line

    // Per-episode accumulators
    private int escortFrames = 0;
    private int roleDiversityFrames = 0;
    private int totalFrames = 0;
    private int capturesThisEpisode = 0;
    private float carrierTimeAlive = 0f;
    private int episodeCount = 0;

    // CSV output
    private string filePath;
    private StreamWriter writer;

    void Start()
    {
        filePath = Path.Combine(Application.dataPath,
            $"../Metrics_{algorithmName}.csv");

        writer = new StreamWriter(filePath, append: false);
        writer.WriteLine(
            "Episode,Captures,EscortRate,RoleDiversityRate," +
            "AvgCarrierSurvivalTime,EpisodeLength");
        writer.Flush();
    }

    void OnDestroy()
    {
        writer?.Close();
    }


    public void RecordFrame(List<CTFAgent> team0, List<CTFAgent> team1)
    {
        totalFrames++;

        // Check both teams for escort and role diversity
        RecordTeamMetrics(team0);
        RecordTeamMetrics(team1);
    }

    private void RecordTeamMetrics(List<CTFAgent> team)
    {
        CTFAgent carrier = null;
        foreach (var a in team)
            if (a.hasEnemyFlag) { carrier = a; break; }

        // Escort rate
        if (carrier != null)
        {
            carrierTimeAlive += Time.fixedDeltaTime;

            foreach (var a in team)
            {
                if (a == carrier) continue;
                float dist = Vector3.Distance(
                    a.transform.position, carrier.transform.position);
                if (dist < escortDistanceThreshold)
                    escortFrames++;
            }
        }

        bool hasAttacker = false, hasDefender = false;
        foreach (var a in team)
        {
            float distToEnemy = Vector3.Distance(a.transform.position, a.enemyBase.position);
            float distToHome = Vector3.Distance(a.transform.position, a.homeBase.position);
            if (distToEnemy < distToHome) hasAttacker = true;
            else hasDefender = true;
        }

        if (hasAttacker && hasDefender) roleDiversityFrames++;
    }

    public void RecordCapture()
    {
        capturesThisEpisode++;
    }

    public void RecordEpisodeEnd()
    {
        Debug.Log($"Episode {episodeCount} ended with {capturesThisEpisode} captures " +
            $"over {totalFrames} frames.");
        episodeCount++;

        float escortRate = totalFrames > 0
            ? (float)escortFrames / totalFrames : 0f;
        float roleDivRate = totalFrames > 0
            ? (float)roleDiversityFrames / totalFrames : 0f;
        float avgSurvival = capturesThisEpisode > 0
            ? carrierTimeAlive / capturesThisEpisode : 0f;

        writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:F4},{3:F4},{4:F2},{5}",
            episodeCount,
            capturesThisEpisode,
            escortRate,
            roleDivRate,
            avgSurvival,
            totalFrames));

        writer.Flush();

        // Reset accumulators
        escortFrames = 0;
        roleDiversityFrames = 0;
        totalFrames = 0;
        capturesThisEpisode = 0;
        carrierTimeAlive = 0f;
    }
}
