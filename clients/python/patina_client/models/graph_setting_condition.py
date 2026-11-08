from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="GraphSettingCondition")



@_attrs_define
class GraphSettingCondition:
    """ When a parameter or a setting is shown: while another of the same graph or node holds a value.

        Attributes:
            setting (str): The other one, by name.
            value (str): What it is compared with, as text. Numbers compare as numbers and anything else as a name, ignoring
                case.
            negated (bool): True to show it while the other one does not hold the value. Default: False.
     """

    setting: str
    value: str
    negated: bool = False
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        setting = self.setting

        value = self.value

        negated = self.negated


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "setting": setting,
            "value": value,
            "negated": negated,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        setting = d.pop("setting")

        value = d.pop("value")

        negated = d.pop("negated")

        graph_setting_condition = cls(
            setting=setting,
            value=value,
            negated=negated,
        )


        graph_setting_condition.additional_properties = d
        return graph_setting_condition

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
