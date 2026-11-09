from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="BakePreviewPart")



@_attrs_define
class BakePreviewPart:
    """ One part name as matching by name sees it: how many faces each side has and the colour it is drawn in.

        Attributes:
            name (str): The name both sides share once their endings are gone.
            low_faces (int): The low mesh's faces of that name, zero for a high part no low part matched.
            high_faces (int): The high mesh's faces of that name, zero for a low part that matched nothing.
            colour (str): How it is drawn, as `#rrggbb`.
     """

    name: str
    low_faces: int
    high_faces: int
    colour: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        low_faces = self.low_faces

        high_faces = self.high_faces

        colour = self.colour


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "lowFaces": low_faces,
            "highFaces": high_faces,
            "colour": colour,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        low_faces = d.pop("lowFaces")

        high_faces = d.pop("highFaces")

        colour = d.pop("colour")

        bake_preview_part = cls(
            name=name,
            low_faces=low_faces,
            high_faces=high_faces,
            colour=colour,
        )


        bake_preview_part.additional_properties = d
        return bake_preview_part

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
