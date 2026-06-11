"""Dice rolling."""

import random

from ..models import DiceRoll


def roll() -> DiceRoll:
    """Roll two six-sided dice."""
    return DiceRoll(die1=random.randint(1, 6), die2=random.randint(1, 6))
