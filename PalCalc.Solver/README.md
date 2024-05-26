# Solving Overview

The solver maintains a list of all "relevant pals" for reaching the target, called the "working set".

It starts from all owned pals and wild pals (if enabled), and performs some "pruning" to filter out duplicate and irrelevant pals. This first pruning looks for at least one of each pal, each gender, and each subset of the desired traits.

Then, for each breeding step, all pals in the working set are combined as parent to produce pals which inherit the desired traits. 