## Miscellaneous Comments

The following options were considered (but ignored) for improving speed of the final results:

- Including self-breeding effort as a discriminator / `GroupIdFn` and/or as part of the "optimal pal" sorting process
  - The effect of a "Required Gender" constraint depends on the bred pal's self-breeding effort. You can have two pals with the same estimate, but if one pal has a lower self-breeding effort, then required-gender constraints on that pal will be more efficient than the other pal
  - The pal pruning process only considers the base effort, not including potential effort from required gender constraints
  - This was [temporarily added + tested]([https://github.com/tylercamp/palcalc/issues/95](https://github.com/tylercamp/palcalc/issues/95#issuecomment-2585340511)). It did not change the final solver results and increased working set size ~10x
