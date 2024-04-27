A partial port of cheahjs/palworld-save-tools to C#:
https://github.com/cheahjs/palworld-save-tools

Only supports reading .sav files, no writing. Most of the "custom" parsers (`rawdata` folder) were left unimplemented.

This is mostly a one-to-one port, with some extra stuff enabling the visitor pattern to minimize memory usage while extracting data and remove the need for excessive casting/unperformant dynamic-type wrappers. Rather than refactor to selectively return specific parts of the data, I wanted to leave the general structure as-is to accommodate any future updates to palworld-save-tools which may need to be reflected here.

Set `ARCHIVE_PRESERVE` as a compiler constant to allow `FArchiveReader` to store in memory all values that are read. Useful for figuring out the right path/visitors for extracting some data.

---

Palworld data notes:

- It's common for properties to be omitted if they're at their "default" value, e.g. pal level (1), in-game day number, (1), traits (empty list)

---

TODOs:

- Remove the need for `ARCHIVE_PRESERVE` flag, making it a type parameter without significant performance hit / change in logic structure
  - Use of a compiler flag lets us skip a lot of allocations + associated GC
  - Would let us remove code duplication in e.g. `CustomReader:GroupReader`, which needs to preserve values regardless of `ARCHIVE_PRESERVE` flag