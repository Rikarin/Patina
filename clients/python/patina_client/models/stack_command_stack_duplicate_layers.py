from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_duplicate_layers_op import StackCommandStackDuplicateLayersOp
from typing import cast






T = TypeVar("T", bound="StackCommandStackDuplicateLayers")



@_attrs_define
class StackCommandStackDuplicateLayers:
    """ Duplicates layers, each just above itself, with everything inside it. A painted canvas is copied, never shared:
    a stroke on the copy leaves the original alone.

        Attributes:
            op (StackCommandStackDuplicateLayersOp):
            layers (list[str]): Which. A layer inside another of them goes with that one.
     """

    op: StackCommandStackDuplicateLayersOp
    layers: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layers = self.layers




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layers": layers,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackDuplicateLayersOp(d.pop("op"))




        layers = cast(list[str], d.pop("layers"))


        stack_command_stack_duplicate_layers = cls(
            op=op,
            layers=layers,
        )


        stack_command_stack_duplicate_layers.additional_properties = d
        return stack_command_stack_duplicate_layers

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
