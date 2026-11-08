from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_apply_preset_op import GraphCommandGraphApplyPresetOp






T = TypeVar("T", bound="GraphCommandGraphApplyPreset")



@_attrs_define
class GraphCommandGraphApplyPreset:
    """ Sets the parameters a preset names to its values, as their defaults. A value a parameter cannot hold, or a
    name the graph no longer declares, is left out. A compound's preset on a node of another graph is that node's
    settings, which GraphSetText sets.

        Attributes:
            op (GraphCommandGraphApplyPresetOp):
            name (str):
     """

    op: GraphCommandGraphApplyPresetOp
    name: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        name = self.name


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "name": name,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = GraphCommandGraphApplyPresetOp(d.pop("op"))




        name = d.pop("name")

        graph_command_graph_apply_preset = cls(
            op=op,
            name=name,
        )


        graph_command_graph_apply_preset.additional_properties = d
        return graph_command_graph_apply_preset

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
