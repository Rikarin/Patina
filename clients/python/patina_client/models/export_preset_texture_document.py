from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_padding_info import ExportPaddingInfo





T = TypeVar("T", bound="ExportPresetTextureDocument")



@_attrs_define
class ExportPresetTextureDocument:
    """ One file of a preset as its file says it, for a client that edits presets (ADR 0019). Every field is the
    preset language's own spelling, so what a client sends is what the file holds.

        Attributes:
            file (str): Its naming template, without an extension.
            format_ (str): Its format: one of IReadOnlyList&lt;ExportFormatInfo&gt; ExportPresetList.Formats.
            space (str): srgb or linear.
            compression (str): What a KTX2 or DDS file holds: none, half, float or bc1 to bc7.
            mips (bool): Whether a KTX2 or DDS file carries a mip chain.
            scale (float): Its side as a share of every other file's: 1, 0.5, 0.25, 0.125 or 0.0625.
            channels (list[str]): One to four lanes, in file order, each as the language writes one: `roughness.r`,
                `1-roughness.r` or a number such as `0.5`.
            quality (None | str | Unset): For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null for the
                format's default.
            dither (bool | None | Unset): Whether this file is dithered, or null for what the preset says.
            padding (ExportPaddingInfo | None | Unset):
     """

    file: str
    format_: str
    space: str
    compression: str
    mips: bool
    scale: float
    channels: list[str]
    quality: None | str | Unset = UNSET
    dither: bool | None | Unset = UNSET
    padding: ExportPaddingInfo | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        file = self.file

        format_ = self.format_

        space = self.space

        compression = self.compression

        mips = self.mips

        scale = self.scale

        channels = self.channels



        quality: None | str | Unset
        if isinstance(self.quality, Unset):
            quality = UNSET
        else:
            quality = self.quality

        dither: bool | None | Unset
        if isinstance(self.dither, Unset):
            dither = UNSET
        else:
            dither = self.dither

        padding: dict[str, Any] | None | Unset
        if isinstance(self.padding, Unset):
            padding = UNSET
        elif isinstance(self.padding, ExportPaddingInfo):
            padding = self.padding.to_dict()
        else:
            padding = self.padding


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "file": file,
            "format": format_,
            "space": space,
            "compression": compression,
            "mips": mips,
            "scale": scale,
            "channels": channels,
        })
        if quality is not UNSET:
            field_dict["quality"] = quality
        if dither is not UNSET:
            field_dict["dither"] = dither
        if padding is not UNSET:
            field_dict["padding"] = padding

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        d = dict(src_dict)
        file = d.pop("file")

        format_ = d.pop("format")

        space = d.pop("space")

        compression = d.pop("compression")

        mips = d.pop("mips")

        scale = d.pop("scale")

        channels = cast(list[str], d.pop("channels"))


        def _parse_quality(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        quality = _parse_quality(d.pop("quality", UNSET))


        def _parse_dither(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        dither = _parse_dither(d.pop("dither", UNSET))


        def _parse_padding(data: object) -> ExportPaddingInfo | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                padding_type_1 = ExportPaddingInfo.from_dict(data)



                return padding_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(ExportPaddingInfo | None | Unset, data)

        padding = _parse_padding(d.pop("padding", UNSET))


        export_preset_texture_document = cls(
            file=file,
            format_=format_,
            space=space,
            compression=compression,
            mips=mips,
            scale=scale,
            channels=channels,
            quality=quality,
            dither=dither,
            padding=padding,
        )


        export_preset_texture_document.additional_properties = d
        return export_preset_texture_document

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
