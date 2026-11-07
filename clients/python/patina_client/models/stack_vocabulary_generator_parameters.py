from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_setting_definition import GraphSettingDefinition





T = TypeVar("T", bound="StackVocabularyGeneratorParameters")



@_attrs_define
class StackVocabularyGeneratorParameters:
    """ The knobs each generator has, by its path.

     """

    additional_properties: dict[str, list[GraphSettingDefinition]] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        
        field_dict: dict[str, Any] = {}
        for prop_name, prop in self.additional_properties.items():
            field_dict[prop_name] = []
            for additional_property_item_data in prop:
                additional_property_item = additional_property_item_data.to_dict()
                field_dict[prop_name].append(additional_property_item)




        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        d = dict(src_dict)
        stack_vocabulary_generator_parameters = cls(
        )


        additional_properties = {}
        for prop_name, prop_dict in d.items():
            additional_property = []
            _additional_property = prop_dict
            for additional_property_item_data in (_additional_property):
                additional_property_item = GraphSettingDefinition.from_dict(additional_property_item_data)



                additional_property.append(additional_property_item)

            additional_properties[prop_name] = additional_property

        stack_vocabulary_generator_parameters.additional_properties = additional_properties
        return stack_vocabulary_generator_parameters

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> list[GraphSettingDefinition]:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: list[GraphSettingDefinition]) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
