from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_layer_op import StackCommandStackSetLayerOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackSetLayer")



@_attrs_define
class StackCommandStackSetLayer:
    """ Changes what a layer's row shows. What is null stays.

        Attributes:
            op (StackCommandStackSetLayerOp):
            layer (str): Which.
            name (None | str | Unset): What it is called.
            enabled (bool | None | Unset): Whether it composites.
            opacity (float | None | Unset): How much of it composites, 0 to 1.
            blend (None | str | Unset): Its blend mode.
     """

    op: StackCommandStackSetLayerOp
    layer: str
    name: None | str | Unset = UNSET
    enabled: bool | None | Unset = UNSET
    opacity: float | None | Unset = UNSET
    blend: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        layer = self.layer

        name: None | str | Unset
        if isinstance(self.name, Unset):
            name = UNSET
        else:
            name = self.name

        enabled: bool | None | Unset
        if isinstance(self.enabled, Unset):
            enabled = UNSET
        else:
            enabled = self.enabled

        opacity: float | None | Unset
        if isinstance(self.opacity, Unset):
            opacity = UNSET
        else:
            opacity = self.opacity

        blend: None | str | Unset
        if isinstance(self.blend, Unset):
            blend = UNSET
        else:
            blend = self.blend


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
        })
        if name is not UNSET:
            field_dict["name"] = name
        if enabled is not UNSET:
            field_dict["enabled"] = enabled
        if opacity is not UNSET:
            field_dict["opacity"] = opacity
        if blend is not UNSET:
            field_dict["blend"] = blend

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetLayerOp(d.pop("op"))




        layer = d.pop("layer")

        def _parse_name(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        name = _parse_name(d.pop("name", UNSET))


        def _parse_enabled(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        enabled = _parse_enabled(d.pop("enabled", UNSET))


        def _parse_opacity(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        opacity = _parse_opacity(d.pop("opacity", UNSET))


        def _parse_blend(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        blend = _parse_blend(d.pop("blend", UNSET))


        stack_command_stack_set_layer = cls(
            op=op,
            layer=layer,
            name=name,
            enabled=enabled,
            opacity=opacity,
            blend=blend,
        )


        stack_command_stack_set_layer.additional_properties = d
        return stack_command_stack_set_layer

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
