from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ProjectTemplateList")



@_attrs_define
class ProjectTemplateList:
    """ The project templates a library keeps (#144): a project's settings under a name, which a new project starts
    from through string? ProjectCreateRequest.Template. A template holds how Auto Unwrap and Preserve Paint
    are set and the export, bake, viewport, checks and paint settings, never a mesh or a document.

        Attributes:
            names (list[str]): Their names, sorted.
     """

    names: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        names = self.names




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "names": names,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        names = cast(list[str], d.pop("names"))


        project_template_list = cls(
            names=names,
        )


        project_template_list.additional_properties = d
        return project_template_list

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
