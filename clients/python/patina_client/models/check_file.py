from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="CheckFile")



@_attrs_define
class CheckFile:
    """ One file an export writes for a texture set, and what it takes on a GPU (#159).

        Attributes:
            file (str): Its name as the export writes it, with its extension.
            format_ (str): Its format, one of the export formats.
            compression (str): What its blocks hold, or `none`.
            width (int): Its width, in texels, as it is written.
            height (int): Its height.
            mips (bool): Whether it carries a mip chain.
            bytes_ (int): The GPU memory it takes, its mips included.
     """

    file: str
    format_: str
    compression: str
    width: int
    height: int
    mips: bool
    bytes_: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        file = self.file

        format_ = self.format_

        compression = self.compression

        width = self.width

        height = self.height

        mips = self.mips

        bytes_ = self.bytes_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "file": file,
            "format": format_,
            "compression": compression,
            "width": width,
            "height": height,
            "mips": mips,
            "bytes": bytes_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        file = d.pop("file")

        format_ = d.pop("format")

        compression = d.pop("compression")

        width = d.pop("width")

        height = d.pop("height")

        mips = d.pop("mips")

        bytes_ = d.pop("bytes")

        check_file = cls(
            file=file,
            format_=format_,
            compression=compression,
            width=width,
            height=height,
            mips=mips,
            bytes_=bytes_,
        )


        check_file.additional_properties = d
        return check_file

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
