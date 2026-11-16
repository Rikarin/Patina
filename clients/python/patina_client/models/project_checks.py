from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.check_problem import CheckProblem
  from ..models.stack_checks import StackChecks





T = TypeVar("T", bound="ProjectChecks")



@_attrs_define
class ProjectChecks:
    """ The reply to `GET /api/v1/library/project/checks`: every texture set and layered material of a project
    checked at once, against one platform (#159).

        Attributes:
            project (str): The project, below `projects/`.
            preset (str): The export preset measured against, by its name.
            memory_bytes (int): The GPU memory all the sets' exported files take together.
            sets (list[StackChecks]): Each set, in the order the project lists its documents.
            problems (list[CheckProblem]): What is wrong with the project's checks themselves rather than any one set:
                limits kept for a platform no preset
                writes for any more.
     """

    project: str
    preset: str
    memory_bytes: int
    sets: list[StackChecks]
    problems: list[CheckProblem]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.check_problem import CheckProblem # noqa: PLC0415
        from ..models.stack_checks import StackChecks # noqa: PLC0415
        project = self.project

        preset = self.preset

        memory_bytes = self.memory_bytes

        sets = []
        for sets_item_data in self.sets:
            sets_item = sets_item_data.to_dict()
            sets.append(sets_item)



        problems = []
        for problems_item_data in self.problems:
            problems_item = problems_item_data.to_dict()
            problems.append(problems_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "preset": preset,
            "memoryBytes": memory_bytes,
            "sets": sets,
            "problems": problems,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.check_problem import CheckProblem # noqa: PLC0415
        from ..models.stack_checks import StackChecks # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        preset = d.pop("preset")

        memory_bytes = d.pop("memoryBytes")

        sets = []
        _sets = d.pop("sets")
        for sets_item_data in (_sets):
            sets_item = StackChecks.from_dict(sets_item_data)



            sets.append(sets_item)


        problems = []
        _problems = d.pop("problems")
        for problems_item_data in (_problems):
            problems_item = CheckProblem.from_dict(problems_item_data)



            problems.append(problems_item)


        project_checks = cls(
            project=project,
            preset=preset,
            memory_bytes=memory_bytes,
            sets=sets,
            problems=problems,
        )


        project_checks.additional_properties = d
        return project_checks

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
