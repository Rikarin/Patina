from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from uuid import UUID






T = TypeVar("T", bound="ProjectReferenceRequest")



@_attrs_define
class ProjectReferenceRequest:
    """ The body of `PATCH /api/v1/library/project/reference`.

        Attributes:
            project (str): The project that refers to the asset, below `projects/`.
            asset (UUID): The asset it refers to itself.
            pinned (bool): True holds it at the bytes the asset has now; false lets it follow the asset again.
     """

    project: str
    asset: UUID
    pinned: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        asset = str(self.asset)

        pinned = self.pinned


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "asset": asset,
            "pinned": pinned,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        asset = UUID(d.pop("asset"))




        pinned = d.pop("pinned")

        project_reference_request = cls(
            project=project,
            asset=asset,
            pinned=pinned,
        )


        project_reference_request.additional_properties = d
        return project_reference_request

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
