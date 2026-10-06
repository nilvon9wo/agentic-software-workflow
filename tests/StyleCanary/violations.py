import os  # expect: ruff:F401


def add(first, second):  # expect: ruff:ANN001
    return first + second


count: int = "three"  # expect: pyright:reportAssignmentType
spaced = {  "a":1  }  # expect: ruff-format:unformatted
