from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_input import StackInput





T = TypeVar("T", bound="StackMaskEffectInputsType1")



@_attrs_define
class StackMaskEffectInputsType1:
    """ Where its other image inputs read, by port. The mask always goes into its first image input.

     """

    additional_properties: dict[str, StackInput] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_input import StackInput # noqa: PLC0415
        
        field_dict: dict[str, Any] = {}
        for prop_name, prop in self.additional_properties.items():
            field_dict[prop_name] = prop.to_dict()


        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_input import StackInput # noqa: PLC0415
        d = dict(src_dict)
        stack_mask_effect_inputs_type_1 = cls(
        )


        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_input_parameters_type_1 import StackInputParametersType1 # noqa: PLC0415
        from ..models.stack_input_values_type_1 import StackInputValuesType1 # noqa: PLC0415
        additional_properties = {}
        for prop_name, prop_dict in d.items():
            additional_property = StackInput.from_dict(prop_dict)



            additional_properties[prop_name] = additional_property

        stack_mask_effect_inputs_type_1.additional_properties = additional_properties
        return stack_mask_effect_inputs_type_1

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> StackInput:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: StackInput) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
