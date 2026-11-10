from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="MeshMaterialFacts")



@_attrs_define
class MeshMaterialFacts:
    """ One material of a mesh, which is one texture set.

        Attributes:
            name (str): Its name, unique in the file.
            triangles (int): How many triangles it is on.
            parts (int): How many parts it is on.
            uv_sets (int): The texture coordinate sets every one of its parts has, so a texture set can paint and bake on
                any of them.
     """

    name: str
    triangles: int
    parts: int
    uv_sets: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        triangles = self.triangles

        parts = self.parts

        uv_sets = self.uv_sets


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "triangles": triangles,
            "parts": parts,
            "uvSets": uv_sets,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        triangles = d.pop("triangles")

        parts = d.pop("parts")

        uv_sets = d.pop("uvSets")

        mesh_material_facts = cls(
            name=name,
            triangles=triangles,
            parts=parts,
            uv_sets=uv_sets,
        )


        mesh_material_facts.additional_properties = d
        return mesh_material_facts

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
