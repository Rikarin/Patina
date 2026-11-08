from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.project_paint_settings import ProjectPaintSettings





T = TypeVar("T", bound="ProjectPaintRequest")



@_attrs_define
class ProjectPaintRequest:
    """ The body of `PUT /api/v1/library/project/paint`.

        Attributes:
            project (str): The project, below `projects/`.
            paint (ProjectPaintSettings): How a project's painting was left: the colours last used and kept, the brush and
                the symmetry. A project keeps
                it in its `project.yaml`, so it survives a restart and any client starts from it.
     """

    project: str
    paint: ProjectPaintSettings
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_paint_settings import ProjectPaintSettings # noqa: PLC0415
        project = self.project

        paint = self.paint.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "paint": paint,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_paint_settings import ProjectPaintSettings # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        paint = ProjectPaintSettings.from_dict(d.pop("paint"))




        project_paint_request = cls(
            project=project,
            paint=paint,
        )


        project_paint_request.additional_properties = d
        return project_paint_request

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
