from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID






T = TypeVar("T", bound="ProjectDocumentRequest")



@_attrs_define
class ProjectDocumentRequest:
    """ The body of `POST /api/v1/library/project/documents`.

        Attributes:
            project (str): The project, below `projects/`.
            kind (str): `texturing`, `layered-material` or `graph`.
            name (str): Its display name. Its file is made from it.
            mesh (None | Unset | UUID): The mesh a `texturing` document is on.
            resolution (int | None | Unset): The width and height of a stack, or the side a graph evaluates at. Null is
                2048.
            image (None | str | Unset): The picture an `image-material` starts from, on the Server's machine.
     """

    project: str
    kind: str
    name: str
    mesh: None | Unset | UUID = UNSET
    resolution: int | None | Unset = UNSET
    image: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        kind = self.kind

        name = self.name

        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        elif isinstance(self.mesh, UUID):
            mesh = str(self.mesh)
        else:
            mesh = self.mesh

        resolution: int | None | Unset
        if isinstance(self.resolution, Unset):
            resolution = UNSET
        else:
            resolution = self.resolution

        image: None | str | Unset
        if isinstance(self.image, Unset):
            image = UNSET
        else:
            image = self.image


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "kind": kind,
            "name": name,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if resolution is not UNSET:
            field_dict["resolution"] = resolution
        if image is not UNSET:
            field_dict["image"] = image

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        kind = d.pop("kind")

        name = d.pop("name")

        def _parse_mesh(data: object) -> None | Unset | UUID:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, str):
                    raise TypeError()
                mesh_type_1 = UUID(data)



                return mesh_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | UUID, data)

        mesh = _parse_mesh(d.pop("mesh", UNSET))


        def _parse_resolution(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        resolution = _parse_resolution(d.pop("resolution", UNSET))


        def _parse_image(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        image = _parse_image(d.pop("image", UNSET))


        project_document_request = cls(
            project=project,
            kind=kind,
            name=name,
            mesh=mesh,
            resolution=resolution,
            image=image,
        )


        project_document_request.additional_properties = d
        return project_document_request

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
