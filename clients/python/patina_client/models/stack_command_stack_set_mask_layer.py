from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_mask_layer_op import StackCommandStackSetMaskLayerOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_anchor_ref import StackAnchorRef
  from ..models.stack_command_stack_set_mask_layer_inputs_type_1 import StackCommandStackSetMaskLayerInputsType1
  from ..models.stack_command_stack_set_mask_layer_parameters_type_1 import StackCommandStackSetMaskLayerParametersType1
  from ..models.stack_placement import StackPlacement





T = TypeVar("T", bound="StackCommandStackSetMaskLayer")



@_attrs_define
class StackCommandStackSetMaskLayer:
    """ Changes an entry of a layer's mask stack. What is null stays.

        Attributes:
            op (StackCommandStackSetMaskLayerOp):
            layer (str): The layer whose mask it is.
            index (int): Which, counted from the bottom.
            source (None | str | Unset): Where its pixels come from. A paint entry keeps its canvas whatever it becomes.
            value (float | None | Unset): A constant entry's value, 0 to 1.
            asset (None | str | Unset): The image a texture entry reads.
            anchor (None | StackAnchorRef | Unset):
            generator (None | str | Unset): The compound a generator entry runs. Another compound starts from its own knobs.
            map_ (None | str | Unset): The mesh map a bake entry reads.
            parameters (None | StackCommandStackSetMaskLayerParametersType1 | Unset): A generator entry's knobs, replacing
                what these names held.
            blend (None | str | Unset): How it composites.
            opacity (float | None | Unset): How much of it composites, 0 to 1.
            enabled (bool | None | Unset): Whether it composites.
            lane (None | str | Unset): Which lane of its picture it reads: Red, Green, Blue or Alpha.
            inputs (None | StackCommandStackSetMaskLayerInputsType1 | Unset): The image inputs of a generator entry that
                anchor points feed, by port, replacing all of them. Another
                generator starts with none.
            output (None | str | Unset): Which picture of a generator entry that writes several it reads, or empty for its
                only one. Another generator
                starts from empty.
            projection (None | str | Unset): How a texture entry lands: Uv, Triplanar, Planar, Spherical or Cylindrical.
            placement (None | StackPlacement | Unset):
     """

    op: StackCommandStackSetMaskLayerOp
    layer: str
    index: int
    source: None | str | Unset = UNSET
    value: float | None | Unset = UNSET
    asset: None | str | Unset = UNSET
    anchor: None | StackAnchorRef | Unset = UNSET
    generator: None | str | Unset = UNSET
    map_: None | str | Unset = UNSET
    parameters: None | StackCommandStackSetMaskLayerParametersType1 | Unset = UNSET
    blend: None | str | Unset = UNSET
    opacity: float | None | Unset = UNSET
    enabled: bool | None | Unset = UNSET
    lane: None | str | Unset = UNSET
    inputs: None | StackCommandStackSetMaskLayerInputsType1 | Unset = UNSET
    output: None | str | Unset = UNSET
    projection: None | str | Unset = UNSET
    placement: None | StackPlacement | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer_inputs_type_1 import StackCommandStackSetMaskLayerInputsType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer_parameters_type_1 import StackCommandStackSetMaskLayerParametersType1 # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
        op = self.op.value

        layer = self.layer

        index = self.index

        source: None | str | Unset
        if isinstance(self.source, Unset):
            source = UNSET
        else:
            source = self.source

        value: float | None | Unset
        if isinstance(self.value, Unset):
            value = UNSET
        else:
            value = self.value

        asset: None | str | Unset
        if isinstance(self.asset, Unset):
            asset = UNSET
        else:
            asset = self.asset

        anchor: dict[str, Any] | None | Unset
        if isinstance(self.anchor, Unset):
            anchor = UNSET
        elif isinstance(self.anchor, StackAnchorRef):
            anchor = self.anchor.to_dict()
        else:
            anchor = self.anchor

        generator: None | str | Unset
        if isinstance(self.generator, Unset):
            generator = UNSET
        else:
            generator = self.generator

        map_: None | str | Unset
        if isinstance(self.map_, Unset):
            map_ = UNSET
        else:
            map_ = self.map_

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, StackCommandStackSetMaskLayerParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters

        blend: None | str | Unset
        if isinstance(self.blend, Unset):
            blend = UNSET
        else:
            blend = self.blend

        opacity: float | None | Unset
        if isinstance(self.opacity, Unset):
            opacity = UNSET
        else:
            opacity = self.opacity

        enabled: bool | None | Unset
        if isinstance(self.enabled, Unset):
            enabled = UNSET
        else:
            enabled = self.enabled

        lane: None | str | Unset
        if isinstance(self.lane, Unset):
            lane = UNSET
        else:
            lane = self.lane

        inputs: dict[str, Any] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, StackCommandStackSetMaskLayerInputsType1):
            inputs = self.inputs.to_dict()
        else:
            inputs = self.inputs

        output: None | str | Unset
        if isinstance(self.output, Unset):
            output = UNSET
        else:
            output = self.output

        projection: None | str | Unset
        if isinstance(self.projection, Unset):
            projection = UNSET
        else:
            projection = self.projection

        placement: dict[str, Any] | None | Unset
        if isinstance(self.placement, Unset):
            placement = UNSET
        elif isinstance(self.placement, StackPlacement):
            placement = self.placement.to_dict()
        else:
            placement = self.placement


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "index": index,
        })
        if source is not UNSET:
            field_dict["source"] = source
        if value is not UNSET:
            field_dict["value"] = value
        if asset is not UNSET:
            field_dict["asset"] = asset
        if anchor is not UNSET:
            field_dict["anchor"] = anchor
        if generator is not UNSET:
            field_dict["generator"] = generator
        if map_ is not UNSET:
            field_dict["map"] = map_
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if blend is not UNSET:
            field_dict["blend"] = blend
        if opacity is not UNSET:
            field_dict["opacity"] = opacity
        if enabled is not UNSET:
            field_dict["enabled"] = enabled
        if lane is not UNSET:
            field_dict["lane"] = lane
        if inputs is not UNSET:
            field_dict["inputs"] = inputs
        if output is not UNSET:
            field_dict["output"] = output
        if projection is not UNSET:
            field_dict["projection"] = projection
        if placement is not UNSET:
            field_dict["placement"] = placement

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer_inputs_type_1 import StackCommandStackSetMaskLayerInputsType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer_parameters_type_1 import StackCommandStackSetMaskLayerParametersType1 # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
        d = dict(src_dict)
        op = StackCommandStackSetMaskLayerOp(d.pop("op"))




        layer = d.pop("layer")

        index = d.pop("index")

        def _parse_source(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        source = _parse_source(d.pop("source", UNSET))


        def _parse_value(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        value = _parse_value(d.pop("value", UNSET))


        def _parse_asset(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        asset = _parse_asset(d.pop("asset", UNSET))


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


        def _parse_generator(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        generator = _parse_generator(d.pop("generator", UNSET))


        def _parse_map_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        map_ = _parse_map_(d.pop("map", UNSET))


        def _parse_parameters(data: object) -> None | StackCommandStackSetMaskLayerParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackCommandStackSetMaskLayerParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetMaskLayerParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_blend(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        blend = _parse_blend(d.pop("blend", UNSET))


        def _parse_opacity(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        opacity = _parse_opacity(d.pop("opacity", UNSET))


        def _parse_enabled(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        enabled = _parse_enabled(d.pop("enabled", UNSET))


        def _parse_lane(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        lane = _parse_lane(d.pop("lane", UNSET))


        def _parse_inputs(data: object) -> None | StackCommandStackSetMaskLayerInputsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                inputs_type_1 = StackCommandStackSetMaskLayerInputsType1.from_dict(data)



                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetMaskLayerInputsType1 | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        def _parse_output(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        output = _parse_output(d.pop("output", UNSET))


        def _parse_projection(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        projection = _parse_projection(d.pop("projection", UNSET))


        def _parse_placement(data: object) -> None | StackPlacement | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                placement_type_1 = StackPlacement.from_dict(data)



                return placement_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackPlacement | Unset, data)

        placement = _parse_placement(d.pop("placement", UNSET))


        stack_command_stack_set_mask_layer = cls(
            op=op,
            layer=layer,
            index=index,
            source=source,
            value=value,
            asset=asset,
            anchor=anchor,
            generator=generator,
            map_=map_,
            parameters=parameters,
            blend=blend,
            opacity=opacity,
            enabled=enabled,
            lane=lane,
            inputs=inputs,
            output=output,
            projection=projection,
            placement=placement,
        )


        stack_command_stack_set_mask_layer.additional_properties = d
        return stack_command_stack_set_mask_layer

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
