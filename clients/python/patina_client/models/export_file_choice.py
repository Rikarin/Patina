from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ExportFileChoice")



@_attrs_define
class ExportFileChoice:
    """ One file of a preset changed for one export.

        Attributes:
            template (str): Which file: its naming template.
            enabled (bool): Whether it is written at all. Default: True.
            format_ (None | str | Unset): Another format for it, or null for the preset's.
     """

    template: str
    enabled: bool = True
    format_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        template = self.template

        enabled = self.enabled

        format_: None | str | Unset
        if isinstance(self.format_, Unset):
            format_ = UNSET
        else:
            format_ = self.format_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "template": template,
            "enabled": enabled,
        })
        if format_ is not UNSET:
            field_dict["format"] = format_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        template = d.pop("template")

        enabled = d.pop("enabled")

        def _parse_format_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        format_ = _parse_format_(d.pop("format", UNSET))


        export_file_choice = cls(
            template=template,
            enabled=enabled,
            format_=format_,
        )


        export_file_choice.additional_properties = d
        return export_file_choice

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
