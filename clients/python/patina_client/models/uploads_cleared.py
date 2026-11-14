from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="UploadsCleared")



@_attrs_define
class UploadsCleared:
    """ What `POST /api/v1/library/uploads/clear` took away (#297).

        Attributes:
            count (int): How many uploads nothing named went to the bin.
            bytes_ (int): The room those take, which emptying the bin frees.
            bin_ (None | str | Unset): The bin's entry that holds them, for a Restore, or null when none went.
     """

    count: int
    bytes_: int
    bin_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        count = self.count

        bytes_ = self.bytes_

        bin_: None | str | Unset
        if isinstance(self.bin_, Unset):
            bin_ = UNSET
        else:
            bin_ = self.bin_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "count": count,
            "bytes": bytes_,
        })
        if bin_ is not UNSET:
            field_dict["bin"] = bin_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        count = d.pop("count")

        bytes_ = d.pop("bytes")

        def _parse_bin_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        bin_ = _parse_bin_(d.pop("bin", UNSET))


        uploads_cleared = cls(
            count=count,
            bytes_=bytes_,
            bin_=bin_,
        )


        uploads_cleared.additional_properties = d
        return uploads_cleared

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
