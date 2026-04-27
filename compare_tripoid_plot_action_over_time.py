import pandas as pd
import matplotlib.pyplot as plt
import os

# --- KONFIGURACJA ---
sac_action_file = "Tripoid_ActionOverTime_SAC_20260427_162016.csv" 
ppo_action_file = "Tripoid_ActionOverTime_PPO_20260427_161925.csv"
OUTPUT_DIR = "tripod_results"

if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)

files_to_process = {
    "PPO": ppo_action_file,
    "SAC": sac_action_file
}

for alg_name, file_path in files_to_process.items():
    if os.path.exists(file_path):
        df = pd.read_csv(file_path)
        
        plt.figure(figsize=(12, 5))
        
        # --- RYSUMEMY TYLKO JEDEN STAW (Action0_deg) ---
        plt.step(df["Step"], df["Action0_deg"], label=f"Staw 0 (Swing nogi 1)", color="#2980b9", linewidth=2)
            
        plt.title(f"Przebieg sygnału sterującego jednym stawem w epizodzie ({alg_name})", fontsize=14, fontweight="bold")
        plt.xlabel("Krok decyzyjny (Step)", fontsize=12)
        plt.ylabel("Kąt wychylenia (stopnie)", fontsize=12)
        
        # Sztywne osie, żeby wykresy PPO i SAC miały tę samą skalę do porównania
        plt.ylim(-65, 65) 
        
        plt.legend(loc='upper right')
        plt.grid(True, alpha=0.4, linestyle='--')
        
        plt.tight_layout()
        output_file = f"{OUTPUT_DIR}/ActionOverTime_Plot_{alg_name}_JednaNoga.png"
        plt.savefig(output_file, dpi=200)
        plt.close()
        print(f"[Info] Wygenerowano czytelny wykres: {output_file}")
    else:
        print(f"[Warning] Nie znaleziono pliku {file_path}")