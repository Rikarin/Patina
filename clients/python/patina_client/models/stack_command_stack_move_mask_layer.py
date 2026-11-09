from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_move_mask_layer_op import StackCommandStackMoveMaskLayerOp






T = TypeVar("T", bound="StackCommandStackMoveMaskLayer")



@_attrs_define
class StackCommandStackMoveMaskLayer:
    """ Moves an entry up or down a layer's mask stack.

        Attributes:
            op (StackCommandStackMoveMaskLayerOp):
            layer (str): The layer whose mask it is.
            index (int): Which, counted from the bottom.
            delta (int): How many places, up the stack when positive.
     """

    op: StackCommandStackMoveMaskLayerOp
    layer: str
    index: int
    delta: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        index = self.index

        delta = self.delta


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "index": index,
            "delta": delta,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackMoveMaskLayerOp(d.pop("op"))




        layer = d.pop("layer")

        index = d.pop("index")

        delta = d.pop("delta")

        stack_command_stack_move_mask_layer = cls(
            op=op,
            layer=layer,
            index=index,
            delta=delta,
        )


        stack_command_stack_move_mask_layer.additional_properties = d
        return stack_command_stack_move_mask_layer

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
