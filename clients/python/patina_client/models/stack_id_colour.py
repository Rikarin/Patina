from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackIdColour")



@_attrs_define
class StackIdColour:
    """ One colour of a stack's id map.

        Attributes:
            colour (list[float]): Red, green and blue, 0 to 1, as the map stores them.
            share (float): How much of the map's covered area it is, 0 to 1.
     """

    colour: list[float]
    share: float
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        colour = self.colour



        share = self.share


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "colour": colour,
            "share": share,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        colour = cast(list[float], d.pop("colour"))


        share = d.pop("share")

        stack_id_colour = cls(
            colour=colour,
            share=share,
        )


        stack_id_colour.additional_properties = d
        return stack_id_colour

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
