from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="LibraryCleaned")



@_attrs_define
class LibraryCleaned:
    """ What `POST /api/v1/library/clean` took away (#162).

        Attributes:
            removed (int): How many folders of copies of moved paint were deleted.
            bytes_ (int): The room those freed on disk.
            pictures (int): How many pictures nothing read went to the bin.
            picture_bytes (int): The room those take, which emptying the bin frees.
            bin_ (None | str | Unset): The bin's entry that holds those pictures, for a Restore, or null when none went.
     """

    removed: int
    bytes_: int
    pictures: int
    picture_bytes: int
    bin_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        removed = self.removed

        bytes_ = self.bytes_

        pictures = self.pictures

        picture_bytes = self.picture_bytes

        bin_: None | str | Unset
        if isinstance(self.bin_, Unset):
            bin_ = UNSET
        else:
            bin_ = self.bin_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "removed": removed,
            "bytes": bytes_,
            "pictures": pictures,
            "pictureBytes": picture_bytes,
        })
        if bin_ is not UNSET:
            field_dict["bin"] = bin_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        removed = d.pop("removed")

        bytes_ = d.pop("bytes")

        pictures = d.pop("pictures")

        picture_bytes = d.pop("pictureBytes")

        def _parse_bin_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        bin_ = _parse_bin_(d.pop("bin", UNSET))


        library_cleaned = cls(
            removed=removed,
            bytes_=bytes_,
            pictures=pictures,
            picture_bytes=picture_bytes,
            bin_=bin_,
        )


        library_cleaned.additional_properties = d
        return library_cleaned

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
