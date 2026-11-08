from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor_ref import StackAnchorRef





T = TypeVar("T", bound="StackLayerInputsType1")



@_attrs_define
class StackLayerInputsType1:
    """ The image inputs of a graph fill or a filter that anchor points feed, by port.

     """

    additional_properties: dict[str, StackAnchorRef] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        
        field_dict: dict[str, Any] = {}
        for prop_name, prop in self.additional_properties.items():
            field_dict[prop_name] = prop.to_dict()


        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        d = dict(src_dict)
        stack_layer_inputs_type_1 = cls(
        )


        additional_properties = {}
        for prop_name, prop_dict in d.items():
            additional_property = StackAnchorRef.from_dict(prop_dict)



            additional_properties[prop_name] = additional_property

        stack_layer_inputs_type_1.additional_properties = additional_properties
        return stack_layer_inputs_type_1

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> StackAnchorRef:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: StackAnchorRef) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
