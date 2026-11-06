from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ProjectMeshRequest")



@_attrs_define
class ProjectMeshRequest:
    """ The body of `POST /api/v1/library/project/meshes`.

        Attributes:
            project (str): The project, below `projects/`.
            mesh (str): A mesh file on the Server's machine.
            high (None | str | Unset): The detailed mesh it bakes from.
     """

    project: str
    mesh: str
    high: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        mesh = self.mesh

        high: None | str | Unset
        if isinstance(self.high, Unset):
            high = UNSET
        else:
            high = self.high


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "mesh": mesh,
        })
        if high is not UNSET:
            field_dict["high"] = high

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        mesh = d.pop("mesh")

        def _parse_high(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        high = _parse_high(d.pop("high", UNSET))


        project_mesh_request = cls(
            project=project,
            mesh=mesh,
            high=high,
        )


        project_mesh_request.additional_properties = d
        return project_mesh_request

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
