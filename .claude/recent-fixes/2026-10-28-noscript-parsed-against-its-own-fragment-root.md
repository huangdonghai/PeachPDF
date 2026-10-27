# noscript content is parsed on a root of its own, not on the noscript box

## What was wrong

`HtmlParser.ParseDocument` sends the text directly inside a `<noscript>` element back through itself,
so that the content is live markup (this renderer runs no script) instead of raw text. The recursive
call was handed the noscript box as its root - and the first token of the recursive parse is the same
data token, met with `curBox` still being the noscript element. The branch fired again with the same
arguments, and again, until the stack was gone. `<noscript>Script is disabled</noscript>` is enough:
one of the reftest corpus's pages, WPT's `mediaqueries/scripting-print-noscript-ref.html`, killed the
process with a `StackOverflowException` (209,644 repeated `ParseDocument` frames) after about a minute
of recursion on a 64 MB thread. In a batch that abandons a timed-out drawing the crash lands on a
thread nothing is waiting for any more, so the page that caused it looks like an ordinary timeout.

Markup with an element in front of the text (`<noscript><p>x</p></noscript>`) never hit the branch -
the text was added against the inner element - which is why the existing noscript test stayed green.

## The change

The fragment is parsed on a root of its own (`ParseDocument(text.Data)`) and the boxes it builds are
moved inside the noscript element afterwards (`child.ParentBox = curBox`, the same reparenting
`EnsureHtmlAndBody` uses). The recursive call no longer starts inside the noscript element, so it
cannot re-enter the branch with the same text.

## Evidence

- A three-line probe (`<noscript>x</noscript>`, 800x600) reproduced the stack overflow before the
  change and renders after it; `<noscript><p>hi</p></noscript>` and `<noscript><b>x</b> y</noscript>`
  keep their markup and text.
- The WPT pair that used to take the process down now draws both pages identically (0 differing
  pixels, 318 ink pixels each).
- `NoscriptContentParsingTests` pins the bare-text, markup and mixed shapes; the engine suite is
  32,124 green on both TFMs.
