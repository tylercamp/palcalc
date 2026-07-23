---
name: port-parser
description: Port read logic from Python palworld-save-tools parsers to C# PalCalc.SaveReader custom type handlers. Use when comparing or updating save file parsers between the Python reference and C# implementation.
---

# Parser Port Review

Port read logic from Python `palworld-save-tools` reference to C# `PalCalc.SaveReader` custom type handlers.

## Scope

- **Read logic only** — ignore all write/encode logic
- **Correctness** with focus on **byte alignment** — one incorrect read can misalign all subsequent reads in a sub-parser
- Sub-parsers operate on isolated byte arrays extracted from the larger save file, so trailing bytes and EOF checks that only serve validation (not alignment of sibling parsers) are optional

## Input

The user may provide file paths directly (e.g., "check `BaseCampReader.cs` and `base_camp.py`"). If paths are not specified, ask the user:

> Which parser(s) to compare? Provide the C# file(s) from `PalCalc.SaveReader/FArchive/Custom/` and/or the Python file(s) from `palworld-save-tools/palworld_save_tools/rawdata/`.

## Process

### 1. Read both files

Read the reference Python file(s) and the target C# file(s) indicated by the user.

### 2. Compare read sequences

For each `decode_bytes` / `decode` function in Python, find the corresponding `Decode` method in C# and compare:

- **Field order** — must match exactly; a single swapped read misaligns everything after it
- **Field types / sizes** — each Python read call maps to a C# `Read*` call; verify they consume the same number of bytes
- **Conditional branches** — enum-driven or type-driven branching must match
- **Array/map structure** — outer container type (ArrayProperty, MapProperty, etc.) and iteration logic

### 3. Output findings

Present a concise list of differences to the user:

```
## [FileName] — [status: OK / issues found]

- [issue or match description]
```

Categories of findings:
- **Alignment risk** — field order mismatch, wrong size read, missing read
- **Missing feature** — Python reads a field the C# version skips entirely
- **Not needed** — Python-only logic for data the solver doesn't use
- **OK** — read sequences match

### 4. Wait for user feedback

Do NOT apply changes automatically. Wait for the user to indicate which findings to address.

### 5. Apply changes

When the user confirms, apply the indicated edits to the C# files. Verify the build passes afterward.

## Reference

- C# custom readers: `PalCalc.SaveReader/FArchive/Custom/`
- C# reader interface: `PalCalc.SaveReader/FArchive/Custom/ICustomReader.cs`
- C# FArchiveReader API: `PalCalc.SaveReader/FArchive/FArchiveReader.cs`
