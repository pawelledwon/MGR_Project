import pandas as pd
import matplotlib.pyplot as plt
import os

# --- KONFIGURACJA ---
PPO_FILE = "Maze_Metrics_PPO_20260428_145955.csv"
SAC_FILE = "Maze_Metrics_SAC_20260428_144709.csv"
OUTPUT_DIR = "maze_results"
ROLL = 50

SAC_COLOR = "#2ecc71"
PPO_COLOR = "#e74c3c"

os.makedirs(OUTPUT_DIR, exist_ok=True)

ppo = pd.read_csv(PPO_FILE)
sac = pd.read_csv(SAC_FILE)

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

def save_plot(ax, fig, filename):
    plt.tight_layout()
    out = f"{OUTPUT_DIR}/{filename}"
    fig.savefig(out, dpi=200, bbox_inches="tight")
    plt.close(fig)
    print(f"[✓] Zapisano: {out}")

def set_ylim(ax, *series_list):
    combined = pd.concat([s.dropna() for s in series_list])
    margin = (combined.max() - combined.min()) * 0.15
    ax.set_ylim(max(0, combined.min() - margin), combined.max() + margin)

# ================================================================
# WYKRES 1 — Wskaźnik sukcesu
# ================================================================
fig, ax = plt.subplots(figsize=(10, 4))

ppo_succ = ppo["Success"].rolling(ROLL).mean() * 100
sac_succ = sac["Success"].rolling(ROLL).mean() * 100

ax.plot(ppo["Episode"], ppo_succ, color=PPO_COLOR, linewidth=2.0, label="PPO")
ax.plot(sac["Episode"], sac_succ, color=SAC_COLOR, linewidth=2.0, label="SAC")

ax.set_title("Wskaźnik sukcesu (Success Rate)")
ax.set_xlabel("Epizod ewaluacji")
ax.set_ylabel("Odsetek sukcesów [%]")
ax.legend(loc="lower right", frameon=True, framealpha=0.85)
set_ylim(ax, ppo_succ, sac_succ)

save_plot(ax, fig, "maze_success_rate.png")

# ================================================================
# WYKRES 2 — Długość epizodu
# ================================================================
fig, ax = plt.subplots(figsize=(10, 4))

ppo_steps = ppo["Steps"].rolling(ROLL).mean()
sac_steps = sac["Steps"].rolling(ROLL).mean()

ax.plot(ppo["Episode"], ppo_steps, color=PPO_COLOR, linewidth=2.0, label="PPO")
ax.plot(sac["Episode"], sac_steps, color=SAC_COLOR, linewidth=2.0, label="SAC")

ax.set_title("Długość epizodu (Episode Length)")
ax.set_xlabel("Epizod ewaluacji")
ax.set_ylabel("Liczba kroków")
ax.legend(loc="upper right", frameon=True, framealpha=0.85)
set_ylim(ax, ppo_steps, sac_steps)

save_plot(ax, fig, "maze_episode_length.png")

print(f"\nGotowe. Pliki w folderze: {OUTPUT_DIR}")