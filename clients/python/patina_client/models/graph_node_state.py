from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

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
            bypassed (bool): True when it is switched off: what feeds its first input of an output's kind passes straight
                through, and a
                node with no such input passes nothing. Default: False.
            level (int | None | Unset): The size it is computed at, in halvings of its graph's size: 1 is half the width and
                height, -1 twice them, 0
                the graph's own. Null follows the finest image arriving at it. On a compound it applies to every node inside.
     """

    id: int
    type_: str
    x: float
    y: float
    values: GraphNodeStateValues
    texts: GraphNodeStateTexts
    bypassed: bool = False
    level: int | None | Unset = UNSET
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

        bypassed = self.bypassed

        level: int | None | Unset
        if isinstance(self.level, Unset):
            level = UNSET
        else:
            level = self.level


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "type": type_,
            "x": x,
            "y": y,
            "values": values,
            "texts": texts,
            "bypassed": bypassed,
        })
        if level is not UNSET:
            field_dict["level"] = level

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




        bypassed = d.pop("bypassed")

        def _parse_level(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        level = _parse_level(d.pop("level", UNSET))


        graph_node_state = cls(
            id=id,
            type_=type_,
            x=x,
            y=y,
            values=values,
            texts=texts,
            bypassed=bypassed,
            level=level,
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
