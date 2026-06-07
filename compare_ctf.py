import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from scipy import stats
import os

# --- KONFIGURACJA ---
MAPOCA_FILE = "Metrics_MAPOCA_2000.csv"
PPO_FILE    = "Metrics_PPO_2000.csv"
OUTPUT_DIR  = "ctf_comparison"
ROLL        = 20

MAPOCA_COLOR = "#2ecc71"
PPO_COLOR    = "#e74c3c"

os.makedirs(OUTPUT_DIR, exist_ok=True)

mapoca = pd.read_csv(MAPOCA_FILE)
ppo    = pd.read_csv(PPO_FILE)

# --- Metryki pochodne ---
for df in (mapoca, ppo):
    df["EscortXSurvival"] = df["EscortRate"] * df["AvgCarrierSurvivalTime"]

# --- Statystyki ---
metrics_list = [
    "Captures", "EscortRate", "RoleDiversityRate",
    "AvgCarrierSurvivalTime", "EpisodeLength", "EscortXSurvival"
]

rows = []
for m in metrics_list:
    ma_vals = mapoca[m].dropna()
    pp_vals = ppo[m].dropna()
    t_stat, p_val = stats.ttest_ind(ma_vals, pp_vals)
    rows.append({
        "Metric":         m,
        "MA-POCA Mean":   f"{ma_vals.mean():.4f}",
        "MA-POCA Std":    f"{ma_vals.std():.4f}",
        "PPO Mean":       f"{pp_vals.mean():.4f}",
        "PPO Std":        f"{pp_vals.std():.4f}",
        "p-value":        f"{p_val:.4f}",
        "Significant":    "YES" if p_val < 0.05 else "NO"
    })

summary = pd.DataFrame(rows)
summary.to_csv(f"{OUTPUT_DIR}/ctf_metrics_summary.csv", index=False)
print("\n=== SUMMARY STATISTICS ===")
print(summary.to_string(index=False))

# --- Styl globalny ---
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

def save_metric_plot(metric, title, ylabel, filename):
    fig, ax = plt.subplots(figsize=(11, 4))

    ma_roll = mapoca[metric].rolling(ROLL).mean()
    pp_roll = ppo[metric].rolling(ROLL).mean()

    # Surowe dane — lekkie tło
    ax.plot(mapoca.index, mapoca[metric],
            color=MAPOCA_COLOR, alpha=0.10, linewidth=0.5, zorder=1)
    ax.plot(ppo.index, ppo[metric],
            color=PPO_COLOR,    alpha=0.10, linewidth=0.5, zorder=2)

    # Wygładzone krzywe
    ax.plot(mapoca.index, ma_roll,
            color=MAPOCA_COLOR, linewidth=2.0, label="MA-POCA", zorder=4)
    ax.plot(ppo.index, pp_roll,
            color=PPO_COLOR,    linewidth=2.0, label="PPO",     zorder=3)

    ax.set_title(title, pad=8)
    ax.set_xlabel("Epizod ewaluacji")
    ax.set_ylabel(ylabel)
    ax.legend(loc="best", frameon=True, framealpha=0.85)

    combined = pd.concat([ma_roll, pp_roll]).dropna()
    margin = (combined.max() - combined.min()) * 0.12
    ax.set_ylim(combined.min() - margin, combined.max() + margin)

    plt.tight_layout()
    out = f"{OUTPUT_DIR}/{filename}"
    plt.savefig(out, dpi=200, bbox_inches="tight")
    plt.close()
    print(f"[✓] Zapisano: {out}")

# --- Osobne wykresy ---
save_metric_plot("Captures",               "Liczba przechwyceni flagi",
                 "Przechwycenia / epizod",  "ctf_captures.png")

save_metric_plot("EscortRate",             "Escort Rate (ER)",
                 "Odsetek klatek [-]",      "ctf_escort_rate.png")

save_metric_plot("RoleDiversityRate",      "Role Diversity Rate (RDR)",
                 "Odsetek klatek [-]",      "ctf_role_diversity.png")

save_metric_plot("AvgCarrierSurvivalTime", "Avg Carrier Survival Time (ACS)",
                 "Czas [s]",               "ctf_carrier_survival.png")

save_metric_plot("EpisodeLength",          "Długość epizodu",
                 "Liczba kroków",           "ctf_episode_length.png")

save_metric_plot("EscortXSurvival",        "Escort × Survival (EXS)",
                 "EscortRate × ACS",        "ctf_escort_x_survival.png")

# ================================================================
# MACIERZ KORELACJI — jeden plik z dwoma panelami
# ================================================================
corr_metrics = [
    "EscortRate", "RoleDiversityRate",
    "AvgCarrierSurvivalTime", "Captures", "EpisodeLength"
]

labels = ["ER", "RDR", "ACS", "Captures", "EpLen"]

fig, axes = plt.subplots(1, 2, figsize=(13, 5))
fig.suptitle("Macierz korelacji Pearsona — metryki CTF",
             fontsize=13, fontweight="bold")

for ax, (df, name, base_color) in zip(axes, [
    (mapoca, "MA-POCA", MAPOCA_COLOR),
    (ppo,    "PPO",     PPO_COLOR)
]):
    corr = df[corr_metrics].corr()

    # Tworzymy mapę kolorów od białego do koloru algorytmu
    from matplotlib.colors import LinearSegmentedColormap
    cmap = LinearSegmentedColormap.from_list(
        name, ["white", base_color], N=256
    )

    im = ax.imshow(corr, cmap=cmap, vmin=-1, vmax=1, aspect="auto")
    ax.set_xticks(range(len(labels)))
    ax.set_yticks(range(len(labels)))
    ax.set_xticklabels(labels, rotation=45, ha="right", fontsize=10)
    ax.set_yticklabels(labels, fontsize=10)
    ax.set_title(name, fontsize=12, pad=10)

    for i in range(len(labels)):
        for j in range(len(labels)):
            val = corr.iloc[i, j]
            ax.text(j, i, f"{val:.2f}",
                    ha="center", va="center", fontsize=9,
                    color="black" if abs(val) < 0.65 else "white")

    plt.colorbar(im, ax=ax, fraction=0.046, pad=0.04)

plt.tight_layout()
out = f"{OUTPUT_DIR}/ctf_correlation_heatmap.png"
plt.savefig(out, dpi=200, bbox_inches="tight")
plt.close()
print(f"[✓] Zapisano: {out}")

print(f"\nGotowe. Pliki w folderze: {OUTPUT_DIR}")