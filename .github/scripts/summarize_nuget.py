"""Summarize `dotnet list package --vulnerable --format json` as a log + Markdown table.

Usage: summarize_nuget.py <report.json> <summary.md>
Prints the findings to the job log and writes the same table to <summary.md>.
Writes `vulnerable=<n>` to $GITHUB_OUTPUT when available.
"""
import json
import os
import sys

report_path, summary_path = sys.argv[1], sys.argv[2]
data = json.load(open(report_path, encoding="utf-8"))

rows = []
for project in data.get("projects", []):
    for fw in project.get("frameworks", []):
        for kind, key in (("Direct", "topLevelPackages"), ("Transitive", "transitivePackages")):
            for pkg in fw.get(key, []):
                for vuln in pkg.get("vulnerabilities", []):
                    rows.append((pkg["id"], pkg.get("resolvedVersion", "?"), kind,
                                 vuln["severity"], vuln["advisoryurl"]))

lines = ["## Dependency scan (NuGet)", ""]
if rows:
    lines += ["| Package | Version | Type | Severity | Advisory |", "|---|---|---|---|---|"]
    lines += [f"| {p} | {v} | {k} | {s} | {u} |" for p, v, k, s, u in rows]
    lines += ["", f"**Result: {len(rows)} vulnerabilities found.** Update the package to a patched version."]
else:
    lines += ["| Check | Result |", "|---|---|",
              "| Direct and transitive packages vs. GitHub Advisory Database | ✅ No known vulnerabilities |"]

text = "\n".join(lines) + "\n"
print(text)
open(summary_path, "w", encoding="utf-8").write(text)

if os.environ.get("GITHUB_OUTPUT"):
    with open(os.environ["GITHUB_OUTPUT"], "a") as out:
        out.write(f"vulnerable={len(rows)}\n")
