import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.gridspec as gridspec
import numpy as np
from scipy import stats

mapoca = pd.read_csv("Metrics_MAPOCA_2000.csv")
ppo    = pd.read_csv("Metrics_PPO_2000.csv")

MAPOCA_COLOR = "orange"
PPO_COLOR    = "purple"
ROLL         = 20

# ── Derived metrics ────────────────────────────────────────
for df in (mapoca, ppo):
    df["CaptureSuccess"]    = (df["Captures"] > 0).astype(int)
    df["StepsPerCapture"]   = df["EpisodeLength"] / df["Captures"].replace(0, np.nan)
    df["EscortXSurvival"]   = df["EscortRate"] * df["AvgCarrierSurvivalTime"]

# ── 1. Summary statistics table ───────────────────────────
metrics_list = [
    "Captures", "EscortRate", "RoleDiversityRate",
    "AvgCarrierSurvivalTime", "EpisodeLength",
    "CaptureSuccess", "StepsPerCapture", "EscortXSurvival"
]

rows = []
for m in metrics_list:
    ma_vals = mapoca[m].dropna()
    pp_vals = ppo[m].dropna()

    t_stat, p_val = stats.ttest_ind(ma_vals, pp_vals)
    cohens_d = (ma_vals.mean() - pp_vals.mean()) / np.sqrt(
        (ma_vals.std()**2 + pp_vals.std()**2) / 2
    )

    rows.append({
        "Metric":         m,
        "MA-POCA Mean":   f"{ma_vals.mean():.4f}",
        "MA-POCA Std":    f"{ma_vals.std():.4f}",
        "MA-POCA Median": f"{ma_vals.median():.4f}",
        "PPO Mean":       f"{pp_vals.mean():.4f}",
        "PPO Std":        f"{pp_vals.std():.4f}",
        "PPO Median":     f"{pp_vals.median():.4f}",
        "p-value":        f"{p_val:.4f}",
        "Cohen's d":      f"{cohens_d:.4f}",
        "Significant":    "YES" if p_val < 0.05 else "NO"
    })

summary = pd.DataFrame(rows)
summary.to_csv("ctf_comparison/metrics_summary.csv", index=False)
print("\n=== SUMMARY STATISTICS ===")
print(summary.to_string(index=False))

# ── 2. Main rolling plots (original 4 + 4 new) ───────────
fig, axes = plt.subplots(4, 2, figsize=(16, 20))
fig.suptitle("MA-POCA vs PPO — Cooperation Metrics (rolling mean 20 episodes)",
             fontsize=14, fontweight="bold")

plot_metrics = [
    ("Captures",               "Flag Captures per Episode"),
    ("EscortRate",             "Escort Rate"),
    ("RoleDiversityRate",      "Role Diversity Rate"),
    ("AvgCarrierSurvivalTime", "Avg Carrier Survival Time (s)"),
    ("EpisodeLength",          "Episode Length (steps)"),
    ("CaptureSuccess",         "Capture Success Rate (any capture)"),
    ("StepsPerCapture",        "Steps per Capture (efficiency)"),
    ("EscortXSurvival",        "Escort × Survival (cooperation index)"),
]

for ax, (metric, title) in zip(axes.flat, plot_metrics):
    ma_roll = mapoca[metric].rolling(ROLL).mean()
    pp_roll = ppo[metric].rolling(ROLL).mean()

    ax.plot(ma_roll, label="MA-POCA", color=MAPOCA_COLOR, linewidth=1.5)
    ax.plot(pp_roll, label="PPO",     color=PPO_COLOR,    linewidth=1.5)

    ma_sem = mapoca[metric].rolling(ROLL).std() / np.sqrt(ROLL)
    pp_sem = ppo[metric].rolling(ROLL).std() / np.sqrt(ROLL)

    ax.set_title(title, fontsize=11)
    ax.set_xlabel("Episode")
    ax.legend()
    ax.grid(True, alpha=0.3)

plt.tight_layout()
plt.savefig("ctf_comparison/cooperation_comparison_full.png", dpi=150)
plt.close()
print("Saved: cooperation_comparison_full.png")

# ── 4. Correlation heatmaps — per algorithm ───────────────
corr_metrics = [
    "EscortRate", "RoleDiversityRate",
    "AvgCarrierSurvivalTime", "Captures", "EpisodeLength"
]

fig, axes = plt.subplots(1, 2, figsize=(14, 6))
fig.suptitle("Metric Correlations", fontsize=13, fontweight="bold")

for ax, (df, name, cmap) in zip(axes, [
    (mapoca, "MA-POCA", "Oranges"),
    (ppo,    "PPO",     "Purples")
]):
    corr = df[corr_metrics].corr()
    im = ax.imshow(corr, cmap=cmap, vmin=-1, vmax=1)
    ax.set_xticks(range(len(corr_metrics)))
    ax.set_yticks(range(len(corr_metrics)))
    ax.set_xticklabels(corr_metrics, rotation=45, ha="right", fontsize=9)
    ax.set_yticklabels(corr_metrics, fontsize=9)
    ax.set_title(name, fontsize=12)

    for i in range(len(corr_metrics)):
        for j in range(len(corr_metrics)):
            ax.text(j, i, f"{corr.iloc[i, j]:.2f}",
                    ha="center", va="center", fontsize=8,
                    color="black" if abs(corr.iloc[i, j]) < 0.7 else "white")

    plt.colorbar(im, ax=ax, fraction=0.046, pad=0.04)

plt.tight_layout()
plt.savefig("ctf_comparison/correlation_heatmaps.png", dpi=150)
plt.close()
print("Saved: correlation_heatmaps.png")