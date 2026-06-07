import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import os

# --- KONFIGURACJA ---
DATA_DIR = "tensorboard-results/maze"
OUTPUT_DIR = "maze_results"

SAC_COLOR = "#2ecc71"
PPO_COLOR = "#e74c3c"
ENTROPY_COLOR = "#3498db"
SMOOTH = 1
SAC_END_STEP = 500_000

os.makedirs(OUTPUT_DIR, exist_ok=True)

# --- Wczytywanie danych ---
def load(filename):
    path = os.path.join(DATA_DIR, filename)
    df = pd.read_csv(path, usecols=["Step", "Value"])
    return df.sort_values("Step").reset_index(drop=True)

ppo_reward  = load("cumulative_reward_PPO.csv")
sac_reward  = load("cumulative_reward_SAC.csv")
ppo_length  = load("episode_length_PPO.csv")
sac_length  = load("episode_length_SAC.csv")
sac_entropy = load("entropy_sac.csv")

# --- Wygładzanie ---
def smooth(series, window=SMOOTH):
    return series.rolling(window, center=True, min_periods=1).mean()

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

def format_steps(x, _):
    if x >= 1_000_000:
        return f"{x/1_000_000:.0f}M" if x % 1_000_000 == 0 else f"{x/1_000_000:.1f}M"
    elif x >= 1_000:
        return f"{x/1_000:.0f}k"
    return str(int(x))

def add_sac_end_line(ax):
    ax.axvline(x=SAC_END_STEP, color=SAC_COLOR, linewidth=1.2,
               linestyle="--", alpha=0.7, zorder=5)
    ymin, ymax = ax.get_ylim()
    xmin, xmax = ax.get_xlim()
    ax.text(SAC_END_STEP + (xmax - xmin) * 0.01,
            ymax - (ymax - ymin) * 0.05,
            "koniec treningu SAC",
            color=SAC_COLOR, fontsize=9, alpha=0.85,
            verticalalignment="top", style="italic")

def plot_dual(ax, ppo_df, sac_df, ylabel, title):
    ax.plot(ppo_df["Step"], ppo_df["Value"],
            color=PPO_COLOR, alpha=0.12, linewidth=0.6, zorder=1)
    ax.plot(sac_df["Step"], sac_df["Value"],
            color=SAC_COLOR, alpha=0.12, linewidth=0.6, zorder=2)
    ax.plot(ppo_df["Step"], smooth(ppo_df["Value"]),
            color=PPO_COLOR, linewidth=2.0, label="PPO", zorder=4)
    ax.plot(sac_df["Step"], smooth(sac_df["Value"]),
            color=SAC_COLOR, linewidth=2.0, label="SAC", zorder=3)
    ax.set_title(title, pad=8)
    ax.set_xlabel("Kroki treningowe")
    ax.set_ylabel(ylabel)
    ax.xaxis.set_major_formatter(mticker.FuncFormatter(format_steps))
    ax.legend(loc="best", frameon=True, framealpha=0.85)
    combined = pd.concat([smooth(ppo_df["Value"]), smooth(sac_df["Value"])]).dropna()
    margin = (combined.max() - combined.min()) * 0.10
    ax.set_ylim(combined.min() - margin, combined.max() + margin)
    add_sac_end_line(ax)

# ================================================================
# WYKRES 1 — Skumulowana nagroda
# ================================================================
fig, ax = plt.subplots(figsize=(10, 4))
plot_dual(ax, ppo_reward, sac_reward,
          ylabel="Nagroda epizodyczna",
          title="Skumulowana nagroda (Cumulative Reward)")
plt.tight_layout()
plt.savefig(f"{OUTPUT_DIR}/maze_reward.png", dpi=200, bbox_inches="tight")
plt.close()
print("[✓] Zapisano: maze_reward.png")

# ================================================================
# WYKRES 2 — Długość epizodu
# ================================================================
fig, ax = plt.subplots(figsize=(10, 4))
plot_dual(ax, ppo_length, sac_length,
          ylabel="Liczba kroków",
          title="Długość epizodu (Episode Length)")
plt.tight_layout()
plt.savefig(f"{OUTPUT_DIR}/maze_episode_length.png", dpi=200, bbox_inches="tight")
plt.close()
print("[✓] Zapisano: maze_episode_length.png")

# ================================================================
# WYKRES 3 — Entropia polityki SAC (tylko SAC)
# ================================================================
fig, ax = plt.subplots(figsize=(10, 4))
ax.plot(sac_entropy["Step"], sac_entropy["Value"],
        color=ENTROPY_COLOR, alpha=0.15, linewidth=0.6, zorder=1)
ax.plot(sac_entropy["Step"], smooth(sac_entropy["Value"]),
        color=ENTROPY_COLOR, linewidth=2.0, label="SAC", zorder=2)
ax.set_title("Entropia polityki SAC", pad=8)
ax.set_xlabel("Kroki treningowe")
ax.set_ylabel("Entropia polityki")
ax.xaxis.set_major_formatter(mticker.FuncFormatter(format_steps))
ax.legend(loc="best", frameon=True, framealpha=0.85)
s = smooth(sac_entropy["Value"]).dropna()
margin = (s.max() - s.min()) * 0.10
ax.set_ylim(s.min() - margin, s.max() + margin)
plt.tight_layout()
plt.savefig(f"{OUTPUT_DIR}/maze_entropy_sac.png", dpi=200, bbox_inches="tight")
plt.close()
print("[✓] Zapisano: maze_entropy_sac.png")

print(f"\nGotowe. Pliki w folderze: {OUTPUT_DIR}")