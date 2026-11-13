from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ViewportHighlight")



@_attrs_define
class ViewportHighlight:
    """ What the viewport marks on the model (#136): named meshes and materials, a texture set being one, washed in one
    colour and outlined over the model as it is shaded, such as the row a client points at in its list of texture
    sets. A part an isolation hides is not marked. Both empty marks nothing.

        Attributes:
            parts (list[str]): Named meshes marked, as the model names them.
            materials (list[str]): Materials marked, as the model names them: a texture set is one.
     """

    parts: list[str]
    materials: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        parts = self.parts



        materials = self.materials




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "parts": parts,
            "materials": materials,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        parts = cast(list[str], d.pop("parts"))


        materials = cast(list[str], d.pop("materials"))


        viewport_highlight = cls(
            parts=parts,
            materials=materials,
        )


        viewport_highlight.additional_properties = d
        return viewport_highlight

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
