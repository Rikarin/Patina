from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_move_layer_op import StackCommandStackMoveLayerOp






T = TypeVar("T", bound="StackCommandStackMoveLayer")



@_attrs_define
class StackCommandStackMoveLayer:
    """ Moves a layer up or down its list.

        Attributes:
            op (StackCommandStackMoveLayerOp):
            layer (str): Which.
            delta (int): How many places, up the stack when positive.
     """

    op: StackCommandStackMoveLayerOp
    layer: str
    delta: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        delta = self.delta


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "delta": delta,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackMoveLayerOp(d.pop("op"))




        layer = d.pop("layer")

        delta = d.pop("delta")

        stack_command_stack_move_layer = cls(
            op=op,
            layer=layer,
            delta=delta,
        )


        stack_command_stack_move_layer.additional_properties = d
        return stack_command_stack_move_layer

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
