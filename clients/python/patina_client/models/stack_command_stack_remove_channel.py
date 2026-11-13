from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_remove_channel_op import StackCommandStackRemoveChannelOp






T = TypeVar("T", bound="StackCommandStackRemoveChannel")



@_attrs_define
class StackCommandStackRemoveChannel:
    """ Takes a channel off the texture set (#141): no layer writes it and no export finds it. What the layers held for
    it, their values and paint, stays in them, so adding the channel again brings it back.

        Attributes:
            op (StackCommandStackRemoveChannelOp):
            usage (str): Which, one the set has. A set keeps at least one channel.
     """

    op: StackCommandStackRemoveChannelOp
    usage: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        usage = self.usage


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "usage": usage,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackRemoveChannelOp(d.pop("op"))




        usage = d.pop("usage")

        stack_command_stack_remove_channel = cls(
            op=op,
            usage=usage,
        )


        stack_command_stack_remove_channel.additional_properties = d
        return stack_command_stack_remove_channel

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
