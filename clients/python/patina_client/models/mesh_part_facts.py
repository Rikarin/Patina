from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="MeshPartFacts")



@_attrs_define
class MeshPartFacts:
    """ One named mesh where the file places it.

        Attributes:
            name (str): Its name in the file.
            material (str): The material, or texture set, it is drawn with.
            triangles (int): How many triangles.
            uv_sets (int): How many texture coordinate sets it has.
     """

    name: str
    material: str
    triangles: int
    uv_sets: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        material = self.material

        triangles = self.triangles

        uv_sets = self.uv_sets


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "material": material,
            "triangles": triangles,
            "uvSets": uv_sets,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        material = d.pop("material")

        triangles = d.pop("triangles")

        uv_sets = d.pop("uvSets")

        mesh_part_facts = cls(
            name=name,
            material=material,
            triangles=triangles,
            uv_sets=uv_sets,
        )


        mesh_part_facts.additional_properties = d
        return mesh_part_facts

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
