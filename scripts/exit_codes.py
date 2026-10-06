"""Process exit codes shared by every command-line script."""

EXIT_SUCCESS = 0
EXIT_FAILURE = 1


def exit_code_for(has_succeeded: bool) -> int:  # noqa: FBT001 - the outcome IS the single, obvious argument
    """The conventional exit code for an outcome."""
    if has_succeeded:
        return EXIT_SUCCESS
    else:
        return EXIT_FAILURE
