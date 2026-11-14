from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackQuickMaskRequest")



@_attrs_define
class StackQuickMaskRequest:
    """ Shows or hides a stack's quick mask in the viewport, or clears it (#127).

        Attributes:
            shown (bool): Whether the viewport lays the quick mask over the model in red.
            clear (bool): Whether to start a new, empty quick mask. Not an undo step: strokes kept to the old one keep what
                they painted,
                and undoing a stroke on the old one changes nothing a later stroke reads. Default: False.
     """

    shown: bool
    clear: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        shown = self.shown

        clear = self.clear


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "shown": shown,
            "clear": clear,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        shown = d.pop("shown")

        clear = d.pop("clear")

        stack_quick_mask_request = cls(
            shown=shown,
            clear=clear,
        )


        stack_quick_mask_request.additional_properties = d
        return stack_quick_mask_request

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
