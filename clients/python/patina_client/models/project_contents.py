from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.project_document_info import ProjectDocumentInfo
  from ..models.project_file import ProjectFile
  from ..models.project_mesh_info import ProjectMeshInfo





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
            preserve_paint (bool): Whether paint moves to a new mesh's layout.
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
    preserve_paint: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_document_info import ProjectDocumentInfo # noqa: PLC0415
        from ..models.project_file import ProjectFile # noqa: PLC0415
        from ..models.project_mesh_info import ProjectMeshInfo # noqa: PLC0415
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

        preserve_paint = self.preserve_paint


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
            "preservePaint": preserve_paint,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_document_info import ProjectDocumentInfo # noqa: PLC0415
        from ..models.project_file import ProjectFile # noqa: PLC0415
        from ..models.project_mesh_info import ProjectMeshInfo # noqa: PLC0415
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

        preserve_paint = d.pop("preservePaint")

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
            preserve_paint=preserve_paint,
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
