import pandas as pd
import matplotlib.pyplot as plt
import os

# --- KONFIGURACJA ---
ppo_file = "Maze_Metrics_PPO_20260428_145955.csv"  # Podmień na swoje nazwy
sac_file = "Maze_Metrics_SAC_20260428_144709.csv"  # Podmień na swoje nazwy
OUTPUT_DIR = "maze_results"
ROLLING_WINDOW = 50 # Rozmiar okna średniej kroczącej

SAC_COLOR = "#2ecc71"
PPO_COLOR = "#e74c3c"

if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)

files_to_process = {"PPO": (ppo_file, PPO_COLOR), "SAC": (sac_file, SAC_COLOR)}
data = {}

for alg, (file_path, color) in files_to_process.items():
    if os.path.exists(file_path):
        data[alg] = pd.read_csv(file_path)

if data:
    fig, axes = plt.subplots(2, 1, figsize=(12, 10))
    fig.suptitle("Analiza Nawigacji w Labiryncie: PPO vs SAC", fontsize=16, fontweight="bold")

    # --- Wykres 1: Success Rate ---
    ax1 = axes[0]
    min_succ, max_succ = 100.0, 0.0

    for alg, df in data.items():
        color = files_to_process[alg][1]
        
        # Wyliczanie średniej kroczącej
        rolling_success = df["Success"].rolling(ROLLING_WINDOW).mean() * 100
        ax1.plot(df["Episode"], rolling_success, label=alg, color=color, linewidth=2.5)

        # Pobieranie min/max by wykres idealnie się przeskalował (zignorowanie nan z początkowych kroków)
        curr_min = rolling_success.dropna().min()
        curr_max = rolling_success.dropna().max()
        if curr_min < min_succ: min_succ = curr_min
        if curr_max > max_succ: max_succ = curr_max

    ax1.set_title(f"Wskaźnik Sukcesu (Rolling mean {ROLLING_WINDOW} eps) - Przybliżenie skali", fontsize=13)
    ax1.set_xlabel("Epizod")
    ax1.set_ylabel("% Zakończonych sukcesem")
    
    # Dynamiczny Y-limit (z lekkim marginesem) żeby maksymalnie rozciągnąć wykres
    buffer_succ = (max_succ - min_succ) * 0.15 if max_succ != min_succ else 5
    ax1.set_ylim(max(0, min_succ - buffer_succ), min(105, max_succ + buffer_succ))
    
    ax1.legend(loc="lower right")
    ax1.grid(True, alpha=0.4, linestyle='--')

    # --- Wykres 2: Długość Epizodu (Kroki) ---
    ax2 = axes[1]
    min_steps, max_steps = float('inf'), 0.0

    for alg, df in data.items():
        color = files_to_process[alg][1]
        
        # Wyliczanie TYLKO średniej kroczącej (usunięto tło)
        rolling_steps = df["Steps"].rolling(ROLLING_WINDOW).mean()
        ax2.plot(df["Episode"], rolling_steps, label=alg, color=color, linewidth=2.5)

        # Pobieranie min/max by dociąć wykres bez ekstremalnych, brudnych logów z 1-go kroku
        curr_min = rolling_steps.dropna().min()
        curr_max = rolling_steps.dropna().max()
        if curr_min < min_steps: min_steps = curr_min
        if curr_max > max_steps: max_steps = curr_max

    ax2.set_title(f"Średnia Długość Epizodu (Rolling mean {ROLLING_WINDOW} eps) - Przybliżenie skali", fontsize=13)
    ax2.set_xlabel("Epizod")
    ax2.set_ylabel("Liczba kroków")
    
    # Dynamiczny Y-limit dla kroków (ucięte u dołu, żeby linie nie leżały na podłodze)
    buffer_steps = (max_steps - min_steps) * 0.15 if max_steps != min_steps else 10
    ax2.set_ylim(max(0, min_steps - buffer_steps), max_steps + buffer_steps)
    
    ax2.legend(loc="upper right")
    ax2.grid(True, alpha=0.4, linestyle='--')

    plt.tight_layout(rect=[0, 0.03, 1, 0.95])
    output_path = f"{OUTPUT_DIR}/maze_metrics_comparison.png"
    plt.savefig(output_path, dpi=200)
    print(f"Zapisano wykres metryk: {output_path}")
else:
    print("Nie znaleziono plików z danymi.")