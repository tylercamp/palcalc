A partial port of cheahjs/palworld-save-tools to C#:
https://github.com/cheahjs/palworld-save-tools

Only supports reading .sav files, no writing. Most of the "custom" parsers (`rawdata` folder) were left unimplemented.

This is mostly a one-to-one port, with some extra stuff enabling the visitor pattern to minimize memory usage while extracting data. Rather than refactor to selectively return specific parts of the data, I wanted to leave the general structure as-is to accommodate any future updates to palworld-save-tools which may need to be reflected here.

Set `ARCHIVE_PRESERVE` as a compiler constant to allow `FArchiveReader` to store in memory all values that are read. Useful for figuring out the right path/visitors for extracting some data.