Program for regenerating the `PalCalc.Model/db.json` file. The project is set up to output directly to the `PalCalc.Model` project folder.

DB generation is based on [exported spreadsheets](https://docs.google.com/spreadsheets/d/1YgPc11dgdBUC8jXNp01b7gI6jNHoBRQGwrY_V6lXMgQ/edit?usp=sharing) from [/u/blahable's data scrape](https://www.reddit.com/r/Palworld/comments/19d98ws/spreadsheet_all_breeding_combinations_datamined/), stored under `ref`. These CSVs were manually tweaked to fix some inconsistencies/inaccuracies.

Some data has been manually added using `ref/extra.json`.

New sources can be used instead of this, for example by scraping a Palworld website with all relevant information. The source should include _at minimum_:

- Pals
  - Pal name displayed in-game
  - Internal pal "code name" used for representing a pal in game data
  - Flag for "variant" pals (e.g. Suzaku v Suzaku Aqua)
  - Paldex Number
  - Internal Index (`IndexOrder` in `ref/fulldata.csv`)
    - This is the internal ordering of pals within the game, apparently used as a fallback to resolve cases where the child breeding power calculation can't resolve to a single pal.
- Passive Traits
  - Trait name displayed in-game
  - Internal trait "code name" used for representing a trait in game data
  - Trait "rank" (corresponds to icon shown with the trait, e.g. yellow "three-up arrow" icon)