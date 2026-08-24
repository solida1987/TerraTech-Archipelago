# -*- coding: utf-8 -*-
"""Build the block table the world is generated from.

    py -3.13 tools/generate_block_table.py

The names come out of the game's own `BlockTypes` enum, read straight from
Assembly-CSharp.dll — never typed by hand. Three groups are thrown away, and
each exclusion is counted so the numbers in the design document can be checked
against this script rather than trusted.

  _deprecated_*   blocks the developers retired. Shipping them as items would
                  put things in the pool that no player can ever obtain.
  vendor / scenery  GSOVendor_*, forcefields, TEMP and Reserved slots. These
                  are world furniture, not blocks a player attaches.
  no grade digits blocks whose name carries no `_ggg` suffix, so no tier can
                  be inferred from it.

⚠ THE TIER IS INFERRED, NOT MEASURED. The name's first digit looks like the
licence grade, and it lines up for the blocks we spot-checked — but the
authoritative answer lives in `ManLicenses.GetBlockTier()` at runtime, not in
the name. The mod exports the real table on first run (`ap_block_table.json`)
and this file is regenerated from that export. Until then every row carries
`"tier_source": "inferred"`.
"""
from __future__ import annotations

import collections
import json
import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parent.parent
ENUM_DUMP = pathlib.Path(r"C:\Users\marco\AppData\Local\Temp\BlockTypes_values.txt")
OUT = ROOT / "worlds" / "terratech" / "data" / "blocks.json"

# The ten corporations, from the game's `Corporations` enum.
CORPS = {
    "GSO": "GSO",
    "GC": "GeoCorp",
    "VEN": "Venture",
    "HE": "Hawkeye",
    "BF": "Better Future",
    "RR": "Reticule Research",
    "SJ": "Space Junkers",
    "LGN": "Legion",
    "EXP": "Experimental",
    "SPE": "Special",
}

# <CORP><Name>_<grade><variant><variant>
BLOCK = re.compile(r"^(GSO|GC|VEN|HE|BF|RR|SJ|LGN|EXP|SPE)(.+?)_(\d)(\d)(\d)$")

# Furniture, not blocks. Vendors are the shops themselves; forcefields and
# maze pieces belong to scripted encounters; TEMP and Reserved are slots the
# developers left for themselves.
NOT_A_BLOCK = re.compile(
    r"Vendor|Forcefield|ForceField|_TEMP|_Old|Reserved|ProgressBar|_Charger_Lab", re.I)


def pretty(corp: str, stem: str) -> str:
    """`GSOFuelTank` -> `GSO Fuel Tank`. Player-facing item names."""
    spaced = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", stem)
    spaced = re.sub(r"(?<=[A-Za-z])(?=\d)", " ", spaced)
    return f"{corp} {spaced}".strip()


def main() -> None:
    if not ENUM_DUMP.exists():
        raise SystemExit(f"mangler {ENUM_DUMP} -- koer TTScan med enum:BlockTypes foerst")

    names = [n.strip() for n in ENUM_DUMP.read_text(encoding="utf-8").splitlines() if n.strip()]
    dropped = collections.Counter()
    blocks: list[dict] = []
    seen_names: set[str] = set()

    for raw in names:
        if raw.lower().startswith("_deprecated"):
            dropped["deprecated"] += 1
            continue
        if NOT_A_BLOCK.search(raw):
            dropped["vendor eller kulisse"] += 1
            continue
        m = BLOCK.match(raw)
        if not m:
            dropped["intet grad-suffiks"] += 1
            continue

        corp_key, stem, grade = m.group(1), m.group(2), int(m.group(3))
        # Grade 0 exists in the enum and is not a licence grade; 6, 8 and 9 are
        # far past the five real grades. Clamp into 1..5 rather than invent
        # tiers the licence system does not have.
        tier = min(max(grade, 1), 5)

        name = pretty(CORPS[corp_key], stem)
        # Two enum entries can prettify to the same words; keep the id unique
        # by falling back to the raw name, so nothing silently collapses.
        if name in seen_names:
            name = f"{name} ({raw})"
        seen_names.add(name)

        blocks.append({
            "id": raw,
            "name": name,
            "corp": CORPS[corp_key],
            "corp_key": corp_key,
            "tier": tier,
            "tier_source": "inferred",
        })

    by_corp = collections.Counter(b["corp"] for b in blocks)
    by_tier = collections.Counter(b["tier"] for b in blocks)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({
        "_source": "BlockTypes enum in Assembly-CSharp.dll, read with tools/TTScan",
        "_generated_by": "tools/generate_block_table.py",
        "_tier_warning": ("Tier is inferred from the first digit of the name suffix. "
                          "The mod exports the authoritative table from "
                          "ManLicenses.GetBlockTier() on first run; regenerate from that."),
        "_dropped": dict(dropped),
        "count": len(blocks),
        "by_corp": dict(by_corp.most_common()),
        "by_tier": {str(k): v for k, v in sorted(by_tier.items())},
        "blocks": blocks,
    }, indent=1, ensure_ascii=False), encoding="utf-8")

    print(f"{len(names)} navne i enum")
    for reason, n in dropped.most_common():
        print(f"   frasorteret: {reason:<22} {n}")
    print(f"\n{len(blocks)} spilbare blokke -> {OUT.relative_to(ROOT)}")
    print("   pr. korporation:", dict(by_corp.most_common()))
    print("   pr. grad       :", {k: by_tier[k] for k in sorted(by_tier)})


if __name__ == "__main__":
    main()
