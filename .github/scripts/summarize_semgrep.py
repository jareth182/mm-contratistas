"""Summarize a Semgrep JSON report as a log + Markdown table.

Usage: summarize_semgrep.py <semgrep.json> <summary.md>
Writes `findings=<n>` to $GITHUB_OUTPUT when available.
"""
import json
import os
import sys
from collections import Counter

report_path, summary_path = sys.argv[1], sys.argv[2]
data = json.load(open(report_path, encoding="utf-8"))

results = data.get("results", [])
scanned = data.get("paths", {}).get("scanned", [])
by_ext = Counter(os.path.splitext(p)[1] or "(none)" for p in scanned)

lines = ["## Code scan (Semgrep)", "",
         "| Metric | Value |", "|---|---|",
         f"| Files scanned | {len(scanned)} |",
         f"| File types | {', '.join(f'{e} ({n})' for e, n in sorted(by_ext.items()))} |",
         "| Rulesets | p/csharp, p/javascript, p/secrets |",
         f"| Findings | {len(results)} |",
         f"| Result | {'✅ Clean' if not results else '❌ Findings to review'} |"]

if results:
    lines += ["", "| Severity | Rule | Location | Message |", "|---|---|---|---|"]
    for r in results:
        msg = r["extra"]["message"].splitlines()[0]
        lines.append(f"| {r['extra']['severity']} | {r['check_id'].split('.')[-1]} "
                     f"| {r['path']}:{r['start']['line']} | {msg} |")

text = "\n".join(lines) + "\n"
print(text)
open(summary_path, "w", encoding="utf-8").write(text)

if os.environ.get("GITHUB_OUTPUT"):
    with open(os.environ["GITHUB_OUTPUT"], "a") as out:
        out.write(f"findings={len(results)}\n")
