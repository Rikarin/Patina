from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_keep_quick_mask_op import StackCommandStackKeepQuickMaskOp






T = TypeVar("T", bound="StackCommandStackKeepQuickMask")



@_attrs_define
class StackCommandStackKeepQuickMask:
    """ Keeps a layer to the session's quick mask (#282): a paint entry on top of its mask's stack that starts as a copy
    of the quick mask as it stands, multiplied over the mask beneath, or subtracted from it to keep it outside.
    A fill, a decal or a path, which no stroke paints, is kept so as a stroke is. The entry is the layer's own from
    then, saved with it, and can be painted, changed or removed as any other. The quick mask must have been painted.

        Attributes:
            op (StackCommandStackKeepQuickMaskOp):
            layer (str): The layer to keep.
            outside (bool): Whether to keep it to where the quick mask is not painted. Default: False.
     """

    op: StackCommandStackKeepQuickMaskOp
    layer: str
    outside: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        outside = self.outside


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "outside": outside,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackKeepQuickMaskOp(d.pop("op"))




        layer = d.pop("layer")

        outside = d.pop("outside")

        stack_command_stack_keep_quick_mask = cls(
            op=op,
            layer=layer,
            outside=outside,
        )


        stack_command_stack_keep_quick_mask.additional_properties = d
        return stack_command_stack_keep_quick_mask

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
