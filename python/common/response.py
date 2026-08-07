"""Shared success/failure envelope every script prints to stdout on exit.

Convention (see docs/TECHNICAL_SPEC.md §8): exactly one JSON object printed to
stdout between ###JSON_START###/###JSON_END### markers on success; human
progress goes to stderr; exit code is 0 on success, non-zero on failure with
the error message on stderr.
"""
from __future__ import annotations

import json
import sys
from typing import Any, NoReturn


class PythonResponse:
    def __init__(self, success: bool, message: str = "", error: str | None = None):
        self.success = success
        self.message = message
        self.error = error
        self.output_files: list[str] = []
        self.summary: dict[str, Any] = {}

    def add_file(self, path: str) -> None:
        self.output_files.append(path)

    def set_summary(self, key: str, value: Any) -> None:
        self.summary[key] = value

    def update_summary(self, values: dict[str, Any]) -> None:
        self.summary.update(values)

    def to_json(self) -> str:
        return json.dumps(
            {
                "success": self.success,
                "message": self.message,
                "error": self.error,
                "outputFiles": self.output_files,
                "summary": self.summary,
            }
        )

    def emit(self) -> None:
        print("###JSON_START###")
        print(self.to_json())
        print("###JSON_END###")


def emit_success(message: str, files: list[str], summary: dict) -> None:
    response = PythonResponse(True, message)
    response.output_files = files
    response.summary = summary
    response.emit()
    sys.exit(0)


def emit_failure(error: str, exit_code: int = 1) -> NoReturn:
    log(error)
    response = PythonResponse(False, "Failed", error)
    response.emit()
    sys.exit(exit_code)


def log(message: str) -> None:
    print(message, file=sys.stderr, flush=True)


def log_progress(current: int, total: int, label: str = "") -> None:
    pct = (current / total * 100) if total else 0
    log(f"[{pct:5.1f}%] {label} ({current}/{total})")
