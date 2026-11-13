from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackFrame")



@_attrs_define
class StackFrame:
    """ Where something fixed on a stroke's picture stands: moved, scaled and turned there, and perhaps tiled.

        Attributes:
            centre (list[float]): Where its middle is on the stroke's picture, in the units stamps are in: two numbers.
            scale (float): How many of those units one of its pixels covers, above zero. Default: 1.0.
            angle (float): How far it is turned, clockwise on the screen, in radians. Default: 0.0.
            tiled (bool): Whether it repeats past its edges, or is nothing there. Default: False.
     """

    centre: list[float]
    scale: float = 1.0
    angle: float = 0.0
    tiled: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        centre = self.centre



        scale = self.scale

        angle = self.angle

        tiled = self.tiled


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "centre": centre,
            "scale": scale,
            "angle": angle,
            "tiled": tiled,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        centre = cast(list[float], d.pop("centre"))


        scale = d.pop("scale")

        angle = d.pop("angle")

        tiled = d.pop("tiled")

        stack_frame = cls(
            centre=centre,
            scale=scale,
            angle=angle,
            tiled=tiled,
        )


        stack_frame.additional_properties = d
        return stack_frame

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
