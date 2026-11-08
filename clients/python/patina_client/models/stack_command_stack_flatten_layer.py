from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_flatten_layer_op import StackCommandStackFlattenLayerOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackFlattenLayer")



@_attrs_define
class StackCommandStackFlattenLayer:
    """ Flattens a group into a canvas that composites what its children did, keeping them, unread, as the way back. The
    whole stack is flattened by gathering every layer into one group first. Undoing it takes the canvas away.

        Attributes:
            op (StackCommandStackFlattenLayerOp):
            layer (None | str | Unset): The group, or null for the whole stack.
     """

    op: StackCommandStackFlattenLayerOp
    layer: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer: None | str | Unset
        if isinstance(self.layer, Unset):
            layer = UNSET
        else:
            layer = self.layer


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
        })
        if layer is not UNSET:
            field_dict["layer"] = layer

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackFlattenLayerOp(d.pop("op"))




        def _parse_layer(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        layer = _parse_layer(d.pop("layer", UNSET))


        stack_command_stack_flatten_layer = cls(
            op=op,
            layer=layer,
        )


        stack_command_stack_flatten_layer.additional_properties = d
        return stack_command_stack_flatten_layer

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
