from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackPace")



@_attrs_define
class StackPace:
    """ How a stack is evaluated while it is edited, which is the session's and never the file's (#208). Saving,
    exporting, checking and reading pixels always evaluate it whole, at full size, paused or not.

        Attributes:
            paused (bool): Whether edits wait to be evaluated, so that a big stack can be set up without paying for each
                step. Resuming
                evaluates everything at once. Default: False.
            proxy (int): What the stack's width and height are divided by for the viewport and previews while it is edited:
                1, 2, 4
                or 8. Painted pixels stay at full size; only what they are composited into is smaller. Default: 1.
     """

    paused: bool = False
    proxy: int = 1
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        paused = self.paused

        proxy = self.proxy


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "paused": paused,
            "proxy": proxy,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        paused = d.pop("paused")

        proxy = d.pop("proxy")

        stack_pace = cls(
            paused=paused,
            proxy=proxy,
        )


        stack_pace.additional_properties = d
        return stack_pace

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
