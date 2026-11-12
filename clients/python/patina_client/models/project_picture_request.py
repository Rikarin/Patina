from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ProjectPictureRequest")



@_attrs_define
class ProjectPictureRequest:
    """ The body of `POST /api/v1/library/project/pictures`.

        Attributes:
            project (str): The project, below `projects/`.
            picture (str): A picture the library holds, by its path below the library, or a picture file on the Server's
                machine by its
                full path. One of another project is copied into this one, as one from the machine is.
     """

    project: str
    picture: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        project = self.project

        picture = self.picture


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "picture": picture,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        project = d.pop("project")

        picture = d.pop("picture")

        project_picture_request = cls(
            project=project,
            picture=picture,
        )


        project_picture_request.additional_properties = d
        return project_picture_request

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
