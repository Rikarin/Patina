from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackQuickMaskState")



@_attrs_define
class StackQuickMaskState:
    """ A stack's quick mask.

        Attributes:
            shown (bool): Whether the viewport lays it over the model.
            painted (bool): Whether a stroke that stands has painted it, so that a stroke kept to it is kept.
     """

    shown: bool
    painted: bool
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        shown = self.shown

        painted = self.painted


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "shown": shown,
            "painted": painted,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        shown = d.pop("shown")

        painted = d.pop("painted")

        stack_quick_mask_state = cls(
            shown=shown,
            painted=painted,
        )


        stack_quick_mask_state.additional_properties = d
        return stack_quick_mask_state

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
