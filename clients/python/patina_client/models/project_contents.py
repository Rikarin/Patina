from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.project_bake_settings import ProjectBakeSettings
  from ..models.project_document_info import ProjectDocumentInfo
  from ..models.project_export_settings import ProjectExportSettings
  from ..models.project_file import ProjectFile
  from ..models.project_mesh_info import ProjectMeshInfo
  from ..models.project_paint_settings import ProjectPaintSettings
  from ..models.unwrap_options import UnwrapOptions
  from ..models.viewport_display import ViewportDisplay





T = TypeVar("T", bound="ProjectContents")



@_attrs_define
class ProjectContents:
    """ A project as it opens.

        Attributes:
            id (UUID): The project's GUID.
            name (str): Its display name.
            path (str): Its path below `projects/`.
            meshes (list[ProjectMeshInfo]): Its meshes.
            documents (list[ProjectDocumentInfo]): What can be opened in it.
            bake_folder (str): Where its bakes go, below the library.
            bakes (list[ProjectFile]): The bakes it has.
            pictures (list[ProjectFile]): The pictures brought in for layers and masks.
            auto_unwrap (bool): Whether a mesh with no UV layout is given one when it is set.
            export_sets (list[str]): The texture sets an export writes, by name: what IReadOnlyList&lt;string&gt;?
                ExportOptions.Sets chooses from. Empty for a
                project of one unnamed set.
            preserve_paint (bool): Whether paint moves to a new mesh's layout.
            unwrap (UnwrapOptions): How Auto Unwrap lays out a mesh. Each texture set gets an atlas of its own.
            bake (ProjectBakeSettings): What a project keeps about baking, in its own file: the options, which texture sets
                to bake, and the files
                beyond the project's mesh that a bake reads.
            export (None | ProjectExportSettings | Unset):
            viewport (None | Unset | ViewportDisplay):
            paint (None | ProjectPaintSettings | Unset):
     """

    id: UUID
    name: str
    path: str
    meshes: list[ProjectMeshInfo]
    documents: list[ProjectDocumentInfo]
    bake_folder: str
    bakes: list[ProjectFile]
    pictures: list[ProjectFile]
    auto_unwrap: bool
    export_sets: list[str]
    preserve_paint: bool
    unwrap: UnwrapOptions
    bake: ProjectBakeSettings
    export: None | ProjectExportSettings | Unset = UNSET
    viewport: None | Unset | ViewportDisplay = UNSET
    paint: None | ProjectPaintSettings | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_bake_settings import ProjectBakeSettings # noqa: PLC0415
        from ..models.project_document_info import ProjectDocumentInfo # noqa: PLC0415
        from ..models.project_export_settings import ProjectExportSettings # noqa: PLC0415
        from ..models.project_file import ProjectFile # noqa: PLC0415
        from ..models.project_mesh_info import ProjectMeshInfo # noqa: PLC0415
        from ..models.project_paint_settings import ProjectPaintSettings # noqa: PLC0415
        from ..models.unwrap_options import UnwrapOptions # noqa: PLC0415
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        id = str(self.id)

        name = self.name

        path = self.path

        meshes = []
        for meshes_item_data in self.meshes:
            meshes_item = meshes_item_data.to_dict()
            meshes.append(meshes_item)



        documents = []
        for documents_item_data in self.documents:
            documents_item = documents_item_data.to_dict()
            documents.append(documents_item)



        bake_folder = self.bake_folder

        bakes = []
        for bakes_item_data in self.bakes:
            bakes_item = bakes_item_data.to_dict()
            bakes.append(bakes_item)



        pictures = []
        for pictures_item_data in self.pictures:
            pictures_item = pictures_item_data.to_dict()
            pictures.append(pictures_item)



        auto_unwrap = self.auto_unwrap

        export_sets = self.export_sets



        preserve_paint = self.preserve_paint

        unwrap = self.unwrap.to_dict()

        bake = self.bake.to_dict()

        export: dict[str, Any] | None | Unset
        if isinstance(self.export, Unset):
            export = UNSET
        elif isinstance(self.export, ProjectExportSettings):
            export = self.export.to_dict()
        else:
            export = self.export

        viewport: dict[str, Any] | None | Unset
        if isinstance(self.viewport, Unset):
            viewport = UNSET
        elif isinstance(self.viewport, ViewportDisplay):
            viewport = self.viewport.to_dict()
        else:
            viewport = self.viewport

        paint: dict[str, Any] | None | Unset
        if isinstance(self.paint, Unset):
            paint = UNSET
        elif isinstance(self.paint, ProjectPaintSettings):
            paint = self.paint.to_dict()
        else:
            paint = self.paint


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "meshes": meshes,
            "documents": documents,
            "bakeFolder": bake_folder,
            "bakes": bakes,
            "pictures": pictures,
            "autoUnwrap": auto_unwrap,
            "exportSets": export_sets,
            "preservePaint": preserve_paint,
            "unwrap": unwrap,
            "bake": bake,
        })
        if export is not UNSET:
            field_dict["export"] = export
        if viewport is not UNSET:
            field_dict["viewport"] = viewport
        if paint is not UNSET:
            field_dict["paint"] = paint

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_bake_settings import ProjectBakeSettings # noqa: PLC0415
        from ..models.project_document_info import ProjectDocumentInfo # noqa: PLC0415
        from ..models.project_export_settings import ProjectExportSettings # noqa: PLC0415
        from ..models.project_file import ProjectFile # noqa: PLC0415
        from ..models.project_mesh_info import ProjectMeshInfo # noqa: PLC0415
        from ..models.project_paint_settings import ProjectPaintSettings # noqa: PLC0415
        from ..models.unwrap_options import UnwrapOptions # noqa: PLC0415
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        meshes = []
        _meshes = d.pop("meshes")
        for meshes_item_data in (_meshes):
            meshes_item = ProjectMeshInfo.from_dict(meshes_item_data)



            meshes.append(meshes_item)


        documents = []
        _documents = d.pop("documents")
        for documents_item_data in (_documents):
            documents_item = ProjectDocumentInfo.from_dict(documents_item_data)



            documents.append(documents_item)


        bake_folder = d.pop("bakeFolder")

        bakes = []
        _bakes = d.pop("bakes")
        for bakes_item_data in (_bakes):
            bakes_item = ProjectFile.from_dict(bakes_item_data)



            bakes.append(bakes_item)


        pictures = []
        _pictures = d.pop("pictures")
        for pictures_item_data in (_pictures):
            pictures_item = ProjectFile.from_dict(pictures_item_data)



            pictures.append(pictures_item)


        auto_unwrap = d.pop("autoUnwrap")

        export_sets = cast(list[str], d.pop("exportSets"))


        preserve_paint = d.pop("preservePaint")

        unwrap = UnwrapOptions.from_dict(d.pop("unwrap"))




        bake = ProjectBakeSettings.from_dict(d.pop("bake"))




        def _parse_export(data: object) -> None | ProjectExportSettings | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                export_type_1 = ProjectExportSettings.from_dict(data)



                return export_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | ProjectExportSettings | Unset, data)

        export = _parse_export(d.pop("export", UNSET))


        def _parse_viewport(data: object) -> None | Unset | ViewportDisplay:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                viewport_type_1 = ViewportDisplay.from_dict(data)



                return viewport_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | ViewportDisplay, data)

        viewport = _parse_viewport(d.pop("viewport", UNSET))


        def _parse_paint(data: object) -> None | ProjectPaintSettings | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                paint_type_1 = ProjectPaintSettings.from_dict(data)



                return paint_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | ProjectPaintSettings | Unset, data)

        paint = _parse_paint(d.pop("paint", UNSET))


        project_contents = cls(
            id=id,
            name=name,
            path=path,
            meshes=meshes,
            documents=documents,
            bake_folder=bake_folder,
            bakes=bakes,
            pictures=pictures,
            auto_unwrap=auto_unwrap,
            export_sets=export_sets,
            preserve_paint=preserve_paint,
            unwrap=unwrap,
            bake=bake,
            export=export,
            viewport=viewport,
            paint=paint,
        )


        project_contents.additional_properties = d
        return project_contents

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
