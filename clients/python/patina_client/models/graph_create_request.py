from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphCreateRequest")



@_attrs_define
class GraphCreateRequest:
    """ The body of `POST /api/v1/library/graphs`.

        Attributes:
            name (str): The graph's name. The Server makes its file name from it.
            project (None | str | Unset): A project's path below `projects/` to put it in, or null for a new project of its
                own.
            resolution (int | None | Unset): The side it evaluates at. Null is 2048. Its authoring base does not depend on
                this.
            template (None | str | Unset): What it starts from: null or `empty` for nothing, `outputs` for an Output node
                per material channel, or a
                starter preset by name, as `GET /api/v1/library/graph-templates` lists them.
     """

    name: str
    project: None | str | Unset = UNSET
    resolution: int | None | Unset = UNSET
    template: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        project: None | str | Unset
        if isinstance(self.project, Unset):
            project = UNSET
        else:
            project = self.project

        resolution: int | None | Unset
        if isinstance(self.resolution, Unset):
            resolution = UNSET
        else:
            resolution = self.resolution

        template: None | str | Unset
        if isinstance(self.template, Unset):
            template = UNSET
        else:
            template = self.template


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
        })
        if project is not UNSET:
            field_dict["project"] = project
        if resolution is not UNSET:
            field_dict["resolution"] = resolution
        if template is not UNSET:
            field_dict["template"] = template

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        def _parse_project(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        project = _parse_project(d.pop("project", UNSET))


        def _parse_resolution(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        resolution = _parse_resolution(d.pop("resolution", UNSET))


        def _parse_template(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        template = _parse_template(d.pop("template", UNSET))


        graph_create_request = cls(
            name=name,
            project=project,
            resolution=resolution,
            template=template,
        )


        graph_create_request.additional_properties = d
        return graph_create_request

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
