# Requirements: Streaming results

**Status:** Complete.

## Goal

The Division Shortcut search can take a while, and today the form freezes until it's done. Instead:

- The search runs in the background, so the form stays usable.
- Lines show up in the Lines box as they're found, kept sorted cheapest first.
- Esc stops a running search, and the lines found so far stay on screen.
- Enter in the S, G or Buttons box stops any running search and starts a new one.

The search itself doesn't get faster. This is about seeing answers sooner and being able to stop.

## Behavior

### Starting a search

- Pressing Enter in the S, G or Buttons box starts a search (as now).
- If a search is already running, it's stopped first, its lines are thrown away, and the new search starts with an empty Lines box.
- Input errors (bad S or G, unknown button, missing `÷` or `10`) show right away in the status label, as now, and no search starts. A running search is still stopped.

### While it runs

- The Lines box shows the cheapest 10 lines found so far, sorted the same way as today (cost, then non-`flip` presses, then divisor, then press text).
- When a cheaper line turns up, it moves into place, so rows may reorder or drop off the bottom while the search runs.
- **Batched updates, every 100 ms.** The search never touches the form. It only adds lines to a queue. Every 100 ms the UI takes what's in the queue, re-sorts the list and redraws the Lines box once. If the top 10 didn't change, it doesn't redraw. This keeps the form responsive however fast lines arrive.
- The status label says `Searching… N line(s) so far. Esc to stop.`
- The S, G and Buttons boxes stay editable. Typing shows "Press Enter to recalculate." as now, but doesn't stop the search. Only Enter or Esc stops it.
- The Modulo Shortcut section keeps updating on every change, as now.

### When it ends

- Finished: the status label says `N line(s)`, as now. If none were found, it shows the error `No division line fits the available buttons.`
- Stopped with Esc: the lines found so far stay, and the status label says `Stopped. N line(s) found.`
- Esc when nothing is running does nothing.
- Closing the window stops a running search.

## Line format

Use whatever line format is current when this is built. If [digit-keys](../digit-keys/requirements.md) is done first, that's `8X ÷ 10 = flip   (4 presses)`.

## Edge cases

- S = G: no search, no lines, no error (as now).
- Pressing Enter many times quickly: only the last search's lines are shown. Lines from a stopped search must never appear in the new one's results.
- A line is never shown twice, even if the search finds the same press sequence twice.

## Open questions

- Should the box show more than 10 lines? Kept at 10 for now.

## Out of scope

- Making the search faster (multi-threading is its own spec, [parallel-search](../parallel-search/requirements.md), done after this one), or changing its loop order so cheap lines tend to be found earlier. The displayed list is always re-sorted (see "While it runs"); this is only about the order the search finds lines in.
- Streaming for the Modulo Shortcut (it's instant).
- A progress bar or percent done.
