from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_apply_mask_op import StackCommandStackApplyMaskOp






T = TypeVar("T", bound="StackCommandStackApplyMask")



@_attrs_define
class StackCommandStackApplyMask:
    """ Gives a layer a smart mask from the shelf (#135). Merged, it narrows the mask the layer has: the smart mask's
    base multiplies over it as one more entry, its entries follow, and its effects run after the layer's own. Not
    merged, it replaces the mask.

        Attributes:
            op (StackCommandStackApplyMaskOp):
            layer (str): The layer.
            source (str): The smart mask, as the shelf names it.
            merge (bool): Whether it joins the layer's mask rather than replacing it. A layer with no mask simply takes it.
                Default: True.
     """

    op: StackCommandStackApplyMaskOp
    layer: str
    source: str
    merge: bool = True
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        source = self.source

        merge = self.merge


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "source": source,
            "merge": merge,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackApplyMaskOp(d.pop("op"))




        layer = d.pop("layer")

        source = d.pop("source")

        merge = d.pop("merge")

        stack_command_stack_apply_mask = cls(
            op=op,
            layer=layer,
            source=source,
            merge=merge,
        )


        stack_command_stack_apply_mask.additional_properties = d
        return stack_command_stack_apply_mask

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
