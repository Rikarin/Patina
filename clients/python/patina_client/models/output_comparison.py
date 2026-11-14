from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="OutputComparison")



@_attrs_define
class OutputComparison:
    """ One output in two builds.

        Attributes:
            name (str): The output.
            change (str): One of BuildChanges: the same key is the same result.
            before (None | str | Unset): Its build key in the earlier build, or null when that build did not touch it.
            after (None | str | Unset): Its build key in the later build, or null.
     """

    name: str
    change: str
    before: None | str | Unset = UNSET
    after: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        name = self.name

        change = self.change

        before: None | str | Unset
        if isinstance(self.before, Unset):
            before = UNSET
        else:
            before = self.before

        after: None | str | Unset
        if isinstance(self.after, Unset):
            after = UNSET
        else:
            after = self.after


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "change": change,
        })
        if before is not UNSET:
            field_dict["before"] = before
        if after is not UNSET:
            field_dict["after"] = after

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        name = d.pop("name")

        change = d.pop("change")

        def _parse_before(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        before = _parse_before(d.pop("before", UNSET))


        def _parse_after(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        after = _parse_after(d.pop("after", UNSET))


        output_comparison = cls(
            name=name,
            change=change,
            before=before,
            after=after,
        )


        output_comparison.additional_properties = d
        return output_comparison

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
