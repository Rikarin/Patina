from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_save_preset_op import GraphCommandGraphSavePresetOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_command_graph_save_preset_values_type_1 import GraphCommandGraphSavePresetValuesType1





T = TypeVar("T", bound="GraphCommandGraphSavePreset")



@_attrs_define
class GraphCommandGraphSavePreset:
    """ Saves a preset, replacing one of the same name where it stands.

        Attributes:
            op (GraphCommandGraphSavePresetOp):
            name (str): What it is called.
            values (GraphCommandGraphSavePresetValuesType1 | None | Unset): What each parameter is set to, by name; null for
                every parameter's default now.
     """

    op: GraphCommandGraphSavePresetOp
    name: str
    values: GraphCommandGraphSavePresetValuesType1 | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_command_graph_save_preset_values_type_1 import GraphCommandGraphSavePresetValuesType1 # noqa: PLC0415
        op = self.op.value

        name = self.name

        values: dict[str, Any] | None | Unset
        if isinstance(self.values, Unset):
            values = UNSET
        elif isinstance(self.values, GraphCommandGraphSavePresetValuesType1):
            values = self.values.to_dict()
        else:
            values = self.values


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "name": name,
        })
        if values is not UNSET:
            field_dict["values"] = values

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_command_graph_save_preset_values_type_1 import GraphCommandGraphSavePresetValuesType1 # noqa: PLC0415
        d = dict(src_dict)
        op = GraphCommandGraphSavePresetOp(d.pop("op"))




        name = d.pop("name")

        def _parse_values(data: object) -> GraphCommandGraphSavePresetValuesType1 | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                values_type_1 = GraphCommandGraphSavePresetValuesType1.from_dict(data)



                return values_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphCommandGraphSavePresetValuesType1 | None | Unset, data)

        values = _parse_values(d.pop("values", UNSET))


        graph_command_graph_save_preset = cls(
            op=op,
            name=name,
            values=values,
        )


        graph_command_graph_save_preset.additional_properties = d
        return graph_command_graph_save_preset

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
