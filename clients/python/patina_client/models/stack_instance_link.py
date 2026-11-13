from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset







T = TypeVar("T", bound="StackInstanceLink")



@_attrs_define
class StackInstanceLink:
    """ An instance layer and the layer it stands for, across two texture sets of one model (#108).

        Attributes:
            set_ (str): The other texture set, by name.
            layer (str): The layer's id in that set.
            name (str): The layer's name there.
            source (str): For an instance in the other set, the id of the layer of this set it stands for; empty for a
                source. Default: ''.
     """

    set_: str
    layer: str
    name: str
    source: str = ''
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        set_ = self.set_

        layer = self.layer

        name = self.name

        source = self.source


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "set": set_,
            "layer": layer,
            "name": name,
            "source": source,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        set_ = d.pop("set")

        layer = d.pop("layer")

        name = d.pop("name")

        source = d.pop("source")

        stack_instance_link = cls(
            set_=set_,
            layer=layer,
            name=name,
            source=source,
        )


        stack_instance_link.additional_properties = d
        return stack_instance_link

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
