# Style canary

Deliberately non-compliant code. **Do not fix it.** It is excluded from the
solution and from `scripts/check_line_layout.py`'s default scan.

Every violation carries an `// expect: <gate>:<rule>` marker naming the gate
that must report it on that line. `scripts/verify_gates.py` runs each gate
against this project and fails if any expected report is missing - so a gate
that silently stops running, or a rule that silently stops being enforced,
breaks CI instead of letting violations slip into real code.

When you add a rule to `.editorconfig`, add a violation for it here.
