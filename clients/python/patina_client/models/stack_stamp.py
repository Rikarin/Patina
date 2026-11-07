from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackStamp")



@_attrs_define
class StackStamp:
    """ Where the brush goes: in texels of the image, or on the picture of a stroke with a view.

        Attributes:
            x (float): Its column.
            y (float): Its row.
            pressure (float): How hard, 0 to 1. Default: 1.0.
     """

    x: float
    y: float
    pressure: float = 1.0
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        x = self.x

        y = self.y

        pressure = self.pressure


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "x": x,
            "y": y,
            "pressure": pressure,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        x = d.pop("x")

        y = d.pop("y")

        pressure = d.pop("pressure")

        stack_stamp = cls(
            x=x,
            y=y,
            pressure=pressure,
        )


        stack_stamp.additional_properties = d
        return stack_stamp

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
