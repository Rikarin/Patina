from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackExportRequest")



@_attrs_define
class StackExportRequest:
    """ Writes each channel of a stack to a file.

        Attributes:
            folder (str): Where, absolute or relative to the library.
            layer (None | str | Unset): One layer to write on its own, such as a flattened folder, composited alone over the
                channels' defaults, or
                null for the whole stack. Its files are named after it.
            format_ (None | str | Unset): The file format of every map, as `GET /export/presets` lists them, or null for
                8-bit RGBA PNGs, which
                hold exactly the pixels string StackPixels.Digest is of. A map keeps its own lanes where the format
                holds them, and otherwise the nearest it does: a JPEG of a color with alpha drops the alpha.
            compression (None | str | Unset): What a KTX2 or DDS file holds: none, half, float or bc1 to bc7. Null is none.
            quality (None | str | Unset): For JPEG, 1 to 100; for BC6H and BC7, fast, balanced or best. Null is the format's
                default.
            mips (bool | None | Unset): Whether a KTX2 or DDS file carries a mip chain.
            mask (bool | None | Unset): With string? StackExportRequest.Layer, whether to write the layer's mask alone
                rather than the layer: one map,
                `mask`, white where the layer shows, with the mask's own layers and effects, even while it is off.
     """

    folder: str
    layer: None | str | Unset = UNSET
    format_: None | str | Unset = UNSET
    compression: None | str | Unset = UNSET
    quality: None | str | Unset = UNSET
    mips: bool | None | Unset = UNSET
    mask: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        folder = self.folder

        layer: None | str | Unset
        if isinstance(self.layer, Unset):
            layer = UNSET
        else:
            layer = self.layer

        format_: None | str | Unset
        if isinstance(self.format_, Unset):
            format_ = UNSET
        else:
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

        mask: bool | None | Unset
        if isinstance(self.mask, Unset):
            mask = UNSET
        else:
            mask = self.mask


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "folder": folder,
        })
        if layer is not UNSET:
            field_dict["layer"] = layer
        if format_ is not UNSET:
            field_dict["format"] = format_
        if compression is not UNSET:
            field_dict["compression"] = compression
        if quality is not UNSET:
            field_dict["quality"] = quality
        if mips is not UNSET:
            field_dict["mips"] = mips
        if mask is not UNSET:
            field_dict["mask"] = mask

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        folder = d.pop("folder")

        def _parse_layer(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        layer = _parse_layer(d.pop("layer", UNSET))


        def _parse_format_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        format_ = _parse_format_(d.pop("format", UNSET))


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


        def _parse_mask(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        mask = _parse_mask(d.pop("mask", UNSET))


        stack_export_request = cls(
            folder=folder,
            layer=layer,
            format_=format_,
            compression=compression,
            quality=quality,
            mips=mips,
            mask=mask,
        )


        stack_export_request.additional_properties = d
        return stack_export_request

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
