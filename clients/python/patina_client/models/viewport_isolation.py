from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ViewportIsolation")



@_attrs_define
class ViewportIsolation:
    """ What the viewport draws alone (#136): the named meshes and the materials, which are texture sets, that show. The
    rest of the model is not drawn and casts no shadow. Both empty is the whole model.

        Attributes:
            parts (list[str]): Named meshes that show, as the model names them.
            materials (list[str]): Materials that show, as the model names them: a texture set is one.
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


        viewport_isolation = cls(
            parts=parts,
            materials=materials,
        )


        viewport_isolation.additional_properties = d
        return viewport_isolation

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
