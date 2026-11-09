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
  from ..models.stack_instance_ref import StackInstanceRef
  from ..models.stack_layer_inputs_type_1 import StackLayerInputsType1
  from ..models.stack_layer_parameters_type_1 import StackLayerParametersType1
  from ..models.stack_layer_settings import StackLayerSettings
  from ..models.stack_layer_textures import StackLayerTextures
  from ..models.stack_layer_values import StackLayerValues
  from ..models.stack_mask import StackMask
  from ..models.stack_placement import StackPlacement





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
            fill (str): What a fill reads: Constant, Texture, Graph or Anchor.
            values (StackLayerValues): What a constant fill writes to each channel.
            textures (StackLayerTextures): What a texture fill reads for each channel.
            graph (str): The graph a graph fill reads.
            projection (str): How a fill lands on the mesh: Uv, Triplanar, Planar, Spherical or Cylindrical.
            filter_ (str): The named filter a file holds: Levels, Hsl, Blur, Invert or Grayscale.
            settings (StackLayerSettings): A filter's numbers.
            mask (StackMask): What decides where a layer shows.
            planar_axis (str): The world axis a planar projection looks along or a cylinder wraps: X, Y or Z. Default: 'Y'.
            filter_node (str): The node or compound a filter layer runs, whichever way the file names it. Default: ''.
            anchor_point (str): The name of the anchor point this layer publishes, or empty for none. Default: ''.
            flattened (bool): Whether this is a group flattened into a canvas: it composites that canvas, and its children
                are kept, unread,
                as the way back. Default: False.
            placement (None | StackPlacement | Unset):
            parameters (None | StackLayerParametersType1 | Unset): What a graph fill sets the graph's knobs to, as text.
            anchor (None | StackAnchorRef | Unset):
            inputs (None | StackLayerInputsType1 | Unset): Where the image inputs of a graph fill or a filter read, by port.
            instance (None | StackInstanceRef | Unset):
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
    fill: str
    values: StackLayerValues
    textures: StackLayerTextures
    graph: str
    projection: str
    filter_: str
    settings: StackLayerSettings
    mask: StackMask
    planar_axis: str = 'Y'
    filter_node: str = ''
    anchor_point: str = ''
    flattened: bool = False
    placement: None | StackPlacement | Unset = UNSET
    parameters: None | StackLayerParametersType1 | Unset = UNSET
    anchor: None | StackAnchorRef | Unset = UNSET
    inputs: None | StackLayerInputsType1 | Unset = UNSET
    instance: None | StackInstanceRef | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        from ..models.stack_layer_inputs_type_1 import StackLayerInputsType1 # noqa: PLC0415
        from ..models.stack_layer_parameters_type_1 import StackLayerParametersType1 # noqa: PLC0415
        from ..models.stack_layer_settings import StackLayerSettings # noqa: PLC0415
        from ..models.stack_layer_textures import StackLayerTextures # noqa: PLC0415
        from ..models.stack_layer_values import StackLayerValues # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
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



        fill = self.fill

        values = self.values.to_dict()

        textures = self.textures.to_dict()

        graph = self.graph

        projection = self.projection

        filter_ = self.filter_

        settings = self.settings.to_dict()

        mask = self.mask.to_dict()

        planar_axis = self.planar_axis

        filter_node = self.filter_node

        anchor_point = self.anchor_point

        flattened = self.flattened

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
        elif isinstance(self.parameters, StackLayerParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters

        anchor: dict[str, Any] | None | Unset
        if isinstance(self.anchor, Unset):
            anchor = UNSET
        elif isinstance(self.anchor, StackAnchorRef):
            anchor = self.anchor.to_dict()
        else:
            anchor = self.anchor

        inputs: dict[str, Any] | None | Unset
        if isinstance(self.inputs, Unset):
            inputs = UNSET
        elif isinstance(self.inputs, StackLayerInputsType1):
            inputs = self.inputs.to_dict()
        else:
            inputs = self.inputs

        instance: dict[str, Any] | None | Unset
        if isinstance(self.instance, Unset):
            instance = UNSET
        elif isinstance(self.instance, StackInstanceRef):
            instance = self.instance.to_dict()
        else:
            instance = self.instance


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
            "fill": fill,
            "values": values,
            "textures": textures,
            "graph": graph,
            "projection": projection,
            "filter": filter_,
            "settings": settings,
            "mask": mask,
            "planarAxis": planar_axis,
            "filterNode": filter_node,
            "anchorPoint": anchor_point,
            "flattened": flattened,
        })
        if placement is not UNSET:
            field_dict["placement"] = placement
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if anchor is not UNSET:
            field_dict["anchor"] = anchor
        if inputs is not UNSET:
            field_dict["inputs"] = inputs
        if instance is not UNSET:
            field_dict["instance"] = instance

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_anchor_ref import StackAnchorRef # noqa: PLC0415
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        from ..models.stack_layer_inputs_type_1 import StackLayerInputsType1 # noqa: PLC0415
        from ..models.stack_layer_parameters_type_1 import StackLayerParametersType1 # noqa: PLC0415
        from ..models.stack_layer_settings import StackLayerSettings # noqa: PLC0415
        from ..models.stack_layer_textures import StackLayerTextures # noqa: PLC0415
        from ..models.stack_layer_values import StackLayerValues # noqa: PLC0415
        from ..models.stack_mask import StackMask # noqa: PLC0415
        from ..models.stack_placement import StackPlacement # noqa: PLC0415
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


        fill = d.pop("fill")

        values = StackLayerValues.from_dict(d.pop("values"))




        textures = StackLayerTextures.from_dict(d.pop("textures"))




        graph = d.pop("graph")

        projection = d.pop("projection")

        filter_ = d.pop("filter")

        settings = StackLayerSettings.from_dict(d.pop("settings"))




        mask = StackMask.from_dict(d.pop("mask"))




        planar_axis = d.pop("planarAxis")

        filter_node = d.pop("filterNode")

        anchor_point = d.pop("anchorPoint")

        flattened = d.pop("flattened")

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


        def _parse_parameters(data: object) -> None | StackLayerParametersType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = StackLayerParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackLayerParametersType1 | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


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


        def _parse_inputs(data: object) -> None | StackLayerInputsType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                inputs_type_1 = StackLayerInputsType1.from_dict(data)



                return inputs_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackLayerInputsType1 | Unset, data)

        inputs = _parse_inputs(d.pop("inputs", UNSET))


        def _parse_instance(data: object) -> None | StackInstanceRef | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                instance_type_1 = StackInstanceRef.from_dict(data)



                return instance_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackInstanceRef | Unset, data)

        instance = _parse_instance(d.pop("instance", UNSET))


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
            fill=fill,
            values=values,
            textures=textures,
            graph=graph,
            projection=projection,
            filter_=filter_,
            settings=settings,
            mask=mask,
            planar_axis=planar_axis,
            filter_node=filter_node,
            anchor_point=anchor_point,
            flattened=flattened,
            placement=placement,
            parameters=parameters,
            anchor=anchor,
            inputs=inputs,
            instance=instance,
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
