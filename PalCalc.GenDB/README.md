Program for regenerating the `PalCalc.Model/db.json` file. The project is set up to output directly to the `PalCalc.Model` project folder.

DB generation was previously based on [exported spreadsheets](https://docs.google.com/spreadsheets/d/1YgPc11dgdBUC8jXNp01b7gI6jNHoBRQGwrY_V6lXMgQ/edit?usp=sharing) from [/u/blahable's data scrape](https://www.reddit.com/r/Palworld/comments/19d98ws/spreadsheet_all_breeding_combinations_datamined/).

The latest data scraping is done from paldb.cc using `../ScrapeData/fetch.js`. See the [README](../ScrapeData/) for more information.

Cached save file data will include the version number of the Pal DB used. When making changes, update the line `PalDB.MakeEmptyUnsafe("...")` in `BuildDBProgram.cs` to change the version number of the generated DB file. Save file data will be reprocessed if its DB version doesn't match the version bundled with `PalCalc.Model`.