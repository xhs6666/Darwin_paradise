"""源码运行和便携发行版共享路径规则，不依赖开发者机器的固定目录。"""
from pathlib import Path


def project_default():
    for candidate in [*Path(__file__).resolve().parents, Path.cwd()]:
        if (candidate / 'ProjectSettings/ProjectVersion.txt').is_file():
            return candidate
    return None


def output_default():
    project = project_default()
    if project:
        return project / 'Artifacts/CorePackages'
    return Path(__file__).resolve().parent.parent / 'packages'
