# Markdown canary

Deliberately broken. **Do not fix it.** Each violation is tagged with the gate
and rule that must report it.

See https://example.com for a bare URL. <!-- expect: markdownlint:MD034 -->

A [link to a missing file](no-such-file.md). <!-- expect: lychee:broken-link -->

A [link to a missing anchor](#no-such-heading). <!-- expect: lychee:broken-link -->

An [external link](https://example.com/never-fetched), which offline mode skips.
