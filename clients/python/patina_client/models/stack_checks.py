from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.check_problem import CheckProblem





T = TypeVar("T", bound="StackChecks")



@_attrs_define
class StackChecks:
    """ The reply to `GET /api/v1/stacks/{id}/checks`: one texture set measured against its project.

        Attributes:
            stack (str): The stack session.
            set_ (str): The texture set, or empty for a model with no named sets.
            width (int): Its maps' width, in texels.
            height (int): Their height.
            texel_density (float): Texels per unit of the model across the whole set, or zero where it has no area.
            target_density (float): What the project holds it to, or zero for nothing.
            memory_bytes (int): The GPU memory its exported files take with the project's preset, mips included.
            memory_budget (int): The project's budget in bytes, or zero for none.
            preset (str): The export preset measured against.
            problems (list[CheckProblem]): What is wrong, in the order of IReadOnlyList&lt;string&gt; CheckCodes.All, which
                puts the project's limits, the errors,
                first.
     """

    stack: str
    set_: str
    width: int
    height: int
    texel_density: float
    target_density: float
    memory_bytes: int
    memory_budget: int
    preset: str
    problems: list[CheckProblem]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.check_problem import CheckProblem # noqa: PLC0415
        stack = self.stack

        set_ = self.set_

        width = self.width

        height = self.height

        texel_density = self.texel_density

        target_density = self.target_density

        memory_bytes = self.memory_bytes

        memory_budget = self.memory_budget

        preset = self.preset

        problems = []
        for problems_item_data in self.problems:
            problems_item = problems_item_data.to_dict()
            problems.append(problems_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "stack": stack,
            "set": set_,
            "width": width,
            "height": height,
            "texelDensity": texel_density,
            "targetDensity": target_density,
            "memoryBytes": memory_bytes,
            "memoryBudget": memory_budget,
            "preset": preset,
            "problems": problems,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.check_problem import CheckProblem # noqa: PLC0415
        d = dict(src_dict)
        stack = d.pop("stack")

        set_ = d.pop("set")

        width = d.pop("width")

        height = d.pop("height")

        texel_density = d.pop("texelDensity")

        target_density = d.pop("targetDensity")

        memory_bytes = d.pop("memoryBytes")

        memory_budget = d.pop("memoryBudget")

        preset = d.pop("preset")

        problems = []
        _problems = d.pop("problems")
        for problems_item_data in (_problems):
            problems_item = CheckProblem.from_dict(problems_item_data)



            problems.append(problems_item)


        stack_checks = cls(
            stack=stack,
            set_=set_,
            width=width,
            height=height,
            texel_density=texel_density,
            target_density=target_density,
            memory_bytes=memory_bytes,
            memory_budget=memory_budget,
            preset=preset,
            problems=problems,
        )


        stack_checks.additional_properties = d
        return stack_checks

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
