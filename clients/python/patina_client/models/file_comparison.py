from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="FileComparison")



@_attrs_define
class FileComparison:
    """ One exported file in two builds.

        Attributes:
            preset (str): The preset that wrote it.
            file (str): Where it is, below the export's folder, with forward slashes.
            change (str): One of BuildChanges.
     """

    preset: str
    file: str
    change: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        preset = self.preset

        file = self.file

        change = self.change


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "preset": preset,
            "file": file,
            "change": change,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        preset = d.pop("preset")

        file = d.pop("file")

        change = d.pop("change")

        file_comparison = cls(
            preset=preset,
            file=file,
            change=change,
        )


        file_comparison.additional_properties = d
        return file_comparison

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
