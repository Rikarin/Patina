from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ProjectContentsRequest")



@_attrs_define
class ProjectContentsRequest:
    """ The body of `PUT /api/v1/library/project`: gives a project what it lacks.

        Attributes:
            path (str): The project, below `projects/`.
            mesh (None | str | Unset): A mesh file on the Server's machine to use. Null leaves the mesh as it is.
            resolution (int | None | Unset): The width and height of a stack it does not have yet. Null is 2048.
     """

    path: str
    mesh: None | str | Unset = UNSET
    resolution: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        path = self.path

        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        else:
            mesh = self.mesh

        resolution: int | None | Unset
        if isinstance(self.resolution, Unset):
            resolution = UNSET
        else:
            resolution = self.resolution


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if resolution is not UNSET:
            field_dict["resolution"] = resolution

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        path = d.pop("path")

        def _parse_mesh(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mesh = _parse_mesh(d.pop("mesh", UNSET))


        def _parse_resolution(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        resolution = _parse_resolution(d.pop("resolution", UNSET))


        project_contents_request = cls(
            path=path,
            mesh=mesh,
            resolution=resolution,
        )


        project_contents_request.additional_properties = d
        return project_contents_request

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
