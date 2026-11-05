from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID






T = TypeVar("T", bound="ProjectSummary")



@_attrs_define
class ProjectSummary:
    """ One project as the browser shows it. The project itself loads on open.

        Attributes:
            id (UUID): The project's identity.
            name (str): The project's display name.
            path (str): The project's folder, relative to `projects/`, with forward slashes.
            tags (list[str]): The project's tags, sorted.
            reference_count (int): How many shared assets the project references directly.
     """

    id: UUID
    name: str
    path: str
    tags: list[str]
    reference_count: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = str(self.id)

        name = self.name

        path = self.path

        tags = self.tags



        reference_count = self.reference_count


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "tags": tags,
            "referenceCount": reference_count,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        tags = cast(list[str], d.pop("tags"))


        reference_count = d.pop("referenceCount")

        project_summary = cls(
            id=id,
            name=name,
            path=path,
            tags=tags,
            reference_count=reference_count,
        )


        project_summary.additional_properties = d
        return project_summary

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
