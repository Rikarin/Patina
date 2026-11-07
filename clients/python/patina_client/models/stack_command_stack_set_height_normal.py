from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_height_normal_op import StackCommandStackSetHeightNormalOp






T = TypeVar("T", bound="StackCommandStackSetHeightNormal")



@_attrs_define
class StackCommandStackSetHeightNormal:
    """ Says how the texture set's height becomes the normal its surface is lit with.

        Attributes:
            op (StackCommandStackSetHeightNormalOp):
            method (str): `Sharp`, the height's slope as painted, or `Smooth`, the slope of the height blurred by a texel,
                which
                            hides an eight-bit height's steps.
     """

    op: StackCommandStackSetHeightNormalOp
    method: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        method = self.method


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "method": method,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetHeightNormalOp(d.pop("op"))




        method = d.pop("method")

        stack_command_stack_set_height_normal = cls(
            op=op,
            method=method,
        )


        stack_command_stack_set_height_normal.additional_properties = d
        return stack_command_stack_set_height_normal

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
