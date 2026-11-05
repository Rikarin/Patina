from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_node_state_texts import GraphNodeStateTexts
  from ..models.graph_node_state_values import GraphNodeStateValues





T = TypeVar("T", bound="GraphNodeState")



@_attrs_define
class GraphNodeState:
    """ A node as a document holds it.

        Attributes:
            id (int): The node's identity, which is never reused within a session.
            type_ (str): The node type's path, from `/node-types`.
            x (float): Its position on the canvas.
            y (float): Its position on the canvas.
            values (GraphNodeStateValues): The numbers set on its inputs, by port. An input left out takes the type's
                default.
            texts (GraphNodeStateTexts): The text settings set on it, by name.
     """

    id: int
    type_: str
    x: float
    y: float
    values: GraphNodeStateValues
    texts: GraphNodeStateTexts
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_node_state_texts import GraphNodeStateTexts # noqa: PLC0415
        from ..models.graph_node_state_values import GraphNodeStateValues # noqa: PLC0415
        id = self.id

        type_ = self.type_

        x = self.x

        y = self.y

        values = self.values.to_dict()

        texts = self.texts.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "type": type_,
            "x": x,
            "y": y,
            "values": values,
            "texts": texts,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_node_state_texts import GraphNodeStateTexts # noqa: PLC0415
        from ..models.graph_node_state_values import GraphNodeStateValues # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        type_ = d.pop("type")

        x = d.pop("x")

        y = d.pop("y")

        values = GraphNodeStateValues.from_dict(d.pop("values"))




        texts = GraphNodeStateTexts.from_dict(d.pop("texts"))




        graph_node_state = cls(
            id=id,
            type_=type_,
            x=x,
            y=y,
            values=values,
            texts=texts,
        )


        graph_node_state.additional_properties = d
        return graph_node_state

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
