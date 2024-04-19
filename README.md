# Pal Calc

A Windows program for calculating the optimal steps to breed a specific pal with a specific set of traits:

- Can detect and read from your local game save files, based on [palworld-save-tools by cheahjs](https://github.com/cheahjs/palworld-save-tools)
- Provides time estimates on each step, based on probabilities and mechanics [derived by /u/mgxts in this Reddit post](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/)
  - Gender probabilities
  - Probability of directly- and randomly-inserted traits
  - For directly-inherited traits, probability of getting the desired traits
- Offers the optimal path
  - Determines "path efficiency" based on calculated probabilities, not just the total number of steps
  - Handles single-root paths, where you successively breed children with another pal you own (one "starting point")
  - Handles multi-root paths, where two children are bred (multiple "starting points")
- Flexible search process
  - Allows wild pals
  - Set a max number of undesired traits if you're ok with imperfect pals
  - Set limits on the number of breeding steps and time estimates
- Efficient
  - Low memory usage and fast load times
  - Relatively fast path-solving process, most searches take under a minute
  - Distributes path-solving work across all available CPU cores

This can all probably be ported into a webpage, but I've put in enough effort with this first version. It would be great if someone else could work on that.

## Main TODOs

- Associate solver results with save file and save-last-modified, show icon if save file was updated
- Allow loading from save files in custom locations
- Game settings (breeding time, multiple breeding farms)
	- Save game settings + associate with save file
- Proper packaging + release
- Logging + error handling
	- Any exceptions
	- Ignore + log any unrecognized pals + traits
	- Errors when reading save files
	- Corrupted cached save data
	- Corrupted solver data
- ~~Disallow changes to list boxes + input fields while solving is running~~
- ~~Add progress bar when solving is running, don't freeze the window during solving~~
- ~~Save solver results~~
- ~~Show total time required~~

## Eventual TODOs
- Add "About" window for licenses + references
- Gracefully handle missing references to pals, traits, and pal instances
- Fix tree layout algorithm
- Option to delete entries from list of target pals
- Allow solving for multiple pals at once
- Show preview of involved pals, num wild pals, etc. in solver results list
- Performance options (max threads)
- Separate list of "Required traits" and "Optional traits" for target pal
- Solver optimizations
- Update PalDB reference data for recent game updates
- General UI/UX improvements
- ~~Notify when a pal involved in a breeding path is no longer available in the source save~~ (WONTFIX - too annoying)

## Maybe TODOs
- Automatically detect changes to save file + reload
- Option to auto-recalc all target pals when changes are detected
- Allow specifying custom db.json
- Support Xbox saves ([sample ref](https://github.com/Tom60chat/Xbox-Live-Save-Exporter/tree/main))
- Figure out how the data miners do their thing and read Pal DB info straight from game files?