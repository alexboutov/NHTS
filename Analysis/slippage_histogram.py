r"""
Slippage Histogram Generator for ActiveNikiTrader logs.
Extracts slippage data using Python file parsing and generates visual charts.

Usage:
    python slippage_histogram.py
    python slippage_histogram.py -LogDir "C:\Path\To\Logs"
    python slippage_histogram.py --log-dir "C:\Path\To\Logs"
"""

import re
import subprocess
import sys
import argparse
import glob
from collections import Counter
import matplotlib.pyplot as plt
import numpy as np
from datetime import datetime


def parse_slippage_from_logs(log_folder):
    """
    Extract slippage values from ActiveNikiTrader log files.
    Uses Python file parsing (more reliable than PowerShell).
    """
    slippages = []
    slippage_details = []
    
    print(f"Scanning logs in: {log_folder}")
    
    log_files = glob.glob(f"{log_folder}\\ActiveNikiTrader_*.txt")
    print(f"Found {len(log_files)} ActiveNikiTrader log files")
    
    for filepath in log_files:
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                for line in f:
                    if 'ENTRY FILLED' in line and 'Slippage:' in line:
                        match = re.search(r'Slippage:\s*([+-]?\d+)t\s*\(\s*\$([+-]?\d+\.?\d*)\)', line)
                        if match:
                            ticks = int(match.group(1))
                            dollars = float(match.group(2))
                            slippages.append(ticks)
                            slippage_details.append({
                                'file': filepath,
                                'ticks': ticks,
                                'dollars': dollars
                            })
        except Exception as e:
            print(f"  Warning: Could not read {filepath}: {e}")
    
    print(f"Found {len(slippages)} entry slippage records")
    return slippages


def create_histogram(slippages, output_file='slippage_histogram.png', log_dir=''):
    """Create and save slippage histogram with statistics."""
    
    if not slippages:
        print("ERROR: No slippage data found!")
        return
    
    # Create figure with subplots
    fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(18, 12))
    fig.suptitle(f'ActiveNikiTrader Entry Slippage Analysis\nLog Directory: {log_dir}', 
                 fontsize=14, fontweight='bold')
    
    # === Plot 1: Full Distribution Histogram ===
    ax1.hist(slippages, bins=range(min(slippages)-1, max(slippages)+2), 
             edgecolor='black', alpha=0.7, color='steelblue')
    ax1.set_xlabel('Slippage (ticks)', fontsize=11)
    ax1.set_ylabel('Frequency', fontsize=11)
    ax1.set_title(f'Full Distribution ({len(slippages)} trades)', fontsize=12, fontweight='bold')
    ax1.grid(axis='y', alpha=0.3)
    ax1.axvline(x=0, color='red', linestyle='--', linewidth=2, label='Zero Slippage')
    ax1.axvline(x=np.mean(slippages), color='green', linestyle='-', linewidth=2, 
                label=f'Mean: {np.mean(slippages):+.1f}t')
    ax1.legend()
    
    # === Plot 2: Zoomed View (-15t to +15t) ===
    zoomed = [s for s in slippages if -15 <= s <= 15]
    ax2.hist(zoomed, bins=range(-16, 17), edgecolor='black', alpha=0.7, color='coral')
    ax2.set_xlabel('Slippage (ticks)', fontsize=11)
    ax2.set_ylabel('Frequency', fontsize=11)
    ax2.set_title(f'Zoomed View: -15t to +15t ({len(zoomed)} trades)', fontsize=12, fontweight='bold')
    ax2.grid(axis='y', alpha=0.3)
    ax2.axvline(x=0, color='red', linestyle='--', linewidth=2)
    ax2.axvline(x=np.mean(zoomed), color='green', linestyle='-', linewidth=2,
                label=f'Mean: {np.mean(zoomed):+.1f}t')
    ax2.legend()
    
    # === Plot 3: Slippage by Category (Pie Chart) ===
    negative = sum(1 for s in slippages if s < 0)
    zero = sum(1 for s in slippages if s == 0)
    positive = sum(1 for s in slippages if s > 0)
    
    categories = ['Negative (Cost)', 'Zero', 'Positive (Gain)']
    counts = [negative, zero, positive]
    colors = ['#ff6b6b', '#95a5a6', '#2ecc71']
    
    wedges, texts, autotexts = ax3.pie(counts, labels=categories, colors=colors, 
                                        autopct='%1.1f%%', startangle=90,
                                        explode=(0.05, 0, 0.05))
    ax3.set_title('Slippage Category Distribution', fontsize=12, fontweight='bold')
    for autotext in autotexts:
        autotext.set_fontsize(10)
        autotext.set_fontweight('bold')
    
    # === Plot 4: Cumulative Slippage Cost ===
    cumulative = np.cumsum(slippages)
    ax4.plot(range(1, len(cumulative)+1), cumulative, color='purple', linewidth=1.5)
    ax4.set_xlabel('Trade Number', fontsize=11)
    ax4.set_ylabel('Cumulative Slippage (ticks)', fontsize=11)
    ax4.set_title(f'Cumulative Slippage Over Time\nFinal: {cumulative[-1]:+.0f}t (${cumulative[-1]*5:+.2f})', 
                  fontsize=12, fontweight='bold')
    ax4.grid(alpha=0.3)
    ax4.axhline(y=0, color='red', linestyle='--', linewidth=1)
    ax4.axhline(y=np.mean(cumulative), color='orange', linestyle=':', linewidth=1,
                label=f'Avg: {np.mean(cumulative):+.0f}t')
    ax4.legend()
    
    plt.tight_layout()
    
    # Add timestamp to filename
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    output_file = f'slippage_histogram_{timestamp}.png'
    
    plt.savefig(output_file, dpi=150, bbox_inches='tight')
    print(f"\nHistogram saved to: {output_file}")
    
    # Print detailed statistics
    print_statistics(slippages)
    
    return output_file


def print_statistics(slippages):
    """Print detailed slippage statistics to console."""
    
    negative = [s for s in slippages if s < 0]
    zero = [s for s in slippages if s == 0]
    positive = [s for s in slippages if s > 0]
    
    TICK_VALUE = 5.00  # NQ tick value
    
    print(f"\n{'='*80}")
    print(f"SLIPPAGE STATISTICS")
    print(f"{'='*80}")
    print(f"Total trades analyzed: {len(slippages)}")
    print(f"")
    print(f"CENTRAL TENDENCY:")
    print(f"  Mean slippage:         {np.mean(slippages):+.1f}t (${np.mean(slippages)*TICK_VALUE:+.2f})")
    print(f"  Median slippage:       {np.median(slippages):+.0f}t (${np.median(slippages)*TICK_VALUE:+.2f})")
    print(f"  Mode slippage:         {Counter(slippages).most_common(1)[0][0]:+.0f}t")
    print(f"")
    print(f"VARIABILITY:")
    print(f"  Std deviation:         {np.std(slippages):.1f}t (${np.std(slippages)*TICK_VALUE:.2f})")
    print(f"  Min slippage:          {min(slippages):+.0f}t (${min(slippages)*TICK_VALUE:+.2f})")
    print(f"  Max slippage:          {max(slippages):+.0f}t (${max(slippages)*TICK_VALUE:+.2f})")
    print(f"  Range:                 {max(slippages) - min(slippages):.0f}t")
    print(f"")
    print(f"DISTRIBUTION:")
    print(f"  Negative (cost):       {len(negative):4} trades ({len(negative)/len(slippages)*100:5.1f}%)  = ${sum(negative)*TICK_VALUE:+.2f}")
    print(f"  Zero:                  {len(zero):4} trades ({len(zero)/len(slippages)*100:5.1f}%)  = ${sum(zero)*TICK_VALUE:+.2f}")
    print(f"  Positive (gain):       {len(positive):4} trades ({len(positive)/len(slippages)*100:5.1f}%)  = ${sum(positive)*TICK_VALUE:+.2f}")
    print(f"  {'-'*40}")
    print(f"  NET SLIPPAGE COST:     ${sum(slippages)*TICK_VALUE:+.2f}")
    print(f"  Average per trade:     ${sum(slippages)*TICK_VALUE/len(slippages):+.2f}")
    print(f"")
    print(f"OUTLIERS:")
    print(f"  Trades with >=5t slippage:  {sum(1 for s in slippages if abs(s) >= 5):3} ({sum(1 for s in slippages if abs(s) >= 5)/len(slippages)*100:5.1f}%)")
    print(f"  Trades with >=10t slippage: {sum(1 for s in slippages if abs(s) >= 10):3} ({sum(1 for s in slippages if abs(s) >= 10)/len(slippages)*100:5.1f}%)")
    print(f"  Trades with >=15t slippage: {sum(1 for s in slippages if abs(s) >= 15):3} ({sum(1 for s in slippages if abs(s) >= 15)/len(slippages)*100:5.1f}%)")
    print(f"")
    print(f"TOP 5 SMALLEST SLIPPAGE:")
    worst_5 = sorted(slippages)[:5]
    for i, s in enumerate(worst_5, 1):
        print(f"  {i}. {s:+.0f}t (${s*TICK_VALUE:+.2f})")
    print(f"")
    print(f"TOP 5 LARGEST SLIPPAGE:")
    best_5 = sorted(slippages, reverse=True)[:5]
    for i, s in enumerate(best_5, 1):
        print(f"  {i}. {s:+.0f}t (${s*TICK_VALUE:+.2f})")
    print(f"{'='*80}")


def main():
    """Main entry point."""
    
    # Parse command line arguments
    parser = argparse.ArgumentParser(
        description='Generate slippage histogram from ActiveNikiTrader logs',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    python slippage_histogram.py
    python slippage_histogram.py -LogDir "C:\\Users\\alexb\\OneDrive\\Documents\\NinjaTrader 8\\log"
    python slippage_histogram.py --log-dir "D:\\Trading\\NT8\\log"
        """
    )
    
    parser.add_argument(
        '-LogDir', '--log-dir',
        type=str,
        default=r'C:\Users\alexb\OneDrive\Documents\NinjaTrader 8\log',
        help='Path to NinjaTrader 8 log directory (default: C:\\Users\\alexb\\OneDrive\\Documents\\NinjaTrader 8\\log)'
    )
    
    parser.add_argument(
        '-Output', '--output',
        type=str,
        default='slippage_histogram.png',
        help='Output filename for histogram (default: slippage_histogram.png)'
    )
    
    args = parser.parse_args()
    
    print(f"{'='*80}")
    print(f"ActiveNikiTrader Slippage Histogram Generator")
    print(f"{'='*80}")
    print(f"Start time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Log directory: {args.log_dir}")
    print(f"Output file: {args.output}")
    print(f"{'='*80}")
    print()
    
    # Extract slippage data
    slippages = parse_slippage_from_logs(args.log_dir)
    
    if not slippages:
        print("\nERROR: No slippage data found. Check that ActiveNikiTrader_*.txt files exist.")
        sys.exit(1)
    
    # Generate histogram
    create_histogram(slippages, args.output, args.log_dir)
    
    print(f"\nComplete! End time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")


if __name__ == '__main__':
    main()
