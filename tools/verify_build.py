# -*- coding: utf-8 -*-
"""The gate. Nothing ships until this passes.

    py -3.13 tools/verify_build.py

Six checks, and each one exists because the thing it tests can fail silently.
A silent failure here does not crash — it produces a seed that generates, looks
fine, and cannot be finished. That is the failure mode worth spending a gate on.
"""
from __future__ import annotations

import ast
import json
import pathlib
import re
import subprocess
import sys
import zipfile

sys.stdout.reconfigure(encoding="utf-8")
ROOT = pathlib.Path(__file__).resolve().parent.parent
WORLD = ROOT / "worlds" / "terratech"
MOD = ROOT / "mod" / "TerraTechArchipelago"

failures: list[str] = []
notes: list[str] = []


def check(name: str, ok: bool, detail: str = "") -> None:
    print(f"  {'ok  ' if ok else 'FAIL'}  {name}" + (f"  {detail}" if detail else ""))
    if not ok:
        failures.append(name)


# 1 -- the two sides agree on every block ------------------------------------
# The apworld sends "GSO Fuel Tank Licence"; the mod looks up "GSO Fuel Tank"
# to find GSOFuelTank_121. If those tables ever drift, the item arrives and
# unlocks nothing -- and the player has no way to tell that from bad luck.
blocks = json.loads((WORLD / "data" / "blocks.json").read_text(encoding="utf-8"))
py_pairs = {b["name"]: b["id"] for b in blocks["blocks"]}

cs = (MOD / "BlockNames.cs").read_text(encoding="utf-8")
cs_pairs = dict(re.findall(r'\{ "((?:[^"\\]|\\.)*)", "([^"]+)" \}', cs))
cs_pairs = {k.replace('\\"', '"').replace("\\\\", "\\"): v for k, v in cs_pairs.items()}

check("block table: python and C# agree",
      py_pairs == cs_pairs,
      f"{len(py_pairs)} vs {len(cs_pairs)}")
if py_pairs != cs_pairs:
    only_py = set(py_pairs) - set(cs_pairs)
    only_cs = set(cs_pairs) - set(py_pairs)
    if only_py:
        notes.append(f"only in python: {sorted(only_py)[:3]}")
    if only_cs:
        notes.append(f"only in C#: {sorted(only_cs)[:3]}")

# 2 -- no dead or deprecated blocks in the pool ------------------------------
dead = [b["id"] for b in blocks["blocks"] if b["id"].lower().startswith("_deprecated")]
check("no deprecated blocks in the pool", not dead, f"{len(dead)} found")

# 3 -- the tier split adds up ------------------------------------------------
# The world names locations from one split and the mod rebuilds its pools from
# another. A rounding difference means the mod places carriers for locations
# that do not exist, or misses ones that do.
sys.path.insert(0, str(WORLD))
import importlib.util
spec = importlib.util.spec_from_file_location("Data", WORLD / "Data.py")
data_mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(data_mod)

split_ok = all(sum(data_mod.split_by_tier(n).values()) == n
               for n in (1, 7, 40, 100, 333, 500, 800))
check("tier split loses no locations", split_ok)

cs_pools = (MOD / "CarrierPools.cs").read_text(encoding="utf-8")
cs_weights = dict(re.findall(r"\{ (\d), (0\.\d+) \}", cs_pools))
py_weights = {str(k): f"{v}" for k, v in data_mod.TIER_WEIGHTS.items()}
check("tier weights match between world and mod",
      {k: float(v) for k, v in cs_weights.items()} ==
      {k: float(v) for k, v in py_weights.items()},
      f"{cs_weights} vs {py_weights}")

# 4 -- every python file parses ----------------------------------------------
bad_py = []
for f in sorted(WORLD.rglob("*.py")):
    try:
        ast.parse(f.read_text(encoding="utf-8"))
    except SyntaxError as e:
        bad_py.append(f"{f.name}:{e.lineno}")
check("apworld python parses", not bad_py, ", ".join(bad_py))

# 5 -- the manifest is present and complete ----------------------------------
manifest_path = WORLD / "archipelago.json"
manifest_ok = False
if manifest_path.exists():
    m = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_ok = all(k in m for k in ("game", "world_version", "minimum_ap_version"))
check("archipelago.json present and complete", manifest_ok)

# 6 -- the mod compiles ------------------------------------------------------
# Reading C# proves nothing about whether it builds; the compiler is the only
# honest answer.
build = subprocess.run(["dotnet", "build", str(MOD), "-v", "q", "--nologo"],
                       capture_output=True, text=True)
errs = [l for l in build.stdout.splitlines() if ": error " in l]
check("mod compiles", build.returncode == 0 and not errs,
      errs[0][:90] if errs else "")

# ---------------------------------------------------------------------------
print()
for n in notes:
    print("   note:", n)
if failures:
    print(f"\n{len(failures)} check(s) FAILED: {', '.join(failures)}")
    sys.exit(1)
print("all checks passed")
