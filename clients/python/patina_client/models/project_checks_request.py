from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_check_settings import ProjectCheckSettings





T = TypeVar("T", bound="ProjectChecksRequest")



@_attrs_define
class ProjectChecksRequest:
    """ The body of `PUT /api/v1/library/project/checks`.

        Attributes:
            project (str): The project, below `projects/`.
            checks (ProjectCheckSettings): What a project holds its texture sets to before they are exported (#159), kept in
                its `project.yaml`. A
                zero is no limit, and a texel density of zero is the library's.
     """

    project: str
    checks: ProjectCheckSettings
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_check_settings import ProjectCheckSettings # noqa: PLC0415
        project = self.project

        checks = self.checks.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "checks": checks,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_check_settings import ProjectCheckSettings # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        checks = ProjectCheckSettings.from_dict(d.pop("checks"))




        project_checks_request = cls(
            project=project,
            checks=checks,
        )


        project_checks_request.additional_properties = d
        return project_checks_request

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
