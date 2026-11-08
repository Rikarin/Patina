from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_export_settings import ProjectExportSettings





T = TypeVar("T", bound="ProjectExportRequest")



@_attrs_define
class ProjectExportRequest:
    """ The body of `PUT /api/v1/library/project/export`.

        Attributes:
            project (str): The project, below `projects/`.
            export (ProjectExportSettings): A project's export settings, kept in its `project.yaml` so they travel with the
                library: what a client's
                export starts from next time. Any client reads and writes them; a build reads only what a request sends, so
                keeping them changes no key.
     """

    project: str
    export: ProjectExportSettings
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_export_settings import ProjectExportSettings # noqa: PLC0415
        project = self.project

        export = self.export.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "export": export,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_export_settings import ProjectExportSettings # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        export = ProjectExportSettings.from_dict(d.pop("export"))




        project_export_request = cls(
            project=project,
            export=export,
        )


        project_export_request.additional_properties = d
        return project_export_request

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
