from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="AutosavePreferences")



@_attrs_define
class AutosavePreferences:
    """ How unsaved work is kept. It goes to the library's cache, never over a document.

        Attributes:
            interval_seconds (int): How often open, unsaved documents are kept at the most. Zero is off. Default: 60.
            idle_seconds (int): How long after the last edit a document is kept, without waiting for the interval. Zero
                waits. Default: 5.
            keep (int): How many autosaves of a document are kept, the latest first. Restoring takes the latest. Default: 3.
     """

    interval_seconds: int = 60
    idle_seconds: int = 5
    keep: int = 3
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        interval_seconds = self.interval_seconds

        idle_seconds = self.idle_seconds

        keep = self.keep


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "intervalSeconds": interval_seconds,
            "idleSeconds": idle_seconds,
            "keep": keep,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        interval_seconds = d.pop("intervalSeconds")

        idle_seconds = d.pop("idleSeconds")

        keep = d.pop("keep")

        autosave_preferences = cls(
            interval_seconds=interval_seconds,
            idle_seconds=idle_seconds,
            keep=keep,
        )


        autosave_preferences.additional_properties = d
        return autosave_preferences

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
