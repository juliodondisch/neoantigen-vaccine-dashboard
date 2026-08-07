from __future__ import annotations

import os
import shutil
from dataclasses import dataclass, field


@dataclass
class ToolConfig:
    bwa_mem2: str = "bwa-mem2"
    samtools: str = "samtools"
    gatk: str = "gatk"
    star: str = "STAR"
    vep: str = "vep"
    optitype: str = "OptiTypePipeline.py"
    pvactools: str = "pvacseq"

    @classmethod
    def from_env(cls) -> "ToolConfig":
        return cls(
            bwa_mem2=os.environ.get("TOOL_BWA_MEM2", "bwa-mem2"),
            samtools=os.environ.get("TOOL_SAMTOOLS", "samtools"),
            gatk=os.environ.get("TOOL_GATK", "gatk"),
            star=os.environ.get("TOOL_STAR", "STAR"),
            vep=os.environ.get("TOOL_VEP", "vep"),
            optitype=os.environ.get("TOOL_OPTITYPE", "OptiTypePipeline.py"),
            pvactools=os.environ.get("TOOL_PVACTOOLS", "pvacseq"),
        )

    def check_available(self, tool_name: str) -> bool:
        path = getattr(self, tool_name, tool_name)
        return shutil.which(path) is not None

    def require(self, tool_name: str) -> str:
        path = getattr(self, tool_name, tool_name)
        if shutil.which(path) is None:
            raise RuntimeError(f"Required tool '{tool_name}' ({path}) is not on PATH.")
        return path


def get_reference_path(genome: str) -> str:
    root = get_data_root()
    return os.path.join(root, "references", genome)


def get_data_root() -> str:
    return os.environ.get("DATA_ROOT", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "data")))
