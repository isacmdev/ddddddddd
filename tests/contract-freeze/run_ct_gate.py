"""Execute the twelve semantic contract tests and verify their TRX evidence."""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

CT_IDS = tuple(f"CT-{i:02d}" for i in range(1, 13))
PRODUCTIVE_SEAM_GATES = (
    ("tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj",
     "PolicyRepositoryTests|IntegrityRuntimePathTests|IntegrityVerdictHandlerTests|BackendClientSingleRequestTests|AuthenticatedBackendClientTests|ProgramHardeningTests|SchemaAdoptionTests|ScheduledWorkServiceAsyncDispatchTests"),
    ("tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj",
     "RealtimeSubscriberTests|WnsLifecycleTests"),
)
PRODUCTIVE_CONTRACT_SEAM_GATES = (
    ("CT-09", "tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj",
     "CT09_ProductivePolicySeamRetainsVersionAndQuarantinesConflict"),
    ("CT-11", "tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj",
     "CT11_ProductiveWnsSeamAcceptsOnlyBoundedHints"),
)
TAUTOLOGY_PATTERNS = (
    r"Assert\.NotEqual\(\s*\"[^\"]+\"\s*,\s*\"[^\"]+\"\s*\)",
    r"Assert\.Equal\(\s*\"[^\"]+\"\s*,\s*\"[^\"]+\"\s*\)",
    r"Assert\.True\(\s*\d+\s*[<>=!]+\s*\d+\s*\)",
    r"Assert\.False\(\s*\d+\s*[<>=!]+\s*\d+\s*\)",
)


def reject_tautologies(project: Path) -> list[str]:
    source = project.with_name("ContractAcceptanceTests.cs").read_text(encoding="utf-8")
    return [
        f"line {line_number}: tautological assertion"
        for line_number, line in enumerate(source.splitlines(), 1)
        if any(re.search(pattern, line) for pattern in TAUTOLOGY_PATTERNS)
    ]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".", type=Path)
    parser.add_argument("--project", default="tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj")
    parser.add_argument("--required", default="CT-01..CT-12")
    args = parser.parse_args()
    root = args.repo.resolve()
    project = (root / args.project).resolve()
    tautologies = reject_tautologies(project)
    if tautologies:
        print("FAIL: CT acceptance contains tautological assertions", file=sys.stderr)
        print("\n".join(tautologies), file=sys.stderr)
        return 1
    required = CT_IDS if args.required == "CT-01..CT-12" else (args.required,)
    if any(item not in CT_IDS for item in required):
        print("FAIL: unknown CT id", file=sys.stderr)
        return 2

    # CTs are an acceptance index, not a substitute for seam tests. Refuse to
    # report a green gate unless the productive runtime projects that own the
    # policy, integrity, WNS, and realtime seams are present and executable.
    for relative_project, test_filter in PRODUCTIVE_SEAM_GATES:
        seam_project = (root / relative_project).resolve()
        if not seam_project.is_file():
            print(f"FAIL: missing productive seam project: {relative_project}", file=sys.stderr)
            return 1
        seam_run = subprocess.run(
            ["dotnet", "test", str(seam_project), "--no-build", "--filter", f"FullyQualifiedName~{test_filter}"],
            cwd=root, capture_output=True, text=True, check=False,
        )
        seam_output = seam_run.stdout + seam_run.stderr
        if seam_run.returncode or "No test matches" in seam_output or re.search(r"Total(?: de pruebas| tests)\s*[:=]\s*0", seam_output, re.IGNORECASE):
            print(f"FAIL: productive seam gate failed or discovered zero tests for {relative_project}\n{seam_output}")
            return 1
        print(f"PASS productive seam: {relative_project} ({test_filter})")

    for ct, relative_project, test_filter in PRODUCTIVE_CONTRACT_SEAM_GATES:
        if ct not in required:
            continue
        seam_project = (root / relative_project).resolve()
        seam_run = subprocess.run(
            ["dotnet", "test", str(seam_project), "--no-build", "--filter", f"FullyQualifiedName~{test_filter}"],
            cwd=root, capture_output=True, text=True, check=False,
        )
        seam_output = seam_run.stdout + seam_run.stderr
        if seam_run.returncode or "No test matches" in seam_output or re.search(r"Total(?: de pruebas| tests)\s*[:=]\s*0", seam_output, re.IGNORECASE):
            print(f"FAIL {ct}: productive contract seam gate failed or discovered zero tests\n{seam_output}")
            return 1
        print(f"PASS {ct}: productive contract seam ({test_filter})")

    with tempfile.TemporaryDirectory(prefix="contract-ct-") as result_dir:
        listed = subprocess.run(
            ["dotnet", "test", str(project), "--no-build", "--list-tests"],
            cwd=root, capture_output=True, text=True, check=False,
        )
        if listed.returncode:
            print(listed.stdout + listed.stderr, file=sys.stderr)
            return listed.returncode
        discovered = {f"CT-{match.group(1)}" for match in re.finditer(r"CT-?(\d{2})", listed.stdout)}
        missing = sorted(set(required) - discovered)
        if missing:
            print(f"FAIL: semantic tests not discovered: {', '.join(missing)}", file=sys.stderr)
            return 1

        for ct in required:
            trx = Path(result_dir) / f"{ct}.trx"
            run = subprocess.run(
                ["dotnet", "test", str(project), "--no-build", "--filter", f"FullyQualifiedName~{ct.replace('-', '')}",
                 "--logger", f"trx;LogFileName={trx.name}", "--results-directory", result_dir],
                cwd=root, capture_output=True, text=True, check=False,
            )
            if run.returncode:
                print(f"FAIL {ct}: test process returned {run.returncode}\n{run.stdout}{run.stderr}")
                return 1
            files = list(Path(result_dir).glob("*.trx"))
            if not files:
                print(f"FAIL {ct}: missing TRX evidence", file=sys.stderr)
                return 1
            evidence = max(files, key=lambda path: path.stat().st_mtime)
            try:
                tree = ET.parse(evidence)
                text = evidence.read_text(encoding="utf-8")
            except (OSError, ET.ParseError) as error:
                print(f"FAIL {ct}: invalid TRX: {error}", file=sys.stderr)
                return 1
            if ct not in text or "Passed" not in text:
                print(f"FAIL {ct}: TRX lacks passing named test/trait", file=sys.stderr)
                return 1
            results = tree.findall(".//{*}UnitTestResult")
            if not results or any(item.get("outcome") != "Passed" for item in results):
                print(f"FAIL {ct}: TRX contains no exclusively passing result", file=sys.stderr)
                return 1
            print(f"PASS {ct}: semantic test + TRX + trait")
    return 0


if __name__ == "__main__":
    sys.exit(main())
