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
            tags (list[str]): What it can be searched by, sorted.
            id (None | Unset | UUID): Its GUID. Null until the project lists it.
            mesh (None | Unset | UUID): The id of the mesh it is on.
            role (None | str | Unset): What a graph is for: `material`, `mask`, `brush` or `filter`.
            material (None | str | Unset): The material of the mesh it paints, as one of the project's texture sets.
            bake (None | str | Unset): The folder of the bake it reads, below the library, or null.
            bake_stale (None | str | Unset): Why that bake no longer fits it, so its maps need baking again, or null:
                `mesh`, made on a mesh it no
                longer stands on, `uvSet`, laid out in another UV set than the one it chose (#238), or `shape`,
                of another shape than its Width by Height (#262).
     """

    kind: str
    name: str
    path: str
    tags: list[str]
    id: None | Unset | UUID = UNSET
    mesh: None | Unset | UUID = UNSET
    role: None | str | Unset = UNSET
    material: None | str | Unset = UNSET
    bake: None | str | Unset = UNSET
    bake_stale: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        kind = self.kind

        name = self.name

        path = self.path

        tags = self.tags



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

        material: None | str | Unset
        if isinstance(self.material, Unset):
            material = UNSET
        else:
            material = self.material

        bake: None | str | Unset
        if isinstance(self.bake, Unset):
            bake = UNSET
        else:
            bake = self.bake

        bake_stale: None | str | Unset
        if isinstance(self.bake_stale, Unset):
            bake_stale = UNSET
        else:
            bake_stale = self.bake_stale


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "name": name,
            "path": path,
            "tags": tags,
        })
        if id is not UNSET:
            field_dict["id"] = id
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if role is not UNSET:
            field_dict["role"] = role
        if material is not UNSET:
            field_dict["material"] = material
        if bake is not UNSET:
            field_dict["bake"] = bake
        if bake_stale is not UNSET:
            field_dict["bakeStale"] = bake_stale

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        kind = d.pop("kind")

        name = d.pop("name")

        path = d.pop("path")

        tags = cast(list[str], d.pop("tags"))


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


        def _parse_material(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        material = _parse_material(d.pop("material", UNSET))


        def _parse_bake(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        bake = _parse_bake(d.pop("bake", UNSET))


        def _parse_bake_stale(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        bake_stale = _parse_bake_stale(d.pop("bakeStale", UNSET))


        project_document_info = cls(
            kind=kind,
            name=name,
            path=path,
            tags=tags,
            id=id,
            mesh=mesh,
            role=role,
            material=material,
            bake=bake,
            bake_stale=bake_stale,
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
