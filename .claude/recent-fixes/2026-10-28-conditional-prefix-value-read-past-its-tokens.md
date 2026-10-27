# A safe/unsafe prefix value no longer reads past its token list or keeps a null value

## What was wrong

`ConditionalStartsWithValueConverter` (behind `align-content`/`align-items`/`justify-content`'s
`safe center`, `first baseline` and friends) had two unguarded reads of the user's declaration:

- `while (enumerator.MoveNext() && …Current.Type == Whitespace) { }` was followed by
  `enumerator.Current`, which is undefined after `MoveNext` has returned false and throws
  `InvalidOperationException: Enumeration already finished` for an array-backed token list. A half of
  a two-value shorthand reaches the converter with an empty list: `place-self: start` (the justify
  side gets no tokens of its own), `place-self: start/start`, `place-self: anchor-center`. Four WPT
  anchor-position pairs and Gecko's `css-grid/grid-percent-grid-gap-001.html` failed this way.
- A prefix whose inner converter rejected the rest (`justify-self: safe anchor-center`; `anchor-center`
  is not a keyword of the wrapped converter) was still wrapped into a `ConditionalStartValue` with a
  null inner value, and reading the property's `CssText` then dereferenced it. WPT's
  `anchor-center-safe.html` and `anchor-center-safe-rtl.html` died in `DomParser.RulesUseRevertKeyword`
  with the property's own `CssText` getter at the top of the stack.

## The change

The scan stops as soon as a non-whitespace token is found and reports "no token" instead of reading
`Current` afterwards; a value whose inner conversion returns null is dropped as an invalid declaration
rather than wrapped. Valid `safe center` / `first baseline` values are unaffected.

## Evidence

- The three spot pages that reproduced both exceptions render after the fix at 800x600:
  `css-anchor-position/anchor-center-overflow-001.html`, `css-grid/grid-percent-grid-gap-001.html`,
  `css-anchor-position/anchor-center-safe.html`.
- New tests in `FlexGridAlignmentTypedStorageTests`: `align-self: safe nonsense` is ignored (stores
  `Auto`) rather than throwing, and `place-self: start` applies `Start` to both axes.
- Engine suite 32,124 green on both TFMs.
