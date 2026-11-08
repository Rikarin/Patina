from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_mask_effect_inputs_type_1 import StackMaskEffectInputsType1
  from ..models.stack_mask_effect_texts_type_1 import StackMaskEffectTextsType1
  from ..models.stack_mask_effect_values_type_1 import StackMaskEffectValuesType1





T = TypeVar("T", bound="StackMaskEffect")



@_attrs_define
class StackMaskEffect:
    """ One step run on a mask.

        Attributes:
            node (str): The node type or compound it is.
            enabled (bool): Whether it runs. Default: True.
            values (None | StackMaskEffectValuesType1 | Unset): Its numbers, by port.
            texts (None | StackMaskEffectTextsType1 | Unset): Its settings, by name.
            inputs (None | StackMaskEffectInputsType1 | Unset): Its other image inputs, by port, each fed by an anchor
                point. The mask goes into the one left.
     """

    node: str
    enabled: bool = True
    values: None | StackMaskEffectValuesType1 | Unset = UNSET
    texts: None | StackMaskEffectTextsType1 | Unset = UNSET
    inputs: None | StackMaskEffectInputsType1 | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_mask_effect_inputs_type_1 import StackMaskEffectInputsType1 # noqa: PLC0415
        from ..models.stack_mask_effect_texts_type_1 import StackMaskEffectTextsType1 # noqa: PLC0415
        from ..models.stack_mask_effect_values_type_1 import StackMaskEffectValuesType1 # noqa: PLC0415
        node = self.node

        enabled = self.enabled

        values: dict[str, Any] | None | Unset
        if isinstance(self.values, Unset):
            values = UNSET
        elif isinstance(self.values, StackMaskEffectValuesType1):
            values = self.values.to_dict()
        else:
            values = self.values

        texts: dict[str, Any] | None | Unset
        if isinstance(self.texts, Unset):
            texts = UNSET
        elif isinstance(self.texts, StackMaskEffectTextsType1):
            texts = self.texts.to_dict()
        else:
            texts = self.texts

        inputs: dict[str, Any] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, StackMaskEffectInputsType1):
            inputs = self.inputs.to_dict()
        else:
            inputs = self.inputs


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "enabled": enabled,
        })
        if values is not UNSET:
            field_dict["values"] = values
        if texts is not UNSET:
            field_dict["texts"] = texts
        if inputs is not UNSET:
            field_dict["inputs"] = inputs

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_mask_effect_inputs_type_1 import StackMaskEffectInputsType1 # noqa: PLC0415
        from ..models.stack_mask_effect_texts_type_1 import StackMaskEffectTextsType1 # noqa: PLC0415
        from ..models.stack_mask_effect_values_type_1 import StackMaskEffectValuesType1 # noqa: PLC0415
        d = dict(src_dict)
        node = d.pop("node")

        enabled = d.pop("enabled")

        def _parse_values(data: object) -> None | StackMaskEffectValuesType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                values_type_1 = StackMaskEffectValuesType1.from_dict(data)



                return values_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskEffectValuesType1 | Unset, data)

        values = _parse_values(d.pop("values", UNSET))


        def _parse_texts(data: object) -> None | StackMaskEffectTextsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                texts_type_1 = StackMaskEffectTextsType1.from_dict(data)



                return texts_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskEffectTextsType1 | Unset, data)

        texts = _parse_texts(d.pop("texts", UNSET))


        def _parse_inputs(data: object) -> None | StackMaskEffectInputsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                inputs_type_1 = StackMaskEffectInputsType1.from_dict(data)



                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskEffectInputsType1 | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        stack_mask_effect = cls(
            node=node,
            enabled=enabled,
            values=values,
            texts=texts,
            inputs=inputs,
        )


        stack_mask_effect.additional_properties = d
        return stack_mask_effect

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
