from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="LibraryTargetsRequest")



@_attrs_define
class LibraryTargetsRequest:
    """ The body of `PUT /api/v1/library/export/targets` (#207).

        Attributes:
            presets (list[str]): The ids of the presets a build of the library writes when its request names none, such as
                one for each platform
                the library ships to. Empty, such a build writes nothing.
     """

    presets: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        presets = self.presets




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "presets": presets,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        presets = cast(list[str], d.pop("presets"))


        library_targets_request = cls(
            presets=presets,
        )


        library_targets_request.additional_properties = d
        return library_targets_request

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
