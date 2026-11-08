from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_preset_state_values import GraphPresetStateValues





T = TypeVar("T", bound="GraphPresetState")



@_attrs_define
class GraphPresetState:
    """ A named set of values for a graph's parameters, kept in the graph's file.

        Attributes:
            name (str): What it is called. Unique within the graph.
            values (GraphPresetStateValues): What each parameter is set to, by parameter name, as text.
     """

    name: str
    values: GraphPresetStateValues
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_preset_state_values import GraphPresetStateValues # noqa: PLC0415
        name = self.name

        values = self.values.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "values": values,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_preset_state_values import GraphPresetStateValues # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        values = GraphPresetStateValues.from_dict(d.pop("values"))




        graph_preset_state = cls(
            name=name,
            values=values,
        )


        graph_preset_state.additional_properties = d
        return graph_preset_state

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
