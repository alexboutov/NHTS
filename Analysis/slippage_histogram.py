import re
import glob
from collections import Counter
import matplotlib.pyplot as plt
import numpy as np

def parse_slippage_from_logs(log_folder):
    """Extract slippage values from ActiveNikiTrader log files."""
    slippages = []
    
    for filepath in glob.glob(f"{log_folder}/ActiveNikiTrader_*.txt"):
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            for line in f:
                if 'ENTRY FILLED' in line and 'Slippage:' in line:
                    match = re.search(r'Slippage:\s*([+-]?\d+)t', line)
                    if match:
                        slippages.append(int(match.group(1)))
    
    return slippages

def create_histogram(slippages, output_file='slippage_histogram.png'):
    """Create and save slippage histogram."""
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(16, 6))
    
    # Histogram 1: Full distribution
    ax1.hist(slippages, bins=range(min(slippages)-1, max(slippages)+2), 
             edgecolor='black', alpha=0.7, color='steelblue')
    ax1.set_xlabel('Slippage (ticks)')
    ax1.set_ylabel('Frequency')
    ax1.set_title(f'Entry Slippage Distribution\n({len(slippages)} trades)')
    ax1.grid(axis='y', alpha=0.3)
    ax1.axvline(x=0, color='red', linestyle='--', linewidth=2, label='Zero Slippage')
    ax1.legend()
    
    # Histogram 2: Zoomed view (-10t to +10t)
    zoomed = [s for s in slippages if -10 <= s <= 10]
    ax2.hist(zoomed, bins=range(-11, 12), edgecolor='black', alpha=0.7, color='coral')
    ax2.set_xlabel('Slippage (ticks)')
    ax2.set_ylabel('Frequency')
    ax2.set_title(f'Slippage Distribution (Zoomed: -10t to +10t)\n({len(zoomed)} trades)')
    ax2.grid(axis='y', alpha=0.3)
    ax2.axvline(x=0, color='red', linestyle='--', linewidth=2)
    
    plt.tight_layout()
    plt.savefig(output_file, dpi=150, bbox_inches='tight')
    print(f"Histogram saved to: {output_file}")
    
    # Print statistics
    print(f"\n{'='*60}")
    print(f"SLIPPAGE STATISTICS")
    print(f"{'='*60}")
    print(f"Total trades analyzed: {len(slippages)}")
    print(f"Mean slippage:         {np.mean(slippages):+.1f}t (${np.mean(slippages)*5:+.2f})")
    print(f"Median slippage:       {np.median(slippages):+.0f}t")
    print(f"Std deviation:         {np.std(slippages):.1f}t")
    print(f"Min slippage:          {min(slippages):+.0f}t")
    print(f"Max slippage:          {max(slippages):+.0f}t")
    print(f"\nNegative slippage:     {sum(1 for s in slippages if s < 0)} trades ({sum(1 for s in slippages if s < 0)/len(slippages)*100:.1f}%)")
    print(f"Zero slippage:         {sum(1 for s in slippages if s == 0)} trades ({sum(1 for s in slippages if s == 0)/len(slippages)*100:.1f}%)")
    print(f"Positive slippage:     {sum(1 for s in slippages if s > 0)} trades ({sum(1 for s in slippages if s > 0)/len(slippages)*100:.1f}%)")
    print(f"{'='*60}")

# Usage
if __name__ == '__main__':
    # Option 1: Parse from log files
    # slippages = parse_slippage_from_logs(r'C:\Users\alexb\OneDrive\Documents\NinjaTrader 8\log')
    
    # Option 2: Use the sample data from your paste (manually extracted)
    slippages = [
        0, 0, 0, 0, 1, -1, -2, -2, -1, 0, -2, -4, 0, -3, 3, 0, 0, -2, -4, -1,
        0, 1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14,
        -7, -12, -6, 1, -4, 12, 6, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14, -7,
        -12, -6, 1, 12, 6, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14, -7, -12, -6,
        1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14, -7,
        -12, -6, 1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2,
        14, -7, -12, -6, 1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0,
        0, 0, 0, 0, 1, -1, -2, -2, -1, 0, -2, -4, 0, -3, 3, 0, 0, -2, -4, -1,
        0, 1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14,
        -7, -12, -6, 1, 12, 6, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 14, -7, -12,
        -6, 1, -18, 3, 9, -7, -24, 12, 5, -6, 11, 7, 2, 2, -1, 0, 8, 1, 2, 26
    ]
    
    create_histogram(slippages)
