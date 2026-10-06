import os  # expect: ruff:F401


def add(first, second):  # expect: ruff:ANN001
    return first + second


count: int = "three"  # expect: pyright:reportAssignmentType
spaced = {  "a":1  }  # expect: ruff-format:unformatted
chosen = 1 if count else 2  # expect: pylint:E9001
printed = print(len(str(count)))  # expect: pylint:E9002
chained = count and spaced and chosen  # expect: pylint:E9004


def nested(groups):
    for group in groups:
        for item in group:
            if item:  # expect: pylint:E9003
                print(item)
