from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_instance_link import StackInstanceLink





T = TypeVar("T", bound="StackInstanceLinks")



@_attrs_define
class StackInstanceLinks:
    """ How a texture set's layers are tied to the model's other sets (#108): the instances elsewhere of its layers, and
    the layers elsewhere its own instances stand for, each with its name, which a client of one set cannot read.

        Attributes:
            instances (list[StackInstanceLink]): Layers of other sets that stand for one of this set's.
            sources (list[StackInstanceLink]): Layers of other sets that one of this set's instances stands for.
     """

    instances: list[StackInstanceLink]
    sources: list[StackInstanceLink]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_instance_link import StackInstanceLink # noqa: PLC0415
        instances = []
        for instances_item_data in self.instances:
            instances_item = instances_item_data.to_dict()
            instances.append(instances_item)



        sources = []
        for sources_item_data in self.sources:
            sources_item = sources_item_data.to_dict()
            sources.append(sources_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "instances": instances,
            "sources": sources,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_instance_link import StackInstanceLink # noqa: PLC0415
        d = dict(src_dict)
        instances = []
        _instances = d.pop("instances")
        for instances_item_data in (_instances):
            instances_item = StackInstanceLink.from_dict(instances_item_data)



            instances.append(instances_item)


        sources = []
        _sources = d.pop("sources")
        for sources_item_data in (_sources):
            sources_item = StackInstanceLink.from_dict(sources_item_data)



            sources.append(sources_item)


        stack_instance_links = cls(
            instances=instances,
            sources=sources,
        )


        stack_instance_links.additional_properties = d
        return stack_instance_links

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
