from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_carry_paint_op import StackCommandStackCarryPaintOp






T = TypeVar("T", bound="StackCommandStackCarryPaint")



@_attrs_define
class StackCommandStackCarryPaint:
    """ Carries every canvas of the texture set from the layout of another of the mesh's UV sets onto the one it is laid
    out in now, as one undo step (#210): each texel takes what was painted at its place on the surface. Sent after a
    StackSetSettings that changes the UV set, in the same batch, it keeps the paint where it was on
    the model; on its own it projects paint laid out in another UV set onto this one. Every canvas becomes a new
    one, so an undo gives the old ones back untouched. A surface the other layout leaves out reads as nothing.

        Attributes:
            op (StackCommandStackCarryPaintOp):
            from_uv_set (int): The UV set the paint is laid out in now, by its number; 0 is the first.
     """

    op: StackCommandStackCarryPaintOp
    from_uv_set: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        from_uv_set = self.from_uv_set


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "fromUvSet": from_uv_set,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackCarryPaintOp(d.pop("op"))




        from_uv_set = d.pop("fromUvSet")

        stack_command_stack_carry_paint = cls(
            op=op,
            from_uv_set=from_uv_set,
        )


        stack_command_stack_carry_paint.additional_properties = d
        return stack_command_stack_carry_paint

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
