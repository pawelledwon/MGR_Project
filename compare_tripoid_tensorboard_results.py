import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np
import os

# --- KONFIGURACJA ---
DATA_DIR = "tensorboard-results/tripoid"
OUTPUT_DIR = "tripod_results"

SAC_COLOR = "#2ecc71"
PPO_COLOR = "#e74c3c"
SMOOTH = 1
SAC_END_STEP = 1_000_000  # Krok zakończenia treningu SAC

os.makedirs(OUTPUT_DIR, exist_ok=True)

# --- Wczytywanie danych ---
def load(filename):
    path = os.path.join(DATA_DIR, filename)
    df = pd.read_csv(path, usecols=["Step", "Value"])
    return df.sort_values("Step").reset_index(drop=True)

ppo_reward = load("PPO_cumulative_reward.csv")
sac_reward = load("SAC_cumulative_reward.csv")
ppo_length = load("PPO_episode_length.csv")
sac_length = load("SAC_episode_length.csv")
ppo_ground = load("PPO_ground_contact.csv")
sac_ground = load("SAC_ground_contact.csv")
ppo_angle  = load("PPO_orientation_angle.csv")
sac_angle  = load("SAC_orientation_angle.csv")

# --- Wygładzanie ---
def smooth(series, window=SMOOTH):
    return series.rolling(window, center=True, min_periods=1).mean()

# --- Styl globalny ---
plt.rcParams.update({
    "font.family":      "DejaVu Sans",
    "font.size":        11,
    "axes.titlesize":   12,
    "axes.labelsize":   11,
    "legend.fontsize":  10,
    "axes.spines.top":  False,
    "axes.spines.right":False,
    "axes.grid":        True,
    "grid.alpha":       0.3,
    "grid.linestyle":   "--",
    "figure.dpi":       150,
})

def format_steps(x, _):
    if x >= 1_000_000:
        return f"{x/1_000_000:.0f}M" if x % 1_000_000 == 0 else f"{x/1_000_000:.1f}M"
    elif x >= 1_000:
        return f"{x/1_000:.0f}k"
    return str(int(x))

def add_sac_end_line(ax):
    """Dodaje pionową linię końca treningu SAC z adnotacją."""
    ax.axvline(x=SAC_END_STEP, color=SAC_COLOR, linewidth=1.2,
               linestyle="--", alpha=0.7, zorder=5)
    ymin, ymax = ax.get_ylim()
    xmin, xmax = ax.get_xlim()
    ax.text(SAC_END_STEP + (xmax - xmin) * 0.01,
            ymax - (ymax - ymin) * 0.05,
            "koniec treningu SAC",
            color=SAC_COLOR, fontsize=9, alpha=0.85,
            verticalalignment="top", style="italic")

def plot_panel(ax, ppo_df, sac_df, ylabel, title,
               ppo_label="PPO", sac_label="SAC", add_line=True):
    # Surowe dane — lekkie tło
    ax.plot(ppo_df["Step"], ppo_df["Value"],
            color=PPO_COLOR, alpha=0.12, linewidth=0.6, zorder=1)
    ax.plot(sac_df["Step"], sac_df["Value"],
            color=SAC_COLOR, alpha=0.12, linewidth=0.6, zorder=2)

    # Wygładzone krzywe
    ax.plot(ppo_df["Step"], smooth(ppo_df["Value"]),
            color=PPO_COLOR, linewidth=2.0, label=ppo_label, zorder=4)
    ax.plot(sac_df["Step"], smooth(sac_df["Value"]),
            color=SAC_COLOR, linewidth=2.0, label=sac_label, zorder=3)

    ax.set_title(title, pad=8)
    ax.set_xlabel("Kroki treningowe")
    ax.set_ylabel(ylabel)
    ax.xaxis.set_major_formatter(mticker.FuncFormatter(format_steps))
    ax.legend(loc="best", frameon=True, framealpha=0.85)

    combined = pd.concat([smooth(ppo_df["Value"]), smooth(sac_df["Value"])]).dropna()
    margin = (combined.max() - combined.min()) * 0.10
    ax.set_ylim(combined.min() - margin, combined.max() + margin)

    # Linia po set_ylim — żeby tekst był dobrze spozycjonowany
    if add_line:
        add_sac_end_line(ax)

# ================================================================
# WYKRES ZBIORCZY — 4 panele 2x2
# ================================================================
fig, axes = plt.subplots(2, 2, figsize=(14, 9))
fig.suptitle("Przebieg procesu uczenia – Robot trójnożny (PPO vs SAC)",
             fontsize=13, fontweight="bold", y=1.01)

plot_panel(axes[0, 0], ppo_reward, sac_reward,
           ylabel="Nagroda epizodyczna",
           title="Skumulowana nagroda (Cumulative Reward)")
plot_panel(axes[0, 1], ppo_length, sac_length,
           ylabel="Liczba kroków",
           title="Długość epizodu (Episode Length)")
plot_panel(axes[1, 0], ppo_ground, sac_ground,
           ylabel="Odsetek kroków [%]",
           title="Kontakt korpusu z podłożem (Ground Contact %)")
plot_panel(axes[1, 1], ppo_angle, sac_angle,
           ylabel="Kąt [°]",
           title="Kąt ciała względem celu")

plt.tight_layout()
plt.savefig(f"{OUTPUT_DIR}/training_curves_tripoid.png",
            dpi=200, bbox_inches="tight")
plt.close()
print(f"[✓] Zapisano: {OUTPUT_DIR}/training_curves_tripoid.png")

# ================================================================
# WYKRESY OSOBNE
# ================================================================
panels = [
    (ppo_reward, sac_reward, "Nagroda epizodyczna",
     "Skumulowana nagroda (Cumulative Reward)", "reward"),
    (ppo_length, sac_length, "Liczba kroków",
     "Długość epizodu (Episode Length)", "episode_length"),
    (ppo_ground, sac_ground, "Odsetek kroków [%]",
     "Kontakt korpusu z podłożem (Ground Contact %)", "ground_contact"),
    (ppo_angle,  sac_angle,  "Kąt [°]",
     "Kąt ciała względem celu", "orientation_angle"),
]

for ppo_df, sac_df, ylabel, title, name in panels:
    fig, ax = plt.subplots(figsize=(10, 4))
    plot_panel(ax, ppo_df, sac_df, ylabel=ylabel, title=title)
    plt.tight_layout()
    out = f"{OUTPUT_DIR}/training_{name}.png"
    plt.savefig(out, dpi=200, bbox_inches="tight")
    plt.close()
    print(f"[✓] Zapisano: {out}")

print("\nGotowe. Pliki w folderze:", OUTPUT_DIR)