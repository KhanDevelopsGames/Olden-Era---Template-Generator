#!/usr/bin/env python3
"""
Fetch reference catalogs from alcaras/homm-olden and write them as clean
JSON files into src/OldenEra.Generator/CommunityData/.

Source: https://github.com/alcaras/homm-olden

Run from repo root:
    python3 src/OldenEra.Generator/CommunityData/scripts/fetch-from-alcaras.py

The output JSON is what the C# code consumes. Re-run when the upstream
datamine refreshes.
"""

import json
import os
import re
import sys
import urllib.request

UPSTREAM = "https://raw.githubusercontent.com/alcaras/homm-olden/master"
OUT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def fetch(path: str) -> str:
    url = f"{UPSTREAM}/{path}"
    print(f"  fetching {path}", flush=True)
    with urllib.request.urlopen(url, timeout=30) as r:
        return r.read().decode("utf-8")


def extract_const_array(src: str, name: str) -> list:
    """Pull `const NAME = [ ... ];` out of a JS module and parse the array."""
    marker = f"const {name} = "
    start = src.index(marker) + len(marker)
    # The array is JSON-shaped; find its matching ] by bracket counting.
    depth = 0
    end = start
    in_str = False
    esc = False
    for i in range(start, len(src)):
        c = src[i]
        if in_str:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
            continue
        if c == '"':
            in_str = True
        elif c == "[":
            depth += 1
        elif c == "]":
            depth -= 1
            if depth == 0:
                end = i + 1
                break
    return json.loads(src[start:end])


def extract_window_object(src: str, name: str) -> dict:
    """Pull `window.NAME = { ... };` out of a JS module and parse the object."""
    marker = f"window.{name} = "
    start = src.index(marker) + len(marker)
    depth = 0
    end = start
    in_str = False
    esc = False
    for i in range(start, len(src)):
        c = src[i]
        if in_str:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
            continue
        if c == '"':
            in_str = True
        elif c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break
    return json.loads(src[start:end])


def write_json(name: str, payload):
    path = os.path.join(OUT_DIR, name)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2, ensure_ascii=False)
    print(f"  wrote {name} ({len(json.dumps(payload))} bytes)", flush=True)


def main():
    print("Fetching reference data from alcaras/homm-olden ...")

    # data.js holds FACTIONS, SKILL_COLUMNS, SUBCLASSES, HEROES, UNITS as JS const arrays
    data_js = fetch("docs/data.js")
    factions = extract_const_array(data_js, "FACTIONS")
    skill_columns = extract_const_array(data_js, "SKILL_COLUMNS")
    subclasses = extract_const_array(data_js, "SUBCLASSES")
    heroes = extract_const_array(data_js, "HEROES")
    units = extract_const_array(data_js, "UNITS")

    # *-data.js files use window.OE_* objects
    skills_js = fetch("docs/skills-data.js")
    skills = extract_window_object(skills_js, "OE_SKILLS_DATA")["SKILLS"]

    spells_js = fetch("docs/spells-data.js")
    spells = extract_window_object(spells_js, "OE_SPELLS_DATA")["SPELLS"]

    write_json("factions.json", factions)
    write_json("heroes.json", heroes)
    write_json("units.json", units)
    write_json("subclasses.json", subclasses)
    write_json("skills.json", skills)
    write_json("skill-columns.json", skill_columns)
    write_json("spells.json", spells)

    print(f"\nDone. {len(factions)} factions, {len(heroes)} heroes, "
          f"{len(units)} units, {len(skills)} skills, {len(spells)} spells, "
          f"{len(subclasses)} subclasses.")


if __name__ == "__main__":
    main()
