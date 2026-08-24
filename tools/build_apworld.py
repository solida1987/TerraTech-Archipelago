# -*- coding: utf-8 -*-
"""Package the apworld, and install it locally when asked.

    py -3.13 tools/build_apworld.py [--install]

⚠ The apworld is a ZIP, and code inside a ZIP cannot open its own data files
with pathlib -- Data.py uses pkgutil for exactly that reason. This packer is
the thing that turns "works from source" into "works when shipped", so it also
proves the package can be read back before it claims success.
"""
from __future__ import annotations

import io
import json
import pathlib
import shutil
import sys
import zipfile

sys.stdout.reconfigure(encoding="utf-8")
ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "worlds" / "terratech"
OUT = ROOT / "dist" / "terratech.apworld"
INSTALL = pathlib.Path(r"C:\ProgramData\Archipelago\custom_worlds\terratech.apworld")


def main() -> None:
    OUT.parent.mkdir(exist_ok=True)
    if OUT.exists():
        OUT.unlink()

    n = 0
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(SRC.rglob("*")):
            if f.is_dir() or "__pycache__" in f.parts:
                continue
            z.write(f, pathlib.Path("terratech") / f.relative_to(SRC))
            n += 1

    # Read it back. A package that cannot be opened is not a package, and
    # finding that out at generation time is finding it out too late.
    with zipfile.ZipFile(OUT) as z:
        names = z.namelist()
        for required in ("terratech/__init__.py",
                         "terratech/archipelago.json",
                         "terratech/data/blocks.json"):
            if required not in names:
                raise SystemExit(f"MANGLER i pakken: {required}")
        blocks = json.loads(z.read("terratech/data/blocks.json"))

    print(f"{n} filer -> {OUT.relative_to(ROOT)}  ({OUT.stat().st_size:,} bytes)")
    print(f"   {blocks['count']} blokke i tabellen")

    if "--install" in sys.argv:
        INSTALL.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(OUT, INSTALL)
        print(f"   installeret: {INSTALL}")


if __name__ == "__main__":
    main()
