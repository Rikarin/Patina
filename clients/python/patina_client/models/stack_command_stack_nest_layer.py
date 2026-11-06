from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_nest_layer_op import StackCommandStackNestLayerOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackNestLayer")



@_attrs_define
class StackCommandStackNestLayer:
    """ Moves a layer to the top of a group, or of the stack.

        Attributes:
            op (StackCommandStackNestLayerOp):
            layer (str): Which.
            parent (None | str | Unset): The group, or null for the stack itself.
     """

    op: StackCommandStackNestLayerOp
    layer: str
    parent: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        parent: None | str | Unset
        if isinstance(self.parent, Unset):
            parent = UNSET
        else:
            parent = self.parent


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
        })
        if parent is not UNSET:
            field_dict["parent"] = parent

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackNestLayerOp(d.pop("op"))




        layer = d.pop("layer")

        def _parse_parent(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        parent = _parse_parent(d.pop("parent", UNSET))


        stack_command_stack_nest_layer = cls(
            op=op,
            layer=layer,
            parent=parent,
        )


        stack_command_stack_nest_layer.additional_properties = d
        return stack_command_stack_nest_layer

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
