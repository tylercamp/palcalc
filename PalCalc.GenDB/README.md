Program for regenerating the `PalCalc.Model/db.json` file and fetching pal icons by exporting data directly from the Palworld game files.

The project is set up to output directly to the `PalCalc.Model` and `PalCalc.UI` project folders.

Requires that Palworld be installed locally and a `.usmap` file is available. See the top-level comment in `BuildDBProgram.cs` for info on how to generate this file.

Cached save file data will include the version number of the Pal DB used. When making changes, update the line `PalDB.MakeEmptyUnsafe("...")` in `BuildDBProgram.cs` to change the version number of the generated DB file. Save file data will be reprocessed if its DB version doesn't match the version bundled with `PalCalc.Model`.
## Data extraction and game logic

This project extracts the data used by PalCalc. The rules for interpreting it are
maintained in [Palworld Breeding Mechanics](../PALWORLD-BREEDING-MECHANICS.md).
To investigate those rules or compare another game build, follow the
[mechanics research workflow](../docres/research/palworld-breeding/README.md).
Matching data fields alone does not establish how the game selects or filters them.

Map export and calibrated coordinate transforms are described under
[additional game logic](../docres/research/palworld-misc/README.md#world-map-coordinates).
