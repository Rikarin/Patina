from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_command_graph_add_parameter_op import GraphCommandGraphAddParameterOp
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_setting_definition import GraphSettingDefinition





T = TypeVar("T", bound="GraphCommandGraphAddParameter")



@_attrs_define
class GraphCommandGraphAddParameter:
    """ Exposes a new parameter. Its name must be free, and its default must suit its kind and range.

        Attributes:
            op (GraphCommandGraphAddParameterOp):
            parameter (GraphSettingDefinition): A value a node type or a graph lets an author set.
     """

    op: GraphCommandGraphAddParameterOp
    parameter: GraphSettingDefinition
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        op = self.op.value

        parameter = self.parameter.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "parameter": parameter,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        d = dict(src_dict)
        op = GraphCommandGraphAddParameterOp(d.pop("op"))




        parameter = GraphSettingDefinition.from_dict(d.pop("parameter"))




        graph_command_graph_add_parameter = cls(
            op=op,
            parameter=parameter,
        )


        graph_command_graph_add_parameter.additional_properties = d
        return graph_command_graph_add_parameter

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
