from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor import StackAnchor
  from ..models.stack_layer import StackLayer
  from ..models.stack_painted import StackPainted





T = TypeVar("T", bound="StackDelta")



@_attrs_define
class StackDelta:
    """ What an edit changed, so that a client need not read the whole stack again.

        Attributes:
            layers (list[StackLayer]): The stack's layers now.
            painted (list[StackPainted]): Each channel a stroke touched, as layer and usage.
            anchors (list[StackAnchor] | None | Unset): The anchor points its set publishes now, and who reads each.
     """

    layers: list[StackLayer]
    painted: list[StackPainted]
    anchors: list[StackAnchor] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor import StackAnchor # noqa: PLC0415
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



        anchors: list[dict[str, Any]] | None | Unset
        if isinstance(self.anchors, Unset):
            anchors = UNSET
        elif isinstance(self.anchors, list):
            anchors = []
            for anchors_type_1_item_data in self.anchors:
                anchors_type_1_item = anchors_type_1_item_data.to_dict()
                anchors.append(anchors_type_1_item)


        else:
            anchors = self.anchors


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "layers": layers,
            "painted": painted,
        })
        if anchors is not UNSET:
            field_dict["anchors"] = anchors

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor import StackAnchor # noqa: PLC0415
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


        def _parse_anchors(data: object) -> list[StackAnchor] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                anchors_type_1 = []
                _anchors_type_1 = data
                for anchors_type_1_item_data in (_anchors_type_1):
                    anchors_type_1_item = StackAnchor.from_dict(anchors_type_1_item_data)



                    anchors_type_1.append(anchors_type_1_item)

                return anchors_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[StackAnchor] | None | Unset, data)

        anchors = _parse_anchors(d.pop("anchors", UNSET))


        stack_delta = cls(
            layers=layers,
            painted=painted,
            anchors=anchors,
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
