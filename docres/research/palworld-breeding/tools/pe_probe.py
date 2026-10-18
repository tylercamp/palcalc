#!/usr/bin/env python3
"""Read-only x64 PE32+ metadata, coordinate mapping, and literal search (Python 3.9+).

No target execution, disassembly, or MCP dependencies. Layout reference:
https://learn.microsoft.com/en-us/windows/win32/debug/pe-format
"""

import argparse
import hashlib
import json
import mmap
import os
from pathlib import Path
import struct
import sys


class PE:
    def __init__(self, data):
        self.data = data

        def read(fmt, offset):
            size = struct.calcsize(fmt)
            if offset < 0 or offset + size > len(data):
                raise ValueError("truncated PE header or section table")
            return struct.unpack_from(fmt, data, offset)[0]

        if len(data) < 64 or data[:2] != b"MZ":
            raise ValueError("not a DOS/PE image")
        pe = read("<I", 0x3C)
        if pe < 64 or data[pe:pe + 4] != b"PE\0\0":
            raise ValueError("invalid PE signature/offset")
        machine, count = read("<H", pe + 4), read("<H", pe + 6)
        optional_size = read("<H", pe + 20)
        optional = pe + 24
        if machine != 0x8664 or read("<H", optional) != 0x20B:
            raise ValueError("only AMD64 PE32+ images are supported")
        if not 1 <= count <= 96 or optional_size < 112:
            raise ValueError("invalid section count or optional-header size")
        table = optional + optional_size
        if table + count * 40 > len(data):
            raise ValueError("truncated optional header or section table")
        directories = read("<I", optional + 108)
        if 112 + directories * 8 > optional_size:
            raise ValueError("data directories exceed optional-header size")
        self.entry_rva = read("<I", optional + 16)
        self.image_base = read("<Q", optional + 24)
        self.image_size = read("<I", optional + 56)
        self.headers_size = read("<I", optional + 60)
        if not table + count * 40 <= self.headers_size <= len(data):
            raise ValueError("invalid SizeOfHeaders")
        if (self.headers_size > self.image_size or self.image_base % 65536
                or self.image_base + self.image_size > 2 ** 64):
            raise ValueError("invalid image base or image bounds")

        self.sections = []
        for index in range(count):
            offset = table + index * 40
            section = {
                "name": data[offset:offset + 8].split(b"\0", 1)[0].decode("ascii", "replace"),
                "virtual_size": read("<I", offset + 8),
                "rva": read("<I", offset + 12),
                "raw_size": read("<I", offset + 16),
                "raw_offset": read("<I", offset + 20),
                "executable": bool(read("<I", offset + 36) & 0x20000000),
            }
            raw, size = section["raw_offset"], section["raw_size"]
            span = max(size, section["virtual_size"])
            if size and (raw < self.headers_size or raw + size > len(data)):
                raise ValueError("section raw bytes exceed file bounds or overlap headers")
            if span and (section["rva"] < self.headers_size
                         or section["rva"] + span > self.image_size):
                raise ValueError("section exceeds image bounds or overlaps headers")
            self.sections.append(section)
        for start_key, size_key in (("raw_offset", "raw_size"), ("rva", None)):
            ranges = []
            for section in self.sections:
                size = section[size_key] if size_key else max(section["raw_size"], section["virtual_size"])
                if size:
                    ranges.append((section[start_key], section[start_key] + size))
            ranges.sort()
            if any(left[1] > right[0] for left, right in zip(ranges, ranges[1:])):
                raise ValueError("overlapping sections: address mapping would be ambiguous")
        if self.entry_rva:
            entry = self.from_rva(self.entry_rva)
            if entry["status"] != "file-backed" or not entry["executable"]:
                raise ValueError("entry point is not file-backed executable section data")

    def from_rva(self, rva):
        result = {"rva": hex(rva), "va": hex(self.image_base + rva) if 0 <= rva < self.image_size else None,
                  "file_offset": None, "section": None, "executable": False, "status": "unmapped"}
        if 0 <= rva < self.headers_size:
            result.update(file_offset=hex(rva), section="<headers>", status="file-backed")
        else:
            for section in self.sections:
                delta = rva - section["rva"]
                if 0 <= delta < max(section["virtual_size"], section["raw_size"]):
                    result.update(section=section["name"], executable=section["executable"])
                    if delta < section["raw_size"]:
                        result.update(file_offset=hex(section["raw_offset"] + delta), status="file-backed")
                    else:
                        result["status"] = "zero-filled"
                    break
        return result

    def from_offset(self, offset):
        if 0 <= offset < self.headers_size:
            return self.from_rva(offset)
        for section in self.sections:
            delta = offset - section["raw_offset"]
            if 0 <= delta < section["raw_size"]:
                return self.from_rva(section["rva"] + delta)
        return {"file_offset": hex(offset), "rva": None, "va": None,
                "section": None, "executable": False, "status": "unmapped"}

    def from_va(self, va):
        if va >= self.image_base:
            result = self.from_rva(va - self.image_base)
            result["va"] = hex(va)
            return result
        return {"va": hex(va), "rva": None, "file_offset": None,
                "section": None, "executable": False, "status": "unmapped"}

    def search(self, literal, limit):
        if not literal or limit < 1:
            raise ValueError("literal must be nonempty and hit limit positive")
        encodings = ["utf-16le"]
        if literal.isascii():
            encodings.insert(0, "ascii")
        results = []
        for encoding in encodings:
            needle = literal.encode(encoding)
            hits, cursor = [], 0
            while True:
                offset = self.data.find(needle, cursor)
                if offset < 0 or len(hits) == limit:
                    break
                location = self.from_offset(offset)
                # A match crossing a mapping boundary has no single image coordinate.
                end = self.from_offset(offset + len(needle) - 1)
                if (location["rva"] is None or end["rva"] is None
                        or int(end["rva"], 16) != int(location["rva"], 16) + len(needle) - 1):
                    location.update(rva=None, va=None, section=None, executable=False, status="unmapped")
                hits.append(location)
                cursor = offset + 1
            results.append({"encoding": encoding, "hits": hits, "truncated": offset >= 0})
        return {"literal": literal, "searches": results}

    def metadata(self):
        return {
            "format": "PE32+", "architecture": "x86_64", "file_size": len(self.data),
            "sha256": hashlib.sha256(self.data).hexdigest(),
            "image_base": hex(self.image_base), "size_of_image": hex(self.image_size),
            "size_of_headers": hex(self.headers_size), "section_count": len(self.sections),
            "entry": self.from_rva(self.entry_rva) if self.entry_rva else None,
            "sections": [{key: hex(value) if key in {"rva", "virtual_size", "raw_size", "raw_offset"}
                          else value for key, value in section.items()} for section in self.sections],
        }


def integer(text):
    try:
        value = int(text, 16) if text.lower().startswith("0x") else int(text, 10)
    except ValueError as error:
        raise argparse.ArgumentTypeError("expected decimal or 0x-prefixed integer") from error
    if not 0 <= value < 2 ** 64:
        raise argparse.ArgumentTypeError("coordinate must be an unsigned 64-bit integer")
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("target", type=Path)
    parser.add_argument("--literal", action="append", default=[], help="case-sensitive substring; repeatable")
    for name in ("file-offset", "rva", "va"):
        parser.add_argument("--" + name, type=integer, action="append", default=[])
    parser.add_argument("--max-hits", type=int, default=100, help="per literal/encoding; default 100")
    parser.add_argument("--output", type=Path, help="new UTF-8 JSON file; parent must exist; never overwritten")
    args = parser.parse_args()
    if args.max_hits < 1 or any(not value for value in args.literal):
        parser.error("hit limit must be positive and literals nonempty")
    try:
        with args.target.open("rb") as source:
            before = os.fstat(source.fileno())
            with mmap.mmap(source.fileno(), 0, access=mmap.ACCESS_READ) as data:
                pe = PE(data)
                report = pe.metadata()
                report.update(path=str(args.target.resolve()),
                              mappings={"file_offset": [pe.from_offset(n) for n in args.file_offset],
                                        "rva": [pe.from_rva(n) for n in args.rva],
                                        "va": [pe.from_va(n) for n in args.va]},
                              literals=[pe.search(value, args.max_hits) for value in args.literal],
                              limitations=["Preferred-base static coordinates, not runtime/ASLR addresses or loader fixups.",
                                           "Literal substrings are discovery candidates, not decoded functions or descriptors.",
                                           "Raw section padding is included; unmapped bytes/virtual gaps have no file/image mapping."])
            after = os.fstat(source.fileno())
            if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                raise ValueError("target changed during inspection; retry on stable bytes")
        payload = json.dumps(report, indent=2, ensure_ascii=True) + "\n"
        if args.output:
            with args.output.open("x", encoding="utf-8") as output:
                output.write(payload)
            print("Wrote " + str(args.output), file=sys.stderr)
        else:
            sys.stdout.write(payload)
    except (OSError, ValueError) as error:
        parser.exit(2, "error: " + str(error) + "\n")


if __name__ == "__main__":
    main()
