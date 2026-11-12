from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="FileFormats")



@_attrs_define
class FileFormats:
    """ What a client may offer, with the dot.

        Attributes:
            pictures (list[str]): What a layer, a mask, an image material and a skew map read.
            environments (list[str]): What lights the viewport.
            meshes (list[str]): What a project takes as its mesh, its detailed mesh or a cage.
            materials (list[str]): What a project brings materials in from, as graphs (#157).
     """

    pictures: list[str]
    environments: list[str]
    meshes: list[str]
    materials: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        pictures = self.pictures



        environments = self.environments



        meshes = self.meshes



        materials = self.materials




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "pictures": pictures,
            "environments": environments,
            "meshes": meshes,
            "materials": materials,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        pictures = cast(list[str], d.pop("pictures"))


        environments = cast(list[str], d.pop("environments"))


        meshes = cast(list[str], d.pop("meshes"))


        materials = cast(list[str], d.pop("materials"))


        file_formats = cls(
            pictures=pictures,
            environments=environments,
            meshes=meshes,
            materials=materials,
        )


        file_formats.additional_properties = d
        return file_formats

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
