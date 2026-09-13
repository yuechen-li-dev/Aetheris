"""X1 entry point; reuses the X0 scorer integration with X1 assembly support."""

from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name("benchcad-aetheris-x0.py")), run_name="__main__")
