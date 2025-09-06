# PalCalc – Product Definition
*(Version 2025-07-02)*

---

## 1. Why PalCalc Exists
Breeding in *Palworld* quickly becomes an NP-hard optimisation puzzle involving:
* hundreds of possible parent pairs
* probabilistic passive-skill inheritance
* gender constraints
* IV (stat) optimisation
* real-time resource cost (breeding farm time, pal capture effort)

Most players resort to manual breeding trees and trial-and-error, wasting dozens of hours. PalCalc eliminates this friction by **importing a save file, analysing the player’s actual pals, and computing the globally optimal breeding tree in minutes.**

---

## 2. Problems Solved
| # | Pain Point | PalCalc Solution |
|---|------------|-----------------|
| 1 | Manual enumeration of breeding pairs | Multi-threaded solver explores the full state-space with pruning heuristics |
| 2 | Guesswork on passive-skill odds | Built-in probability tables derived from reverse-engineered game code |
| 3 | Tedious data entry | Automatic save-file parsing (Steam & Xbox) + JSON db of game assets |
| 4 | Planning effort/time | Quantitative effort estimates per step (breeding duration, capture cost) |
| 5 | Visual comprehension | WPF UI renders an interactive breeding graph with GraphSharp |

---

## 3. Target Audience
* Mid- / late-game *Palworld* players chasing perfect pals
* Min-max / speed-runner communities
* Content creators producing breeding guides

---

## 4. Core Value Proposition
1. **Save-aware** – Results tailored to *your* existing pals, not theoretical lists.
2. **Optimal & Repeatable** – Deterministic solver (given fixed random seeds) guarantees fastest path.
3. **Convenient** – Fast, displays breeding effort, owned-pal locations with mini-map popup.
4. **Extensible** – Modular architecture; localisation pipeline.

---

## 5. Key Features
* One-click save import (Steam / Xbox)
* Adjustable constraints:
  * Required + optional passives
  * Max irrelevant passives
  * Max breeding steps
  * Wild-pal allowance
  * IV thresholds
* Multi-language UI – .resx based
* Save Inspector (filter, search, edit custom pals)
* Probability-driven time estimates (breeding + capture)

---

## 6. Non-Goals  
* Editing or writing save files (read-only architecture)
* Mobile / Web front-end (desktop WPF focus)
* Real-time in-game overlay - this is a standalone program

---

## 7. Open-Source Positioning
* MIT licensed except embedded 3rd-party libs (GraphSharp, AdonisUI copy-left licences)
* Accepts community PRs for translations, DB patches, solver improvements
* Issues triaged using GitHub Issues

---

## 8. Success Metrics  
* < 60 s median solve time on 16-core CPU for 500-pal saves
* 1000+ GitHub stars, 200+ Discord active users
* Localisation coverage ≥ 90 % of in-app strings across top 8 languages

---

## 9. Future Directions
* Attack-skill inheritance & solving
* UI refactor to MVVM-CommunityToolkit purity + async data-flows
* GPU-accelerated solver (SIMD probability kernels)

---

*Generated during initial Memory Bank **initialisation**. Review & adjust as requirements evolve.*