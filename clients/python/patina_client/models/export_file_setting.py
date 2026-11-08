from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="ExportFileSetting")



@_attrs_define
class ExportFileSetting:
    """ One preset file as a project's export last had it.

        Attributes:
            preset (str): The preset it is a file of.
            template (str): Which file: its naming template.
            enabled (bool): Whether it is written.
            format_ (str): The format it is written in.
     """

    preset: str
    template: str
    enabled: bool
    format_: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset = self.preset

        template = self.template

        enabled = self.enabled

        format_ = self.format_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
            "template": template,
            "enabled": enabled,
            "format": format_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        preset = d.pop("preset")

        template = d.pop("template")

        enabled = d.pop("enabled")

        format_ = d.pop("format")

        export_file_setting = cls(
            preset=preset,
            template=template,
            enabled=enabled,
            format_=format_,
        )


        export_file_setting.additional_properties = d
        return export_file_setting

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
