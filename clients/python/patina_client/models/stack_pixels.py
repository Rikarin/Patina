from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackPixels")



@_attrs_define
class StackPixels:
    """ A digest of a map's pixels, so that two pictures can be compared with neither crossing the API.

        Attributes:
            usage (str): Which channel.
            width (int): Its width in pixels.
            height (int): Its height in pixels.
            digest (str): SHA-256 of its pixels as 8-bit RGBA, row by row.
            file (None | str | Unset): Where it was written, for an export.
     """

    usage: str
    width: int
    height: int
    digest: str
    file: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        usage = self.usage

        width = self.width

        height = self.height

        digest = self.digest

        file: None | str | Unset
        if isinstance(self.file, Unset):
            file = UNSET
        else:
            file = self.file


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "usage": usage,
            "width": width,
            "height": height,
            "digest": digest,
        })
        if file is not UNSET:
            field_dict["file"] = file

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        usage = d.pop("usage")

        width = d.pop("width")

        height = d.pop("height")

        digest = d.pop("digest")

        def _parse_file(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        file = _parse_file(d.pop("file", UNSET))


        stack_pixels = cls(
            usage=usage,
            width=width,
            height=height,
            digest=digest,
            file=file,
        )


        stack_pixels.additional_properties = d
        return stack_pixels

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
