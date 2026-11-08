from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor_use import StackAnchorUse





T = TypeVar("T", bound="StackAnchor")



@_attrs_define
class StackAnchor:
    """ An anchor point and every place that reads it.

        Attributes:
            name (str): The anchor point's name.
            layer (str): The id of the layer that publishes it.
            uses (list[StackAnchorUse]): Who reads it, and in which channel.
     """

    name: str
    layer: str
    uses: list[StackAnchorUse]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_use import StackAnchorUse # noqa: PLC0415
        name = self.name

        layer = self.layer

        uses = []
        for uses_item_data in self.uses:
            uses_item = uses_item_data.to_dict()
            uses.append(uses_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "layer": layer,
            "uses": uses,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_use import StackAnchorUse # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        layer = d.pop("layer")

        uses = []
        _uses = d.pop("uses")
        for uses_item_data in (_uses):
            uses_item = StackAnchorUse.from_dict(uses_item_data)



            uses.append(uses_item)


        stack_anchor = cls(
            name=name,
            layer=layer,
            uses=uses,
        )


        stack_anchor.additional_properties = d
        return stack_anchor

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
