from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackPathPoint")



@_attrs_define
class StackPathPoint:
    """ One point a path passes through, as `POST /viewport/pick` found it on the model.

        Attributes:
            position (list[float]): Where, in the model's own units: three numbers.
            normal (list[float]): The surface's normal there: three numbers.
            pressure (float): How much of the width and strength the path has here, 0 to 1. Default: 1.0.
     """

    position: list[float]
    normal: list[float]
    pressure: float = 1.0
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        position = self.position



        normal = self.normal



        pressure = self.pressure


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "position": position,
            "normal": normal,
            "pressure": pressure,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        position = cast(list[float], d.pop("position"))


        normal = cast(list[float], d.pop("normal"))


        pressure = d.pop("pressure")

        stack_path_point = cls(
            position=position,
            normal=normal,
            pressure=pressure,
        )


        stack_path_point.additional_properties = d
        return stack_path_point

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
