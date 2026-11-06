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
  from ..models.project_file import ProjectFile





T = TypeVar("T", bound="ProjectContents")



@_attrs_define
class ProjectContents:
    """ A project as it opens: what it paints, on what, and everything else in its folder.

        Attributes:
            id (UUID): The project's GUID.
            name (str): Its display name.
            path (str): Its path below `projects/`.
            bakes (str): Where its bakes go, below the library.
            files (list[ProjectFile]): The documents in its folder.
            mesh (None | str | Unset): Its mesh, below the library, or null before it has one.
            stack (None | str | Unset): Its layer stack, below the library, or null before it has one.
     """

    id: UUID
    name: str
    path: str
    bakes: str
    files: list[ProjectFile]
    mesh: None | str | Unset = UNSET
    stack: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_file import ProjectFile # noqa: PLC0415
        id = str(self.id)

        name = self.name

        path = self.path

        bakes = self.bakes

        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)



        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        else:
            mesh = self.mesh

        stack: None | str | Unset
        if isinstance(self.stack, Unset):
            stack = UNSET
        else:
            stack = self.stack


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "bakes": bakes,
            "files": files,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if stack is not UNSET:
            field_dict["stack"] = stack

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_file import ProjectFile # noqa: PLC0415
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        bakes = d.pop("bakes")

        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = ProjectFile.from_dict(files_item_data)



            files.append(files_item)


        def _parse_mesh(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mesh = _parse_mesh(d.pop("mesh", UNSET))


        def _parse_stack(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        stack = _parse_stack(d.pop("stack", UNSET))


        project_contents = cls(
            id=id,
            name=name,
            path=path,
            bakes=bakes,
            files=files,
            mesh=mesh,
            stack=stack,
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
