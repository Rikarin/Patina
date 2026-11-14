from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.file_comparison import FileComparison
  from ..models.output_comparison import OutputComparison





T = TypeVar("T", bound="ProjectComparison")



@_attrs_define
class ProjectComparison:
    """ What differs in one project between two builds.

        Attributes:
            id (UUID): The project.
            name (str): Its name.
            path (str): Its path below `projects/`.
            touched (str): Which builds touched it: both, before or after. A build that leaves a project alone writes
                nothing of it, so a
                project only one build touched has its files listed as that build left them.
            outputs (list[OutputComparison]): Its outputs, with their keys in each build.
            files (list[FileComparison]): Its exported files, with their change.
     """

    id: UUID
    name: str
    path: str
    touched: str
    outputs: list[OutputComparison]
    files: list[FileComparison]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.file_comparison import FileComparison # noqa: PLC0415
        from ..models.output_comparison import OutputComparison # noqa: PLC0415
        id = str(self.id)

        name = self.name

        path = self.path

        touched = self.touched

        outputs = []
        for outputs_item_data in self.outputs:
            outputs_item = outputs_item_data.to_dict()
            outputs.append(outputs_item)



        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "touched": touched,
            "outputs": outputs,
            "files": files,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.file_comparison import FileComparison # noqa: PLC0415
        from ..models.output_comparison import OutputComparison # noqa: PLC0415
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        touched = d.pop("touched")

        outputs = []
        _outputs = d.pop("outputs")
        for outputs_item_data in (_outputs):
            outputs_item = OutputComparison.from_dict(outputs_item_data)



            outputs.append(outputs_item)


        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = FileComparison.from_dict(files_item_data)



            files.append(files_item)


        project_comparison = cls(
            id=id,
            name=name,
            path=path,
            touched=touched,
            outputs=outputs,
            files=files,
        )


        project_comparison.additional_properties = d
        return project_comparison

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
