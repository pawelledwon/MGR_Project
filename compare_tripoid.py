import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from scipy import stats
import os

# --- KONFIGURACJA ---
SAC_FILE = "Tripoid_Metrics_SAC_20260427_153808.csv"
PPO_FILE = "Tripoid_Metrics_PPO_20260427_154602.csv"
OUTPUT_DIR = "tripod_results"

SAC_COLOR = "#2ecc71"
PPO_COLOR = "#e74c3c"
ROLL = 20

os.makedirs(OUTPUT_DIR, exist_ok=True)

sac = pd.read_csv(SAC_FILE)
ppo = pd.read_csv(PPO_FILE)

# --- STATYSTYKI ---
metrics_list = ["MeanActionJitter", "MeanMechJitter"]
rows = []
for m in metrics_list:
    s_vals = sac[m].dropna()
    p_vals = ppo[m].dropna()
    t_stat, p_val = stats.ttest_ind(s_vals, p_vals)
    rows.append({
        "Metric": m,
        "SAC Mean": f"{s_vals.mean():.4f}",
        "PPO Mean": f"{p_vals.mean():.4f}",
        "p-value": f"{p_val:.4e}",
        "Significant": "YES" if p_val < 0.05 else "NO"
    })
summary_df = pd.DataFrame(rows)
summary_df.to_csv(f"{OUTPUT_DIR}/smoothness_summary.csv", index=False)
print("\n=== SUMMARY STATISTICS ===")
print(summary_df.to_string(index=False))

# --- STYL GLOBALNY ---
plt.rcParams.update({
    "font.family":       "DejaVu Sans",
    "font.size":         11,
    "axes.titlesize":    12,
    "axes.labelsize":    11,
    "legend.fontsize":   10,
    "axes.spines.top":   False,
    "axes.spines.right": False,
    "axes.grid":         True,
    "grid.alpha":        0.3,
    "grid.linestyle":    "--",
    "figure.dpi":        150,
})

# --- DEFINICJE WYKRESÓW ---
plots = [
    (
        "MeanActionJitter",
        "Mean Action Jitter (MAJ)",
        "Zmiana akcji między krokami",
        "action_jitter"
    ),
    (
        "MeanMechJitter",
        "Mean Mechanical Jitter (MMJ)",
        "Zmiana prędkości kątowej stawów [rad/s]",
        "mech_jitter"
    ),
    (
        "MeanLinearSpeed",
        "Średnia prędkość liniowa",
        "Prędkość liniowa korpusu",
        "linear_speed"
    ),
]

for metric, title, ylabel, filename in plots:
    fig, ax = plt.subplots(figsize=(11, 4))

    sac_roll = sac[metric].rolling(ROLL).mean()
    ppo_roll = ppo[metric].rolling(ROLL).mean()

    # Surowe dane — lekkie tło
    ax.plot(sac["Episode"], sac[metric],
            color=SAC_COLOR, alpha=0.08, linewidth=0.5, zorder=1)
    ax.plot(ppo["Episode"], ppo[metric],
            color=PPO_COLOR, alpha=0.08, linewidth=0.5, zorder=2)

    # Krzywe wygładzone
    ax.plot(sac["Episode"], sac_roll,
            label="SAC", color=SAC_COLOR, linewidth=2.0, zorder=4)
    ax.plot(ppo["Episode"], ppo_roll,
            label="PPO", color=PPO_COLOR, linewidth=2.0, zorder=3)

    ax.set_title(title, pad=8)
    ax.set_xlabel("Epizod ewaluacji")
    ax.set_ylabel(ylabel)
    ax.set_xlim(sac["Episode"].min(), sac["Episode"].max())

    combined_roll = pd.concat([sac_roll, ppo_roll]).dropna()
    margin = (combined_roll.max() - combined_roll.min()) * 0.15
    ax.set_ylim(combined_roll.min() - margin, combined_roll.max() + margin)

    ax.legend(loc="upper right", frameon=True, framealpha=0.85)

    plt.tight_layout()
    out = f"{OUTPUT_DIR}/smoothness_{filename}.png"
    plt.savefig(out, dpi=200, bbox_inches="tight")
    plt.close()
    print(f"[✓] Zapisano: {out}")

print("\nGotowe. Pliki w folderze:", OUTPUT_DIR)