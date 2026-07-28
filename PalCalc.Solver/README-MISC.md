## Miscellaneous Comments

The following options were considered (but ignored) for improving speed of the final results:

- Including self-breeding effort as a frontier-state discriminator (`EffectivePropertiesKey`) and/or as part of the retained-alternative selection process
  - The effect of a "Required Gender" constraint depends on the bred pal's self-breeding effort. You can have two pals with the same estimate, but if one pal has a lower self-breeding effort, then required-gender constraints on that pal will be more efficient than the other pal
  - Frontier selection only considers the base effort, not potential effort from a later required-gender constraint
  - This was [temporarily added and tested](https://github.com/tylercamp/palcalc/issues/95#issuecomment-2585340511). It did not change the final solver results and increased frontier size ~10x
