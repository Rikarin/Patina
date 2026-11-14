from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_channel_format_op import StackCommandStackSetChannelFormatOp






T = TypeVar("T", bound="StackCommandStackSetChannelFormat")



@_attrs_define
class StackCommandStackSetChannelFormat:
    """ How wide the texels are that strokes paint a channel at (#19): eight bits a lane, or sixteen or thirty-two bit
    floats, which keep what eight bits round away, for a height above all. One undo step, <i>Channel depth</i>. A
    layer painted before is widened, exactly, by its next stroke, unless a stroke of the session already painted it,
    which holds the pixels it painted over; one wider than the set asks keeps its width, so paint is never rounded.

        Attributes:
            op (StackCommandStackSetChannelFormatOp):
            usage (str): Which channel, one the set has.
            format_ (str): One of the vocabulary's formats.
     """

    op: StackCommandStackSetChannelFormatOp
    usage: str
    format_: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        usage = self.usage

        format_ = self.format_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "usage": usage,
            "format": format_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetChannelFormatOp(d.pop("op"))




        usage = d.pop("usage")

        format_ = d.pop("format")

        stack_command_stack_set_channel_format = cls(
            op=op,
            usage=usage,
            format_=format_,
        )


        stack_command_stack_set_channel_format.additional_properties = d
        return stack_command_stack_set_channel_format

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
