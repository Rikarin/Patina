from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor_ref import StackAnchorRef
  from ..models.stack_mask_layer_inputs_type_1 import StackMaskLayerInputsType1
  from ..models.stack_mask_layer_parameters_type_1 import StackMaskLayerParametersType1





T = TypeVar("T", bound="StackMaskLayer")



@_attrs_define
class StackMaskLayer:
    """ One entry of a mask's own stack: a source, composited over the base and the entries beneath it with its own
    blend and opacity. Paint over a generator is a Paint entry over a Generator base.

        Attributes:
            source (str): Constant, Texture, Anchor, Generator, Bake or Paint.
            value (float): A constant entry's value. Default: 1.0.
            asset (str): The image a texture entry reads. Default: ''.
            generator (str): The compound a generator entry runs. Default: ''.
            map_ (str): The mesh map a bake entry reads. Default: ''.
            painted (bool): Whether a paint entry has a canvas yet. Default: False.
            blend (str): How it composites over what is beneath it. Default: 'Copy'.
            opacity (float): How much of it composites, 0 to 1. Default: 1.0.
            enabled (bool): Whether it composites at all. Default: True.
            lane (str): Which lane of its picture it reads: Red, Green, Blue or Alpha. Default: 'Red'.
            anchor (None | StackAnchorRef | Unset):
            parameters (None | StackMaskLayerParametersType1 | Unset): What a generator entry's knobs are set to, as text.
            inputs (None | StackMaskLayerInputsType1 | Unset): The image inputs of a generator entry that anchor points
                feed, by port.
     """

    source: str
    value: float = 1.0
    asset: str = ''
    generator: str = ''
    map_: str = ''
    painted: bool = False
    blend: str = 'Copy'
    opacity: float = 1.0
    enabled: bool = True
    lane: str = 'Red'
    anchor: None | StackAnchorRef | Unset = UNSET
    parameters: None | StackMaskLayerParametersType1 | Unset = UNSET
    inputs: None | StackMaskLayerInputsType1 | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_mask_layer_inputs_type_1 import StackMaskLayerInputsType1 # noqa: PLC0415
        from ..models.stack_mask_layer_parameters_type_1 import StackMaskLayerParametersType1 # noqa: PLC0415
        source = self.source

        value = self.value

        asset = self.asset

        generator = self.generator

        map_ = self.map_

        painted = self.painted

        blend = self.blend

        opacity = self.opacity

        enabled = self.enabled

        lane = self.lane

        anchor: dict[str, Any] | None | Unset
        if isinstance(self.anchor, Unset):
            anchor = UNSET
        elif isinstance(self.anchor, StackAnchorRef):
            anchor = self.anchor.to_dict()
        else:
            anchor = self.anchor

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, StackMaskLayerParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters

        inputs: dict[str, Any] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, StackMaskLayerInputsType1):
            inputs = self.inputs.to_dict()
        else:
            inputs = self.inputs


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "source": source,
            "value": value,
            "asset": asset,
            "generator": generator,
            "map": map_,
            "painted": painted,
            "blend": blend,
            "opacity": opacity,
            "enabled": enabled,
            "lane": lane,
        })
        if anchor is not UNSET:
            field_dict["anchor"] = anchor
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if inputs is not UNSET:
            field_dict["inputs"] = inputs

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_mask_layer_inputs_type_1 import StackMaskLayerInputsType1 # noqa: PLC0415
        from ..models.stack_mask_layer_parameters_type_1 import StackMaskLayerParametersType1 # noqa: PLC0415
        d = dict(src_dict)
        source = d.pop("source")

        value = d.pop("value")

        asset = d.pop("asset")

        generator = d.pop("generator")

        map_ = d.pop("map")

        painted = d.pop("painted")

        blend = d.pop("blend")

        opacity = d.pop("opacity")

        enabled = d.pop("enabled")

        lane = d.pop("lane")

        def _parse_anchor(data: object) -> None | StackAnchorRef | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                anchor_type_1 = StackAnchorRef.from_dict(data)



                return anchor_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackAnchorRef | Unset, data)

        anchor = _parse_anchor(d.pop("anchor", UNSET))


        def _parse_parameters(data: object) -> None | StackMaskLayerParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackMaskLayerParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskLayerParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_inputs(data: object) -> None | StackMaskLayerInputsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                inputs_type_1 = StackMaskLayerInputsType1.from_dict(data)



                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMaskLayerInputsType1 | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        stack_mask_layer = cls(
            source=source,
            value=value,
            asset=asset,
            generator=generator,
            map_=map_,
            painted=painted,
            blend=blend,
            opacity=opacity,
            enabled=enabled,
            lane=lane,
            anchor=anchor,
            parameters=parameters,
            inputs=inputs,
        )


        stack_mask_layer.additional_properties = d
        return stack_mask_layer

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
