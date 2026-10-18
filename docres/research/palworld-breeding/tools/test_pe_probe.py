"""Synthetic PE fixtures: no game files, engine, or third-party modules required."""

import argparse
import struct
import unittest

from pe_probe import PE, integer


BASE = 0x140000000
OPTIONAL = 0x80 + 24
TABLE = OPTIONAL + 240


def fixture():
    data = bytearray(0x800)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<HH", data, 0x84, 0x8664, 2)
    struct.pack_into("<H", data, 0x80 + 20, 240)
    struct.pack_into("<H", data, OPTIONAL, 0x20B)
    struct.pack_into("<I", data, OPTIONAL + 16, 0x1010)
    struct.pack_into("<Q", data, OPTIONAL + 24, BASE)
    struct.pack_into("<II", data, OPTIONAL + 56, 0x3000, 0x200)
    struct.pack_into("<I", data, OPTIONAL + 108, 16)
    for index, (name, rva) in enumerate(((b".text", 0x1000), (b".data", 0x2000))):
        offset = TABLE + index * 40
        data[offset:offset + len(name)] = name
        struct.pack_into("<IIII", data, offset + 8, 0x300, rva, 0x200, 0x200 + index * 0x200)
        struct.pack_into("<I", data, offset + 36, 0x60000020 if index == 0 else 0xC0000040)
    data[0x220:0x224] = b"Seed"
    data[0x440:0x448] = "Seed".encode("utf-16le")
    return data


class ProbeTests(unittest.TestCase):
    def test_header_offsets_and_mapping_round_trips(self):
        pe = PE(fixture())
        self.assertEqual((pe.image_base, pe.image_size, pe.entry_rva), (BASE, 0x3000, 0x1010))
        self.assertEqual(len(pe.sections), 2)
        for offset, rva in ((0, 0), (0x1FF, 0x1FF), (0x200, 0x1000),
                            (0x3FF, 0x11FF), (0x400, 0x2000), (0x5FF, 0x21FF)):
            result = pe.from_offset(offset)
            self.assertEqual(result["rva"], hex(rva))
            self.assertEqual(result["va"], hex(BASE + rva))
            self.assertEqual(pe.from_va(BASE + rva)["file_offset"], hex(offset))

    def test_virtual_tails_gaps_overlay_and_missing_entry(self):
        pe = PE(fixture())
        self.assertEqual(pe.from_rva(0x1200)["status"], "zero-filled")
        self.assertIsNone(pe.from_rva(0x12FF)["file_offset"])
        for rva in (0x1300, 0x1FFF, 0x2300, 0x3000):
            self.assertEqual(pe.from_rva(rva)["status"], "unmapped")
        for offset in (0x600, 0x7FF, 0x800):
            self.assertIsNone(pe.from_offset(offset)["va"])
        self.assertIsNone(pe.from_va(BASE - 1)["rva"])
        data = fixture()
        struct.pack_into("<I", data, OPTIONAL + 16, 0)
        self.assertIsNone(PE(data).metadata()["entry"])

    def test_literal_encodings_limits_and_overlapping_hits(self):
        data = fixture()
        data[0x600:0x604] = b"Seed"  # Overlay text has no VA.
        pe = PE(data)
        found = pe.search("Seed", 10)["searches"]
        self.assertEqual([len(item["hits"]) for item in found], [2, 1])
        self.assertEqual(found[0]["hits"][0]["va"], hex(BASE + 0x1020))
        self.assertIsNone(found[0]["hits"][1]["va"])
        self.assertEqual(found[1]["hits"][0]["rva"], "0x2040")
        self.assertTrue(pe.search("Seed", 1)["searches"][0]["truncated"])
        self.assertFalse(found[1]["truncated"])
        self.assertTrue(all(not item["hits"] for item in pe.search("seed", 10)["searches"]))
        data[0x230:0x235] = b"aaaaa"
        hits = PE(data).search("aaa", 10)["searches"][0]["hits"]
        self.assertEqual([h["file_offset"] for h in hits], ["0x230", "0x231", "0x232"])
        data[0x3FE:0x402] = b"Edge"
        self.assertIsNone(PE(data).search("Edge", 10)["searches"][0]["hits"][0]["va"])
        data[0x460:0x462] = "\u03a9".encode("utf-16le")
        unicode_hits = PE(data).search("\u03a9", 10)["searches"]
        self.assertEqual([item["encoding"] for item in unicode_hits], ["utf-16le"])
        self.assertEqual(unicode_hits[0]["hits"][0]["file_offset"], "0x460")

    def test_rejects_bad_headers_and_section_ranges(self):
        for offset, fmt, value in ((0x3C, "<I", 0xFFFFFFF0), (0x84, "<H", 0x14C),
                                   (0x86, "<H", 0), (0x94, "<H", 60),
                                   (OPTIONAL, "<H", 0x10B), (OPTIONAL + 60, "<I", 0x100),
                                   (OPTIONAL + 108, "<I", 100), (OPTIONAL + 16, "<I", 0x2010),
                                   (OPTIONAL + 16, "<I", 0x1200), (TABLE + 20, "<I", 0x100),
                                   (TABLE + 20, "<I", 0x700), (TABLE + 12, "<I", 0x2F00),
                                   (TABLE + 40 + 12, "<I", 0x1100),
                                   (TABLE + 40 + 20, "<I", 0x300),
                                   (OPTIONAL + 24, "<Q", BASE + 1)):
            with self.subTest(offset=offset, value=value):
                data = fixture()
                struct.pack_into(fmt, data, offset, value)
                with self.assertRaises(ValueError):
                    PE(data)
        for data in (b"", b"MZ", fixture()[:TABLE + 60]):
            with self.assertRaises(ValueError):
                PE(data)

    def test_invalid_search_and_coordinates(self):
        self.assertEqual(integer("010"), 10)
        self.assertEqual(integer("0x10"), 16)
        for text in ("-1", "oops", str(2 ** 64)):
            with self.assertRaises(argparse.ArgumentTypeError):
                integer(text)
        for literal, limit in (("", 1), ("Seed", 0)):
            with self.assertRaises(ValueError):
                PE(fixture()).search(literal, limit)


if __name__ == "__main__":
    unittest.main()
