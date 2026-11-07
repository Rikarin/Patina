from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_layer_op import StackCommandStackSetLayerOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_command_stack_set_layer_parameters_type_1 import StackCommandStackSetLayerParametersType1
  from ..models.stack_command_stack_set_layer_settings_type_1 import StackCommandStackSetLayerSettingsType1
  from ..models.stack_command_stack_set_layer_textures_type_1 import StackCommandStackSetLayerTexturesType1
  from ..models.stack_command_stack_set_layer_values_type_1 import StackCommandStackSetLayerValuesType1
  from ..models.stack_mask import StackMask
  from ..models.stack_mesh_mask import StackMeshMask
  from ..models.stack_placement import StackPlacement





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
            channels (list[str] | None | Unset): Which channels it writes. Empty means all of them.
            fill (None | str | Unset): What a fill reads: Constant, Texture or Graph.
            values (None | StackCommandStackSetLayerValuesType1 | Unset): What a constant fill writes, replacing what these
                channels held.
            textures (None | StackCommandStackSetLayerTexturesType1 | Unset): What a texture fill reads, replacing what
                these channels held.
            graph (None | str | Unset): The graph a graph fill reads.
            projection (None | str | Unset): How a fill lands on the mesh: Uv, Triplanar, Planar, Spherical or Cylindrical.
            filter_ (None | str | Unset): What a filter does.
            settings (None | StackCommandStackSetLayerSettingsType1 | Unset): A filter's numbers, replacing what these names
                held.
            mask (None | StackMask | Unset):
            mesh_mask (None | StackMeshMask | Unset):
            planar_axis (None | str | Unset): The world axis a planar projection looks along or a cylinder wraps: X, Y or Z.
            placement (None | StackPlacement | Unset):
            parameters (None | StackCommandStackSetLayerParametersType1 | Unset): What a graph fill sets the graph's knobs
                to, replacing what these names held.
            filter_node (None | str | Unset): The compound a filter runs, or empty to go back to the named filter.
     """

    op: StackCommandStackSetLayerOp
    layer: str
    name: None | str | Unset = UNSET
    enabled: bool | None | Unset = UNSET
    opacity: float | None | Unset = UNSET
    blend: None | str | Unset = UNSET
    channels: list[str] | None | Unset = UNSET
    fill: None | str | Unset = UNSET
    values: None | StackCommandStackSetLayerValuesType1 | Unset = UNSET
    textures: None | StackCommandStackSetLayerTexturesType1 | Unset = UNSET
    graph: None | str | Unset = UNSET
    projection: None | str | Unset = UNSET
    filter_: None | str | Unset = UNSET
    settings: None | StackCommandStackSetLayerSettingsType1 | Unset = UNSET
    mask: None | StackMask | Unset = UNSET
    mesh_mask: None | StackMeshMask | Unset = UNSET
    planar_axis: None | str | Unset = UNSET
    placement: None | StackPlacement | Unset = UNSET
    parameters: None | StackCommandStackSetLayerParametersType1 | Unset = UNSET
    filter_node: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_command_stack_set_layer_parameters_type_1 import StackCommandStackSetLayerParametersType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_settings_type_1 import StackCommandStackSetLayerSettingsType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_textures_type_1 import StackCommandStackSetLayerTexturesType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_values_type_1 import StackCommandStackSetLayerValuesType1 # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        from ..models.stack_mesh_mask import StackMeshMask # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
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

        channels: list[str] | None | Unset
        if isinstance(self.channels, Unset):
            channels = UNSET
        elif isinstance(self.channels, list):
            channels = self.channels


        else:
            channels = self.channels

        fill: None | str | Unset
        if isinstance(self.fill, Unset):
            fill = UNSET
        else:
            fill = self.fill

        values: dict[str, Any] | None | Unset
        if isinstance(self.values, Unset):
            values = UNSET
        elif isinstance(self.values, StackCommandStackSetLayerValuesType1):
            values = self.values.to_dict()
        else:
            values = self.values

        textures: dict[str, Any] | None | Unset
        if isinstance(self.textures, Unset):
            textures = UNSET
        elif isinstance(self.textures, StackCommandStackSetLayerTexturesType1):
            textures = self.textures.to_dict()
        else:
            textures = self.textures

        graph: None | str | Unset
        if isinstance(self.graph, Unset):
            graph = UNSET
        else:
            graph = self.graph

        projection: None | str | Unset
        if isinstance(self.projection, Unset):
            projection = UNSET
        else:
            projection = self.projection

        filter_: None | str | Unset
        if isinstance(self.filter_, Unset):
            filter_ = UNSET
        else:
            filter_ = self.filter_

        settings: dict[str, Any] | None | Unset
        if isinstance(self.settings, Unset):
            settings = UNSET
        elif isinstance(self.settings, StackCommandStackSetLayerSettingsType1):
            settings = self.settings.to_dict()
        else:
            settings = self.settings

        mask: dict[str, Any] | None | Unset
        if isinstance(self.mask, Unset):
            mask = UNSET
        elif isinstance(self.mask, StackMask):
            mask = self.mask.to_dict()
        else:
            mask = self.mask

        mesh_mask: dict[str, Any] | None | Unset
        if isinstance(self.mesh_mask, Unset):
            mesh_mask = UNSET
        elif isinstance(self.mesh_mask, StackMeshMask):
            mesh_mask = self.mesh_mask.to_dict()
        else:
            mesh_mask = self.mesh_mask

        planar_axis: None | str | Unset
        if isinstance(self.planar_axis, Unset):
            planar_axis = UNSET
        else:
            planar_axis = self.planar_axis

        placement: dict[str, Any] | None | Unset
        if isinstance(self.placement, Unset):
            placement = UNSET
        elif isinstance(self.placement, StackPlacement):
            placement = self.placement.to_dict()
        else:
            placement = self.placement

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, StackCommandStackSetLayerParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters

        filter_node: None | str | Unset
        if isinstance(self.filter_node, Unset):
            filter_node = UNSET
        else:
            filter_node = self.filter_node


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
        if channels is not UNSET:
            field_dict["channels"] = channels
        if fill is not UNSET:
            field_dict["fill"] = fill
        if values is not UNSET:
            field_dict["values"] = values
        if textures is not UNSET:
            field_dict["textures"] = textures
        if graph is not UNSET:
            field_dict["graph"] = graph
        if projection is not UNSET:
            field_dict["projection"] = projection
        if filter_ is not UNSET:
            field_dict["filter"] = filter_
        if settings is not UNSET:
            field_dict["settings"] = settings
        if mask is not UNSET:
            field_dict["mask"] = mask
        if mesh_mask is not UNSET:
            field_dict["meshMask"] = mesh_mask
        if planar_axis is not UNSET:
            field_dict["planarAxis"] = planar_axis
        if placement is not UNSET:
            field_dict["placement"] = placement
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if filter_node is not UNSET:
            field_dict["filterNode"] = filter_node

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_command_stack_set_layer_parameters_type_1 import StackCommandStackSetLayerParametersType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_settings_type_1 import StackCommandStackSetLayerSettingsType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_textures_type_1 import StackCommandStackSetLayerTexturesType1 # noqa: PLC0415
        from ..models.stack_command_stack_set_layer_values_type_1 import StackCommandStackSetLayerValuesType1 # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        from ..models.stack_mesh_mask import StackMeshMask # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
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


        def _parse_channels(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                channels_type_1 = cast(list[str], data)

                return channels_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        channels = _parse_channels(d.pop("channels", UNSET))


        def _parse_fill(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        fill = _parse_fill(d.pop("fill", UNSET))


        def _parse_values(data: object) -> None | StackCommandStackSetLayerValuesType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                values_type_1 = StackCommandStackSetLayerValuesType1.from_dict(data)



                return values_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetLayerValuesType1 | Unset, data)

        values = _parse_values(d.pop("values", UNSET))


        def _parse_textures(data: object) -> None | StackCommandStackSetLayerTexturesType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                textures_type_1 = StackCommandStackSetLayerTexturesType1.from_dict(data)



                return textures_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetLayerTexturesType1 | Unset, data)

        textures = _parse_textures(d.pop("textures", UNSET))


        def _parse_graph(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        graph = _parse_graph(d.pop("graph", UNSET))


        def _parse_projection(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        projection = _parse_projection(d.pop("projection", UNSET))


        def _parse_filter_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        filter_ = _parse_filter_(d.pop("filter", UNSET))


        def _parse_settings(data: object) -> None | StackCommandStackSetLayerSettingsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                settings_type_1 = StackCommandStackSetLayerSettingsType1.from_dict(data)



                return settings_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetLayerSettingsType1 | Unset, data)

        settings = _parse_settings(d.pop("settings", UNSET))


        def _parse_mask(data: object) -> None | StackMask | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                mask_type_1 = StackMask.from_dict(data)



                return mask_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMask | Unset, data)

        mask = _parse_mask(d.pop("mask", UNSET))


        def _parse_mesh_mask(data: object) -> None | StackMeshMask | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                mesh_mask_type_1 = StackMeshMask.from_dict(data)



                return mesh_mask_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackMeshMask | Unset, data)

        mesh_mask = _parse_mesh_mask(d.pop("meshMask", UNSET))


        def _parse_planar_axis(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        planar_axis = _parse_planar_axis(d.pop("planarAxis", UNSET))


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


        def _parse_parameters(data: object) -> None | StackCommandStackSetLayerParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackCommandStackSetLayerParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackSetLayerParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_filter_node(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        filter_node = _parse_filter_node(d.pop("filterNode", UNSET))


        stack_command_stack_set_layer = cls(
            op=op,
            layer=layer,
            name=name,
            enabled=enabled,
            opacity=opacity,
            blend=blend,
            channels=channels,
            fill=fill,
            values=values,
            textures=textures,
            graph=graph,
            projection=projection,
            filter_=filter_,
            settings=settings,
            mask=mask,
            mesh_mask=mesh_mask,
            planar_axis=planar_axis,
            placement=placement,
            parameters=parameters,
            filter_node=filter_node,
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
