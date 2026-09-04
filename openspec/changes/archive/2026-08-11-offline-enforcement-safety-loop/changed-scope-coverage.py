import re
import subprocess
import sys
import xml.etree.ElementTree as ET

FILES = sys.argv[3:]
coverage = {}
for report in sys.argv[1:3]:
    for item in ET.parse(report).findall(".//class"):
        name = item.get("filename", "").replace("\\", "/")
        lines = coverage.setdefault(name, {})
        for line in item.findall("./lines/line"):
            number = int(line.get("number"))
            value = lines.setdefault(number, [0, 0, 0])
            value[0] = max(value[0], int(line.get("hits", "0")))
            match = re.search(r"\((\d+)/(\d+)\)", line.get("condition-coverage", ""))
            if match:
                value[1] = max(value[1], int(match.group(1)))
                value[2] = max(value[2], int(match.group(2)))

totals = [0, 0, 0, 0]
for path in FILES:
    key = next((name for name in coverage if name.endswith(path.removeprefix("src/"))), None)
    if key is None:
        continue
    tracked = subprocess.run(
        ["git", "ls-files", "--error-unmatch", path], capture_output=True).returncode == 0
    changed = set(coverage[key]) if not tracked else set()
    if tracked:
        diff = subprocess.run(
            ["git", "diff", "--unified=0", "HEAD", "--", path],
            capture_output=True, text=True, check=True).stdout
        for start, count in re.findall(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", diff, re.MULTILINE):
            changed.update(range(int(start), int(start) + int(count or 1)))
    values = [coverage[key][number] for number in changed if number in coverage[key]]
    uncovered = [number for number in sorted(changed) if number in coverage[key]
                 and coverage[key][number][0] == 0]
    result = [sum(value[0] > 0 for value in values), len(values),
              sum(value[1] for value in values), sum(value[2] for value in values)]
    totals = [left + right for left, right in zip(totals, result)]
    print(path, *result, "uncovered=" + ",".join(map(str, uncovered)))

print("TOTAL", *totals, f"lines={100 * totals[0] / totals[1]:.2f}%",
      f"branches={100 * totals[2] / totals[3]:.2f}%")
