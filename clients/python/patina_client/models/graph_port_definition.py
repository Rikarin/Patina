from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_port_range import GraphPortRange





T = TypeVar("T", bound="GraphPortDefinition")



@_attrs_define
class GraphPortDefinition:
    """ One port of a node type.

        Attributes:
            name (str): What an edge calls it.
            direction (str): Input or Output.
            kind (str): What it carries: Image, Float, Float4 and so on.
            default (list[float]): The numbers an unset input takes.
            summary (str): One line about it.
            range_ (GraphPortRange | None | Unset):
     """

    name: str
    direction: str
    kind: str
    default: list[float]
    summary: str
    range_: GraphPortRange | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_port_range import GraphPortRange # noqa: PLC0415
        name = self.name

        direction = self.direction

        kind = self.kind

        default = self.default



        summary = self.summary

        range_: dict[str, Any] | None | Unset
        if isinstance(self.range_, Unset):
            range_ = UNSET
        elif isinstance(self.range_, GraphPortRange):
            range_ = self.range_.to_dict()
        else:
            range_ = self.range_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "direction": direction,
            "kind": kind,
            "default": default,
            "summary": summary,
        })
        if range_ is not UNSET:
            field_dict["range"] = range_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_port_range import GraphPortRange # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        direction = d.pop("direction")

        kind = d.pop("kind")

        default = cast(list[float], d.pop("default"))


        summary = d.pop("summary")

        def _parse_range_(data: object) -> GraphPortRange | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                range_type_1 = GraphPortRange.from_dict(data)



                return range_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphPortRange | None | Unset, data)

        range_ = _parse_range_(d.pop("range", UNSET))


        graph_port_definition = cls(
            name=name,
            direction=direction,
            kind=kind,
            default=default,
            summary=summary,
            range_=range_,
        )


        graph_port_definition.additional_properties = d
        return graph_port_definition

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
