from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_effect_port import StackEffectPort





T = TypeVar("T", bound="StackEffectKind")



@_attrs_define
class StackEffectKind:
    """ A step a mask can run.

        Attributes:
            node (str): The node type.
            name (str): What a person calls it.
            ports (list[StackEffectPort]): The numbers it takes.
     """

    node: str
    name: str
    ports: list[StackEffectPort]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_effect_port import StackEffectPort # noqa: PLC0415
        node = self.node

        name = self.name

        ports = []
        for ports_item_data in self.ports:
            ports_item = ports_item_data.to_dict()
            ports.append(ports_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "node": node,
            "name": name,
            "ports": ports,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_effect_port import StackEffectPort # noqa: PLC0415
        d = dict(src_dict)
        node = d.pop("node")

        name = d.pop("name")

        ports = []
        _ports = d.pop("ports")
        for ports_item_data in (_ports):
            ports_item = StackEffectPort.from_dict(ports_item_data)



            ports.append(ports_item)


        stack_effect_kind = cls(
            node=node,
            name=name,
            ports=ports,
        )


        stack_effect_kind.additional_properties = d
        return stack_effect_kind

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
