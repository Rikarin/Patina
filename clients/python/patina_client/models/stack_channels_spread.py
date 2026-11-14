from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackChannelsSpread")



@_attrs_define
class StackChannelsSpread:
    """ What Apply Channels to All Sets did to one other texture set of the model (#141).

        Attributes:
            set_ (str): The set, by the material it paints.
            path (str): Its stack file.
            added (list[str]): The channels it was given, by usage; none when it had them all or was left alone.
            open_ (bool): Whether it is open in a session, which took the channels as an undo step of its own, unsaved. A
                set nobody has
                open had them written to its file.
            problem (None | str | Unset): Why it was left as it was, or null.
     """

    set_: str
    path: str
    added: list[str]
    open_: bool
    problem: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        set_ = self.set_

        path = self.path

        added = self.added



        open_ = self.open_

        problem: None | str | Unset
        if isinstance(self.problem, Unset):
            problem = UNSET
        else:
            problem = self.problem


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "set": set_,
            "path": path,
            "added": added,
            "open": open_,
        })
        if problem is not UNSET:
            field_dict["problem"] = problem

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        set_ = d.pop("set")

        path = d.pop("path")

        added = cast(list[str], d.pop("added"))


        open_ = d.pop("open")

        def _parse_problem(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        problem = _parse_problem(d.pop("problem", UNSET))


        stack_channels_spread = cls(
            set_=set_,
            path=path,
            added=added,
            open_=open_,
            problem=problem,
        )


        stack_channels_spread.additional_properties = d
        return stack_channels_spread

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
