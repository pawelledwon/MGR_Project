import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
from scipy import stats
import os

# --- KONFIGURACJA ---
# Wpisz nazwy swoich wygenerowanych plików
sac_file = "Tripoid_Metrics_SAC_20260427_153808.csv" 
ppo_file = "Tripoid_Metrics_PPO_20260427_154602.csv"
OUTPUT_DIR = "tripod_results"

SAC_COLOR = "#2ecc71" # Zielony (SAC często kojarzony z "soft")
PPO_COLOR = "#e74c3c" # Czerwony/Pomarańczowy
ROLL = 20 # Okno średniej kroczącej (wygładzanie wykresów)

if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)

# Wczytywanie danych
sac = pd.read_csv(sac_file)
ppo = pd.read_csv(ppo_file)

# --- 1. STATYSTYKI (Tabela do magisterki) ---
metrics_list = ["MeanActionJitter", "MeanMechJitter"]
rows = []

for m in metrics_list:
    s_vals = sac[m].dropna()
    p_vals = ppo[m].dropna()

    t_stat, p_val = stats.ttest_ind(s_vals, p_vals)
    cohens_d = (s_vals.mean() - p_vals.mean()) / np.sqrt((s_vals.std()**2 + p_vals.std()**2) / 2)

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

# --- 2. WYKRESY (Styl identyczny z Twoim przykładem MA-POCA) ---
fig, axes = plt.subplots(3, 1, figsize=(12, 14)) # Zwiększono wysokość figsize
fig.suptitle("Analiza Metryk Chodu: SAC vs PPO\n(Rolling mean 20 episodes)", 
             fontsize=14, fontweight="bold")

plot_metrics = [
    ("MeanActionJitter", "Szum Akcji (Decyzje Sieci)"),
    ("MeanMechJitter", "Szum Mechaniczny (Szarpanie Stawów)"),
    ("MeanLinearSpeed", "Średnia Prędkość Liniowa (Szybkość Chodu)") # Nowy wykres
]

for ax, (metric, title) in zip(axes, plot_metrics):
    # Obliczanie średniej kroczącej
    sac_roll = sac[metric].rolling(ROLL).mean()
    ppo_roll = ppo[metric].rolling(ROLL).mean()

    # Rysowanie linii trendu
    ax.plot(sac["Episode"], sac_roll, label="SAC", color=SAC_COLOR, linewidth=2, zorder=3)
    ax.plot(ppo["Episode"], ppo_roll, label="PPO", color=PPO_COLOR, linewidth=2, zorder=4)

    # Lekkie tło dla surowych danych
    ax.plot(sac["Episode"], sac[metric], color=SAC_COLOR, alpha=0.08, linewidth=0.5, zorder=1)
    ax.plot(ppo["Episode"], ppo[metric], color=PPO_COLOR, alpha=0.08, linewidth=0.5, zorder=2)

    # --- DOPASOWANIE OSI ---
    ax.set_xlim(sac["Episode"].min(), sac["Episode"].max())

    # Dynamiczny zakres na podstawie średniej kroczącej + 15% zapasu
    combined_roll = pd.concat([sac_roll, ppo_roll]).dropna()
    y_min = combined_roll.min() * 0.85
    y_max = combined_roll.max() * 1.15
    ax.set_ylim(y_min, y_max)
    # -----------------------

    ax.set_title(title, fontsize=12)
    ax.set_xlabel("Episode")
    ax.set_ylabel("Value")
    ax.legend(loc="upper right", frameon=True, shadow=True)
    ax.grid(True, alpha=0.3, linestyle='--')

plt.tight_layout(rect=[0, 0.03, 1, 0.95])
plt.savefig(f"{OUTPUT_DIR}/metrics_comparison_final.png", dpi=200)
print(f"\n[Info] Zapisano wykresy w folderze: {OUTPUT_DIR}")
