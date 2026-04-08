import pandas as pd
import numpy as np

ppo = pd.read_csv("Tripoid_PPO_Reward.csv")  # columns: Step, Value
sac = pd.read_csv("Tripoid_SAC_Reward.csv")

# Rolling standard deviation - lower = more stable
window = 10  # adjust based on your summary_freq

ppo["rolling_std"] = ppo["Value"].rolling(window).std()
sac["rolling_std"] = sac["Value"].rolling(window).std()

print("PPO mean rolling std:", ppo["rolling_std"].mean())
print("SAC mean rolling std:", sac["rolling_std"].mean())

# Also compute after convergence only
# PPO converges ~2.5M, SAC ~400k
ppo_converged = ppo[ppo["Step"] > 2_500_000]["Value"].std()
sac_converged = sac[sac["Step"] > 400_000]["Value"].std()

print("PPO std after convergence:", ppo_converged)
print("SAC std after convergence:", sac_converged)