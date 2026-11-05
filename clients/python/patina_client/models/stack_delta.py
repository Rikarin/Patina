from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.stack_layer import StackLayer
  from ..models.stack_painted import StackPainted





T = TypeVar("T", bound="StackDelta")



@_attrs_define
class StackDelta:
    """ What an edit changed, so that a client need not read the whole stack again.

        Attributes:
            layers (list[StackLayer]): The stack's layers now.
            painted (list[StackPainted]): Each channel a stroke touched, as layer and usage.
     """

    layers: list[StackLayer]
    painted: list[StackPainted]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_layer import StackLayer # noqa: PLC0415
        from ..models.stack_painted import StackPainted # noqa: PLC0415
        layers = []
        for layers_item_data in self.layers:
            layers_item = layers_item_data.to_dict()
            layers.append(layers_item)



        painted = []
        for painted_item_data in self.painted:
            painted_item = painted_item_data.to_dict()
            painted.append(painted_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "layers": layers,
            "painted": painted,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_layer import StackLayer # noqa: PLC0415
        from ..models.stack_painted import StackPainted # noqa: PLC0415
        d = dict(src_dict)
        layers = []
        _layers = d.pop("layers")
        for layers_item_data in (_layers):
            layers_item = StackLayer.from_dict(layers_item_data)



            layers.append(layers_item)


        painted = []
        _painted = d.pop("painted")
        for painted_item_data in (_painted):
            painted_item = StackPainted.from_dict(painted_item_data)



            painted.append(painted_item)


        stack_delta = cls(
            layers=layers,
            painted=painted,
        )


        stack_delta.additional_properties = d
        return stack_delta

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
