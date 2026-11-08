from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="ExportPresetFile")



@_attrs_define
class ExportPresetFile:
    """ One file of a preset, as a client shows it.

        Attributes:
            template (str): Its naming template.
            format_ (str): Its format: one of IReadOnlyList&lt;ExportFormatInfo&gt; ExportPresetList.Formats.
            compression (str): What is inside a KTX2 or DDS file, or none.
            space (str): srgb or linear.
            mips (bool): Whether it carries a mip chain.
            maps (list[str]): The maps it reads.
            lanes (int): How many lanes it has, which decides the formats it can change to. Default: 4.
            quality (str): For JPEG, 1 to 100; for block compression, fast, balanced or best. Default: 'balanced'.
     """

    template: str
    format_: str
    compression: str
    space: str
    mips: bool
    maps: list[str]
    lanes: int = 4
    quality: str = 'balanced'
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        template = self.template

        format_ = self.format_

        compression = self.compression

        space = self.space

        mips = self.mips

        maps = self.maps



        lanes = self.lanes

        quality = self.quality


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "template": template,
            "format": format_,
            "compression": compression,
            "space": space,
            "mips": mips,
            "maps": maps,
            "lanes": lanes,
            "quality": quality,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        template = d.pop("template")

        format_ = d.pop("format")

        compression = d.pop("compression")

        space = d.pop("space")

        mips = d.pop("mips")

        maps = cast(list[str], d.pop("maps"))


        lanes = d.pop("lanes")

        quality = d.pop("quality")

        export_preset_file = cls(
            template=template,
            format_=format_,
            compression=compression,
            space=space,
            mips=mips,
            maps=maps,
            lanes=lanes,
            quality=quality,
        )


        export_preset_file.additional_properties = d
        return export_preset_file

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
