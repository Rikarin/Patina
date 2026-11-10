from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="ExportFileSetting")



@_attrs_define
class ExportFileSetting:
    """ One preset file as a project's export last had it.

        Attributes:
            preset (str): The preset it is a file of.
            template (str): Which file: its naming template.
            enabled (bool): Whether it is written.
            format_ (str): The format it is written in.
            compression (None | str | Unset): What a KTX2 or DDS file holds, or null for the preset's.
            quality (None | str | Unset): For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null for the
                preset's.
            mips (bool | None | Unset): Whether a KTX2 or DDS file carries a mip chain, or null for the preset's.
     """

    preset: str
    template: str
    enabled: bool
    format_: str
    compression: None | str | Unset = UNSET
    quality: None | str | Unset = UNSET
    mips: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset = self.preset

        template = self.template

        enabled = self.enabled

        format_ = self.format_

        compression: None | str | Unset
        if isinstance(self.compression, Unset):
            compression = UNSET
        else:
            compression = self.compression

        quality: None | str | Unset
        if isinstance(self.quality, Unset):
            quality = UNSET
        else:
            quality = self.quality

        mips: bool | None | Unset
        if isinstance(self.mips, Unset):
            mips = UNSET
        else:
            mips = self.mips


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
            "template": template,
            "enabled": enabled,
            "format": format_,
        })
        if compression is not UNSET:
            field_dict["compression"] = compression
        if quality is not UNSET:
            field_dict["quality"] = quality
        if mips is not UNSET:
            field_dict["mips"] = mips

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        preset = d.pop("preset")

        template = d.pop("template")

        enabled = d.pop("enabled")

        format_ = d.pop("format")

        def _parse_compression(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        compression = _parse_compression(d.pop("compression", UNSET))


        def _parse_quality(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        quality = _parse_quality(d.pop("quality", UNSET))


        def _parse_mips(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        mips = _parse_mips(d.pop("mips", UNSET))


        export_file_setting = cls(
            preset=preset,
            template=template,
            enabled=enabled,
            format_=format_,
            compression=compression,
            quality=quality,
            mips=mips,
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
