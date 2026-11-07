from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID






T = TypeVar("T", bound="ProjectDocumentInfo")



@_attrs_define
class ProjectDocumentInfo:
    """ Something in a project that can be opened and edited (ADR 0012).

        Attributes:
            kind (str): `texturing`, `layered-material`, `image-material` or `graph`.
            name (str): Its display name.
            path (str): Its path below the library, which the graph and stack routes take.
            id (None | Unset | UUID): Its GUID. Null until the project lists it.
            mesh (None | Unset | UUID): The id of the mesh it is on.
            role (None | str | Unset): What a graph is for: `material`, `mask`, `brush` or `filter`.
     """

    kind: str
    name: str
    path: str
    id: None | Unset | UUID = UNSET
    mesh: None | Unset | UUID = UNSET
    role: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        name = self.name

        path = self.path

        id: None | str | Unset
        if isinstance(self.id, Unset):
            id = UNSET
        elif isinstance(self.id, UUID):
            id = str(self.id)
        else:
            id = self.id

        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        elif isinstance(self.mesh, UUID):
            mesh = str(self.mesh)
        else:
            mesh = self.mesh

        role: None | str | Unset
        if isinstance(self.role, Unset):
            role = UNSET
        else:
            role = self.role


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "name": name,
            "path": path,
        })
        if id is not UNSET:
            field_dict["id"] = id
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if role is not UNSET:
            field_dict["role"] = role

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        name = d.pop("name")

        path = d.pop("path")

        def _parse_id(data: object) -> None | Unset | UUID:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, str):
                    raise TypeError()
                id_type_1 = UUID(data)



                return id_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | UUID, data)

        id = _parse_id(d.pop("id", UNSET))


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


        def _parse_role(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        role = _parse_role(d.pop("role", UNSET))


        project_document_info = cls(
            kind=kind,
            name=name,
            path=path,
            id=id,
            mesh=mesh,
            role=role,
        )


        project_document_info.additional_properties = d
        return project_document_info

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
