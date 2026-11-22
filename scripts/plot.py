"""Render the scalability charts from bench/results/*.csv into docs/report/*.png."""
import csv
from collections import defaultdict
from pathlib import Path
from statistics import median

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

ROOT = Path(__file__).resolve().parent.parent
RESULTS = ROOT / "bench" / "results"
OUT = ROOT / "docs" / "report"
MODES = ("striped", "global")
DPI = 150


def read(name):
    with open(RESULTS / name, newline="") as f:
        return list(csv.DictReader(f))


def save(fig, name):
    OUT.mkdir(parents=True, exist_ok=True)
    fig.tight_layout()
    fig.savefig(OUT / name, dpi=DPI)
    plt.close(fig)


def build_medians():
    times = defaultdict(list)
    for row in read("build.csv"):
        times[(row["mode"], int(row["threads"]))].append(float(row["time_ms"]))
    return {key: median(values) for key, values in times.items()}


def plot_build(medians):
    fig, ax = plt.subplots(figsize=(7, 4.5))
    for mode in MODES:
        points = sorted((t, ms / 1000) for (m, t), ms in medians.items() if m == mode)
        ax.plot(*zip(*points), marker="o", label=mode)
    ax.set(xlabel="Indexing threads", ylabel="Build time (s, median of runs)", title="Index build time vs threads")
    ax.grid(alpha=0.3)
    ax.legend(title="Lock mode")
    save(fig, "build_time_vs_threads.png")


def plot_speedup(medians):
    fig, ax = plt.subplots(figsize=(7, 4.5))
    max_threads = 0
    for mode in MODES:
        points = sorted((t, ms) for (m, t), ms in medians.items() if m == mode)
        base = dict(points)[1]
        ax.plot([t for t, _ in points], [base / ms for _, ms in points], marker="o", label=mode)
        max_threads = max(max_threads, points[-1][0])
    ax.plot([1, max_threads], [1, max_threads], "k--", label="ideal")
    ax.set(xlabel="Indexing threads", ylabel="Speedup T1 / Tp", title="Index build speedup")
    ax.grid(alpha=0.3)
    ax.legend()
    save(fig, "speedup.png")


def plot_throughput():
    rows = read("throughput.csv")
    for column, ylabel, title, name in (
        ("rps", "Requests per second", "Search throughput vs concurrent clients", "throughput_vs_clients.png"),
        ("p50_ms", "Latency (ms)", "Search latency vs concurrent clients", "latency_vs_clients.png"),
    ):
        fig, ax = plt.subplots(figsize=(7, 4.5))
        for expand in ("false", "true"):
            subset = [r for r in rows if r["expand"] == expand]
            if not subset:
                continue
            clients = [int(r["clients"]) for r in subset]
            label = "expand=true (LLM)" if expand == "true" else "expand=false"
            ax.plot(clients, [float(r[column]) for r in subset], marker="o", label=label)
            if column == "p50_ms":
                ax.plot(clients, [float(r["p95_ms"]) for r in subset], marker="x", linestyle=":", label=label + " p95")
        ax.set(xlabel="Concurrent clients", ylabel=ylabel, title=title, xscale="log", yscale="log" if column != "rps" else "linear")
        ax.set_xticks(sorted({int(r["clients"]) for r in rows}))
        ax.get_xaxis().set_major_formatter(matplotlib.ticker.ScalarFormatter())
        ax.grid(alpha=0.3)
        ax.legend()
        save(fig, name)


if __name__ == "__main__":
    medians = build_medians()
    plot_build(medians)
    plot_speedup(medians)
    plot_throughput()
