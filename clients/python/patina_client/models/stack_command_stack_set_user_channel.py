from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_user_channel_op import StackCommandStackSetUserChannelOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackSetUserChannel")



@_attrs_define
class StackCommandStackSetUserChannel:
    """ Names a user channel and says whether it holds a color (#141), as one undo step, <i>User channel</i>. A user
    channel, `user0` to `user7`, means nothing to the material: its name is what a person reads, and its
    usage stays what a preset and a graph name it by. What is left out stays as it is.

        Attributes:
            op (StackCommandStackSetUserChannelOp):
            usage (str): Which channel, a user channel the set has.
            name (None | str | Unset): What it is called; empty for its usage's own label.
            colour (bool | None | Unset): Whether it holds a color, painted and chosen as one, rather than one number.
     """

    op: StackCommandStackSetUserChannelOp
    usage: str
    name: None | str | Unset = UNSET
    colour: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        usage = self.usage

        name: None | str | Unset
        if isinstance(self.name, Unset):
            name = UNSET
        else:
            name = self.name

        colour: bool | None | Unset
        if isinstance(self.colour, Unset):
            colour = UNSET
        else:
            colour = self.colour


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "usage": usage,
        })
        if name is not UNSET:
            field_dict["name"] = name
        if colour is not UNSET:
            field_dict["colour"] = colour

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetUserChannelOp(d.pop("op"))




        usage = d.pop("usage")

        def _parse_name(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        name = _parse_name(d.pop("name", UNSET))


        def _parse_colour(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        colour = _parse_colour(d.pop("colour", UNSET))


        stack_command_stack_set_user_channel = cls(
            op=op,
            usage=usage,
            name=name,
            colour=colour,
        )


        stack_command_stack_set_user_channel.additional_properties = d
        return stack_command_stack_set_user_channel

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
