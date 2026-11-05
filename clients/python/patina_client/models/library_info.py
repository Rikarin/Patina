from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID






T = TypeVar("T", bound="LibraryInfo")



@_attrs_define
class LibraryInfo:
    """ The reply to `GET /api/v1/library`: what opening a library reads, and nothing more.

        Attributes:
            id (UUID): The library's identity, from `library.yaml`.
            name (str): The library's display name.
            path (str): The library's folder on the Server's machine.
            schema_version (int): The version of `library.yaml` as it stands on disk.
            texel_density (float): The texel density new projects start from, in texels per metre.
            channels (list[str]): The channels the library authors.
            default_shader (str): The shader new projects start from.
            export_targets (list[str]): The export targets the library builds for.
            project_count (int): The projects in the index.
            asset_count (int): The shared assets in the index.
            indexing (bool): Whether a rescan is running; counts and pages may lag the files until it ends.
     """

    id: UUID
    name: str
    path: str
    schema_version: int
    texel_density: float
    channels: list[str]
    default_shader: str
    export_targets: list[str]
    project_count: int
    asset_count: int
    indexing: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = str(self.id)

        name = self.name

        path = self.path

        schema_version = self.schema_version

        texel_density = self.texel_density

        channels = self.channels



        default_shader = self.default_shader

        export_targets = self.export_targets



        project_count = self.project_count

        asset_count = self.asset_count

        indexing = self.indexing


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "schemaVersion": schema_version,
            "texelDensity": texel_density,
            "channels": channels,
            "defaultShader": default_shader,
            "exportTargets": export_targets,
            "projectCount": project_count,
            "assetCount": asset_count,
            "indexing": indexing,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        schema_version = d.pop("schemaVersion")

        texel_density = d.pop("texelDensity")

        channels = cast(list[str], d.pop("channels"))


        default_shader = d.pop("defaultShader")

        export_targets = cast(list[str], d.pop("exportTargets"))


        project_count = d.pop("projectCount")

        asset_count = d.pop("assetCount")

        indexing = d.pop("indexing")

        library_info = cls(
            id=id,
            name=name,
            path=path,
            schema_version=schema_version,
            texel_density=texel_density,
            channels=channels,
            default_shader=default_shader,
            export_targets=export_targets,
            project_count=project_count,
            asset_count=asset_count,
            indexing=indexing,
        )


        library_info.additional_properties = d
        return library_info

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
