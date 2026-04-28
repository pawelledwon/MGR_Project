import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
import os

# --- KONFIGURACJA ---
ppo_heatmap_file = "Maze_Heatmap_PPO_20260428_145955.csv" # Podmień na swoje
sac_heatmap_file = "Maze_Heatmap_SAC_20260428_144709.csv"                 # Podmień, gdy zrobisz test SAC
OUTPUT_DIR = "maze_results"

if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)

heatmaps = {}
if os.path.exists(ppo_heatmap_file):
    heatmaps["PPO"] = pd.read_csv(ppo_heatmap_file, header=None)

if os.path.exists(sac_heatmap_file):
    heatmaps["SAC"] = pd.read_csv(sac_heatmap_file, header=None)

num_plots = len(heatmaps)

if num_plots > 0:
    fig, axes = plt.subplots(1, num_plots, figsize=(9 * num_plots, 8))
    
    if num_plots == 1:
        axes = [axes]

    for ax, (alg_name, df) in zip(axes, heatmaps.items()):
        # Ulepszona konfiguracja wizualna:
        # cmap="YlOrRd" -> od białego/żółtego do mocnej czerwieni
        # linewidths=1, linecolor='lightgray' -> rysuje siatkę labiryntu!
        # xticklabels=1, yticklabels=1 -> wymusza podpisanie każdej kolumny/wiersza
        sns.heatmap(df, ax=ax, cmap="YlOrRd", cbar_kws={'label': 'Częstotliwość odwiedzin'}, 
                    square=True, xticklabels=1, yticklabels=1, 
                    linewidths=1, linecolor='lightgray')
        
        ax.set_title(f"Mapa Eksploracji Labiryntu ({alg_name})", fontsize=15, fontweight="bold", pad=15)
        ax.set_xlabel("Pozycja X", fontsize=12)
        ax.set_ylabel("Pozycja Z", fontsize=12)
        
        # Pogrubiamy ramkę dookoła całego labiryntu
        for _, spine in ax.spines.items():
            spine.set_visible(True)
            spine.set_linewidth(2)

    plt.tight_layout()
    output_path = f"{OUTPUT_DIR}/maze_heatmap_comparison.png"
    plt.savefig(output_path, dpi=200)
    print(f"Zapisano heatmapę: {output_path}")
else:
    print("Nie znaleziono plików heatmap.")