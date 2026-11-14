from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackPaceChange")



@_attrs_define
class StackPaceChange:
    """ A change to how a stack is evaluated while it is edited (#208). What is left out stays as it is.

        Attributes:
            paused (bool | None | Unset): Whether edits wait to be evaluated.
            proxy (int | None | Unset): What the stack's sides are divided by: 1, 2, 4 or 8.
     """

    paused: bool | None | Unset = UNSET
    proxy: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        paused: bool | None | Unset
        if isinstance(self.paused, Unset):
            paused = UNSET
        else:
            paused = self.paused

        proxy: int | None | Unset
        if isinstance(self.proxy, Unset):
            proxy = UNSET
        else:
            proxy = self.proxy


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if paused is not UNSET:
            field_dict["paused"] = paused
        if proxy is not UNSET:
            field_dict["proxy"] = proxy

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        def _parse_paused(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        paused = _parse_paused(d.pop("paused", UNSET))


        def _parse_proxy(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        proxy = _parse_proxy(d.pop("proxy", UNSET))


        stack_pace_change = cls(
            paused=paused,
            proxy=proxy,
        )


        stack_pace_change.additional_properties = d
        return stack_pace_change

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
