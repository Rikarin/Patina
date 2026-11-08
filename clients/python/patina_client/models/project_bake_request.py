from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_bake_settings import ProjectBakeSettings





T = TypeVar("T", bound="ProjectBakeRequest")



@_attrs_define
class ProjectBakeRequest:
    """ The body of `PUT /api/v1/library/project/bake`.

        Attributes:
            project (str): The project, below `projects/`.
            bake (ProjectBakeSettings): What a project keeps about baking, in its own file: the options, which texture sets
                to bake, and the files
                beyond the project's mesh that a bake reads.
     """

    project: str
    bake: ProjectBakeSettings
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_bake_settings import ProjectBakeSettings # noqa: PLC0415
        project = self.project

        bake = self.bake.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "bake": bake,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_bake_settings import ProjectBakeSettings # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        bake = ProjectBakeSettings.from_dict(d.pop("bake"))




        project_bake_request = cls(
            project=project,
            bake=bake,
        )


        project_bake_request.additional_properties = d
        return project_bake_request

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
