from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from uuid import UUID






T = TypeVar("T", bound="BuildPreviewProject")



@_attrs_define
class BuildPreviewProject:
    """ What a build would do for one project.

        Attributes:
            id (UUID): The project.
            name (str): Its name.
            path (str): Its path below `projects/`.
            outputs (int): The outputs that would be made.
            exports (int): The exports that would be written.
     """

    id: UUID
    name: str
    path: str
    outputs: int
    exports: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = str(self.id)

        name = self.name

        path = self.path

        outputs = self.outputs

        exports = self.exports


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "outputs": outputs,
            "exports": exports,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        outputs = d.pop("outputs")

        exports = d.pop("exports")

        build_preview_project = cls(
            id=id,
            name=name,
            path=path,
            outputs=outputs,
            exports=exports,
        )


        build_preview_project.additional_properties = d
        return build_preview_project

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
