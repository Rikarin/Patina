from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="StackLayer")



@_attrs_define
class StackLayer:
    """ One layer, without its pixels.

        Attributes:
            id (str): Its identifier, stable across renames.
            name (str): What it is called.
            kind (str): fill, paint, filter or group.
            enabled (bool): Whether it composites.
            opacity (float): How much of it composites.
            blend (str): Its blend mode.
            channels (list[str]): Which channels it writes. Empty means all of them.
            painted (bool): Whether it names a paint file yet.
            children (list[StackLayer]): What a group holds.
     """

    id: str
    name: str
    kind: str
    enabled: bool
    opacity: float
    blend: str
    channels: list[str]
    painted: bool
    children: list[StackLayer]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        id = self.id

        name = self.name

        kind = self.kind

        enabled = self.enabled

        opacity = self.opacity

        blend = self.blend

        channels = self.channels



        painted = self.painted

        children = []
        for children_item_data in self.children:
            children_item = children_item_data.to_dict()
            children.append(children_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "kind": kind,
            "enabled": enabled,
            "opacity": opacity,
            "blend": blend,
            "channels": channels,
            "painted": painted,
            "children": children,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        id = d.pop("id")

        name = d.pop("name")

        kind = d.pop("kind")

        enabled = d.pop("enabled")

        opacity = d.pop("opacity")

        blend = d.pop("blend")

        channels = cast(list[str], d.pop("channels"))


        painted = d.pop("painted")

        children = []
        _children = d.pop("children")
        for children_item_data in (_children):
            children_item = StackLayer.from_dict(children_item_data)



            children.append(children_item)


        stack_layer = cls(
            id=id,
            name=name,
            kind=kind,
            enabled=enabled,
            opacity=opacity,
            blend=blend,
            channels=channels,
            painted=painted,
            children=children,
        )


        stack_layer.additional_properties = d
        return stack_layer

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
