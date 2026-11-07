from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ProjectFile")



@_attrs_define
class ProjectFile:
    """ A result of a bake in a project's folder.

        Attributes:
            kind (str): What it is.
            name (str): Its name.
            path (str): Its path below the library.
            stale (bool): For a bake, whether the mesh has changed since it was made. Default: False.
     """

    kind: str
    name: str
    path: str
    stale: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        name = self.name

        path = self.path

        stale = self.stale


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "name": name,
            "path": path,
            "stale": stale,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        name = d.pop("name")

        path = d.pop("path")

        stale = d.pop("stale")

        project_file = cls(
            kind=kind,
            name=name,
            path=path,
            stale=stale,
        )


        project_file.additional_properties = d
        return project_file

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
