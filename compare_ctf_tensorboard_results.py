import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import os

# --- KONFIGURACJA ---
DATA_DIR = "tensorboard-results/ctf"
OUTPUT_DIR = "ctf_comparison"

MAPOCA_COLOR = "#2ecc71"   # zielony — MA-POCA
PPO_COLOR    = "#e74c3c"   # czerwony — PPO
SMOOTH       = 20

os.makedirs(OUTPUT_DIR, exist_ok=True)

# --- Wczytywanie ---
def load(filename):
    path = os.path.join(DATA_DIR, filename)
    df = pd.read_csv(path, usecols=["Step", "Value"])
    return df.sort_values("Step").reset_index(drop=True)

ppo    = load("cumulative_reward_ppo.csv")
mapoca = load("group_cumulative_reward_ma_poca.csv")

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

# ================================================================
# WYKRES — Krzywa nagrody PPO vs MA-POCA
# ================================================================
fig, ax = plt.subplots(figsize=(11, 4))

# Surowe dane — lekkie tło
ax.plot(ppo["Step"],    ppo["Value"],
        color=PPO_COLOR,    alpha=0.12, linewidth=0.6, zorder=1)
ax.plot(mapoca["Step"], mapoca["Value"],
        color=MAPOCA_COLOR, alpha=0.12, linewidth=0.6, zorder=2)

# Wygładzone krzywe
ax.plot(ppo["Step"],    smooth(ppo["Value"]),
        color=PPO_COLOR,    linewidth=2.0, label="PPO (IPPO)", zorder=4)
ax.plot(mapoca["Step"], smooth(mapoca["Value"]),
        color=MAPOCA_COLOR, linewidth=2.0, label="MA-POCA",    zorder=3)

ax.set_title("Skumulowana nagroda (Cumulative Reward)")
ax.set_xlabel("Kroki treningowe")
ax.set_ylabel("Nagroda epizodyczna")
ax.xaxis.set_major_formatter(mticker.FuncFormatter(format_steps))
ax.legend(loc="upper left", frameon=True, framealpha=0.85)

combined = pd.concat([smooth(ppo["Value"]), smooth(mapoca["Value"])]).dropna()
margin = (combined.max() - combined.min()) * 0.10
ax.set_ylim(combined.min() - margin, combined.max() + margin)

plt.tight_layout()
out = f"{OUTPUT_DIR}/ctf_reward.png"
plt.savefig(out, dpi=200, bbox_inches="tight")
plt.close()
print(f"[✓] Zapisano: {out}")

print(f"\nGotowe. Pliki w folderze: {OUTPUT_DIR}")