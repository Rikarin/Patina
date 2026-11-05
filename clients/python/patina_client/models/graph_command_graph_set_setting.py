from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_set_setting_op import GraphCommandGraphSetSettingOp






T = TypeVar("T", bound="GraphCommandGraphSetSetting")



@_attrs_define
class GraphCommandGraphSetSetting:
    """ Sets a setting of the graph: `baseWidth`, `baseHeight` or `seed`.

        Attributes:
            op (GraphCommandGraphSetSettingOp):
            key (str):
            value (str):
     """

    op: GraphCommandGraphSetSettingOp
    key: str
    value: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        key = self.key

        value = self.value


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "key": key,
            "value": value,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphSetSettingOp(d.pop("op"))




        key = d.pop("key")

        value = d.pop("value")

        graph_command_graph_set_setting = cls(
            op=op,
            key=key,
            value=value,
        )


        graph_command_graph_set_setting.additional_properties = d
        return graph_command_graph_set_setting

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
