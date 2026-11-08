from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ExportPaddingInfo")



@_attrs_define
class ExportPaddingInfo:
    """ What fills the texels a mesh's layout leaves empty.

        Attributes:
            mode (str): none, infinite, transparent, colour or diffusion.
            distance (int): How many texels the islands grow by first, for every mode but none and infinite.
            colour (list[float]): For colour, the value of each lane of a file, 0 to 1.
     """

    mode: str
    distance: int
    colour: list[float]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        mode = self.mode

        distance = self.distance

        colour = self.colour




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "mode": mode,
            "distance": distance,
            "colour": colour,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        mode = d.pop("mode")

        distance = d.pop("distance")

        colour = cast(list[float], d.pop("colour"))


        export_padding_info = cls(
            mode=mode,
            distance=distance,
            colour=colour,
        )


        export_padding_info.additional_properties = d
        return export_padding_info

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
