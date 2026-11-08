from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_unflatten_layer_op import StackCommandStackUnflattenLayerOp






T = TypeVar("T", bound="StackCommandStackUnflattenLayer")



@_attrs_define
class StackCommandStackUnflattenLayer:
    """ Takes a flattened group back to its children, which composite again.

        Attributes:
            op (StackCommandStackUnflattenLayerOp):
            layer (str): The group.
     """

    op: StackCommandStackUnflattenLayerOp
    layer: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackUnflattenLayerOp(d.pop("op"))




        layer = d.pop("layer")

        stack_command_stack_unflatten_layer = cls(
            op=op,
            layer=layer,
        )


        stack_command_stack_unflatten_layer.additional_properties = d
        return stack_command_stack_unflatten_layer

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
