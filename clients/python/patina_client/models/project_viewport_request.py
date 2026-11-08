from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.viewport_display import ViewportDisplay





T = TypeVar("T", bound="ProjectViewportRequest")



@_attrs_define
class ProjectViewportRequest:
    """ The body of `PUT /api/v1/library/project/viewport`.

        Attributes:
            project (str): The project, below `projects/`.
            viewport (ViewportDisplay): How the viewport is lit, shown and finished. None of it reaches a file that is
                exported; a project keeps it, so
                it opens as it was left. Angles are radians and exposure is in stops; an effect at zero is off. A field left out
                of a request is the one the viewport opens with.
     """

    project: str
    viewport: ViewportDisplay
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        project = self.project

        viewport = self.viewport.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "viewport": viewport,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.viewport_display import ViewportDisplay # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        viewport = ViewportDisplay.from_dict(d.pop("viewport"))




        project_viewport_request = cls(
            project=project,
            viewport=viewport,
        )


        project_viewport_request.additional_properties = d
        return project_viewport_request

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
