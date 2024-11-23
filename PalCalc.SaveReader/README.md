A partial port of cheahjs/palworld-save-tools to C#:
https://github.com/cheahjs/palworld-save-tools

Only supports reading .sav files, no writing. Most of the "custom" parsers (`rawdata` folder) were left unimplemented.

This is mostly a one-to-one port, with some extra stuff enabling the visitor pattern to minimize memory usage while extracting data and remove the need for excessive casting/unperformant dynamic-type wrappers. Rather than refactor to selectively return specific parts of the data, the general structure was left as-is to accommodate any future updates to palworld-save-tools which may need to be reflected here.

By default the `FArchiveReader` won't preserve parsed values, relying on `IVisitors` to extract data instead. Use `ISaveGame.ParseGvas(true)` to store in memory all values that are read. Useful for figuring out the right path/visitors for extracting some data.

---

Each `IProperty` that is parsed includes a `Meta` field which contains the full "property path" to that value. When writing new parsers, you should set a breakpoint in the `PalCalc.SaveReader.CLI` program (after a call to `var x = ....ParseGvas(true)`), run the program, and inspect the parsed data.

Once you find the relevant data you can expand the entry's Meta, copy the `Path`, and use that as a parameter for an `IVisitor` to extract that data. See `SaveFile/LevelMetaSaveFile.cs` for a simple example.

Most of the `Visit*Begin` methods on an `IVisitor` allow you to return a list of `IVisitors` which will be used until the corresponding `Visit*End` is called for that scope. "Child" visitors will have their `Exit()` method called before the parent's `Visit*End` is called, allowing you to postprocess the data in the child visitor before the parent makes use of its results.

---

Palworld data notes:

- It's common for properties to be omitted if they're at their "default" value, e.g. pal level (1), in-game day number, (1), passive skills (empty list)

---

TODOs:

- Generally optimize